using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;

namespace Selcuk.Otomasyon.BolumKurul.Pages.Fakulte;

/// <summary>Fakültedeki bölüm kurullarının kararları, bölümlere göre (yetki: fakulte_sekreteri).</summary>
public class GelenKararlarModel(IBolumKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<Toplanti> Toplantilar { get; private set; } = [];

    public async Task OnGetAsync() => Toplantilar = await veri.FakulteKararlariAsync(HttpContext.RequestAborted);
}
