using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.CapYandal.Pages.Koordinator;
using Selcuk.Otomasyon.CapYandal.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.CapYandal.Pages.Baskan;

/// <summary>Bölüm başkanı onayındaki başvurular, koordinatör sıralamasıyla; Onayla temsilidir (yetki: bolum_baskani).</summary>
public class BasvuruOnayiModel(ICapYandalVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<CapBasvurusu> Basvurular { get; private set; } = [];

    public async Task OnGetAsync() =>
        Basvurular = [.. (await veri.BasvurularAsync(CapDurumu.BaskanOnayinda, HttpContext.RequestAborted))
            .GroupBy(b => b.ProgramKodu).OrderBy(g => g.Key).SelectMany(GelenBasvurularModel.SiraliBasvurular)];

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
