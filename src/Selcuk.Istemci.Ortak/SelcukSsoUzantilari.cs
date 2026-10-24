using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Alt uygulamaları merkezi kimlik sunucusuna bağlayan ortak yapılandırma.
/// Ayarlar "Kimlik" bölümünden okunur: Authority, ClientId, ClientSecret (user-secrets) ve isteğe bağlı CerezAdi,
/// GirisHatasiYolu (varsayılan /GirisHatasi) ile OturumSuresi (varsayılan 8 saat, kimlik sunucusuyla aynı).
/// </summary>
public static class SelcukSsoUzantilari
{
    public static readonly TimeSpan VarsayilanOturumSuresi = TimeSpan.FromHours(8);

    public static IServiceCollection AddSelcukSSO(this IServiceCollection services, IConfiguration configuration)
    {
        var kimlik = configuration.GetSection("Kimlik");
        var clientId = kimlik["ClientId"]
            ?? throw new InvalidOperationException("'Kimlik:ClientId' ayarı tanımlı değil.");
        var girisHatasiYolu = kimlik["GirisHatasiYolu"] ?? GirisDonusHatasi.VarsayilanYol;
        var oturumSuresi = kimlik.GetValue<TimeSpan?>("OturumSuresi") ?? VarsayilanOturumSuresi;

        // Uygulama Redis veya SQL Server dağıtık önbelleği kaydettiyse o kullanılır; yoksa bellek içi.
        services.AddDistributedMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OturumIptalDeposu>();
        services.AddAuthorization();

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                // Belirtilmezse Forbid challenge şemasına (OIDC) düşer ve düz 403 döner; yetkisiz rol
                // çerez şemasının AccessDeniedPath'ine (Erişim reddedildi sayfası) yönlendirilsin.
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                // Aynı makinedeki uygulamalar yalnızca port ile ayrıldığından çerez adları farklı olmalı.
                options.Cookie.Name = kimlik["CerezAdi"] ?? $"{clientId}.Oturum";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.AccessDeniedPath = "/ErisimReddedildi";
                // Kimlik sunucusu oturumuyla aynı süre, kayan: kullanıcı süre boyunca işlem yapmazsa oturum kapanır.
                options.ExpireTimeSpan = oturumSuresi;
                options.SlidingExpiration = true;
                // Back-Channel Logout ile iptal edilen oturumlar her istekte reddedilir.
                options.Events.OnValidatePrincipal = OturumIptalDeposu.CerezDogrulaAsync;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = kimlik["Authority"];
                options.ClientId = clientId;
                // Gizli istemci: secret user-secrets'tan (Development) veya ortam değişkeninden okunur.
                options.ClientSecret = kimlik["ClientSecret"];

                options.ResponseType = OpenIdConnectResponseType.Code;
                // Kod 302 ile sorgu dizesinde döner. form_post'un otomatik gönderilen sayfası satır içi betik
                // kullandığı için kimlik sunucusunun CSP'sine takılırdı.
                options.ResponseMode = OpenIdConnectResponseMode.Query;
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add("roles");

                // Claim adları kimlik sunucusunun verdiği OIDC adlarıyla kalsın (name, role, fakulte, bolum, sid).
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
                // id_token'da zaten gelen bilgiler; userinfo yanıtında da varsa tekrar eklenmez.
                options.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");
                options.ClaimActions.MapUniqueJsonKey("fakulte", "fakulte");
                options.ClaimActions.MapUniqueJsonKey("bolum", "bolum");
                // sid, Back-Channel Logout'ta oturumu eşlemek için çerezde kalmalı.
                options.ClaimActions.Remove("sid");

                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
                options.SignedOutRedirectUri = "/";
                // Geri tuşu, süresi dolmuş giriş veya reddedilen onay: geliştirici hata sayfası yerine yönlendirme.
                options.Events.OnRemoteFailure = context => GirisDonusHatasi.IsleAsync(context, girisHatasiYolu);
                // initiate_login_uri'ye kimlik sunucusundan gelen iss parametresi doğrulanır.
                options.Events.OnRedirectToIdentityProvider = UcuncuTarafGiris.IssDogrulaAsync;
            });

        return services;
    }

    /// <summary>Tüm yanıtlara tarayıcı güvenlik başlıklarını ekler.</summary>
    public static IApplicationBuilder UseSelcukGuvenlikBasliklari(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // OnStarting: hata sayfası gibi yanıtı sıfırlayan ara katmanlardan sonra da başlıklar korunur.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.XFrameOptions = "DENY";
                headers.ContentSecurityPolicy = GuvenlikBasliklari.ContentSecurityPolicy;
                return Task.CompletedTask;
            });

            return next(context);
        });

    /// <summary>OIDC Back-Channel Logout alıcısı: POST /signout-backchannel.</summary>
    public static IEndpointConventionBuilder MapSelcukBackChannelLogout(this IEndpointRouteBuilder app) =>
        app.MapPost(BackChannelLogout.Yol, BackChannelLogout.AlAsync)
            .AllowAnonymous()
            // Çerez kullanmayan sunucudan sunucuya çağrı; koruma imzalı logout_token doğrulamasıyla sağlanır.
            .DisableAntiforgery();
}

public static class GuvenlikBasliklari
{
    // form-action bilerek kısıtlanmadı: çıkış formu kimlik sunucusunun end-session adresine yönlendirir.
    public const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
}
