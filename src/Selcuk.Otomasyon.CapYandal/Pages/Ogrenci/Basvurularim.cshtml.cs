using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.CapYandal.Sozlesme;

namespace Selcuk.Otomasyon.CapYandal.Pages.Ogrenci;

/// <summary>Öğrencinin kendi ÇAP ve yandal başvuruları, durum ve aşamalarıyla (yetki: ogrenci).</summary>
public class BasvurularimModel(ICapYandalVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<CapBasvurusu> Basvurular { get; private set; } = [];

    public async Task OnGetAsync() => Basvurular = await veri.BasvurularimAsync(HttpContext.RequestAborted);
}
