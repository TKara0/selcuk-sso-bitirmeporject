using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.CapYandal.Sozlesme;

namespace Selcuk.Otomasyon.CapYandal.Pages.Sekreter;

/// <summary>Bölümün programlarında kesin kayıt evraklarının durumu (yetki: bolum_sekreteri).</summary>
public class EvrakModel(ICapYandalVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<CapBasvurusu> Basvurular { get; private set; } = [];

    public async Task OnGetAsync() =>
        Basvurular = [.. (await veri.BasvurularAsync(null, HttpContext.RequestAborted))
            .Where(b => b.Durum is CapDurumu.KesinKayitBekliyor or CapDurumu.KesinKayitYapildi)
            .OrderBy(b => b.Durum).ThenBy(b => b.OgrenciNo)];
}
