using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Sekreter;

/// <summary>Bölümün staj evrakları (yetki: bolum_sekreteri).</summary>
public class EvrakTakibiModel(IStajVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<StajEvragi> Evraklar { get; private set; } = [];

    public async Task OnGetAsync() => Evraklar = await veri.EvraklarAsync(HttpContext.RequestAborted);
}
