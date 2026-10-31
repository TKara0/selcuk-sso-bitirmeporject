using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Intibak.Pages.Sekreter;

/// <summary>Bölümün intibak kararları (yönetim kurulu, sonuçlanan, reddedilen); Yazdır temsilidir (yetki: bolum_sekreteri).</summary>
public class RaporlarModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Kararlar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Kararlar = [.. (await veri.BasvurularAsync(null, HttpContext.RequestAborted))
            .Where(b => b.Durum is IntibakDurumu.YonetimKurulunda or IntibakDurumu.Sonuclandi or IntibakDurumu.Reddedildi)
            .OrderByDescending(b => b.KararTarihi ?? b.BasvuruTarihi)];

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
