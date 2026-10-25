using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Demo OBİS'i kimlik sunucusu olmadan çalıştırır. Discovery bilgisi sabit verilir; böylece
/// OpenID Connect challenge'ı ağ isteği yapmadan üretilir. sahteKimlik açıkken kullanıcı,
/// istekteki X-Test-Rol başlığından oluşturulur (gerçek kimlik sunucusu yerine); oturumda sahte bir access token
/// (<see cref="SahteAccessToken"/>) bulunur. Demo API'ye istekler demoApi işleyicisine gider; verilmezse API kapalıdır
/// (bağlantı hatası), testler ağa çıkmaz.
/// </summary>
public sealed class ObisFactory(bool sahteKimlik = false, HttpMessageHandler? demoApi = null) : WebApplicationFactory<Program>
{
    public const string RolBasligi = "X-Test-Rol";
    public const string SahteAccessToken = "test-erisim-tokeni";
    private const string TestSemasi = "Test";
    private const string Authority = "https://localhost:5001/";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = Authority,
                    AuthorizationEndpoint = Authority + "connect/authorize",
                    TokenEndpoint = Authority + "connect/token",
                    UserInfoEndpoint = Authority + "connect/userinfo",
                    EndSessionEndpoint = Authority + "connect/logout",
                };
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
            });

            services.AddHttpClient(NotlarApiIstemcisi.Ad)
                .ConfigurePrimaryHttpMessageHandler(() => demoApi ?? new KapaliApi());

            if (sahteKimlik)
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SahteKimlikHandler>(TestSemasi, null);
                // Kimlik doğrulama sahte şemadan; yetkisiz rol için Forbid uygulamanın çerez şemasında kalır
                // (Erişim reddedildi sayfasına yönlendirme gerçek yapılandırmayla test edilir).
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestSemasi;
                    options.DefaultChallengeScheme = TestSemasi;
                });
            }
        });
    }

    public HttpClient HttpsIstemcisi(string? rol = null)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });
        if (rol is not null)
        {
            client.DefaultRequestHeaders.Add(RolBasligi, rol);
        }

        return client;
    }

    private sealed class SahteKimlikHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(RolBasligi, out var rol))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
            [
                new Claim("sub", "test-kullanici"),
                new Claim("name", "Test Kullanıcısı"),
                new Claim("role", rol.ToString()),
            ], Scheme.Name, "name", "role");

            var ozellikler = new AuthenticationProperties();
            ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = SahteAccessToken }]);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), ozellikler, Scheme.Name)));
        }
    }

    /// <summary>Demo API kapalıymış gibi: her istekte bağlantı hatası.</summary>
    private sealed class KapaliApi : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Sahte bağlantı hatası (Demo API kapalı).");
    }
}
