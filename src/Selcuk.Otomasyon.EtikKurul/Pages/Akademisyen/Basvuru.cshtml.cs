using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.EtikKurul.Pages.Akademisyen;

/// <summary>Etik başvuru formu. Gönderim temsilidir: antiforgery doğrulanır, belge okunmaz, veri değişmez.</summary>
public class BasvuruModel : PageModel
{
    public void OnGet()
    {
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
