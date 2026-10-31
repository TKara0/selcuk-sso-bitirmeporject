using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

namespace Selcuk.Otomasyon.SinavAnalizi.Pages.Akademisyen;

/// <summary>Akademisyenin yalnızca hocası olduğu derslerin toplu sonuçları (şartlar ayardan; yetki: akademisyen).</summary>
public class DerslerimModel(ISinavAnaliziVeriKaynagi veri, IOptions<SinavSartlari> sartlar) : PageModel
{
    public IReadOnlyList<DersSonucu> Dersler { get; private set; } = [];

    public SinavSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() =>
        Dersler = [.. (await veri.DerslerimAsync(HttpContext.RequestAborted))
            .OrderBy(d => d.DersKodu, StringComparer.Ordinal).ThenByDescending(d => d.Donem, StringComparer.Ordinal)];
}
