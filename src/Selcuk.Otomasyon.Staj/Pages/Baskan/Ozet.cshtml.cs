using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Baskan;

/// <summary>Bölüm özeti: sayı kartları ve yıllık tablo (yetki: bolum_baskani).</summary>
public class OzetModel(IStajVeriKaynagi veri, TimeProvider zaman) : PageModel
{
    public StajOzeti Ozet { get; private set; } = new(0, 0, 0, 0, []);

    public async Task OnGetAsync() => Ozet = await veri.OzetAsync(zaman.GetLocalNow().Year, HttpContext.RequestAborted);
}
