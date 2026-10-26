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
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Pages.Connect;

/// <summary>
/// OIDC authorize uç noktası. İsteği OpenIddict doğrular (istemci, redirect_uri, PKCE);
/// bu sayfa kullanıcı girişini ve ilk girişteki onay ekranını yönetir. Hatalı denemelerden kaynaklanan geçici hesap
/// kilidi açık oturumu etkilemez: kilit yeni girişleri engeller, bu oturumdan kod alınabilir (token uç noktasıyla aynı kural).
/// İstemci bazlı erişim izni olmayan kullanıcı istemciye error=access_denied ile döner (<see cref="IstemciErisimi"/>).
/// mfaZorunlu istemcide oturum çok faktörlü değilse (amr "mfa" yok; "Bu cihazı hatırla" ile açılan oturum dahil) 2FA'sı
/// açık kullanıcı ek doğrulamaya (step-up), kapalı kullanıcı 2FA kurulumuna gider; ikisinden sonra bu isteğe dönülür.
/// </summary>
public class AuthorizeModel(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    UserManager<ApplicationUser> userManager,
    IkiAdimIslemleri ikiAdim,
    IstemciErisimi istemciErisimi,
    TokenIptali tokenIptali,
    DenetimKaydedici denetim) : PageModel
{
    public const string IzinYokAciklamasi = "Bu uygulamaya erişim izniniz yok.";

    public string UygulamaAdi { get; private set; } = string.Empty;

    public IReadOnlyList<string> Kapsamlar { get; private set; } = [];

    public static (string Baslik, string Aciklama) KapsamBilgisi(string kapsam) => kapsam switch
    {
        Scopes.OpenId => ("Kimlik", "Hesabına ait benzersiz kimlik numarası"),
        Scopes.Profile => ("Profil", "Ad soyadın, kullanıcı adın veya öğrenci numaran, fakülte ve bölüm bilgin"),
        Scopes.Email => ("E-posta", "E-posta adresin"),
        Scopes.Roles => ("Rol", "Rolün (öğrenci, akademisyen veya personel)"),
        Scopes.OfflineAccess => ("Erişimi yenileme",
            "Uygulama, oturumun açıkken erişimini sen tekrar giriş yapmadan yenileyebilir."),
        Istemciler.NotlarOkuKapsami => ("Notlar", "Not bilgilerini okuma"),
        _ => (kapsam, "Uygulamaya özel izin"),
    };

    public async Task<IActionResult> OnGetAsync()
    {
        // Onay ekranı ve yönlendirmeler önbelleğe alınmaz (geri tuşuyla eski onay formu gösterilmesin).
        Response.Headers.CacheControl = "no-store";
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

        // İstemci bazlı erişim izni: izinsiz kullanıcı onay ekranı veya 2FA kurulumu görmeden istemciye döner.
        var application = await UygulamaAsync(request);
        if (!await istemciErisimi.IzinliMiAsync(application, user, "authorize"))
        {
            return Reddet(Errors.AccessDenied, IzinYokAciklamasi);
        }

        // Rolü iki aşamalı doğrulama gerektiren kullanıcı kurulumu bitirmeden kod alamaz; kurulumdan sonra bu isteğe döner.
        if (await ikiAdim.KurulumGerekliAsync(user))
        {
            return request.HasPromptValue(PromptValues.None)
                ? Reddet(Errors.InteractionRequired, "İki aşamalı doğrulama kurulumu gerekli.")
                : RedirectToPage("/Account/IkiAdimDogrulama",
                    new { returnUrl = Request.PathBase + Request.Path + Request.QueryString });
        }

        // Uygulama iki aşamalı doğrulama istiyor ve oturum yalnızca şifreyle açılmış: step-up veya kurulum.
        if (await istemciErisimi.MfaZorunluMuAsync(application) && !CokFaktorlu(result.Principal))
        {
            var ikiAdimAcik = await userManager.GetTwoFactorEnabledAsync(user);
            denetim.Ekle(DenetimOlayi.IstemciMfaGerekli, DenetimSonucu.Basarisiz, user.Id, request.ClientId,
                $"uc=authorize; yonlendirme={(ikiAdimAcik ? "ek-dogrulama" : "kurulum")}");
            if (request.HasPromptValue(PromptValues.None))
            {
                return Reddet(Errors.InteractionRequired, "Bu uygulama iki aşamalı doğrulama gerektiriyor.");
            }

            var donus = Request.PathBase + Request.Path + Request.QueryString;
            return ikiAdimAcik
                ? RedirectToPage("/Account/EkDogrulama", new { returnUrl = donus })
                : RedirectToPage("/Account/IkiAdimDogrulama", new { returnUrl = donus });
        }

        var authorizations = await YetkilendirmelerAsync(user, application, request);

        switch (await applicationManager.GetConsentTypeAsync(application))
        {
            case ConsentTypes.External when authorizations.Count is 0:
                return Reddet(Errors.ConsentRequired, "Bu uygulamaya erişim yetkiniz yok.");

            case ConsentTypes.Implicit:
            case ConsentTypes.External:
            case ConsentTypes.Explicit when authorizations.Count is not 0 && !request.HasPromptValue(PromptValues.Consent):
                return await OturumAcAsync(user, application, request, authorizations.LastOrDefault(), result.Principal);

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
        Response.Headers.CacheControl = "no-store";
        var request = OidcIstegi();
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Reddet(Errors.LoginRequired, "Kullanıcı oturum açmamış.");
        }

        // Onay formu doğrudan gönderilse de izinsiz kullanıcı kod alamaz.
        var application = await UygulamaAsync(request);
        if (!await istemciErisimi.IzinliMiAsync(application, user, "authorize"))
        {
            return Reddet(Errors.AccessDenied, IzinYokAciklamasi);
        }

        // Kurulumu bitmemiş zorunlu 2FA kullanıcısı onay formunu doğrudan gönderse de kod alamaz.
        if (await ikiAdim.KurulumGerekliAsync(user))
        {
            return Reddet(Errors.AccessDenied, "İki aşamalı doğrulama kurulumu gerekli.");
        }

        // Onay formu doğrudan gönderilse de çok faktörlü olmayan oturum mfaZorunlu istemciden kod alamaz.
        if (await istemciErisimi.MfaZorunluMuAsync(application) && !CokFaktorlu(User))
        {
            denetim.Ekle(DenetimOlayi.IstemciMfaGerekli, DenetimSonucu.Basarisiz, user.Id, request.ClientId,
                "uc=authorize; neden=onay-formu");
            return Reddet(Errors.AccessDenied, "Bu uygulama iki aşamalı doğrulama gerektiriyor.");
        }

        if (karar != "kabul")
        {
            denetim.Ekle(DenetimOlayi.OnayReddedildi, DenetimSonucu.Basarisiz, user.Id, request.ClientId);
            return Reddet(Errors.AccessDenied, "Kullanıcı erişim iznini reddetti.");
        }

        var authorizations = await YetkilendirmelerAsync(user, application, request);

        if (authorizations.Count is 0 && await applicationManager.HasConsentTypeAsync(application, ConsentTypes.External))
        {
            return Reddet(Errors.ConsentRequired, "Bu uygulamaya erişim yetkiniz yok.");
        }

        denetim.Ekle(DenetimOlayi.OnayVerildi, DenetimSonucu.Basarili, user.Id, request.ClientId,
            $"scope={string.Join(' ', request.GetScopes())}");
        return await OturumAcAsync(user, application, request, authorizations.LastOrDefault(), User);
    }

    private async Task<IActionResult> OturumAcAsync(
        ApplicationUser user, object application, OpenIddictRequest request, object? authorization,
        ClaimsPrincipal oturum)
    {
        var identity = OidcKimlik.YeniKimlik();
        await OidcKimlik.KullaniciClaimleriniEkleAsync(identity, user, userManager);
        // Kimlik sunucusu oturumu: aynı oturumdan giriş yapılan tüm istemcilere aynı sid verilir.
        identity.SetClaim(OidcKimlik.SidClaim, oturum.FindFirst(OidcKimlik.SidClaim)?.Value);
        // Kullanıcının bu oturumda nasıl doğrulandığı (RFC 8176): ["pwd"] veya ["pwd", "mfa"].
        OidcKimlik.AmrAyarla(identity, OidcKimlik.AmrDegerleri(oturum));

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        // Kalıcı yetkilendirme, kullanıcının bu istemciye verdiği onayı saklar; sonraki girişlerde onay sorulmaz.
        authorization ??= await authorizationManager.CreateAsync(
            identity: identity,
            subject: await userManager.GetUserIdAsync(user),
            client: (await applicationManager.GetIdAsync(application))!,
            type: AuthorizationTypes.Permanent,
            scopes: identity.GetScopes());

        // Kod ve ondan türeyen token'lar (refresh token zinciri dahil) bu oturuma (sid) ait ayrı bir ad-hoc
        // yetkilendirmeye bağlanır: çıkışta veya şifre/2FA değişikliğinde oturum bazında iptal edilebilir, kalıcı onay
        // kaydı etkilenmez. Tekrar kullanılan bir refresh token'da OpenIddict de yalnızca bu zinciri iptal eder.
        var oturumYetkisi = await tokenIptali.OturumYetkisiOlusturAsync(
            await userManager.GetUserIdAsync(user), (await applicationManager.GetIdAsync(application))!,
            identity.GetClaim(OidcKimlik.SidClaim), identity.GetScopes());
        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(oturumYetkisi));
        identity.SetDestinations(OidcKimlik.Hedefler);

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static bool CokFaktorlu(ClaimsPrincipal oturum) =>
        OidcKimlik.AmrDegerleri(oturum).Contains(OidcKimlik.AmrCokFaktorlu);

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
