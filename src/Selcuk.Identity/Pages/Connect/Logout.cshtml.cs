using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OpenIddict.Server.AspNetCore;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Pages.Connect;

/// <summary>OIDC end-session uç noktası ve çıkış sayfası.</summary>
public class LogoutModel(SignInManager<ApplicationUser> signInManager) : PageModel
{
    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await signInManager.SignOutAsync();

        // Geçerli bir post_logout_redirect_uri varsa OpenIddict istemciye yönlendirir, yoksa ana sayfaya.
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
