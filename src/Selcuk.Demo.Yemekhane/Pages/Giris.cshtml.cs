using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Demo.Yemekhane.Pages;

/// <summary>
/// Kullanıcıyı kimlik sunucusunun authorize uç noktasına yönlendirir. Kimlik sunucusunun
/// "Uygulamalarım" sayfasındaki kart bu adrese (initiate_login_uri) gelir.
/// </summary>
public class GirisModel : PageModel
{
    public IActionResult OnGet(string? returnUrl) =>
        Challenge(new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/Rezervasyonlarim" },
            OpenIdConnectDefaults.AuthenticationScheme);
}
