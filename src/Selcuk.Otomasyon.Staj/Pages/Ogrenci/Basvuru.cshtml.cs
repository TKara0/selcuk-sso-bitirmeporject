using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Staj.Pages.Ogrenci;

/// <summary>Staj başvuru formu. Gönderim temsilidir: antiforgery doğrulanır, veri değişmez (TemsiliIslem).</summary>
public class BasvuruModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
