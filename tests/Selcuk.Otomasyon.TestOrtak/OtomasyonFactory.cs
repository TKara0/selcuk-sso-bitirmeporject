using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.TestOrtak;

/// <summary>
/// Bir bölüm otomasyonunu kimlik sunucusu olmadan çalıştırır (Staj'daki StajFactory'nin genel hâli; bu dosya yeni
/// otomasyonların test projelerine bağlantıyla eklenir). Discovery bilgisi ve imza anahtarı sabittir (ağ yok;
/// <see cref="ImzaAnahtari"/> Back-Channel Logout testinde logout_token imzalar). sahteKimlik açıkken kullanıcı
/// istekteki başlıklardan oluşturulur (X-Test-Kullanici, virgülle ayrılmış X-Test-Rol, X-Test-Bolum, X-Test-Fakulte);
/// oturumda accessToken bulunur. Api modunda veri kaynağı istekleri api işleyicisine gider; verilmezse API kapalıdır.
/// ayarlar appsettings değerlerini geçersiz kılar; ortam verilirse o ortamda çalışır.
/// </summary>
public partial class OtomasyonFactory<TProgram>(
    bool sahteKimlik = true,
    IReadOnlyDictionary<string, string?>? ayarlar = null,
    HttpMessageHandler? api = null,
    string accessToken = OtomasyonFactory.SahteAccessToken,
    string ortam = "Testing") : WebApplicationFactory<TProgram> where TProgram : class
{
    private const string TestSemasi = "Test";

    public RsaSecurityKey ImzaAnahtari { get; } = new(RSA.Create(2048)) { KeyId = "otomasyon-test" };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ortam);
        builder.UseStaticWebAssets();
        if (ayarlar is not null)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(ayarlar));
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(new OtomasyonFactory.SahteToken(accessToken));
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                var yapilandirma = new OpenIdConnectConfiguration
                {
                    Issuer = OtomasyonFactory.Authority,
                    AuthorizationEndpoint = OtomasyonFactory.Authority + "connect/authorize",
                    TokenEndpoint = OtomasyonFactory.Authority + "connect/token",
                    UserInfoEndpoint = OtomasyonFactory.Authority + "connect/userinfo",
                    EndSessionEndpoint = OtomasyonFactory.Authority + "connect/logout",
                };
                yapilandirma.SigningKeys.Add(new RsaSecurityKey(ImzaAnahtari.Rsa.ExportParameters(false)) { KeyId = ImzaAnahtari.KeyId });
                options.Configuration = yapilandirma;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(yapilandirma);
            });
            services.AddHttpClient(OtomasyonApiIstemcisi.HttpIstemciAdi)
                .ConfigurePrimaryHttpMessageHandler(() => api ?? new OtomasyonFactory.KapaliApi());

            if (sahteKimlik)
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, OtomasyonFactory.SahteKimlikHandler>(TestSemasi, null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestSemasi;
                    options.DefaultChallengeScheme = TestSemasi;
                });
            }
        });
    }

    /// <summary>Çerezleri tutan (antiforgery, TempData), yönlendirmeyi izlemeyen bir istemci; rol yoksa girişsiz.</summary>
    public HttpClient Istemci(string? kullaniciAdi = null, string? bolum = OtomasyonFactory.Bilgisayar,
        string? fakulte = OtomasyonFactory.Teknoloji, params string[] roller)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        if (roller.Length > 0)
        {
            client.DefaultRequestHeaders.Add(OtomasyonFactory.RolBasligi, string.Join(",", roller));
            foreach (var (baslik, deger) in new[]
                     {
                         (OtomasyonFactory.KullaniciBasligi, kullaniciAdi), (OtomasyonFactory.BolumBasligi, bolum),
                         (OtomasyonFactory.FakulteBasligi, fakulte),
                     })
            {
                if (deger is not null)
                {
                    client.DefaultRequestHeaders.Add(baslik, Uri.EscapeDataString(deger));
                }
            }
        }

        return client;
    }
}

/// <summary>Fabrikanın sabitleri ve yardımcıları.</summary>
public static partial class OtomasyonFactory
{
    public const string SahteAccessToken = "test-erisim-tokeni";
    public const string KullaniciBasligi = "X-Test-Kullanici";
    public const string RolBasligi = "X-Test-Rol";
    public const string BolumBasligi = "X-Test-Bolum";
    public const string FakulteBasligi = "X-Test-Fakulte";
    public const string Authority = "https://localhost:5001/";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string Teknoloji = "Teknoloji Fakültesi";

    public static async Task<string> MetinAsync(HttpResponseMessage yanit) =>
        WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());

    /// <summary>Sayfadaki kayıtların kimlikleri (data-&lt;veriAdi&gt;), sırasıyla.</summary>
    public static IReadOnlyList<string> Kayitlar(string html, string veriAdi) =>
        [.. Regex.Matches(html, $"data-{veriAdi}=\"([^\"]+)\"").Select(m => m.Groups[1].Value)];

    public static string AntiforgeryTokenAl(string html)
    {
        var token = AntiforgeryToken().Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Formda antiforgery token bulunamadı.");
        return token;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();

    internal sealed record SahteToken(string Deger);

    internal sealed class SahteKimlikHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, SahteToken token)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolBasligi, out var rol))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string? Deger(string baslik) =>
                Request.Headers.TryGetValue(baslik, out var d) ? Uri.UnescapeDataString(d.ToString()) : null;

            var kullaniciAdi = Deger(KullaniciBasligi) ?? "test.kullanici";
            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "sub-" + kullaniciAdi),
                new Claim("name", "Test Kullanıcısı"),
                new Claim("preferred_username", kullaniciAdi),
                .. rol.ToString().Split(',').Select(r => new Claim("role", r)),
            ], Scheme.Name, "name", "role");
            foreach (var (baslik, claim) in new[] { (BolumBasligi, "bolum"), (FakulteBasligi, "fakulte") })
            {
                if (Deger(baslik) is { } deger)
                {
                    identity.AddClaim(new Claim(claim, deger));
                }
            }

            var ozellikler = new AuthenticationProperties();
            ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token.Deger }]);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), ozellikler, Scheme.Name)));
        }
    }

    /// <summary>API kapalıymış gibi: her istekte bağlantı hatası.</summary>
    internal sealed class KapaliApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Sahte bağlantı hatası (API kapalı).");
    }
}
