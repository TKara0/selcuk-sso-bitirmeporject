using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.LisansProjeleri.Pages.Baskan;

/// <summary>
/// Bölümün içinde bulunulan dönemdeki öğrenci-hoca eşleştirmesi ve hoca başına kontenjan göstergesi (en fazla proje
/// ayardan); Eşleştir temsilidir (yetki: bolum_baskani).
/// </summary>
public class EslestirmeModel(ILisansProjeleriVeriKaynagi veri, IOptions<ProjeSartlari> sartlar) : PageModel
{
    public IReadOnlyList<LisansProjesi> Projeler { get; private set; } = [];

    public IReadOnlyList<ProjeKonusu> Konular { get; private set; } = [];

    public ProjeSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        Projeler = await veri.ProjelerAsync(HttpContext.RequestAborted);
        Konular = await veri.KonularAsync(HttpContext.RequestAborted);
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
