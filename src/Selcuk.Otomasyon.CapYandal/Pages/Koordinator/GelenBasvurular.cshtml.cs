using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.CapYandal.Sozlesme;

namespace Selcuk.Otomasyon.CapYandal.Pages.Koordinator;

/// <summary>
/// Bölümün programlarına gelen, süreci devam eden başvurular; program başına GNO'ya göre sıralı ve kontenjan
/// göstergeli (kontenjan ayardan; yetki: cap_yandal_koordinatoru).
/// </summary>
public class GelenBasvurularModel(ICapYandalVeriKaynagi veri, IOptions<CapYandalSartlari> sartlar) : PageModel
{
    public IReadOnlyList<(CapProgrami Program, IReadOnlyList<CapBasvurusu> Basvurular)> Programlar { get; private set; } = [];

    public CapYandalSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync()
    {
        var programlar = await veri.ProgramlarAsync(HttpContext.RequestAborted);
        var basvurular = (await veri.BasvurularAsync(null, HttpContext.RequestAborted))
            .Where(b => b.Durum is CapDurumu.KoordinatorDegerlendirmesinde or CapDurumu.BaskanOnayinda or CapDurumu.KesinKayitBekliyor);
        Programlar = [.. basvurular.GroupBy(b => b.ProgramKodu)
            .Select(g => (programlar.Single(p => p.Kod == g.Key), (IReadOnlyList<CapBasvurusu>)[.. SiraliBasvurular(g)]))
            .OrderBy(p => p.Item1.Tur).ThenBy(p => p.Item1.Kod)];
    }

    /// <summary>Koordinatör sıralaması: GNO yüksekten düşüğe, eşitlikte başarı sırası.</summary>
    internal static IEnumerable<CapBasvurusu> SiraliBasvurular(IEnumerable<CapBasvurusu> basvurular) =>
        basvurular.OrderByDescending(b => b.Gno).ThenBy(b => b.BasariSirasi);
}
