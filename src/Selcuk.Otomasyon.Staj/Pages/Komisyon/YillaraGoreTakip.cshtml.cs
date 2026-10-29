using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Komisyon;

/// <summary>Bölümün yıl ve staj türüne göre sayıları (yetki: staj_komisyonu).</summary>
public class YillaraGoreTakipModel(IStajVeriKaynagi veri, TimeProvider zaman) : PageModel
{
    public IReadOnlyList<YillikStajSayisi> Yillar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Yillar = (await veri.OzetAsync(zaman.GetLocalNow().Year, HttpContext.RequestAborted)).Yillar;
}
