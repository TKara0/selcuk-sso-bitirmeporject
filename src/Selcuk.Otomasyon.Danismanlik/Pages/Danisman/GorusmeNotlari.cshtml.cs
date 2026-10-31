using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Danismanlik.Pages.Danisman;

/// <summary>Danışmanın kendi görüşme notları ve yeni not formu (temsili; yetki: danisman).</summary>
public class GorusmeNotlariModel(IDanismanlikVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<GorusmeNotu> Notlar { get; private set; } = [];

    public IReadOnlyList<DanismanlikOgrencisi> Ogrenciler { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Notlar = await veri.GorusmeNotlarimAsync(HttpContext.RequestAborted);
        Ogrenciler = await veri.OgrencilerimAsync(HttpContext.RequestAborted);
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
