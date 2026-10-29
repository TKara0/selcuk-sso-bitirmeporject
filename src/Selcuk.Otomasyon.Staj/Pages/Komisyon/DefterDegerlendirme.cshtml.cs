using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Komisyon;

/// <summary>Bölümün teslim edilmiş defterleri; kabul gün sayısı girişi temsilidir (yetki: staj_komisyonu).</summary>
public class DefterDegerlendirmeModel(IStajVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<StajKaydi> Defterler { get; private set; } = [];

    public async Task OnGetAsync() => Defterler = await veri.DefterlerAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
