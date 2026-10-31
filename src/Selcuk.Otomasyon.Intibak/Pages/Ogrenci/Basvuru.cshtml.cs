using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Intibak.Pages.Ogrenci;

/// <summary>İntibak başvuru formu. Gönderim temsilidir: antiforgery doğrulanır, transkript okunmaz, veri değişmez.</summary>
public class BasvuruModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
