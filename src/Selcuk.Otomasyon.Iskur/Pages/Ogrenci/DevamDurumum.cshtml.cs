using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Iskur.Sozlesme;

namespace Selcuk.Otomasyon.Iskur.Pages.Ogrenci;

/// <summary>Öğrencinin kendi aylık devam kayıtları ve toplamı (yetki: ogrenci).</summary>
public class DevamDurumumModel(IIskurVeriKaynagi veri, IOptions<IskurSartlari> sartlar) : PageModel
{
    public IReadOnlyList<AylikDevam> Kayitlar { get; private set; } = [];

    public IskurSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Kayitlar = await veri.DevamKayitlarimAsync(HttpContext.RequestAborted);
}
