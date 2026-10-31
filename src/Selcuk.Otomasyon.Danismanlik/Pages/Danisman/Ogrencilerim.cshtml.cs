using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;

namespace Selcuk.Otomasyon.Danismanlik.Pages.Danisman;

/// <summary>Danışmanın öğrencileri ve görüşme uyarısı (hedef ayardan; yetki: danisman).</summary>
public class OgrencilerimModel(IDanismanlikVeriKaynagi veri, IOptions<DanismanlikSartlari> sartlar, TimeProvider zaman) : PageModel
{
    public IReadOnlyList<DanismanlikOgrencisi> Ogrenciler { get; private set; } = [];

    public DanismanlikSartlari Sartlar => sartlar.Value;

    public DateOnly Bugun => DanismanlikOtomasyonu.Bugun(zaman);

    public async Task OnGetAsync() => Ogrenciler = await veri.OgrencilerimAsync(HttpContext.RequestAborted);
}
