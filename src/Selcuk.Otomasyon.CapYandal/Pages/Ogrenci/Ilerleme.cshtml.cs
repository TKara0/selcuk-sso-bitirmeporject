using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.CapYandal.Sozlesme;

namespace Selcuk.Otomasyon.CapYandal.Pages.Ogrenci;

/// <summary>Öğrencinin kesin kaydı yapılmış programlarındaki ilerlemesi (yetki: ogrenci).</summary>
public class IlerlemeModel(ICapYandalVeriKaynagi veri, IOptions<CapYandalSartlari> sartlar) : PageModel
{
    public IReadOnlyList<ProgramIlerlemesi> Ilerlemeler { get; private set; } = [];

    public CapYandalSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Ilerlemeler = await veri.IlerlememAsync(HttpContext.RequestAborted);
}
