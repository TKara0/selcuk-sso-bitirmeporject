using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

namespace Selcuk.Otomasyon.SinavAnalizi.Pages.Baskan;

/// <summary>Bölümdeki derslerin karşılaştırması ve hedef altında kalanlar; Yazdır temsilidir (yetki: bolum_baskani).</summary>
public class BolumGeneliModel(ISinavAnaliziVeriKaynagi veri, IOptions<SinavSartlari> sartlar) : PageModel
{
    public IReadOnlyList<DersSonucu> Dersler { get; private set; } = [];

    public SinavSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() =>
        Dersler = [.. (await veri.BolumDersleriAsync(HttpContext.RequestAborted))
            .OrderBy(d => d.DersKodu, StringComparer.Ordinal).ThenByDescending(d => d.Donem, StringComparer.Ordinal)];

    public IActionResult OnPost() => this.TemsiliIslemSonucu();
}
