using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.CapYandal.Sozlesme;

namespace Selcuk.Otomasyon.CapYandal.Pages.Koordinator;

/// <summary>Bölümün programlarındaki öğrenciler; GNO'su devam şartının altına düşenler uyarılı (yetki: cap_yandal_koordinatoru).</summary>
public class OgrenciTakibiModel(ICapYandalVeriKaynagi veri, IOptions<CapYandalSartlari> sartlar) : PageModel
{
    public IReadOnlyList<ProgramIlerlemesi> Ogrenciler { get; private set; } = [];

    public CapYandalSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Ogrenciler = await veri.ProgramdakiOgrencilerAsync(HttpContext.RequestAborted);
}
