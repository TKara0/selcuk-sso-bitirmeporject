using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.LisansProjeleri.Veri;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.LisansProjeleri.Pages.Ogrenci;

/// <summary>Açık konular ve öğrencinin tercih formu (tercih sayısı ayardan); gönderim temsilidir (yetki: ogrenci).</summary>
public class KonuSecimiModel(ILisansProjeleriVeriKaynagi veri, IOptions<ProjeSartlari> sartlar) : PageModel
{
    public IReadOnlyList<ProjeKonusu> Konular { get; private set; } = [];

    /// <summary>Öğrencinin içinde bulunulan dönemdeki proje kaydı.</summary>
    public LisansProjesi? GuncelProje { get; private set; }

    public ProjeSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        Konular = await veri.KonularAsync(HttpContext.RequestAborted);
        GuncelProje = (await veri.ProjelerimAsync(HttpContext.RequestAborted))
            .FirstOrDefault(p => p.Donem == OrnekLisansProjeleriVerisi.GuncelDonem);
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
