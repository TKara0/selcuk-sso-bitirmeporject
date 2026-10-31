using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Ogrenci;

/// <summary>Öğrencinin mezuniyet durumu, ayardaki şartlara göre eksikleriyle (yetki: ogrenci).</summary>
public class DurumumModel(IMezuniyetVeriKaynagi veri, IOptions<MezuniyetSartlari> sartlar) : PageModel
{
    public MezuniyetAdayi? Aday { get; private set; }

    public MezuniyetSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Aday = (await veri.DurumumAsync(HttpContext.RequestAborted)).FirstOrDefault();
}
