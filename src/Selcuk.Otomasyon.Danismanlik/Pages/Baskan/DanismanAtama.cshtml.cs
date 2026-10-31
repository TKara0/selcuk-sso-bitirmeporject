using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Danismanlik.Pages.Baskan;

/// <summary>
/// Bölümün öğrenci-danışman eşleşmeleri ve danışman başına öğrenci göstergesi (en fazla öğrenci ayardan); Ata temsilidir
/// (yetki: bolum_baskani).
/// </summary>
public class DanismanAtamaModel(IDanismanlikVeriKaynagi veri, IOptions<DanismanlikSartlari> sartlar) : PageModel
{
    public IReadOnlyList<DanismanlikOgrencisi> Ogrenciler { get; private set; } = [];

    public DanismanlikSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Ogrenciler = await veri.BolumOgrencileriAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
