using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;

namespace Selcuk.Otomasyon.EtikKurul.Pages.Akademisyen;

/// <summary>Akademisyenin kendi etik başvuruları, durum ve aşamalarıyla (yetki: akademisyen).</summary>
public class BasvurularimModel(IEtikKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<EtikBasvurusu> Basvurular { get; private set; } = [];

    public async Task OnGetAsync() => Basvurular = await veri.BasvurularimAsync(HttpContext.RequestAborted);
}
