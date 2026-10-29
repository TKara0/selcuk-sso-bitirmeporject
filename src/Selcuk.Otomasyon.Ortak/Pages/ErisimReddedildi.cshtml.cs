using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Otomasyon.Ortak.Pages;

/// <summary>
/// Rolüne açık olmayan bir ekranın adresine gidilince çerez şemasının yönlendirdiği sayfa (AccessDeniedPath). Yanıt
/// 403'tür.
/// </summary>
public class ErisimReddedildiModel : PageModel
{
    public void OnGet() => Response.StatusCode = StatusCodes.Status403Forbidden;
}
