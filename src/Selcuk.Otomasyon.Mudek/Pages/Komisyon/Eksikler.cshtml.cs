using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mudek.Sozlesme;

namespace Selcuk.Otomasyon.Mudek.Pages.Komisyon;

/// <summary>Bölümün eşik altındaki veya verisi girilmemiş ders ve program çıktıları (eşikler ayardan; yetki: mudek_komisyonu).</summary>
public class EksiklerModel(IMudekVeriKaynagi veri, IOptions<MudekSartlari> sartlar) : PageModel
{
    public IReadOnlyList<ProgramCiktisi> ProgramCiktilari { get; private set; } = [];

    public IReadOnlyList<DersCiktisi> DersCiktilari { get; private set; } = [];

    public MudekSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        ProgramCiktilari = await veri.ProgramCiktilariAsync(HttpContext.RequestAborted);
        DersCiktilari = await veri.DersCiktilariAsync(HttpContext.RequestAborted);
    }
}
