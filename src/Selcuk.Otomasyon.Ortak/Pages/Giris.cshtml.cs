using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Otomasyon.Ortak.Pages;

/// <summary>Kullanıcıyı kimlik sunucusunun authorize uç noktasına yönlendirir (initiate_login_uri).</summary>
public class GirisModel : PageModel
{
    public IActionResult OnGet(string? returnUrl) =>
        Challenge(new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" },
            OpenIdConnectDefaults.AuthenticationScheme);
}
