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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>
/// AddSelcukSSO kullanan küçük bir test uygulaması. Kimlik sunucusu yerine yerel bir RSA anahtar çifti ve
/// bu anahtarı yayınlayan sahte discovery/JWKS uç noktaları kullanılır (OIDC ara katmanının backchannel'ı).
/// </summary>
public sealed class TestUygulamasi : IAsyncDisposable
{
    public const string Issuer = "https://kimlik.test.local/";
    public const string ClientId = "test-istemci";
    public const string OlayAdi = "http://schemas.openid.net/event/backchannel-logout";
    private const string AnahtarKimligi = "test-anahtar";

    private readonly WebApplication _app;
    private readonly RsaSecurityKey _imzaAnahtari;

    private TestUygulamasi(WebApplication app, RsaSecurityKey imzaAnahtari)
    {
        _app = app;
        _imzaAnahtari = imzaAnahtari;
    }

    public static async Task<TestUygulamasi> BaslatAsync()
    {
        var imzaAnahtari = new RsaSecurityKey(RSA.Create(2048)) { KeyId = AnahtarKimligi };

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:Authority"] = Issuer,
            ["Kimlik:ClientId"] = ClientId,
            ["Kimlik:ClientSecret"] = "kullanilmayan-test-degeri",
        });

        builder.Services.AddSelcukSSO(builder.Configuration);
        builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
            options => options.BackchannelHttpHandler = new SahteKimlikSunucusu(imzaAnahtari));

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
        app.MapGet("/korumali", () => "korumalı içerik").RequireAuthorization();

        await app.StartAsync();
        return new TestUygulamasi(app, imzaAnahtari);
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

    /// <summary>Kimlik sunucusunun discovery ve JWKS yanıtlarını taklit eder.</summary>
    private sealed class SahteKimlikSunucusu(RsaSecurityKey imzaAnahtari) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var adres = request.RequestUri!.AbsoluteUri;
            string? json = null;

            if (adres == Issuer + ".well-known/openid-configuration")
            {
                json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["issuer"] = Issuer,
                    ["authorization_endpoint"] = Issuer + "connect/authorize",
                    ["token_endpoint"] = Issuer + "connect/token",
                    ["userinfo_endpoint"] = Issuer + "connect/userinfo",
                    ["end_session_endpoint"] = Issuer + "connect/logout",
                    ["jwks_uri"] = Issuer + ".well-known/jwks",
                });
            }
            else if (adres == Issuer + ".well-known/jwks")
            {
                // Yalnızca açık anahtar yayınlanır.
                var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(
                    new RsaSecurityKey(imzaAnahtari.Rsa.ExportParameters(false)) { KeyId = imzaAnahtari.KeyId });
                json = JsonSerializer.Serialize(new
                {
                    keys = new[] { new { kty = jwk.Kty, use = "sig", alg = "RS256", kid = jwk.Kid, n = jwk.N, e = jwk.E } },
                });
            }

            return Task.FromResult(json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
