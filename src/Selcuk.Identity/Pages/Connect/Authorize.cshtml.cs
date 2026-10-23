using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Pages.Connect;

/// <summary>
/// OIDC authorize uç noktası. İsteği OpenIddict doğrular (istemci, redirect_uri, PKCE);
/// bu sayfa kullanıcı girişini ve ilk girişteki onay ekranını yönetir.
/// </summary>
public class AuthorizeModel(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public string UygulamaAdi { get; private set; } = string.Empty;

    public IReadOnlyList<string> Kapsamlar { get; private set; } = [];

    public static (string Baslik, string Aciklama) KapsamBilgisi(string kapsam) => kapsam switch
    {
        Scopes.OpenId => ("Kimlik", "Hesabınıza ait benzersiz kimlik numarası"),
        Scopes.Profile => ("Profil", "Ad soyadınız, kullanıcı adınız / öğrenci numaranız, fakülte ve bölüm bilginiz"),
        Scopes.Email => ("E-posta", "E-posta adresiniz"),
        Scopes.Roles => ("Rol", "Rolünüz (öğrenci, akademisyen veya personel)"),
        _ => (kapsam, "Uygulamaya özel izin"),
    };

    public async Task<IActionResult> OnGetAsync()
    {
        var request = OidcIstegi();
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!result.Succeeded || request.HasPromptValue(PromptValues.Login) || OturumSuresiAsildi(request, result))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return Reddet(Errors.LoginRequired, "Kullanıcı oturum açmamış.");
            }

            // prompt=login giriş sonrasında tekrar uygulanmasın diye çıkarılır.
            var prompt = string.Join(" ", request.GetPromptValues().Remove(PromptValues.Login));
            var parametreler = Request.Query.Where(p => p.Key != Parameters.Prompt).ToList();
            parametreler.Add(KeyValuePair.Create(Parameters.Prompt, new StringValues(prompt)));

            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parametreler),
            }, IdentityConstants.ApplicationScheme);
        }

        var user = await userManager.GetUserAsync(result.Principal)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
        var application = await UygulamaAsync(request);
        var authorizations = await YetkilendirmelerAsync(user, application, request);

        switch (await applicationManager.GetConsentTypeAsync(application))
        {
            case ConsentTypes.External when authorizations.Count is 0:
                return Reddet(Errors.ConsentRequired, "Bu uygulamaya erişim yetkiniz yok.");

            case ConsentTypes.Implicit:
            case ConsentTypes.External:
            case ConsentTypes.Explicit when authorizations.Count is not 0 && !request.HasPromptValue(PromptValues.Consent):
                return await OturumAcAsync(user, application, request, authorizations.LastOrDefault());

            case ConsentTypes.Explicit when request.HasPromptValue(PromptValues.None):
            case ConsentTypes.Systematic when request.HasPromptValue(PromptValues.None):
                return Reddet(Errors.ConsentRequired, "Kullanıcı onayı gerekli.");

            default:
                UygulamaAdi = await applicationManager.GetLocalizedDisplayNameAsync(application) ?? request.ClientId!;
                Kapsamlar = request.GetScopes();
                return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(string? karar)
    {
        var request = OidcIstegi();
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Reddet(Errors.LoginRequired, "Kullanıcı oturum açmamış.");
        }

        if (karar != "kabul")
        {
            return Reddet(Errors.AccessDenied, "Kullanıcı erişim iznini reddetti.");
        }

        var application = await UygulamaAsync(request);
        var authorizations = await YetkilendirmelerAsync(user, application, request);

        if (authorizations.Count is 0 && await applicationManager.HasConsentTypeAsync(application, ConsentTypes.External))
        {
            return Reddet(Errors.ConsentRequired, "Bu uygulamaya erişim yetkiniz yok.");
        }

        return await OturumAcAsync(user, application, request, authorizations.LastOrDefault());
    }

    private async Task<IActionResult> OturumAcAsync(
        ApplicationUser user, object application, OpenIddictRequest request, object? authorization)
    {
        var identity = OidcKimlik.YeniKimlik();
        await OidcKimlik.KullaniciClaimleriniEkleAsync(identity, user, userManager);

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        // Kalıcı yetkilendirme, kullanıcının bu istemciye verdiği onayı saklar; sonraki girişlerde onay sorulmaz.
        authorization ??= await authorizationManager.CreateAsync(
            identity: identity,
            subject: await userManager.GetUserIdAsync(user),
            client: (await applicationManager.GetIdAsync(application))!,
            type: AuthorizationTypes.Permanent,
            scopes: identity.GetScopes());

        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));
        identity.SetDestinations(OidcKimlik.Hedefler);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private OpenIddictRequest OidcIstegi() =>
        HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect isteği alınamadı.");

    private async Task<object> UygulamaAsync(OpenIddictRequest request) =>
        await applicationManager.FindByClientIdAsync(request.ClientId!)
            ?? throw new InvalidOperationException("İstemci uygulama bulunamadı.");

    private async Task<List<object>> YetkilendirmelerAsync(
        ApplicationUser user, object application, OpenIddictRequest request) =>
        await authorizationManager.FindAsync(
            subject: await userManager.GetUserIdAsync(user),
            client: await applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

    private static bool OturumSuresiAsildi(OpenIddictRequest request, AuthenticateResult result) =>
        request.MaxAge is not null && result.Properties?.IssuedUtc is not null &&
        DateTimeOffset.UtcNow - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value);

    private ForbidResult Reddet(string error, string description) =>
        Forbid(new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        }), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
