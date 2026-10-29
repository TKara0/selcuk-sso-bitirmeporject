using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;
using Selcuk.Otomasyon.Staj.Veri;

namespace Selcuk.Otomasyon.Staj.Tests;

/// <summary>
/// Veri kaynağı seçimi (VeriKaynagi:Mod), Api modu (sahte HttpMessageHandler: Bearer token, hata sayfaları) ve
/// Back-Channel Logout'un Staj oturumunu kapatması.
/// </summary>
public sealed class VeriKaynagiTests
{
    private const string ApiAdresi = "https://api.test.local/";

    private static Dictionary<string, string?> ApiAyarlari(string? zamanAsimi = null)
    {
        var ayarlar = new Dictionary<string, string?> { ["VeriKaynagi:Mod"] = "Api", ["VeriKaynagi:ApiAdresi"] = ApiAdresi };
        if (zamanAsimi is not null)
        {
            ayarlar["VeriKaynagi:ZamanAsimi"] = zamanAsimi;
        }

        return ayarlar;
    }

    /// <summary>Kimlik sunucusunun staj.oku/staj.yaz ile verdiği access token gibi (aud staj-api); imzası önemsiz.</summary>
    private static string StajApiTokeni() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = StajFactory.Authority,
        Audience = "staj-api",
        TokenType = "at+jwt",
        Claims = new Dictionary<string, object> { ["scope"] = "openid profile roles staj.oku staj.yaz", ["sub"] = "sub-203011001" },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256),
    });

    private static Type Uygulama(StajFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IStajVeriKaynagi>().GetType();
    }

    // ---------- f) Seçim ----------

    [Fact]
    public void Varsayilan_ve_Mock_modunda_Mock_Api_modunda_Api_secilir()
    {
        using (var varsayilan = new StajFactory())
        {
            Assert.Equal(typeof(MockStajVeriKaynagi), Uygulama(varsayilan));
        }

        using (var mock = new StajFactory(ayarlar: new Dictionary<string, string?> { ["VeriKaynagi:Mod"] = "Mock" }))
        {
            Assert.Equal(typeof(MockStajVeriKaynagi), Uygulama(mock));
        }

        using var api = new StajFactory(ayarlar: ApiAyarlari());
        Assert.Equal(typeof(ApiStajVeriKaynagi), Uygulama(api));
        Assert.True(api.Services.GetRequiredService<IOptions<VeriKaynagiAyarlari>>().Value.ApiMi);
    }

    [Theory]
    [InlineData("Gecersiz", null, "VeriKaynagi:Mod")]
    [InlineData("api", null, "VeriKaynagi:Mod")]
    [InlineData("Api", "", "VeriKaynagi:ApiAdresi")]
    [InlineData("Api", "http://uzak.test.local/", "VeriKaynagi:ApiAdresi")]
    public void Gecersiz_ayar_acilista_acik_hatayla_duruyor(string mod, string? adres, string beklenen)
    {
        using var factory = new StajFactory(ayarlar: new Dictionary<string, string?>
        {
            ["VeriKaynagi:Mod"] = mod,
            ["VeriKaynagi:ApiAdresi"] = adres,
        });

        var hata = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(beklenen, hata.Message);
    }

    [Fact]
    public async Task Alt_bilgide_veri_kaynagi_rozeti_moda_gore()
    {
        using var mock = new StajFactory();
        Assert.Contains("Veri: örnek veri", await StajFactory.MetinAsync(await mock.Istemci().GetAsync("/")));

        using var api = new StajFactory(ayarlar: ApiAyarlari());
        Assert.Contains("Veri: API", await StajFactory.MetinAsync(await api.Istemci().GetAsync("/")));
    }

    // ---------- g) Api modu ----------

    [Fact]
    public async Task Api_istegi_oturumdaki_tokeni_Bearer_olarak_tasiyor_token_staj_api_icin()
    {
        var token = StajApiTokeni();
        var api = new SahteApi(_ => SahteApi.Json(new[]
        {
            new StajKaydi("API-1", "203011001", "Test Öğrenci", "Teknoloji Fakültesi", "Bilgisayar Mühendisliği", StajTuru.StajII,
                "Örnek API Firması A.Ş.", "Örnek Mah. No: 1", new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 31), 20,
                StajDurumu.DevamEdiyor, new DateOnly(2026, 5, 11)),
        }));
        using var factory = new StajFactory(ayarlar: ApiAyarlari(), api: api, accessToken: token);

        var yanit = await factory.Istemci("203011001", roller: Roller.Ogrenci).GetAsync("/Ogrenci/Stajlarim");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = await StajFactory.MetinAsync(yanit);
        Assert.Equal(["API-1"], StajFactory.Kayitlar(html));
        Assert.Contains("Örnek API Firması A.Ş.", html);
        Assert.Contains("Devam ediyor", html);
        Assert.Contains("Veri: API", html);
        // Token sayfaya yazılmaz.
        Assert.DoesNotContain(token, html);

        var istek = Assert.Single(api.Istekler);
        Assert.Equal(ApiAdresi + "api/staj/basvurularim", istek.Adres);
        Assert.Equal("Bearer " + token, istek.Yetki);
        // Taşınan token staj-api için ve staj.oku kapsamlı (kimlik sunucusunun verdiği biçim).
        var jwt = new JsonWebToken(istek.Yetki!["Bearer ".Length..]);
        Assert.Contains("staj-api", jwt.Audiences);
        Assert.Contains("staj.oku", jwt.GetClaim("scope").Value.Split(' '));
    }

    [Fact]
    public async Task Api_durum_filtresi_ve_ozet_yili_sorgu_dizesinde()
    {
        var api = new SahteApi(istek => istek.RequestUri!.AbsolutePath == "/api/staj/ozet"
            ? SahteApi.Json(new StajOzeti(2026, 1, 2, 3, []))
            : SahteApi.Json(Array.Empty<StajKaydi>()));
        using var factory = new StajFactory(ayarlar: ApiAyarlari(), api: api);
        var komisyon = factory.Istemci("komisyon.test", roller: [Roller.Akademisyen, Roller.StajKomisyonu, Roller.BolumBaskani]);

        Assert.Equal(HttpStatusCode.OK, (await komisyon.GetAsync("/Komisyon/BasvuruInceleme")).StatusCode);
        var ozet = await StajFactory.MetinAsync(await komisyon.GetAsync("/Baskan/Ozet"));

        Assert.Contains("data-ozet=\"bekleyen\">2<", ozet);
        Assert.Equal(
            [ApiAdresi + "api/staj/basvurular?durum=basvuruBekliyor", ApiAdresi + $"api/staj/ozet?yil={DateTime.Now.Year}"],
            api.Istekler.Select(i => i.Adres));
    }

    public static TheoryData<string> UlasilamayanApiler => ["kapali", "500", "503", "zaman-asimi", "bozuk-json"];

    [Theory]
    [MemberData(nameof(UlasilamayanApiler))]
    public async Task Api_ulasilamazsa_Turkce_sayfa_ve_stack_trace_yok(string durum)
    {
        var api = new SahteApi(async _ => durum switch
        {
            "kapali" => throw new HttpRequestException("Sahte bağlantı hatası."),
            "zaman-asimi" => await Bekle(),
            "bozuk-json" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bozuk", Encoding.UTF8, "application/json") },
            _ => new HttpResponseMessage((HttpStatusCode)int.Parse(durum)) { Content = new StringContent("System.Exception: iç hata ayrıntısı") },
        });
        using var factory = new StajFactory(ayarlar: ApiAyarlari("00:00:00.300"), api: api);

        var yanit = await factory.Istemci("203011001", roller: Roller.Ogrenci).GetAsync("/Ogrenci/Stajlarim");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        var html = await StajFactory.MetinAsync(yanit);
        Assert.Contains("Veri kaynağına şu an ulaşılamıyor", html);
        StackTraceYok(html);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Api_yetki_vermezse_yetki_sayfasi(HttpStatusCode durum)
    {
        using var factory = new StajFactory(ayarlar: ApiAyarlari(), api: new SahteApi(_ => Task.FromResult(new HttpResponseMessage(durum))));

        var yanit = await factory.Istemci("baskan.test", roller: [Roller.Akademisyen, Roller.BolumBaskani]).GetAsync("/Baskan/Ozet");

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        var html = await StajFactory.MetinAsync(yanit);
        Assert.Contains("Bu verilere erişim yetkin yok", html);
        Assert.DoesNotContain("Veri kaynağına şu an ulaşılamıyor", html);
        StackTraceYok(html);
    }

    private static void StackTraceYok(string html)
    {
        Assert.DoesNotContain("Exception", html);
        Assert.DoesNotContain("   at ", html);
        Assert.DoesNotContain("iç hata ayrıntısı", html);
        Assert.DoesNotContain("api.test.local", html);
    }

    private static async Task<HttpResponseMessage> Bekle()
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        return new HttpResponseMessage(HttpStatusCode.OK);
    }

    // ---------- i) Back-Channel Logout ----------

    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public async Task Back_channel_logout_Staj_oturumunu_kapatiyor(string ortam)
    {
        using var factory = new StajFactory(sahteKimlik: false, ortam: ortam);
        var client = factory.Istemci();
        const string Sid = "oturum-staj-1";

        // OIDC girişinden sonraki gibi bir çerez: uygulamanın kendi çerez biçimiyle korunur.
        var ayarlar = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "sub-203011001"), new Claim("sid", Sid), new Claim("name", "Test Öğrenci"),
            new Claim("preferred_username", "203011001"), new Claim("role", Roller.Ogrenci),
        ], "test", "name", "role");
        var ozellikler = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
        ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = StajApiTokeni() }]);
        // Bilet sunucu tarafındaki depoda (OturumBiletDeposu); çerezde yalnızca anahtarı taşıyan küçük bir bilet.
        Assert.IsType<OturumBiletDeposu>(ayarlar.SessionStore);
        var anahtar = await ayarlar.SessionStore!.StoreAsync(new AuthenticationTicket(new ClaimsPrincipal(identity), ozellikler,
            CookieAuthenticationDefaults.AuthenticationScheme));
        var cerez = ayarlar.TicketDataFormat.Protect(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("Microsoft.AspNetCore.Authentication.Cookies-SessionId", anahtar)],
                CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.True(cerez.Length < 1024, $"Çerez {cerez.Length} karakter.");
        client.DefaultRequestHeaders.Add("Cookie", $"{ayarlar.Cookie.Name}={cerez}");

        Assert.Equal("StajOtomasyonu.Oturum", ayarlar.Cookie.Name);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Ogrenci/Stajlarim")).StatusCode);
        // Gerçek çerez şemasıyla da rolü olmayan ekran erişim reddi sayfasına yönlendirilir.
        var red = await client.GetAsync("/Komisyon/BasvuruInceleme");
        Assert.Equal(HttpStatusCode.Redirect, red.StatusCode);
        Assert.StartsWith("/ErisimReddedildi", red.Headers.Location!.PathAndQuery);

        var simdi = DateTime.UtcNow;
        var logoutToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "logout+jwt",
            Issuer = StajFactory.Authority,
            Audience = "staj-otomasyonu",
            IssuedAt = simdi,
            NotBefore = simdi,
            Expires = simdi.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["sid"] = Sid,
                ["sub"] = "sub-203011001",
                ["events"] = new Dictionary<string, object> { ["http://schemas.openid.net/event/backchannel-logout"] = new Dictionary<string, object>() },
            },
            SigningCredentials = new SigningCredentials(factory.ImzaAnahtari, SecurityAlgorithms.RsaSha256),
        });
        var bildirim = await factory.Istemci().PostAsync("/signout-backchannel",
            new FormUrlEncodedContent([KeyValuePair.Create("logout_token", logoutToken)]));
        Assert.Equal(HttpStatusCode.OK, bildirim.StatusCode);

        // Aynı çerez artık reddedilir: kimlik sunucusuna yönlendirilir.
        var sonra = await client.GetAsync("/Ogrenci/Stajlarim");
        Assert.Equal(HttpStatusCode.Redirect, sonra.StatusCode);
        Assert.StartsWith(StajFactory.Authority + "connect/authorize", sonra.Headers.Location!.AbsoluteUri);

        // Geçersiz logout_token HTML hata sayfasına çevrilmez.
        var gecersiz = await factory.Istemci().PostAsync("/signout-backchannel",
            new FormUrlEncodedContent([KeyValuePair.Create("logout_token", "gecersiz")]));
        Assert.Equal(HttpStatusCode.BadRequest, gecersiz.StatusCode);
        Assert.Empty(await gecersiz.Content.ReadAsStringAsync());
    }

    /// <summary>Staj API'si yerine: istekleri kaydeder ve verilen yanıtı döndürür.</summary>
    private sealed class SahteApi(Func<HttpRequestMessage, Task<HttpResponseMessage>> yanit) : HttpMessageHandler
    {
        public sealed record Istek(string Adres, string? Yetki);

        public ConcurrentQueue<Istek> Istekler { get; } = new();

        public static Task<HttpResponseMessage> Json<T>(T govde) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(govde, OtomasyonApiIstemcisi.JsonAyarlari), Encoding.UTF8, "application/json"),
        });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Enqueue(new Istek(request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString()));
            return await yanit(request).WaitAsync(cancellationToken);
        }
    }
}
