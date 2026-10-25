using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Server.AspNetCore;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Pages.Connect;

/// <summary>OIDC end-session uç noktası ve çıkış sayfası.</summary>
public class LogoutModel(
    SignInManager<ApplicationUser> signInManager,
    BackChannelLogoutGonderici backChannelLogout,
    TokenIptali tokenIptali,
    DenetimKaydedici denetim) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var sid = User.FindFirst(OidcKimlik.SidClaim)?.Value;
        var kullaniciId = User.FindFirst(Claims.Subject)?.Value;
        if (kullaniciId is not null)
        {
            denetim.Ekle(DenetimOlayi.Cikis, DenetimSonucu.Basarili, kullaniciId);
        }

        await signInManager.SignOutAsync();

        // Bu oturumdan alınmış tüm token'lar (refresh token'lar dahil) iptal edilir.
        if (sid is not null && kullaniciId is not null)
        {
            await tokenIptali.OturumuIptalEtAsync(kullaniciId, sid, CancellationToken.None);
        }

        // Tek çıkış: bu oturumda giriş yapılmış tüm istemcilere Back-Channel Logout bildirimi.
        // Tarayıcı bağlantıyı kapatsa da bildirimler tamamlansın (süre sınırı HttpClient'ta).
        if (sid is not null)
        {
            await backChannelLogout.OturumuKapatAsync(sid, CancellationToken.None);
        }

        // Geçerli bir post_logout_redirect_uri varsa OpenIddict istemciye yönlendirir, yoksa ana sayfaya.
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
