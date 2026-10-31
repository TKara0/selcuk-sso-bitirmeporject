using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mudek.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mudek.Pages.Rapor;

/// <summary>Bölümün dönem raporu özeti; Yazdır temsilidir (yetki: mudek_komisyonu, bolum_baskani).</summary>
public class DonemModel(IMudekVeriKaynagi veri, IOptions<MudekSartlari> sartlar) : PageModel
{
    public IReadOnlyList<ProgramCiktisi> ProgramCiktilari { get; private set; } = [];

    public IReadOnlyList<DersCiktisi> DersCiktilari { get; private set; } = [];

    public MudekSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        ProgramCiktilari = await veri.ProgramCiktilariAsync(HttpContext.RequestAborted);
        DersCiktilari = await veri.DersCiktilariAsync(HttpContext.RequestAborted);
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
