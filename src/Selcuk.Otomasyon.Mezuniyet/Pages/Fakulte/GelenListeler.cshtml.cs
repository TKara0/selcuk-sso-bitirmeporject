using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Fakulte;

/// <summary>Fakültenin bölümlerinden gelen mezuniyet listeleri ve diploma hazırlık durumu (yetki: fakulte_sekreteri).</summary>
public class GelenListelerModel(IMezuniyetVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<MezuniyetAdayi> Adaylar { get; private set; } = [];

    public async Task OnGetAsync() => Adaylar = await veri.FakulteListeleriAsync(HttpContext.RequestAborted);
}
