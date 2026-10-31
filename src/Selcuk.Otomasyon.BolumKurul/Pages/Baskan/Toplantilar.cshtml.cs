using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.BolumKurul.Pages.Baskan;

/// <summary>Bölümün toplantıları; yeni toplantı ve gündem maddesi formları temsilidir (yetki: bolum_baskani).</summary>
public class ToplantilarModel(IBolumKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<Toplanti> Toplantilar { get; private set; } = [];

    public async Task OnGetAsync() => Toplantilar = await veri.ToplantilarAsync(HttpContext.RequestAborted);

    public IActionResult OnPostYeniToplanti() => this.TemsiliIslemSonucu();

    public IActionResult OnPostMaddeEkle() => this.TemsiliIslemSonucu();
}
