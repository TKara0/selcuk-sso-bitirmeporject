using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.EtikKurul.Pages.Kurul;

/// <summary>Kurul değerlendirmesindeki, üyeye atanan dosyalar için görüş formu; gönderim temsilidir (yetki: etik_kurul_uyesi).</summary>
public class GorusModel(IEtikKurulVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<EtikBasvurusu> Dosyalar { get; private set; } = [];

    public async Task OnGetAsync() =>
        Dosyalar = [.. (await veri.AtananDosyalarimAsync(HttpContext.RequestAborted))
            .Where(b => b.Durum == EtikDurumu.KurulDegerlendirmesinde)];

    public IActionResult OnPost() => this.TemsiliIslemSonucu();

    public UyeGorusu? BenimGorusum(EtikBasvurusu b) =>
        AtananDosyalarModel.KendiGorusu(b, User.FindFirst("preferred_username")?.Value);
}
