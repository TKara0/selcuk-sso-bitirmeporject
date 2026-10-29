using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Otomasyon.Ortak.Pages;

/// <summary>
/// RP-initiated logout: yerel çerezi siler, refresh token'ı iptal eder (TokenYenileyici) ve kimlik sunucusunun
/// end-session uç noktasına yönlendirir.
/// </summary>
public class CikisModel : PageModel
{
    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? Page() : LocalRedirect("/");

    public IActionResult OnPost() =>
        SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
}
