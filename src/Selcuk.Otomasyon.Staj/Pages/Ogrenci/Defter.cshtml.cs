using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Ogrenci;

/// <summary>Öğrencinin güncel stajının defteri ve yeni giriş formu (gönderim temsilidir).</summary>
public class DefterModel(IStajVeriKaynagi veri) : PageModel
{
    public StajDefteri Defter { get; private set; } = new(null, []);

    public async Task OnGetAsync() => Defter = await veri.DefterimAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
