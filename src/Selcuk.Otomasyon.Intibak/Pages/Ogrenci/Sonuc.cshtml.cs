using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;

namespace Selcuk.Otomasyon.Intibak.Pages.Ogrenci;

/// <summary>Öğrencinin sonuçlanmış başvurularının ders eşleştirmeleri ve muafiyet kararları.</summary>
public class SonucModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Sonuclar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Sonuclar = [.. (await veri.BasvurularimAsync(HttpContext.RequestAborted)).Where(b => b.Durum == IntibakDurumu.Sonuclandi)];
}
