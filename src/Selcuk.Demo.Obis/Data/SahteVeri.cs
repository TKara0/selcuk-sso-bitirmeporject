namespace Selcuk.Demo.Obis.Data;

public static class Roller
{
    public const string Ogrenci = "ogrenci";
    public const string Akademisyen = "akademisyen";
}

/// <summary>Demo API'den (GET /api/notlar) gelen not; notlar artık OBİS'te değil, API'de tutulur.</summary>
public sealed record DersNotu(string Kod, string Ad, int Vize, int Final, string Harf);

public sealed record Ders(string Kod, string Ad, string Donem, int OgrenciSayisi);

/// <summary>
/// Demo için uydurma, bellek içinde sabit veriler. Gerçek bir OBİS'e bağlanılmaz. Öğrenci notları Demo API'dedir
/// (src/Selcuk.Demo.Api, GET /api/notlar).
/// </summary>
public static class SahteVeri
{
    public static readonly IReadOnlyList<Ders> Dersler =
    [
        new("BIL203", "Veri Yapıları", "2026-2027 Güz", 64),
        new("BIL305", "Veritabanı Sistemleri", "2026-2027 Güz", 58),
        new("BIL412", "Bilgi Güvenliği", "2026-2027 Güz", 41),
        new("BIL498", "Bitirme Projesi", "2026-2027 Güz", 12),
    ];

    /// <summary>
    /// Ders kataloğundaki krediler (ders koduna göre). Demo API yalnızca notları verir; kredi OBİS'in kendi ders
    /// kataloğundandır. Katalogda olmayan dersin kredisi bilinmez ve özet hesaplarına girmez.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Krediler = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["BIL101"] = 3,
        ["BIL203"] = 4,
        ["BIL305"] = 4,
        ["BIL307"] = 3,
        ["BIL412"] = 3,
        ["BIL498"] = 4,
        ["MAT201"] = 3,
    };
}

/// <summary>
/// Notlarım özeti: toplam kredi ve dönem ağırlıklı ortalaması (4'lük sistem, harf notu katsayısı × kredi). Yalnızca
/// kredisi katalogda olan ve harf notu katsayısı bilinen dersler sayılır; hiçbiri yoksa ortalama null.
/// </summary>
public static class NotOzeti
{
    public static readonly IReadOnlyDictionary<string, decimal> Katsayilar = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["AA"] = 4.0m,
        ["BA"] = 3.5m,
        ["BB"] = 3.0m,
        ["CB"] = 2.5m,
        ["CC"] = 2.0m,
        ["DC"] = 1.5m,
        ["DD"] = 1.0m,
        ["FD"] = 0.5m,
        ["FF"] = 0.0m,
    };

    public static int? Kredi(DersNotu not) => SahteVeri.Krediler.TryGetValue(not.Kod, out var kredi) ? kredi : null;

    public static (int ToplamKredi, decimal? Ortalama) Hesapla(IEnumerable<DersNotu> notlar)
    {
        var sayilan = notlar
            .Select(n => (Kredi: Kredi(n), Katsayi: Katsayilar.TryGetValue(n.Harf, out var k) ? k : (decimal?)null))
            .Where(n => n.Kredi is not null && n.Katsayi is not null)
            .Select(n => (Kredi: n.Kredi!.Value, Katsayi: n.Katsayi!.Value))
            .ToList();
        var toplam = sayilan.Sum(n => n.Kredi);
        return (toplam, toplam == 0 ? null : sayilan.Sum(n => n.Kredi * n.Katsayi) / toplam);
    }
}
