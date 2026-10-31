using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.TestOrtak;

/// <summary>Bir rolün test kullanıcısı (kimlik sunucusunun Development test kullanıcılarındaki gibi).</summary>
public sealed record TestKullanicisi(string KullaniciAdi, string? Bolum, string? Fakulte, params string[] Roller);

/// <summary>Temsili bir işlem: sayfa (kayıtları VeriAdi ile listeler), POST işleyicisi ve işlemi yapan rol.</summary>
public sealed record TemsiliIslemOrnegi(string Sayfa, string? Handler, string Rol);

/// <summary>
/// Bütün bölüm otomasyonlarında aynı olan davranışların testleri (Staj'daki StajEkranTests ve VeriKaynagiTests'in
/// genel hâli): rol tabanlı menü, adresle yetki reddi, ortak düzen, güvenlik başlıkları, vurgu kontrastı, temsili
/// işlem (antiforgery, veri değişmez), Mock/Api seçimi, Api modu (Bearer ve aud, hata sayfaları), Back-Channel Logout
/// ve çıkışta oturum biletinin hemen silinmesi. Her otomasyonun test projesi bu sınıftan türetir ve otomasyona özgü
/// değerleri verir; kapsam ve şart testleri otomasyonun kendi sınıflarındadır.
/// </summary>
public abstract partial class OtomasyonTemelTestleri<TProgram> : IDisposable where TProgram : class
{
    private const string ApiAdresi = "https://api.test.local/";

    protected OtomasyonFactory<TProgram> Fabrika { get; } = new();

    public void Dispose()
    {
        Fabrika.Dispose();
        GC.SuppressFinalize(this);
    }

    protected abstract string Ad { get; }

    protected abstract string Simge { get; }

    protected abstract string ClientId { get; }

    protected abstract string CerezAdi { get; }

    /// <summary>Access token'ın aud'u (ör. intibak-api) ve okuma kapsamı (ör. intibak.oku).</summary>
    protected abstract string ApiKaynagi { get; }

    protected abstract string OkuKapsami { get; }

    /// <summary>Kayıt satırlarının data- özniteliği (ör. "intibak").</summary>
    protected abstract string VeriAdi { get; }

    /// <summary>site.css'teki açık tema vurgu rengi.</summary>
    protected abstract string AcikVurgu { get; }

    protected abstract IReadOnlyList<MenuOgesi> Menu { get; }

    protected abstract Type MockTuru { get; }

    protected abstract Type ApiTuru { get; }

    protected abstract Type VeriArayuzu { get; }

    /// <summary>Her izinli rol için bir test kullanıcısı.</summary>
    protected abstract IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar { get; }

    /// <summary>Api modu örneği: öğrencinin sayfası, beklenen API yolu, API'nin yanıtı ve sayfada görünecek kayıt kimliği.</summary>
    protected abstract (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi { get; }

    protected abstract IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler { get; }

    /// <summary>
    /// Api, Back-Channel Logout ve adresle yetki reddi testlerinde kullanılan rol (varsayılan ogrenci; öğrenci rolü
    /// olmayan otomasyonlar, ör. Etik Kurul, kendi rolünü verir).
    /// </summary>
    protected virtual string AnaRol => Roller.Ogrenci;

    /// <summary>
    /// Uygulamaya hiç giremeyen bir kullanıcının rolleri (varsayılan İŞKUR sorumlusu; İŞKUR otomasyonu başka bir rol
    /// verir).
    /// </summary>
    protected virtual string[] IzinsizRoller => [Roller.Personel, Roller.IskurSorumlusu];

    protected HttpClient Istemci(string rol, OtomasyonFactory<TProgram>? fabrika = null)
    {
        var k = Kullanicilar[rol];
        return (fabrika ?? Fabrika).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    protected static string RolAdi(string rol) => Selcuk.Roller.Bul(rol)!.Ad;

    protected static List<string> MenuBasliklari(string html) =>
        [.. MenuGrubu().Matches(html).Select(m => m.Groups[1].Value)];

    protected static List<string> MenuBaglantilari(string html)
    {
        var bas = html.IndexOf("id=\"otomasyon-menu-listesi\"", StringComparison.Ordinal);
        Assert.True(bas >= 0, "Menü yok.");
        var menu = html[bas..html.IndexOf("</nav>", bas, StringComparison.Ordinal)];
        return [.. Baglanti().Matches(menu).Select(m => m.Groups[1].Value)];
    }

    // ---------- Menü ----------

    [Fact]
    public async Task Her_rol_menude_yalnizca_kendi_ogelerini_goruyor()
    {
        foreach (var (rol, _) in Kullanicilar)
        {
            var html = await OtomasyonFactory.MetinAsync(await Istemci(rol).GetAsync("/"));

            Assert.Equal([RolAdi(rol)], MenuBasliklari(html));
            Assert.Equal(["/", .. Menu.Where(o => o.Roller.Contains(rol)).Select(o => o.Sayfa)], MenuBaglantilari(html));
        }
    }

    [Fact]
    public async Task Cok_rollu_kullanici_birlesimi_rol_basliklariyla_katalog_sirasinda_goruyor()
    {
        var roller = Kullanicilar.Keys.Where(r => r != Roller.Ogrenci).ToArray();
        var client = Fabrika.Istemci("cok.rollu", roller: [Roller.Akademisyen, .. roller]);

        var html = await OtomasyonFactory.MetinAsync(await client.GetAsync("/"));

        var sira = Selcuk.Roller.Katalog.Select(r => r.Kimlik).Where(roller.Contains).ToList();
        Assert.Equal(sira.Select(RolAdi), MenuBasliklari(html));
        Assert.Equal(["/", .. sira.SelectMany(r => Menu.Where(o => o.Roller.Contains(r)).Select(o => o.Sayfa)).Distinct()],
            MenuBaglantilari(html));
        Assert.Contains($"<h2 id=\"kisayol-0\">{RolAdi(sira[0])}</h2>", html);
    }

    [Fact]
    public async Task Girissiz_ana_sayfa_menusuz_ve_tek_sifre_girisi_var()
    {
        var html = await OtomasyonFactory.MetinAsync(await Fabrika.Istemci().GetAsync("/"));

        Assert.DoesNotContain("otomasyon-menu-listesi", html);
        Assert.Contains("Tek Şifre ile giriş yap", html);
        Assert.Contains("href=\"/Giris\"", html);
        Assert.Contains($"<h1>{Ad}</h1>", html);
    }

    // ---------- Yetki ----------

    [Fact]
    public async Task Ogrenci_baska_rolun_sayfasina_adresle_giremiyor_Turkce_sayfa_goruyor()
    {
        var client = Istemci(AnaRol);
        var sayfa = Menu.First(o => !o.Roller.Contains(AnaRol)).Sayfa;

        var yanit = await client.GetAsync(sayfa);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith("/ErisimReddedildi", yanit.Headers.Location!.PathAndQuery);
        var red = await client.GetAsync(yanit.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, red.StatusCode);
        Assert.Contains("Bu ekrana erişim yetkin yok", await OtomasyonFactory.MetinAsync(red));
    }

    [Fact]
    public async Task Her_menu_sayfasi_yalnizca_kendi_rollerine_acik_girissize_giris_istiyor()
    {
        var izinsiz = Fabrika.Istemci("izinsiz.test", bolum: null, roller: IzinsizRoller);
        foreach (var oge in Menu)
        {
            var yanit = await izinsiz.GetAsync(oge.Sayfa);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith("/ErisimReddedildi", yanit.Headers.Location!.PathAndQuery);
            Assert.Equal(HttpStatusCode.Redirect, (await izinsiz.PostAsync(oge.Sayfa, new FormUrlEncodedContent([]))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Fabrika.Istemci().GetAsync(oge.Sayfa)).StatusCode);

            // Uygulamanın diğer rolleri de bu sayfaya adresle giremez.
            foreach (var rol in Kullanicilar.Keys.Where(r => !oge.Roller.Contains(r)))
            {
                var red = await Istemci(rol).GetAsync(oge.Sayfa);
                Assert.True(red.StatusCode == HttpStatusCode.Redirect, $"{rol} → {oge.Sayfa}: {red.StatusCode}");
                Assert.StartsWith("/ErisimReddedildi", red.Headers.Location!.PathAndQuery);
            }
        }
    }

    [Fact]
    public async Task Her_rol_kendi_ekranlarini_aciyor()
    {
        foreach (var oge in Menu)
        {
            var yanit = await Istemci(oge.Roller.First()).GetAsync(oge.Sayfa);
            Assert.True(yanit.StatusCode == HttpStatusCode.OK, $"{oge.Sayfa}: {yanit.StatusCode}");
            var html = await OtomasyonFactory.MetinAsync(yanit);
            Assert.Single(H1().Matches(html));
            Assert.Contains("aria-current=\"page\">" + oge.Baslik + "</a>", html);
        }
    }

    // ---------- Düzen, başlıklar, kontrast ----------

    [Fact]
    public async Task Duzen_temsili_bant_veri_rozeti_simge_ve_kaynaklar()
    {
        var oge = Menu[0];
        var html = await OtomasyonFactory.MetinAsync(await Istemci(oge.Roller.First()).GetAsync(oge.Sayfa));

        Assert.Contains("Temsili ekran: gösterilen veriler örnektir, yapılan işlemler kaydedilmez.", html);
        Assert.Contains("Veri: örnek veri", html);
        Assert.Contains($"<span class=\"uygulama-simgesi\" aria-hidden=\"true\">{Simge}</span>", html);
        Assert.Contains($"<span>{Ad}</span>", html);
        Assert.Contains($"<nav class=\"otomasyon-menusu menu-genis\" aria-label=\"{Ad} menüsü\">", html);
        Assert.Single(Regex.Matches(html, "class=\"kapi-kucuk\""));
        foreach (var dosya in new[] { "/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css",
                     "/_content/Selcuk.Otomasyon.Ortak/css/otomasyon-betiksiz.css", "/css/site.css" })
        {
            Assert.Equal(HttpStatusCode.OK, (await Fabrika.Istemci().GetAsync(dosya)).StatusCode);
        }
    }

    [Fact]
    public async Task Profilim_ve_bilinmeyen_sayfa()
    {
        var rol = Kullanicilar.Keys.Last();
        var client = Istemci(rol);
        Assert.Contains(RolAdi(rol), await OtomasyonFactory.MetinAsync(await client.GetAsync("/Profilim")));

        var yok = await client.GetAsync("/Olmayan/Sayfa");
        Assert.Equal(HttpStatusCode.NotFound, yok.StatusCode);
        Assert.Contains("Sayfa bulunamadı", await OtomasyonFactory.MetinAsync(yok));
    }

    [Fact]
    public async Task Guvenlik_basliklari_ve_giris_isteyen_sayfalarda_no_store()
    {
        foreach (var oge in Menu)
        {
            var yanit = await Istemci(oge.Roller.First()).GetAsync(oge.Sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.True(yanit.Headers.CacheControl?.NoStore, oge.Sayfa);
            Assert.Equal("nosniff", yanit.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", yanit.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", yanit.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("default-src 'self'", yanit.Headers.GetValues("Content-Security-Policy").Single());
            Assert.DoesNotContain("<script>", await OtomasyonFactory.MetinAsync(yanit));
        }

        Assert.False((await Fabrika.Istemci().GetAsync("/css/site.css")).Headers.CacheControl?.NoStore ?? false);
        Assert.Contains("frame-ancestors 'none'",
            (await Fabrika.Istemci().GetAsync("/")).Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Vurgu_renginin_kontrasti_AA()
    {
        var css = await (await Fabrika.Istemci().GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        var renkler = Vurgu().Matches(css).Select(m => m.Groups[2].Value).ToList();
        Assert.Equal(4, renkler.Count);
        var (acikVurgu, acikZemin, koyuVurgu, koyuZemin) = (renkler[0], renkler[1], renkler[2], renkler[3]);

        Assert.Equal(AcikVurgu, acikVurgu);
        foreach (var zemin in new[] { "#FFFFFF", "#EDF0EF", acikZemin })
        {
            Assert.True(Kontrast(acikVurgu, zemin) >= 4.5, $"{acikVurgu} / {zemin}: {Kontrast(acikVurgu, zemin):0.00}");
        }

        foreach (var zemin in new[] { "#15273A", "#0E1B27", koyuZemin })
        {
            Assert.True(Kontrast(koyuVurgu, zemin) >= 4.5, $"{koyuVurgu} / {zemin}: {Kontrast(koyuVurgu, zemin):0.00}");
        }
    }

    /// <summary>WCAG 2.1 kontrast oranı.</summary>
    public static double Kontrast(string a, string b)
    {
        static double Parlaklik(string renk)
        {
            double Kanal(int i)
            {
                var c = int.Parse(renk.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber) / 255.0;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Kanal(0) + 0.7152 * Kanal(1) + 0.0722 * Kanal(2);
        }

        var (la, lb) = (Parlaklik(a), Parlaklik(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    // ---------- Temsili işlem ----------

    [Fact]
    public async Task Temsili_islem_antiforgery_olmadan_reddediliyor()
    {
        foreach (var islem in TemsiliIslemler)
        {
            var client = Istemci(islem.Rol);
            await client.GetAsync(islem.Sayfa);

            var yanit = await client.PostAsync(Adres(islem), new FormUrlEncodedContent([KeyValuePair.Create("id", "x")]));

            Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
            Assert.DoesNotContain(TemsiliIslem.Mesaj, await OtomasyonFactory.MetinAsync(yanit));
        }
    }

    [Fact]
    public async Task Temsili_islem_mesaj_gosteriyor_veri_degismiyor()
    {
        foreach (var islem in TemsiliIslemler)
        {
            var client = Istemci(islem.Rol);
            var once = await OtomasyonFactory.MetinAsync(await client.GetAsync(islem.Sayfa));
            var kayitlar = OtomasyonFactory.Kayitlar(once, VeriAdi);
            Assert.NotEmpty(kayitlar);
            Assert.DoesNotContain(TemsiliIslem.Mesaj, once);

            var yanit = await client.PostAsync(Adres(islem), new FormUrlEncodedContent(
            [
                KeyValuePair.Create("id", kayitlar[0]),
                KeyValuePair.Create("__RequestVerificationToken", OtomasyonFactory.AntiforgeryTokenAl(once)),
            ]));
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.Equal(islem.Sayfa, yanit.Headers.Location!.OriginalString);

            var sonra = await OtomasyonFactory.MetinAsync(await client.GetAsync(islem.Sayfa));
            Assert.Contains(TemsiliIslem.Mesaj, sonra);
            Assert.Equal(kayitlar, OtomasyonFactory.Kayitlar(sonra, VeriAdi));
            Assert.DoesNotContain(TemsiliIslem.Mesaj, await OtomasyonFactory.MetinAsync(await client.GetAsync(islem.Sayfa)));
        }
    }

    private static string Adres(TemsiliIslemOrnegi islem) =>
        islem.Sayfa + (islem.Handler is null ? "" : "?handler=" + islem.Handler);

    // ---------- Veri kaynağı ----------

    private static Dictionary<string, string?> ApiAyarlari(string? zamanAsimi = null)
    {
        var ayarlar = new Dictionary<string, string?> { ["VeriKaynagi:Mod"] = "Api", ["VeriKaynagi:ApiAdresi"] = ApiAdresi };
        if (zamanAsimi is not null)
        {
            ayarlar["VeriKaynagi:ZamanAsimi"] = zamanAsimi;
        }

        return ayarlar;
    }

    /// <summary>Kimlik sunucusunun &lt;ad&gt;.oku/&lt;ad&gt;.yaz ile verdiği access token gibi (aud &lt;ad&gt;-api); imzası önemsiz.</summary>
    protected string ApiTokeni() => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = OtomasyonFactory.Authority,
        Audience = ApiKaynagi,
        TokenType = "at+jwt",
        Claims = new Dictionary<string, object> { ["scope"] = $"openid profile roles {OkuKapsami}", ["sub"] = "sub-203011001" },
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256),
    });

    private Type SecilenTur(OtomasyonFactory<TProgram> factory)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService(VeriArayuzu).GetType();
    }

    [Fact]
    public void Varsayilan_ve_Mock_modunda_Mock_Api_modunda_Api_secilir()
    {
        Assert.Equal(MockTuru, SecilenTur(Fabrika));
        using (var mock = new OtomasyonFactory<TProgram>(ayarlar: new Dictionary<string, string?> { ["VeriKaynagi:Mod"] = "Mock" }))
        {
            Assert.Equal(MockTuru, SecilenTur(mock));
        }

        using var api = new OtomasyonFactory<TProgram>(ayarlar: ApiAyarlari());
        Assert.Equal(ApiTuru, SecilenTur(api));
    }

    [Theory]
    [InlineData("Gecersiz", null, "VeriKaynagi:Mod")]
    [InlineData("Api", "http://uzak.test.local/", "VeriKaynagi:ApiAdresi")]
    public void Gecersiz_veri_kaynagi_ayari_acilista_duruyor(string mod, string? adres, string beklenen)
    {
        using var factory = new OtomasyonFactory<TProgram>(ayarlar: new Dictionary<string, string?>
        {
            ["VeriKaynagi:Mod"] = mod,
            ["VeriKaynagi:ApiAdresi"] = adres,
        });

        Assert.Contains(beklenen, Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [Fact]
    public async Task Api_istegi_oturumdaki_tokeni_Bearer_olarak_tasiyor_token_dogru_kaynak_icin()
    {
        var token = ApiTokeni();
        var (sayfa, yol, govde, kimlik) = ApiOrnegi;
        var api = new SahteApi(_ => SahteApi.Json(govde));
        using var factory = new OtomasyonFactory<TProgram>(ayarlar: ApiAyarlari(), api: api, accessToken: token);

        var yanit = await Istemci(AnaRol, factory).GetAsync(sayfa);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = await OtomasyonFactory.MetinAsync(yanit);
        Assert.Contains(kimlik, OtomasyonFactory.Kayitlar(html, VeriAdi));
        Assert.Contains("Veri: API", html);
        Assert.DoesNotContain(token, html);

        var istek = api.Istekler.First();
        Assert.Equal(ApiAdresi + yol, istek.Adres);
        Assert.Equal("Bearer " + token, istek.Yetki);
        var jwt = new JsonWebToken(istek.Yetki!["Bearer ".Length..]);
        Assert.Equal([ApiKaynagi], jwt.Audiences);
        Assert.Contains(OkuKapsami, jwt.GetClaim("scope").Value.Split(' '));
    }

    [Theory]
    [InlineData("kapali")]
    [InlineData("500")]
    [InlineData("503")]
    [InlineData("zaman-asimi")]
    [InlineData("bozuk-json")]
    public async Task Api_ulasilamazsa_Turkce_sayfa_ve_stack_trace_yok(string durum)
    {
        var api = new SahteApi(async _ => durum switch
        {
            "kapali" => throw new HttpRequestException("Sahte bağlantı hatası."),
            "zaman-asimi" => await Bekle(),
            "bozuk-json" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{bozuk", Encoding.UTF8, "application/json") },
            _ => new HttpResponseMessage((HttpStatusCode)int.Parse(durum)) { Content = new StringContent("System.Exception: iç hata ayrıntısı") },
        });
        using var factory = new OtomasyonFactory<TProgram>(ayarlar: ApiAyarlari("00:00:00.300"), api: api);

        var yanit = await Istemci(AnaRol, factory).GetAsync(ApiOrnegi.Sayfa);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        var html = await OtomasyonFactory.MetinAsync(yanit);
        Assert.Contains("Veri kaynağına şu an ulaşılamıyor", html);
        StackTraceYok(html);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Api_yetki_vermezse_yetki_sayfasi(HttpStatusCode durum)
    {
        using var factory = new OtomasyonFactory<TProgram>(ayarlar: ApiAyarlari(),
            api: new SahteApi(_ => Task.FromResult(new HttpResponseMessage(durum))));

        var yanit = await Istemci(AnaRol, factory).GetAsync(ApiOrnegi.Sayfa);

        Assert.Equal(HttpStatusCode.Forbidden, yanit.StatusCode);
        var html = await OtomasyonFactory.MetinAsync(yanit);
        Assert.Contains("Bu verilere erişim yetkin yok", html);
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

    // ---------- Oturum: Back-Channel Logout ve çıkış ----------

    /// <summary>OIDC girişinden sonraki gibi: bilet sunucu tarafındaki depoda, çerezde yalnızca anahtarı.</summary>
    private async Task<(HttpClient Client, string Anahtar, CookieAuthenticationOptions Ayarlar)> GercekOturumAsync(
        OtomasyonFactory<TProgram> factory, string sid)
    {
        var client = factory.Istemci();
        var ayarlar = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "sub-203011001"), new Claim("sid", sid), new Claim("name", "Test Öğrenci"),
            new Claim("preferred_username", Kullanicilar[AnaRol].KullaniciAdi), new Claim("role", AnaRol),
            new Claim("bolum", OtomasyonFactory.Bilgisayar), new Claim("fakulte", OtomasyonFactory.Teknoloji),
        ], "test", "name", "role");
        var ozellikler = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
        ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = ApiTokeni() }]);
        Assert.IsType<OturumBiletDeposu>(ayarlar.SessionStore);
        var anahtar = await ayarlar.SessionStore!.StoreAsync(new AuthenticationTicket(new ClaimsPrincipal(identity), ozellikler,
            CookieAuthenticationDefaults.AuthenticationScheme));
        var cerez = ayarlar.TicketDataFormat.Protect(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("Microsoft.AspNetCore.Authentication.Cookies-SessionId", anahtar)],
                CookieAuthenticationDefaults.AuthenticationScheme)),
            CookieAuthenticationDefaults.AuthenticationScheme));
        client.DefaultRequestHeaders.Add("Cookie", $"{ayarlar.Cookie.Name}={cerez}");
        return (client, anahtar, ayarlar);
    }

    private static async Task BiletSilinmisAsync(OtomasyonFactory<TProgram> factory, CookieAuthenticationOptions ayarlar, string anahtar)
    {
        Assert.Null(await ayarlar.SessionStore!.RetrieveAsync(anahtar));
        Assert.Null(await factory.Services.GetRequiredService<IDistributedCache>().GetAsync("otomasyon-oturum:" + anahtar));
    }

    [Fact]
    public async Task Back_channel_logout_oturumu_kapatiyor_ve_bileti_hemen_siliyor()
    {
        using var factory = new OtomasyonFactory<TProgram>(sahteKimlik: false);
        const string Sid = "oturum-bcl-1";
        var (client, anahtar, ayarlar) = await GercekOturumAsync(factory, Sid);
        var ogrenciSayfasi = Menu.First(o => o.Roller.Contains(AnaRol)).Sayfa;

        Assert.Equal(CerezAdi, ayarlar.Cookie.Name);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ogrenciSayfasi)).StatusCode);
        var red = await client.GetAsync(Menu.First(o => !o.Roller.Contains(AnaRol)).Sayfa);
        Assert.StartsWith("/ErisimReddedildi", red.Headers.Location!.PathAndQuery);

        var simdi = DateTime.UtcNow;
        var logoutToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "logout+jwt",
            Issuer = OtomasyonFactory.Authority,
            Audience = ClientId,
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

        // Bilet, çerez bir daha gelmeden silinmiştir.
        await BiletSilinmisAsync(factory, ayarlar, anahtar);
        var sonra = await client.GetAsync(ogrenciSayfasi);
        Assert.Equal(HttpStatusCode.Redirect, sonra.StatusCode);
        Assert.StartsWith(OtomasyonFactory.Authority + "connect/authorize", sonra.Headers.Location!.AbsoluteUri);
    }

    [Fact]
    public async Task Cikis_oturum_biletini_hemen_siliyor()
    {
        using var factory = new OtomasyonFactory<TProgram>(sahteKimlik: false);
        var (client, anahtar, ayarlar) = await GercekOturumAsync(factory, "oturum-cikis-1");
        var sayfa = await OtomasyonFactory.MetinAsync(await client.GetAsync("/Cikis"));
        Assert.NotNull(await ayarlar.SessionStore!.RetrieveAsync(anahtar));

        var yanit = await client.PostAsync("/Cikis", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", OtomasyonFactory.AntiforgeryTokenAl(sayfa))]));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(OtomasyonFactory.Authority + "connect/logout", yanit.Headers.Location!.AbsoluteUri);
        await BiletSilinmisAsync(factory, ayarlar, anahtar);
    }

    /// <summary>API yerine: istekleri kaydeder ve verilen yanıtı döndürür.</summary>
    protected sealed class SahteApi(Func<HttpRequestMessage, Task<HttpResponseMessage>> yanit) : HttpMessageHandler
    {
        public sealed record Istek(string Adres, string? Yetki);

        public ConcurrentQueue<Istek> Istekler { get; } = new();

        public static Task<HttpResponseMessage> Json(object govde) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(govde, govde.GetType(), OtomasyonApiIstemcisi.JsonAyarlari),
                Encoding.UTF8, "application/json"),
        });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Enqueue(new Istek(request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString()));
            return await yanit(request).WaitAsync(cancellationToken);
        }
    }

    [GeneratedRegex("<p class=\"menu-grubu\" id=\"menu-grubu-\\d+\">([^<]+)</p>")]
    private static partial Regex MenuGrubu();

    [GeneratedRegex("<a href=\"([^\"]+)\"")]
    private static partial Regex Baglanti();

    [GeneratedRegex("<h1\\b")]
    private static partial Regex H1();

    [GeneratedRegex("(--uygulama-vurgu(?:-acik)?):\\s*(#[0-9A-Fa-f]{6})")]
    private static partial Regex Vurgu();
}
