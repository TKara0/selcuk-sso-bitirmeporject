using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Komisyon;

/// <summary>
/// Bölümün komisyon kontrolündeki ve eksik bildirilmiş adayları, şartlara göre kontrol sonuçlarıyla. Onayla ve Eksik
/// bildir temsilidir (yetki: mezuniyet_komisyonu).
/// </summary>
public class AdaylarModel(IMezuniyetVeriKaynagi veri, IOptions<MezuniyetSartlari> sartlar) : PageModel
{
    public IReadOnlyList<MezuniyetAdayi> Adaylar { get; private set; } = [];

    public MezuniyetSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() =>
        Adaylar = [.. (await veri.AdaylarAsync(null, HttpContext.RequestAborted))
            .Where(a => a.Durum is MezuniyetDurumu.KomisyonKontrolunde or MezuniyetDurumu.EksikBildirildi)];

    public IActionResult OnPostOnayla() => this.TemsiliIslemSonucu();

    public IActionResult OnPostEksikBildir() => this.TemsiliIslemSonucu();
}
