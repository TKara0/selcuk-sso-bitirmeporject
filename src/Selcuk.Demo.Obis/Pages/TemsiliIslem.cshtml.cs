using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Demo OBİS'teki bütün temsili işlemlerin ortak POST uç noktası ("Danışmanıma Mail At" ve benzeri düğmeler). Hiçbir
/// veriyi değiştirmez ve hiçbir yere göndermez; yalnızca "işlem kaydedilmedi" mesajını bırakıp geldiği sayfaya döner.
/// Razor Pages'in antiforgery doğrulaması açıktır (token yoksa 400). Dönüş adresi yalnızca uygulama içi olabilir.
/// </summary>
[Authorize]
public class TemsiliIslemModel : PageModel
{
    public const string TempDataAnahtari = "TemsiliIslemMesaji";

    public const string Mesaj = "Bu ekran temsilidir; işlem kaydedilmedi.";

    public IActionResult OnGet() => RedirectToPage("/Index");

    public IActionResult OnPost(string? donus)
    {
        TempData[TempDataAnahtari] = Mesaj;
        return LocalRedirect(Url.IsLocalUrl(donus) ? donus! : "/");
    }
}
