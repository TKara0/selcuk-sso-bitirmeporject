using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.BolumKurul.Pages.Sekreter;

/// <summary>Bölümün kurul kararları ve tutanak durumu; Yazdır temsilidir (yetki: bolum_sekreteri).</summary>
public class ArsivModel(IBolumKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<Toplanti> Toplantilar { get; private set; } = [];

    public IReadOnlyList<(Toplanti Toplanti, GundemMaddesi Madde)> Kararlar { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Toplantilar = [.. (await veri.ToplantilarAsync(HttpContext.RequestAborted)).Where(t => t.Durum != ToplantiDurumu.Planlandi)];
        Kararlar = [.. Toplantilar.SelectMany(t => t.Maddeler.Where(m => m.KararNo is not null).Select(m => (t, m)))];
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
