using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Iskur.Sozlesme;

namespace Selcuk.Otomasyon.Iskur.Pages.Sorumlu;

/// <summary>Öğrenci başına aylık devam özeti ve eşik uyarıları (eşikler ayardan; yetki: iskur_sorumlusu).</summary>
public class TakipModel(IIskurVeriKaynagi veri, IOptions<IskurSartlari> sartlar) : PageModel
{
    public IReadOnlyList<AylikDevam> Kayitlar { get; private set; } = [];

    public IskurSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Kayitlar = await veri.DevamKayitlariAsync(HttpContext.RequestAborted);
}
