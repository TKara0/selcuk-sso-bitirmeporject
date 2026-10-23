using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>Kullanıcıyı kimlik sunucusunun authorize uç noktasına yönlendirir.</summary>
public class GirisModel : PageModel
{
    public IActionResult OnGet(string? returnUrl) =>
        Challenge(new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" },
            OpenIdConnectDefaults.AuthenticationScheme);
}
