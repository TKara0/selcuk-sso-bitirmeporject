using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.LisansProjeleri.Pages.Akademisyen;

/// <summary>Konu önerisi formu (temsili) ve akademisyenin önerdiği konular (yetki: akademisyen).</summary>
public class KonuOnerisiModel(ILisansProjeleriVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<ProjeKonusu> Konular { get; private set; } = [];

    public async Task OnGetAsync() => Konular = await veri.KonularimAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
