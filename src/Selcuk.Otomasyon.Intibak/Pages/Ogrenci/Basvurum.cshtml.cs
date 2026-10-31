using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;

namespace Selcuk.Otomasyon.Intibak.Pages.Ogrenci;

/// <summary>Öğrencinin kendi intibak başvuruları, durum rozeti ve aşamalarıyla (yetki: ogrenci).</summary>
public class BasvurumModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Basvurular { get; private set; } = [];

    public async Task OnGetAsync() => Basvurular = await veri.BasvurularimAsync(HttpContext.RequestAborted);
}
