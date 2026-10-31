using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;

namespace Selcuk.Otomasyon.EtikKurul.Pages.Baskan;

/// <summary>Bölümün etik kurul kararları, karar süresi ve süre hedefiyle (hedef ayardan; yetki: bolum_baskani).</summary>
public class KararListesiModel(IEtikKurulVeriKaynagi veri, IOptions<EtikKurulSartlari> sartlar) : PageModel
{
    public IReadOnlyList<EtikBasvurusu> Kararlar { get; private set; } = [];

    public EtikKurulSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Kararlar = await veri.KararlarAsync(HttpContext.RequestAborted);
}
