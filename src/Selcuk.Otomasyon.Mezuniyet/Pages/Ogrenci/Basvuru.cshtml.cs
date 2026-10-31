using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Ogrenci;

/// <summary>Mezuniyet başvuru formu ve başvurunun aşamaları. Gönderim temsilidir (TemsiliIslem).</summary>
public class BasvuruModel(IMezuniyetVeriKaynagi veri) : PageModel
{
    public MezuniyetAdayi? Aday { get; private set; }

    public async Task OnGetAsync() => Aday = (await veri.DurumumAsync(HttpContext.RequestAborted)).FirstOrDefault();

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
