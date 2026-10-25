using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using Selcuk.Identity;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
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
        // İki aşamalı doğrulama: Identity'nin TOTP (RFC 6238) sağlayıcısı, tekrar oynatma korumasıyla.
        options.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;

        // Oturum çerezindeki claim adları OIDC adlarıyla aynı olsun.
        options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
        options.ClaimsIdentity.UserNameClaimType = Claims.Name;
        options.ClaimsIdentity.RoleClaimType = Claims.Role;
        options.ClaimsIdentity.EmailClaimType = Claims.Email;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    // Kurtarma kodları düz metin değil, IPasswordHasher özetiyle saklanır.
    .AddUserStore<KurtarmaKoduOzetDeposu>()
    .AddTokenProvider<SifreSifirlamaTokenSaglayici>(SifreSifirlamaTokenSaglayici.Ad)
    .AddTokenProvider<TekKullanimlikTotpSaglayici>(TokenOptions.DefaultAuthenticatorProvider)
    .AddErrorDescriber<TurkceIdentityHatalari>();

builder.Services.AddOptions<IkiAdimAyarlari>().Configure<IConfiguration>(IkiAdimAyarlari.Oku);

// "Bu cihazı hatırla" çerezi (Identity TwoFactorRememberMe): IkiAdimCihazHatirlamaGun (varsayılan 30) gün.
builder.Services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.TwoFactorRememberMeScheme)
    .Configure<IOptions<IkiAdimAyarlari>>((options, ikiAdim) =>
        options.ExpireTimeSpan = TimeSpan.FromDays(ikiAdim.Value.CihazHatirlamaGun));

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
builder.Services.AddScoped<IkiAdimIslemleri>();
builder.Services.AddHizSiniri(builder.Configuration);

// Var olmayan kullanıcı adı ve kullanılmış TOTP kodları için anahtarlı özet (HMAC-SHA256). Anahtar Development dışında
// zorunludur; yoksa uygulama açılmaz (aşağıda, Build'den hemen sonra çözümlenir).
builder.Services.AddSingleton(sp => AnahtarliOzet.Olustur(sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<IHostEnvironment>(), sp.GetRequiredService<ILogger<AnahtarliOzet>>()));

// Denetim kaydı: sayfalar kuyruğa bırakır, arka plan hizmeti yazar; eski kayıtlar günde bir kez silinir (KVKK).
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DenetimKaydedici>();
builder.Services.AddHostedService<DenetimYazmaHizmeti>();
builder.Services.AddSingleton<DenetimTemizlemeHizmeti>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DenetimTemizlemeHizmeti>());

// X-Forwarded-For ve X-Forwarded-Proto yalnızca GuvenilenProxyler listesindeki adreslerden kabul edilir (boşsa hiç).
builder.Services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>(GuvenilenProxyler.Yapilandir);

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
            OidcKimlik.FakulteClaim, OidcKimlik.BolumClaim, OidcKimlik.SidClaim, Claims.AuthenticationMethodReference);

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

// Fail fast: HMAC anahtarı eksik veya geçersizse ilk istekte değil, açılışta dur.
app.Services.GetRequiredService<AnahtarliOzet>();

// İlk sırada: istek sınırı ve denetim kaydı gerçek istemci IP'sini görsün.
app.UseForwardedHeaders();
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
