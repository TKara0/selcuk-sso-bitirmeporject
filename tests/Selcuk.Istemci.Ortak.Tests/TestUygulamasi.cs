using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>
/// AddSelcukSSO kullanan küçük bir test uygulaması. Kimlik sunucusu yerine yerel bir RSA anahtar çifti ve
/// bu anahtarı yayınlayan sahte discovery/JWKS uç noktaları kullanılır (OIDC ara katmanının backchannel'ı).
/// İsteğe bağlı: ek "Kimlik" ayarları, birden çok örneğin paylaştığı dağıtık önbellek, ulaşılamayan JWKS ve log
/// yakalayıcı. Sahte sunucunun token ve revocation uç noktaları <see cref="TokenUcu"/> ile yönetilir.
/// </summary>
public sealed class TestUygulamasi : IAsyncDisposable
{
    public const string Issuer = "https://kimlik.test.local/";
    public const string ClientId = "test-istemci";
    public const string OlayAdi = "http://schemas.openid.net/event/backchannel-logout";
    private const string AnahtarKimligi = "test-anahtar";

    private readonly WebApplication _app;
    private readonly RsaSecurityKey _imzaAnahtari;

    private TestUygulamasi(WebApplication app, RsaSecurityKey imzaAnahtari, SahteTokenUcu tokenUcu)
    {
        _app = app;
        _imzaAnahtari = imzaAnahtari;
        TokenUcu = tokenUcu;
    }

    public IServiceProvider Services => _app.Services;

    /// <summary>Sahte kimlik sunucusunun token (refresh_token grant) ve revocation uç noktaları.</summary>
    public SahteTokenUcu TokenUcu { get; }

    public static async Task<TestUygulamasi> BaslatAsync(
        IDictionary<string, string?>? ekAyarlar = null,
        IDistributedCache? ortakOnbellek = null,
        bool jwksUlasilamaz = false,
        ILoggerProvider? logYakalayici = null)
    {
        var imzaAnahtari = new RsaSecurityKey(RSA.Create(2048)) { KeyId = AnahtarKimligi };
        var tokenUcu = new SahteTokenUcu();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:Authority"] = Issuer,
            ["Kimlik:ClientId"] = ClientId,
            ["Kimlik:ClientSecret"] = "kullanilmayan-test-degeri",
        });
        if (ekAyarlar is not null)
        {
            builder.Configuration.AddInMemoryCollection(ekAyarlar);
        }

        if (logYakalayici is not null)
        {
            builder.Logging.AddProvider(logYakalayici);
        }

        if (ortakOnbellek is not null)
        {
            // AddSelcukSSO'dan önce kaydedilen dağıtık önbellek kullanılır (uygulamanın Redis kaydetmesi gibi).
            builder.Services.AddSingleton(ortakOnbellek);
        }

        builder.Services.AddSelcukSSO(builder.Configuration);
        builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
            options => options.BackchannelHttpHandler = new SahteKimlikSunucusu(imzaAnahtari, jwksUlasilamaz, tokenUcu));

        var app = builder.Build();
        app.UseSelcukGuvenlikBasliklari();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSelcukBackChannelLogout();

        // Test için: verilen sid/sub ile yerel oturum çerezi açar (gerçekte OIDC girişinden sonra açılır).
        app.MapGet("/test/oturum-ac", async (HttpContext context, string sid, string sub) =>
        {
            var identity = new ClaimsIdentity([new Claim("sub", sub), new Claim("sid", sid), new Claim("name", "Test")],
                "test", "name", "role");
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Ok();
        });
        // Test için: OIDC girişinden sonraki gibi token'ları (SaveTokens) olan bir oturum açar; access token'ın kalan
        // süresi verilir.
        app.MapGet("/test/tokenli-oturum-ac", async (HttpContext context, int kalanSaniye) =>
        {
            var identity = new ClaimsIdentity([new Claim("sub", "kullanici-1"), new Claim("sid", "oturum-1"), new Claim("name", "Test")],
                "test", "name", "role");
            var ozellikler = new AuthenticationProperties();
            ozellikler.StoreTokens(
            [
                new AuthenticationToken { Name = "access_token", Value = "eski-at" },
                new AuthenticationToken { Name = "refresh_token", Value = "eski-rt" },
                new AuthenticationToken { Name = "id_token", Value = "eski-idt" },
                new AuthenticationToken
                {
                    Name = "expires_at",
                    Value = DateTimeOffset.UtcNow.AddSeconds(kalanSaniye).ToString("o", CultureInfo.InvariantCulture),
                },
            ]);
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), ozellikler);
            return Results.Ok();
        });
        // Dönüş türü açık: yoksa lambda RequestDelegate'e bağlanır ve sonuç yazılmaz.
        app.MapGet("/test/tokenlar", async Task<IResult> (HttpContext context) => Results.Json(new
        {
            access_token = await context.GetTokenAsync("access_token"),
            refresh_token = await context.GetTokenAsync("refresh_token"),
            expires_at = await context.GetTokenAsync("expires_at"),
        })).RequireAuthorization();
        app.MapGet("/test/cikis", () => Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
            [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]));
        app.MapGet("/korumali", () => "korumalı içerik").RequireAuthorization();
        // Demo uygulamalardaki /Giris sayfası gibi: kimlik sunucusuna OIDC challenge (initiate_login_uri).
        app.MapGet("/giris", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]));

        await app.StartAsync();
        return new TestUygulamasi(app, imzaAnahtari, tokenUcu);
    }

    /// <summary>Kendi çerez kabı olan yeni bir "tarayıcı".</summary>
    public HttpClient YeniTarayici() =>
        new(new CookieContainerHandler { InnerHandler = _app.GetTestServer().CreateHandler() })
        {
            BaseAddress = new Uri("https://localhost"),
        };
    public async Task<HttpClient> OturumAcmisTarayiciAsync(string sid, string sub = "kullanici-1")
    {
        var tarayici = YeniTarayici();
        var yanit = await tarayici.GetAsync($"/test/oturum-ac?sid={Uri.EscapeDataString(sid)}&sub={Uri.EscapeDataString(sub)}");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync("/korumali")).StatusCode);
        return tarayici;
    }

    /// <summary>Kimlik sunucusunun göndereceği gibi bir logout_token; testler claim'leri değiştirebilir.</summary>
    public string LogoutToken(string sid, string sub = "kullanici-1", string audience = ClientId,
        Action<Dictionary<string, object>>? degistir = null, SecurityKey? anahtar = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["sid"] = sid,
            ["sub"] = sub,
            ["events"] = new Dictionary<string, object> { [OlayAdi] = new Dictionary<string, object>() },
        };
        degistir?.Invoke(claims);

        var simdi = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "logout+jwt",
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = simdi,
            NotBefore = simdi,
            Expires = simdi.AddMinutes(2),
            Claims = claims,
            SigningCredentials = new SigningCredentials(anahtar ?? _imzaAnahtari, SecurityAlgorithms.RsaSha256),
        });
    }

    /// <summary>Aynı anahtar kimliğine (kid) sahip ama JWKS'te yayınlanmamış başka bir anahtar.</summary>
    public static SecurityKey YabanciAnahtar() => new RsaSecurityKey(RSA.Create(2048)) { KeyId = AnahtarKimligi };

    public async Task<HttpResponseMessage> LogoutGonderAsync(string token)
    {
        using var client = YeniTarayici();
        return await client.PostAsync("/signout-backchannel",
            new FormUrlEncodedContent([KeyValuePair.Create("logout_token", token)]));
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>
    /// Sahte token uç noktası: refresh_token grant'ine yeni access/refresh token (yeni-at-N, yeni-rt-N) veya istenirse
    /// invalid_grant döner; isteğe gecikme eklenebilir (eşzamanlılık testi). Revocation istekleri kaydedilir.
    /// </summary>
    public sealed class SahteTokenUcu
    {
        private int _yenilemeSayisi;

        public int YenilemeSayisi => _yenilemeSayisi;

        public bool InvalidGrant { get; set; }

        public TimeSpan Gecikme { get; set; }

        public ConcurrentQueue<IReadOnlyDictionary<string, string>> YenilemeIstekleri { get; } = new();

        public ConcurrentQueue<IReadOnlyDictionary<string, string>> IptalIstekleri { get; } = new();

        internal async Task<HttpResponseMessage> TokenAsync(IReadOnlyDictionary<string, string> form)
        {
            YenilemeIstekleri.Enqueue(form);
            var sira = Interlocked.Increment(ref _yenilemeSayisi);
            await Task.Delay(Gecikme);
            if (InvalidGrant || form.GetValueOrDefault("grant_type") != "refresh_token")
            {
                return Yanit(HttpStatusCode.BadRequest, new { error = "invalid_grant" });
            }

            return Yanit(HttpStatusCode.OK, new
            {
                access_token = "yeni-at-" + sira, refresh_token = "yeni-rt-" + sira, token_type = "Bearer", expires_in = 900,
            });
        }

        internal HttpResponseMessage Iptal(IReadOnlyDictionary<string, string> form)
        {
            IptalIstekleri.Enqueue(form);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private static HttpResponseMessage Yanit(HttpStatusCode durum, object govde) =>
            new(durum) { Content = new StringContent(JsonSerializer.Serialize(govde), Encoding.UTF8, "application/json") };
    }

    /// <summary>Kimlik sunucusunun discovery, JWKS, token ve revocation yanıtlarını taklit eder.</summary>
    private sealed class SahteKimlikSunucusu(RsaSecurityKey imzaAnahtari, bool jwksUlasilamaz, SahteTokenUcu tokenUcu)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var adres = request.RequestUri!.AbsoluteUri;
            string? json = null;

            if (adres is Issuer + "connect/token" or Issuer + "connect/revoke")
            {
                var form = (await request.Content!.ReadAsStringAsync(cancellationToken)).Split('&')
                    .Select(p => p.Split('=', 2))
                    .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => WebUtility.UrlDecode(p[1]));
                return adres == Issuer + "connect/token" ? await tokenUcu.TokenAsync(form) : tokenUcu.Iptal(form);
            }

            if (adres == Issuer + ".well-known/openid-configuration")
            {
                json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["issuer"] = Issuer,
                    ["authorization_endpoint"] = Issuer + "connect/authorize",
                    ["token_endpoint"] = Issuer + "connect/token",
                    ["userinfo_endpoint"] = Issuer + "connect/userinfo",
                    ["end_session_endpoint"] = Issuer + "connect/logout",
                    ["revocation_endpoint"] = Issuer + "connect/revoke",
                    ["jwks_uri"] = Issuer + ".well-known/jwks",
                });
            }
            else if (adres == Issuer + ".well-known/jwks")
            {
                if (jwksUlasilamaz)
                {
                    throw new HttpRequestException("Sahte bağlantı hatası.");
                }

                // Yalnızca açık anahtar yayınlanır.
                var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
                    new RsaSecurityKey(imzaAnahtari.Rsa.ExportParameters(false)) { KeyId = imzaAnahtari.KeyId });
                json = JsonSerializer.Serialize(new
                {
                    keys = new[] { new { kty = jwk.Kty, use = "sig", alg = "RS256", kid = jwk.Kid, n = jwk.N, e = jwk.E } },
                });
            }

            return json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
