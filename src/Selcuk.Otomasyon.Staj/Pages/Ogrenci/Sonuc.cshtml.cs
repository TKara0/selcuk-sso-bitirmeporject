using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Staj.Sozlesme;

namespace Selcuk.Otomasyon.Staj.Pages.Ogrenci;

/// <summary>Öğrencinin değerlendirilmiş veya reddedilmiş stajları, komisyon notu ve kabul edilen gün sayısıyla.</summary>
public class SonucModel(IStajVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<StajKaydi> Sonuclar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Sonuclar = [.. (await veri.BasvurularimAsync(HttpContext.RequestAborted))
            .Where(k => k.Durum is StajDurumu.Degerlendirildi or StajDurumu.Reddedildi)
            .OrderByDescending(k => k.Baslangic)];
}
