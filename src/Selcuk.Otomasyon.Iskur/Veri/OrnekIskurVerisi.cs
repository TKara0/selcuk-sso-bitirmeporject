using Selcuk.Otomasyon.Iskur.Sozlesme;
using static Selcuk.Otomasyon.Iskur.Sozlesme.DevamDurumu;

namespace Selcuk.Otomasyon.Iskur.Veri;

/// <summary>
/// Örnek (mock) veri: 2025-10'dan 2026-10'a 17 aylık devam kaydı (üç durumun hepsi). Öğrenciler test öğrencileri
/// (203011001, 203011002) ve uydurma öğrencilerdir (ORN-…); ikisi Elektrik-Elektronik Mühendisliği'nden.
/// </summary>
public static class OrnekIskurVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";

    private static readonly Dictionary<string, (string Ad, string Bolum)> Ogrenciler = new()
    {
        ["203011001"] = ("Test Öğrenci", Bilgisayar),
        ["203011002"] = ("Test Öğrenci İki", Bilgisayar),
        ["ORN-0101"] = ("Örnek Öğrenci Bir", Bilgisayar),
        ["ORN-0102"] = ("Örnek Öğrenci İki", Bilgisayar),
        ["ORN-0103"] = ("Örnek Öğrenci Üç", Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", ElektrikElektronik),
    };

    public static readonly IReadOnlyList<AylikDevam> Kayitlar =
    [
        K("I25-01", "ORN-0101", "2025-10", 58, Odendi),
        K("I25-02", "ORN-0102", "2025-10", 42, Odendi),
        K("I25-03", "ORN-0101", "2025-11", 60, Odendi),
        K("I25-04", "ORN-0102", "2025-11", 55, Odendi),
        K("I25-05", "ORN-0201", "2025-11", 57, Odendi),
        K("I26-01", "203011001", "2026-02", 56, Odendi),
        K("I26-02", "203011001", "2026-03", 60, Odendi),
        K("I26-03", "203011002", "2026-03", 45, Odendi),
        K("I26-04", "ORN-0103", "2026-03", 62, Onaylandi),
        K("I26-05", "203011001", "2026-09", 54, Onaylandi),
        K("I26-06", "203011002", "2026-09", 50, Onaylandi),
        K("I26-07", "ORN-0101", "2026-09", 36, Onaylandi),
        K("I26-08", "ORN-0201", "2026-09", 48, Onaylandi),
        K("I26-09", "203011001", "2026-10", 40, Giriliyor),
        K("I26-10", "203011002", "2026-10", 52, Giriliyor),
        K("I26-11", "ORN-0102", "2026-10", 60, Giriliyor),
        K("I26-12", "ORN-0103", "2026-10", 30, Giriliyor),
    ];

    private static AylikDevam K(string id, string no, string ay, int devam, DevamDurumu durum)
    {
        var (ad, bolum) = Ogrenciler[no];
        return new AylikDevam(id, no, ad, Teknoloji, bolum, ay, 60, devam, durum);
    }
}
