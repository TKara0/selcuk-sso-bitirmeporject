using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Danismanlik.Pages.Ogrenci;

/// <summary>Görüşme talebi formu (temsili) ve öğrencinin kendi talepleri, aşamalarıyla (yetki: ogrenci).</summary>
public class GorusmeTalebiModel(IDanismanlikVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<GorusmeTalebi> Talepler { get; private set; } = [];

    public async Task OnGetAsync() => Talepler = await veri.TaleplerimAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
