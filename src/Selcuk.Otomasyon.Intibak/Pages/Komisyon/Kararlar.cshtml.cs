using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;

namespace Selcuk.Otomasyon.Intibak.Pages.Komisyon;

/// <summary>Bölümde komisyonun karar verdiği başvurular (incelemede bekleyenler hariç; yetki: intibak_komisyonu).</summary>
public class KararlarModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Kararlar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Kararlar = [.. (await veri.BasvurularAsync(null, HttpContext.RequestAborted))
            .Where(b => b.Durum != IntibakDurumu.KomisyonIncelemesinde)
            .OrderByDescending(b => b.KararTarihi ?? b.BasvuruTarihi)];
}
