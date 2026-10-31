using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;

namespace Selcuk.Otomasyon.BolumKurul.Pages.Uye;

/// <summary>Bölümün planlanmış toplantılarının gündemi (yetki: bolum_kurulu_uyesi).</summary>
public class GundemModel(IBolumKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<Toplanti> Toplantilar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Toplantilar = [.. (await veri.ToplantilarAsync(HttpContext.RequestAborted))
            .Where(t => t.Durum == ToplantiDurumu.Planlandi).OrderBy(t => t.Tarih)];
}
