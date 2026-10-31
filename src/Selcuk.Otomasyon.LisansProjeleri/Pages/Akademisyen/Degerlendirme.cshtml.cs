using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.LisansProjeleri.Pages.Akademisyen;

/// <summary>
/// Akademisyenin hocası olduğu, konu seçimini geçmiş projeler; rubrik ve not girişi temsilidir (yetki: akademisyen).
/// Hocalık kayıt verisidir: yalnızca kendi projeleri görünür.
/// </summary>
public class DegerlendirmeModel(ILisansProjeleriVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<LisansProjesi> Projeler { get; private set; } = [];

    public async Task OnGetAsync() =>
        Projeler = [.. (await veri.DanismanligimdakiProjelerAsync(HttpContext.RequestAborted))
            .Where(p => p.Asama != ProjeAsamasi.KonuSecimi)];

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
