using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Intibak.Pages.Baskan;

/// <summary>Bölüm başkanı onayındaki komisyon kararları; Onayla ve Geri gönder temsilidir (yetki: bolum_baskani).</summary>
public class OnayListesiModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Bekleyenler { get; private set; } = [];

    public async Task OnGetAsync() =>
        Bekleyenler = await veri.BasvurularAsync(IntibakDurumu.BaskanOnayinda, HttpContext.RequestAborted);

    public IActionResult OnPostOnayla() => this.TemsiliIslemSonucu();

    public IActionResult OnPostGeriGonder() => this.TemsiliIslemSonucu();
}
