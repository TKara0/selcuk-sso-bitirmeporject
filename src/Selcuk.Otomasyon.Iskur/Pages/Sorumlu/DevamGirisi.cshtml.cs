using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Iskur.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Iskur.Pages.Sorumlu;

/// <summary>Devam girişi formu (temsili) ve girişi süren aylar (yetki: iskur_sorumlusu; kullanıcının bölümü).</summary>
public class DevamGirisiModel(IIskurVeriKaynagi veri, IOptions<IskurSartlari> sartlar) : PageModel
{
    public IReadOnlyList<AylikDevam> Kayitlar { get; private set; } = [];

    public IskurSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Kayitlar = await veri.DevamKayitlariAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
