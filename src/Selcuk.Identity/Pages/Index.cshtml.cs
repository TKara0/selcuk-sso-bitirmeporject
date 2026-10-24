using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Pages;

/// <summary>
/// "Uygulamalarım": kayıtlı istemcilerin kartları. Kart, istemcinin giriş başlatma adresine (initiate_login_uri)
/// gider; kimlik sunucusunda oturum açık olduğu için şifre sorulmadan içeri girilir. OIDC Core 1.0 bölüm 4'e göre
/// initiate_login_uri'ye iss parametresi eklenir; istemci bunu kendi kimlik sunucusuyla karşılaştırır.
/// </summary>
[Authorize]
public class IndexModel(
    IOpenIddictApplicationManager applicationManager,
    IOptionsMonitor<OpenIddictServerOptions> serverAyarlari) : PageModel
{
    public sealed record UygulamaKarti(string Ad, string Aciklama, string AnaSayfa, string GirisAdresi);

    public List<UygulamaKarti> Uygulamalar { get; } = [];

    public async Task OnGetAsync()
    {
        var uygulamalar = await applicationManager.ListAsync(count: null, offset: null, HttpContext.RequestAborted)
            .ToListAsync(HttpContext.RequestAborted);

        foreach (var uygulama in uygulamalar)
        {
            var ozellikler = await applicationManager.GetPropertiesAsync(uygulama, HttpContext.RequestAborted);
            if (!ozellikler.TryGetValue(Istemciler.ClientUriOzelligi, out var anaSayfa))
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

            Uygulamalar.Add(new UygulamaKarti(ad, aciklama, anaSayfa.GetString()!, girisAdresi));
        }

        Uygulamalar.Sort((x, y) => string.Compare(x.Ad, y.Ad, StringComparison.CurrentCulture));
    }
}
