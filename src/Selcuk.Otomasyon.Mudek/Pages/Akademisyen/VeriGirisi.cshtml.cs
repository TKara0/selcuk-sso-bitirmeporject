using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mudek.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mudek.Pages.Akademisyen;

/// <summary>
/// Akademisyenin yalnızca hocası olduğu derslerin ders çıktıları için ölçülen oran formu; kaydet temsilidir (yetki:
/// akademisyen).
/// </summary>
public class VeriGirisiModel(IMudekVeriKaynagi veri, IOptions<MudekSartlari> sartlar) : PageModel
{
    public IReadOnlyList<DersCiktisi> DersCiktilari { get; private set; } = [];

    public MudekSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => DersCiktilari = await veri.DersCiktilarimAsync(HttpContext.RequestAborted);

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
