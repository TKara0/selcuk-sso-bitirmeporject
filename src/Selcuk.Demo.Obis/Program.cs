using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        // Belirtilmezse Forbid challenge şemasına (OIDC) düşer ve düz 403 döner; yetkisiz rol
        // çerez şemasının AccessDeniedPath'ine (Erişim reddedildi sayfası) yönlendirilsin.
        options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "DemoObis.Oturum";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.AccessDeniedPath = "/ErisimReddedildi";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["Kimlik:Authority"];
        options.ClientId = builder.Configuration["Kimlik:ClientId"];
        // Gizli istemci: secret user-secrets'tan (Development) veya ortam değişkeninden okunur.
        options.ClientSecret = builder.Configuration["Kimlik:ClientSecret"];

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

        // Claim adları kimlik sunucusunun verdiği OIDC adlarıyla kalsın (name, role, fakulte, bolum).
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
        // id_token'da zaten gelen bilgiler; userinfo yanıtında da varsa tekrar eklenmez.
        options.ClaimActions.MapUniqueJsonKey("preferred_username", "preferred_username");
        options.ClaimActions.MapUniqueJsonKey("fakulte", "fakulte");
        options.ClaimActions.MapUniqueJsonKey("bolum", "bolum");

        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SignedOutRedirectUri = "/";
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Hata");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();

public partial class Program;
