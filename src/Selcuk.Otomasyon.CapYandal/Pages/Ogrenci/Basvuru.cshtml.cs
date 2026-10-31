using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.CapYandal.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.CapYandal.Pages.Ogrenci;

/// <summary>
/// ÇAP/yandal başvuru formu ve öğrencinin GNO'su ile başarı sırasına göre şart göstergesi (şartlar ayardan,
/// CapYandalSartlari). Gönderim temsilidir.
/// </summary>
public class BasvuruModel(ICapYandalVeriKaynagi veri, IOptions<CapYandalSartlari> sartlar) : PageModel
{
    public AkademikBilgi? Bilgi { get; private set; }

    public IReadOnlyList<CapProgrami> Programlar { get; private set; } = [];

    public CapYandalSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        Bilgi = (await veri.AkademikBilgimAsync(HttpContext.RequestAborted)).FirstOrDefault();
        var bolum = User.FindFirst("bolum")?.Value;
        Programlar = [.. (await veri.ProgramlarAsync(HttpContext.RequestAborted)).Where(p => p.Bolum != bolum)];
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
