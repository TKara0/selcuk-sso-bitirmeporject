using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Demo OBİS oturum bileti sunucu tarafında saklanır (AddSelcukSSO, SunucuTarafliOturum): çerezde yalnızca anahtar,
/// token'lar çerezde değil; çıkışta ve Back-Channel Logout'ta kayıt hemen silinir. Giriş, OIDC dönüşü yerine bir test
/// ucundan (/__test/giris) gerçek çerez şemasıyla yapılır; kimlik sunucusuna ağ isteği yoktur.
/// </summary>
public sealed class SunucuTarafliOturumTests
{
    private const string CerezAdi = "DemoObis.Oturum";
    private const string Sid = "oturum-demo-obis-1";
    private const string Sub = "sub-203011001";
    private const string Authority = "https://localhost:5001/";

    // Gerçek token'lardan büyük: çerezde olsaydı birkaç parçaya bölünürdü (4 KB sınırı).
    private static readonly string AccessToken = "a" + new string('A', 3000);
    private static readonly string IdToken = "i" + new string('I', 2500);
    private static readonly string RefreshToken = "r" + new string('R', 1500);

    private static readonly RsaSecurityKey ImzaAnahtari = new(RSA.Create(2048)) { KeyId = "demo-test" };

    private sealed class GirisUcu : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, devam) =>
            {
                if (context.Request.Path != "/__test/giris")
                {
                    await devam(context);
                    return;
                }

                var kimlik = new ClaimsIdentity(
                [
                    new Claim("sub", Sub), new Claim("sid", Sid), new Claim("name", "Test Öğrenci"),
                    new Claim("preferred_username", "203011001"), new Claim("role", "ogrenci"),
                ], "test", "name", "role");
                var ozellikler = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
                var tokenlar = new List<AuthenticationToken>
                {
                    new() { Name = "access_token", Value = AccessToken },
                    new() { Name = "id_token", Value = IdToken },
                };
                if (context.Request.Query.ContainsKey("refresh"))
                {
                    tokenlar.Add(new AuthenticationToken { Name = "refresh_token", Value = RefreshToken });
                }

                ozellikler.StoreTokens(tokenlar);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(kimlik), ozellikler);
            });
            next(app);
        };
    }

    /// <summary>Çerez şeması gerçek; kimlik sunucusu taklit (sabit discovery, Back-Channel Logout için imza anahtarı).</summary>
    private static WebApplicationFactory<Program> Uygulama(ObisFactory kok) => kok.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<IStartupFilter, GirisUcu>();
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
                options.Configuration!.SigningKeys.Add(new RsaSecurityKey(ImzaAnahtari.Rsa.ExportParameters(false)) { KeyId = ImzaAnahtari.KeyId }));
        }));

    private static HttpClient Istemci(WebApplicationFactory<Program> uygulama) => uygulama.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    private static CookieAuthenticationOptions CerezAyarlari(WebApplicationFactory<Program> uygulama) =>
        uygulama.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

    private static string CerezDegeri(string setCookie) => setCookie[(setCookie.IndexOf('=') + 1)..].Split(';')[0];

    [Fact]
    public async Task Giris_sonrasi_oturum_tek_kucuk_cerez_cerezde_token_yok_token_lar_depoda()
    {
        using var kok = new ObisFactory();
        using var uygulama = Uygulama(kok);
        var ayarlar = CerezAyarlari(uygulama);

        var yanit = await Istemci(uygulama).GetAsync("/__test/giris?refresh=1");

        // Tek çerez: parça (.C1, .C2…) yok, adı yapılandırılan çerez adı.
        var cerezler = yanit.Headers.GetValues("Set-Cookie").ToList();
        var oturum = Assert.Single(cerezler);
        Assert.StartsWith(CerezAdi + "=", oturum);
        Assert.DoesNotContain(".C1", oturum);
        Assert.Contains("httponly", oturum, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", oturum, StringComparison.OrdinalIgnoreCase);

        // Çerez küçük (token'lar toplam 7 KB'tan fazla) ve içinde token yok: yalnızca oturum anahtarı.
        var deger = CerezDegeri(oturum);
        Assert.True(deger.Length < 500, $"Çerez değeri {deger.Length} karakter.");
        foreach (var token in new[] { AccessToken, IdToken, RefreshToken })
        {
            Assert.DoesNotContain(token[..100], oturum);
        }

        var bilet = ayarlar.TicketDataFormat.Unprotect(deger)!;
        Assert.Empty(bilet.Properties.GetTokens());
        var anahtar = Assert.Single(bilet.Principal.Claims).Value;
        Assert.Equal("Microsoft.AspNetCore.Authentication.Cookies-SessionId", Assert.Single(bilet.Principal.Claims).Type);

        // Token'lar sunucu tarafındaki depoda (Data Protection ile şifreli).
        Assert.IsType<Selcuk.Otomasyon.Ortak.OturumBiletDeposu>(ayarlar.SessionStore);
        var depoda = (await ayarlar.SessionStore!.RetrieveAsync(anahtar))!;
        Assert.Equal(AccessToken, depoda.Properties.GetTokenValue("access_token"));
        Assert.Equal(RefreshToken, depoda.Properties.GetTokenValue("refresh_token"));
        var ham = await uygulama.Services.GetRequiredService<IDistributedCache>().GetAsync("otomasyon-oturum:" + anahtar);
        Assert.DoesNotContain(AccessToken[..100], Encoding.UTF8.GetString(ham!));
    }

    private sealed class SahteDemoApi : HttpMessageHandler
    {
        public ConcurrentQueue<HttpRequestMessage> Istekler { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Enqueue(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ogrenci":"x","donem":"2026-2027 Güz","notlar":[{"kod":"DEP101","ad":"Depodan Gelen Oturum","vize":80,"final":90,"harf":"AA"}]}""",
                    Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Notlarim_ve_Token_Inceleme_depodaki_token_larla_calisiyor()
    {
        var api = new SahteDemoApi();
        using var kok = new ObisFactory(demoApi: api, ortam: "Development");
        using var uygulama = Uygulama(kok);
        var client = Istemci(uygulama);
        await client.GetAsync("/__test/giris?refresh=1");

        var notlarim = await client.GetAsync("/Notlarim");

        Assert.Equal(HttpStatusCode.OK, notlarim.StatusCode);
        Assert.Contains("Depodan Gelen Oturum", WebUtility.HtmlDecode(await notlarim.Content.ReadAsStringAsync()));
        // Demo API çağrısı, çerezde olmayan access token'ı depodan alıp Bearer olarak gönderir.
        var istek = Assert.Single(api.Istekler);
        Assert.Equal(AccessToken, istek.Headers.Authorization!.Parameter);

        // Token İnceleme (Development) de oturumdaki token'ları depodan okur (test token'ları JWT değil; refresh token var).
        var inceleme = await client.GetAsync("/TokenInceleme");
        Assert.Equal(HttpStatusCode.OK, inceleme.StatusCode);
        var incelemeHtml = WebUtility.HtmlDecode(await inceleme.Content.ReadAsStringAsync());
        Assert.Contains("id_token", incelemeHtml);
        Assert.Contains("access_token", incelemeHtml);
        Assert.Contains("(JWT biçiminde değil)", incelemeHtml);
        Assert.Contains("refresh_token", incelemeHtml);
        Assert.DoesNotContain(AccessToken[..100], incelemeHtml);
    }

    [Fact]
    public async Task Cikis_oturum_kaydini_hemen_siliyor()
    {
        using var kok = new ObisFactory();
        using var uygulama = Uygulama(kok);
        var client = Istemci(uygulama);
        var ayarlar = CerezAyarlari(uygulama);
        var giris = await client.GetAsync("/__test/giris");
        var anahtar = ayarlar.TicketDataFormat.Unprotect(CerezDegeri(giris.Headers.GetValues("Set-Cookie").Single()))!
            .Principal.Claims.Single().Value;
        Assert.NotNull(await ayarlar.SessionStore!.RetrieveAsync(anahtar));
        var sayfa = await (await client.GetAsync("/Cikis")).Content.ReadAsStringAsync();
        var antiforgery = Regex.Match(sayfa, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(antiforgery));

        var yanit = await client.PostAsync("/Cikis", new FormUrlEncodedContent([KeyValuePair.Create("__RequestVerificationToken", antiforgery)]));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(Authority + "connect/logout", yanit.Headers.Location!.AbsoluteUri);
        Assert.Null(await ayarlar.SessionStore!.RetrieveAsync(anahtar));
        Assert.Null(await uygulama.Services.GetRequiredService<IDistributedCache>().GetAsync("otomasyon-oturum:" + anahtar));
    }

    [Fact]
    public async Task Back_channel_logout_oturum_kaydini_hemen_siliyor_cerez_bir_daha_gecmiyor()
    {
        using var kok = new ObisFactory();
        using var uygulama = Uygulama(kok);
        var client = Istemci(uygulama);
        var ayarlar = CerezAyarlari(uygulama);
        var giris = await client.GetAsync("/__test/giris");
        var anahtar = ayarlar.TicketDataFormat.Unprotect(CerezDegeri(giris.Headers.GetValues("Set-Cookie").Single()))!
            .Principal.Claims.Single().Value;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Profilim")).StatusCode);

        var simdi = DateTime.UtcNow;
        var logoutToken = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "logout+jwt",
            Issuer = Authority,
            Audience = "demo-obis",
            IssuedAt = simdi,
            NotBefore = simdi,
            Expires = simdi.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                ["jti"] = Guid.NewGuid().ToString("N"),
                ["sid"] = Sid,
                ["sub"] = Sub,
                ["events"] = new Dictionary<string, object> { ["http://schemas.openid.net/event/backchannel-logout"] = new Dictionary<string, object>() },
            },
            SigningCredentials = new SigningCredentials(ImzaAnahtari, SecurityAlgorithms.RsaSha256),
        });
        var bildirim = await Istemci(uygulama).PostAsync("/signout-backchannel",
            new FormUrlEncodedContent([KeyValuePair.Create("logout_token", logoutToken)]));
        Assert.Equal(HttpStatusCode.OK, bildirim.StatusCode);

        // Kayıt, çerez bir daha gelmeden silinmiştir; aynı çerezle sayfa artık girişe yönlendirir.
        Assert.Null(await ayarlar.SessionStore!.RetrieveAsync(anahtar));
        Assert.Null(await uygulama.Services.GetRequiredService<IDistributedCache>().GetAsync("otomasyon-oturum:" + anahtar));
        var sonra = await client.GetAsync("/Profilim");
        Assert.Equal(HttpStatusCode.Redirect, sonra.StatusCode);
        Assert.StartsWith(Authority + "connect/authorize", sonra.Headers.Location!.AbsoluteUri);
    }

    [Fact]
    public async Task Cikis_ve_Uygulamalarim_baglantisi_girisli_sayfada_duruyor_href_Authority_kokune_esit()
    {
        using var kok = new ObisFactory();
        using var uygulama = Uygulama(kok);
        var client = Istemci(uygulama);
        await client.GetAsync("/__test/giris");

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/")).Content.ReadAsStringAsync());

        Assert.Contains($"<a class=\"uygulamalarim-baglantisi\" href=\"{Authority}\">Uygulamalarım</a>", html);
        Assert.Contains("<button type=\"submit\">Çıkış yap</button>", html);
    }
}
