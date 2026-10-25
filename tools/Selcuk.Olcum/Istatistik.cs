namespace Selcuk.Olcum;

/// <summary>Bir senaryonun (ve eşzamanlılık düzeyinin) ısınma dışı turlarının özeti. Süreler ms.</summary>
public sealed record Ozet(
    string Senaryo, int Eszamanlilik, int Tur, int Basarili, int Hata,
    double P50, double P95, double P99, double Ortalama, double Min, double Max)
{
    public double HataOrani => Tur == 0 ? 0 : (double)Hata / Tur;
}

public static class Istatistik
{
    /// <summary>
    /// Yüzdelikler en yakın sıra yöntemiyle (nearest-rank): sıralı n değerde p. yüzdelik, ⌈p/100·n⌉. sıradaki değer.
    /// Yalnızca başarılı turların süreleri; hatalı turlar hata sayısında.
    /// </summary>
    public static double Yuzdelik(IReadOnlyList<double> sirali, double p)
    {
        if (sirali.Count == 0)
        {
            return double.NaN;
        }

        var sira = (int)Math.Ceiling(p / 100 * sirali.Count);
        return sirali[Math.Clamp(sira, 1, sirali.Count) - 1];
    }

    public static List<Ozet> Ozetle(IEnumerable<OlcumKaydi> kayitlar) =>
    [
        .. kayitlar.Where(k => !k.Isinma)
            .GroupBy(k => (k.Senaryo, k.Eszamanlilik))
            .OrderBy(g => g.Key.Senaryo, StringComparer.Ordinal).ThenBy(g => g.Key.Eszamanlilik)
            .Select(g =>
            {
                var sureler = g.Where(k => k.Basarili).Select(k => k.SureMs).Order().ToList();
                return new Ozet(g.Key.Senaryo, g.Key.Eszamanlilik, g.Count(), sureler.Count, g.Count(k => !k.Basarili),
                    Yuzdelik(sureler, 50), Yuzdelik(sureler, 95), Yuzdelik(sureler, 99),
                    sureler.Count == 0 ? double.NaN : sureler.Average(),
                    sureler.Count == 0 ? double.NaN : sureler[0],
                    sureler.Count == 0 ? double.NaN : sureler[^1]);
            }),
    ];
}
