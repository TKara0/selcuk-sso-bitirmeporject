using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Server;
using Selcuk.Identity;
using Selcuk.Identity.Data;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SelcukSSO"));
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;

        // Şifre sıfırlama token'ı ayrı sağlayıcıdan: 30 dakika geçerli, güvenlik damgasına bağlı (tek kullanımlık).
        options.Tokens.PasswordResetTokenProvider = SifreSifirlamaTokenSaglayici.Ad;

        // Oturum çerezindeki claim adları OIDC adlarıyla aynı olsun.
        options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
        options.ClaimsIdentity.UserNameClaimType = Claims.Name;
        options.ClaimsIdentity.RoleClaimType = Claims.Role;
        options.ClaimsIdentity.EmailClaimType = Claims.Email;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddTokenProvider<SifreSifirlamaTokenSaglayici>(SifreSifirlamaTokenSaglayici.Ad)
    .AddErrorDescriber<TurkceIdentityHatalari>();

// Kimlik sunucusu oturumu ve istemci çerezleri aynı süreyi kullanır (istemcide Kimlik:OturumSuresi).
var oturumSuresi = builder.Configuration.GetValue<TimeSpan?>("Oturum:Sure") ?? TimeSpan.FromHours(8);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SelcukSSO.Oturum";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // Lax: istemci uygulamadan /connect/authorize'a yapılan yönlendirmede oturum çerezi gönderilebilsin.
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = oturumSuresi;
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/connect/logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    // Her oturuma bir oturum kimliği (sid) verilir; id_token'a ve Back-Channel Logout'a girer.
    options.Events.OnSigningIn = OturumKimligi.OturumAcilirkenAsync;
    // Güvenlik damgası her istekte kontrol edilir: şifre değişince diğer oturumlar hemen kapanır.
    options.Events.OnValidatePrincipal = OturumDamgasi.DogrulaAsync;
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.OnRefreshingPrincipal = OturumKimligi.PrincipalYenilenirkenAsync);

builder.Services.TryAddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(BackChannelLogoutGonderici.HttpIstemciAdi,
    client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<BackChannelLogoutGonderici>();

// E-posta: Development'ta App_Data/eposta/ klasörüne .eml, diğer ortamlarda SMTP. Gönderim arka planda (kuyruk).
builder.Services.Configure<EpostaAyarlari>(builder.Configuration.GetSection(EpostaAyarlari.Bolum));
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IEpostaGonderici, DosyaEpostaGonderici>();
}
else
{
    builder.Services.AddSingleton<IEpostaGonderici, SmtpEpostaGonderici>();
}

builder.Services.AddSingleton<EpostaKuyrugu>();
builder.Services.AddHostedService<EpostaGonderimHizmeti>();

builder.Services.AddScoped<SahteSifreDogrulayici>();
builder.Services.AddScoped<SifreDegisikligi>();
builder.Services.AddHizSiniri(builder.Configuration);

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "SelcukSSO.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Uygulamadaki tüm çerezler için alt sınır (Identity'nin dış giriş çerezleri dahil).
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.Secure = CookieSecurePolicy.Always;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<ApplicationDbContext>())
    .AddServer(options =>
    {
        var issuer = builder.Configuration["OpenIddict:Issuer"]
            ?? throw new InvalidOperationException("'OpenIddict:Issuer' ayarı tanımlı değil.");
        options.SetIssuer(new Uri(issuer));

        options.SetAuthorizationEndpointUris("connect/authorize")
               .SetTokenEndpointUris("connect/token")
               .SetUserInfoEndpointUris("connect/userinfo")
               .SetEndSessionEndpointUris("connect/logout");

        // Yalnızca Authorization Code (+ PKCE) ve Refresh Token. Implicit ve password grant kapalı.
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();
        options.RequireProofKeyForCodeExchange();
        // Yalnızca S256: "plain" yöntemi code_verifier'ı açıkta taşıdığı için kabul edilmez.
        options.Configure(server => server.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));

        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles);
        options.RegisterClaims(Claims.Subject, Claims.Name, Claims.Email, Claims.Role,
            OidcKimlik.FakulteClaim, OidcKimlik.BolumClaim, OidcKimlik.SidClaim);

        // OIDC Back-Channel Logout 1.0 desteği discovery'de ilan edilir (gönderim: BackChannelLogoutGonderici).
        options.AddEventHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>(handler =>
            handler.UseInlineHandler(context =>
            {
                context.Metadata["backchannel_logout_supported"] = true;
                context.Metadata["backchannel_logout_session_supported"] = true;
                return default;
            }));

        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else if (builder.Environment.IsEnvironment("Testing"))
        {
            options.AddEphemeralEncryptionKey()
                   .AddEphemeralSigningKey();
        }
        else
        {
            throw new InvalidOperationException(
                "Production imzalama/şifreleme sertifikaları henüz yapılandırılmadı (README > Yapılacaklar).");
        }

        // Access token'lar imzalı ama şifresiz JWT: içerikleri jwt.io vb. ile incelenebilir.
        options.DisableAccessTokenEncryption();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough()
               .EnableEndSessionEndpointPassthrough();
    });

builder.Services.AddRazorPages();
builder.Services.AddHostedService<BaslangicVerisi>();

var app = builder.Build();

app.UseGuvenlikBasliklari();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Hata");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCookiePolicy();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapOidcUcNoktalari();

app.Run();

public partial class Program;
