using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;

namespace Selcuk.Otomasyon.LisansProjeleri.Pages.Ogrenci;

/// <summary>Öğrencinin kendi lisans projeleri ve aşamaları (yetki: ogrenci).</summary>
public class AsamalarimModel(ILisansProjeleriVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<LisansProjesi> Projeler { get; private set; } = [];

    public async Task OnGetAsync() => Projeler = await veri.ProjelerimAsync(HttpContext.RequestAborted);
}
