using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;

namespace Selcuk.Otomasyon.Intibak.Pages.Fakulte;

/// <summary>Fakültede yönetim kuruluna gidecek intibak kararları, bölümlere göre (yetki: fakulte_sekreteri).</summary>
public class YonetimKuruluEvrakiModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Evrak { get; private set; } = [];

    public async Task OnGetAsync() => Evrak = await veri.YonetimKuruluEvrakiAsync(HttpContext.RequestAborted);
}
