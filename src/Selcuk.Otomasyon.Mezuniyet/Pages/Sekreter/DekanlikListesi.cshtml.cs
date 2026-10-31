using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Sekreter;

/// <summary>Bölümün dekanlığa gidecek ve gönderilmiş mezuniyet listesi; gönderim temsilidir (yetki: bolum_sekreteri).</summary>
public class DekanlikListesiModel(IMezuniyetVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<MezuniyetAdayi> Gonderilecekler { get; private set; } = [];

    public IReadOnlyList<MezuniyetAdayi> Gonderilenler { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var adaylar = await veri.AdaylarAsync(null, HttpContext.RequestAborted);
        Gonderilecekler = [.. adaylar.Where(a => a.Durum == MezuniyetDurumu.BolumOnayinda)];
        Gonderilenler = [.. adaylar.Where(a => a.Durum == MezuniyetDurumu.Dekanlikta)];
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
