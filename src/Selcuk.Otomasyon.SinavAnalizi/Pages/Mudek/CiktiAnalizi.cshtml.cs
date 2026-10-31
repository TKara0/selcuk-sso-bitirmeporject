using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

namespace Selcuk.Otomasyon.SinavAnalizi.Pages.Mudek;

/// <summary>Program çıktısına bağlı derslerin ortalama başarısı (bölüm; yetki: mudek_komisyonu).</summary>
public class CiktiAnaliziModel(ISinavAnaliziVeriKaynagi veri, IOptions<SinavSartlari> sartlar) : PageModel
{
    public sealed record CiktiOzeti(string Kod, int DersSayisi, decimal Ortalama, decimal GecmeOrani);

    public IReadOnlyList<CiktiOzeti> Ozetler { get; private set; } = [];

    public SinavSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        var dersler = await veri.BolumDersleriAsync(HttpContext.RequestAborted);
        Ozetler = [.. dersler.SelectMany(d => d.ProgramCiktilari.Select(pc => (Pc: pc, Ders: d)))
            .GroupBy(x => x.Pc)
            .OrderBy(g => int.Parse(g.Key.AsSpan(3)))
            .Select(g => new CiktiOzeti(g.Key, g.Count(),
                g.Average(x => SinavAnaliziOtomasyonu.Ortalama(x.Ders)),
                g.Average(x => Sartlar.GecmeOrani(x.Ders))))];
    }
}
