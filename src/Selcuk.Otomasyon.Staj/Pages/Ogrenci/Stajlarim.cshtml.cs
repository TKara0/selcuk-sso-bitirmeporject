using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Ogrenci;

/// <summary>Öğrencinin kendi staj kayıtları (yetki: menüdeki rol, ogrenci).</summary>
public class StajlarimModel(IStajVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<StajKaydi> Kayitlar { get; private set; } = [];

    public async Task OnGetAsync() => Kayitlar = await veri.BasvurularimAsync(HttpContext.RequestAborted);
}
