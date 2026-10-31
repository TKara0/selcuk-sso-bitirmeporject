using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mudek.Sozlesme;

namespace Selcuk.Otomasyon.Mudek.Pages.Komisyon;

/// <summary>Bölümün program çıktıları ve gerçekleşme oranları (hedef ayardan; yetki: mudek_komisyonu).</summary>
public class ProgramCiktilariModel(IMudekVeriKaynagi veri, IOptions<MudekSartlari> sartlar) : PageModel
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
