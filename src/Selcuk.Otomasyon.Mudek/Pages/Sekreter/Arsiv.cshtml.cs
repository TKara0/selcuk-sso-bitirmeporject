using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Mudek.Sozlesme;

namespace Selcuk.Otomasyon.Mudek.Pages.Sekreter;

/// <summary>Bölümün MÜDEK raporları ve belgeleri, durumlarıyla (yetki: bolum_sekreteri).</summary>
public class ArsivModel(IMudekVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<MudekBelgesi> Belgeler { get; private set; } = [];

    public async Task OnGetAsync() => Belgeler = await veri.BelgelerAsync(HttpContext.RequestAborted);
}
