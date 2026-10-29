using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Komisyon;

/// <summary>Bölümün bekleyen başvuruları; Onayla ve Reddet temsilidir (yetki: staj_komisyonu).</summary>
public class BasvuruIncelemeModel(IStajVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<StajKaydi> Bekleyenler { get; private set; } = [];

    public async Task OnGetAsync() =>
        Bekleyenler = await veri.BasvurularAsync(StajDurumu.BasvuruBekliyor, HttpContext.RequestAborted);

    public IActionResult OnPostOnayla() => this.TemsiliIslemSonucu();

    public IActionResult OnPostReddet() => this.TemsiliIslemSonucu();
}
