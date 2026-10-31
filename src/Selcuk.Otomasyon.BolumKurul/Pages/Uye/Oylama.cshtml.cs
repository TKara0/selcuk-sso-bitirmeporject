using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.BolumKurul.Pages.Uye;

/// <summary>
/// Oylamaya açık maddeler, oy sayıları, yeter sayı ve kabul çoğunluğu göstergesi (şartlar ayardan); Kabul, Ret ve
/// Çekimser temsilidir (yetki: bolum_kurulu_uyesi).
/// </summary>
public class OylamaModel(IBolumKurulVeriKaynagi veri, IOptions<ToplantiSartlari> sartlar) : PageModel
{
    public IReadOnlyList<Toplanti> Toplantilar { get; private set; } = [];

    public ToplantiSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() =>
        Toplantilar = [.. (await veri.ToplantilarAsync(HttpContext.RequestAborted))
            .Where(t => t.Durum == ToplantiDurumu.Planlandi && t.Maddeler.Any(m => m.Durum == MaddeDurumu.OylamaAcik))
            .OrderBy(t => t.Tarih)];

    public IActionResult OnPostKabul() => this.TemsiliIslemSonucu();

    public IActionResult OnPostRet() => this.TemsiliIslemSonucu();

    public IActionResult OnPostCekimser() => this.TemsiliIslemSonucu();
}
