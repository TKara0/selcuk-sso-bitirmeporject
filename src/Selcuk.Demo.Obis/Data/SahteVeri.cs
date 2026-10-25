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
}
