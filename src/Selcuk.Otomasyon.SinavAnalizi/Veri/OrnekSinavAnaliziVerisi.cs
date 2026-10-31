using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

namespace Selcuk.Otomasyon.SinavAnalizi.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2025 ve 2025-2026 dönemlerinde 16 ders sonucu (harf notu dağılımları; öğrenci düzeyinde veri
/// yok). Hocalar hoca.test, mudek.test ve uydurma akademisyenlerdir (orn.akademisyen…); ikisi Elektrik-Elektronik
/// Mühendisliği'nden. Sayılar uydurmadır.
/// </summary>
public static class OrnekSinavAnaliziVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";

    /// <summary>Harf notları (yüksekten düşüğe) ve 4'lük katsayıları.</summary>
    public static readonly IReadOnlyList<(string Harf, decimal Katsayi)> Harfler =
    [
        ("AA", 4.0m), ("BA", 3.5m), ("BB", 3.0m), ("CB", 2.5m), ("CC", 2.0m), ("DC", 1.5m), ("DD", 1.0m), ("FD", 0.5m), ("FF", 0m),
    ];

    private static readonly Dictionary<string, string> Hocalar = new()
    {
        ["hoca.test"] = "Test Akademisyen",
        ["mudek.test"] = "Test MÜDEK Üyesi",
        ["orn.akademisyen01"] = "Örnek Akademisyen Bir",
        ["orn.akademisyen02"] = "Örnek Akademisyen İki",
        ["orn.akademisyen03"] = "Örnek Akademisyen Üç",
        ["orn.akademisyen05"] = "Örnek Akademisyen EEM",
    };

    private static readonly Dictionary<string, (string Ad, string[] Pc)> Dersler = new()
    {
        ["BM101"] = ("Programlamaya Giriş", ["PÇ-1", "PÇ-2"]),
        ["BM201"] = ("Veri Yapıları", ["PÇ-1", "PÇ-4"]),
        ["BM205"] = ("Ayrık Matematik", ["PÇ-1", "PÇ-2"]),
        ["BM301"] = ("Veritabanı Sistemleri", ["PÇ-3", "PÇ-7"]),
        ["BM303"] = ("İşletim Sistemleri", ["PÇ-4", "PÇ-6"]),
        ["BM305"] = ("Bilgisayar Ağları", ["PÇ-5", "PÇ-9"]),
        ["BM401"] = ("Bitirme Projesi", ["PÇ-9", "PÇ-10"]),
        ["BM403"] = ("Yazılım Mühendisliği", ["PÇ-8", "PÇ-10"]),
        ["EEM101"] = ("Devre Teorisi I", ["PÇ-1"]),
    };

    public static readonly IReadOnlyList<DersSonucu> Kayitlar =
    [
        S("S01", "BM101", "hoca.test", "2024-2025 Güz", [6, 8, 10, 9, 8, 5, 4, 3, 7]),
        S("S02", "BM101", "hoca.test", "2025-2026 Güz", [8, 9, 11, 10, 7, 4, 3, 2, 4]),
        S("S03", "BM201", "hoca.test", "2024-2025 Bahar", [4, 5, 8, 9, 10, 6, 4, 4, 8]),
        S("S04", "BM201", "hoca.test", "2025-2026 Bahar", [3, 4, 6, 7, 8, 7, 5, 6, 10]),
        S("S05", "BM303", "mudek.test", "2024-2025 Güz", [5, 7, 9, 8, 7, 5, 3, 2, 4]),
        S("S06", "BM303", "mudek.test", "2025-2026 Güz", [4, 6, 8, 8, 9, 6, 4, 3, 6]),
        S("S07", "BM301", "orn.akademisyen01", "2024-2025 Bahar", [7, 9, 10, 8, 6, 4, 3, 1, 2]),
        S("S08", "BM301", "orn.akademisyen01", "2025-2026 Bahar", [6, 8, 9, 9, 7, 5, 3, 2, 3]),
        S("S09", "BM305", "orn.akademisyen02", "2024-2025 Güz", [2, 3, 5, 7, 9, 8, 6, 6, 12]),
        S("S10", "BM305", "orn.akademisyen02", "2025-2026 Güz", [3, 4, 6, 8, 9, 7, 5, 4, 8]),
        S("S11", "BM205", "orn.akademisyen03", "2024-2025 Güz", [3, 4, 5, 6, 8, 8, 7, 7, 12]),
        S("S12", "BM205", "orn.akademisyen03", "2025-2026 Güz", [4, 5, 6, 7, 8, 7, 6, 5, 10]),
        S("S13", "BM401", "orn.akademisyen03", "2025-2026 Bahar", [10, 9, 8, 6, 4, 2, 1, 0, 1]),
        S("S14", "BM403", "orn.akademisyen01", "2025-2026 Bahar", [6, 7, 9, 8, 6, 4, 3, 1, 2]),
        S("S15", "EEM101", "orn.akademisyen05", "2024-2025 Güz", [4, 6, 8, 8, 9, 6, 5, 4, 8], ElektrikElektronik),
        S("S16", "EEM101", "orn.akademisyen05", "2025-2026 Güz", [5, 6, 9, 9, 8, 5, 4, 3, 6], ElektrikElektronik),
    ];

    private static DersSonucu S(string id, string kod, string hoca, string donem, int[] dagilim, string bolum = Bilgisayar)
    {
        var (ad, pc) = Dersler[kod];
        return new DersSonucu(id, kod, ad, hoca, Hocalar[hoca], Teknoloji, bolum, donem,
            [.. Harfler.Select((h, i) => new HarfSayisi(h.Harf, dagilim[i]))], pc);
    }
}
