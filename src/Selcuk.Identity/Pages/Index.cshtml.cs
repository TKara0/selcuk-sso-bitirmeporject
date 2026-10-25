using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Pages;

/// <summary>
/// "Uygulamalarım": kayıtlı istemcilerin kartları. Kart, istemcinin giriş başlatma adresine (initiate_login_uri)
/// gider; kimlik sunucusunda oturum açık olduğu için şifre sorulmadan içeri girilir. OIDC Core 1.0 bölüm 4'e göre
/// initiate_login_uri'ye iss parametresi eklenir; istemci bunu kendi kimlik sunucusuyla karşılaştırır. Kullanıcının
/// istemci bazlı erişim izni olmayan uygulamaların kartı gösterilmez (<see cref="IstemciErisimi"/>).
/// </summary>
[Authorize]
public class IndexModel(
    IOpenIddictApplicationManager applicationManager,
    UserManager<ApplicationUser> userManager,
    IOptionsMonitor<OpenIddictServerOptions> serverAyarlari) : PageModel
{
    public sealed record UygulamaKarti(string Ad, string Aciklama, string AnaSayfa, string GirisAdresi, bool MfaGerekli);

    public List<UygulamaKarti> Uygulamalar { get; } = [];

    public async Task OnGetAsync()
    {
        var uygulamalar = await applicationManager.ListAsync(count: null, offset: null, HttpContext.RequestAborted)
            .ToListAsync(HttpContext.RequestAborted);
        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
        var roller = await userManager.GetRolesAsync(user);

        foreach (var uygulama in uygulamalar)
        {
            var ozellikler = await applicationManager.GetPropertiesAsync(uygulama, HttpContext.RequestAborted);
            if (!ozellikler.TryGetValue(Istemciler.ClientUriOzelligi, out var anaSayfa)
                || !IstemciErisimi.Izinli(ozellikler, user.UserName, roller))
            {
                continue;
            }

            var girisAdresi = ozellikler.TryGetValue(Istemciler.InitiateLoginUriOzelligi, out var giris)
                ? QueryHelpers.AddQueryString(giris.GetString()!, "iss", serverAyarlari.CurrentValue.Issuer!.AbsoluteUri)
                : anaSayfa.GetString()!;
            var aciklama = ozellikler.TryGetValue(Istemciler.AciklamaOzelligi, out var a) ? a.GetString()! : string.Empty;
            var ad = await applicationManager.GetLocalizedDisplayNameAsync(uygulama, HttpContext.RequestAborted)
                ?? await applicationManager.GetClientIdAsync(uygulama, HttpContext.RequestAborted)
                ?? string.Empty;

            Uygulamalar.Add(new UygulamaKarti(ad, aciklama, anaSayfa.GetString()!, girisAdresi,
                IstemciErisimi.MfaZorunlu(ozellikler)));
        }

        Uygulamalar.Sort((x, y) => string.Compare(x.Ad, y.Ad, StringComparison.CurrentCulture));
    }
}
