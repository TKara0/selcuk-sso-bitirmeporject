using Selcuk.Otomasyon.Mudek.Sozlesme;
using static Selcuk.Otomasyon.Mudek.Sozlesme.BelgeDurumu;
using static Selcuk.Otomasyon.Mudek.Sozlesme.BelgeTuru;

namespace Selcuk.Otomasyon.Mudek.Veri;

/// <summary>
/// Örnek (mock) veri: iki bölümün 11'er örnek program çıktısı, 16 ders çıktısı (2025-2026; ikisi Elektrik-Elektronik
/// Mühendisliği'nden) ve 7 rapor/belge (2024-2026). Hocalar hoca.test, mudek.test ve uydurma akademisyenlerdir
/// (orn.akademisyen…). Program ve ders çıktısı adları örnektir; gerçek akreditasyon metni değildir.
/// </summary>
public static class OrnekMudekVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string Donem = "2025-2026";

    private static readonly Dictionary<string, string> Hocalar = new()
    {
        ["hoca.test"] = "Test Akademisyen",
        ["mudek.test"] = "Test MÜDEK Üyesi",
        ["orn.akademisyen01"] = "Örnek Akademisyen Bir",
        ["orn.akademisyen02"] = "Örnek Akademisyen İki",
        ["orn.akademisyen03"] = "Örnek Akademisyen Üç",
        ["orn.akademisyen05"] = "Örnek Akademisyen EEM",
    };

    public static readonly IReadOnlyList<ProgramCiktisi> ProgramCiktilari =
    [
        .. new[] { Bilgisayar, ElektrikElektronik }.SelectMany(bolum => Enumerable.Range(1, 11)
            .Select(i => new ProgramCiktisi($"PÇ-{i}", $"Örnek program çıktısı {i}", Teknoloji, bolum))),
    ];

    public static readonly IReadOnlyList<DersCiktisi> DersCiktilari =
    [
        D("BM101", "Programlamaya Giriş", "hoca.test", 1, ["PÇ-1", "PÇ-2"], 0.78m),
        D("BM101", "Programlamaya Giriş", "hoca.test", 2, ["PÇ-3"], 0.64m),
        D("BM101", "Programlamaya Giriş", "hoca.test", 3, ["PÇ-5"], 0.55m),
        D("BM201", "Veri Yapıları", "hoca.test", 1, ["PÇ-1"], 0.72m),
        D("BM201", "Veri Yapıları", "hoca.test", 2, ["PÇ-2", "PÇ-4"], null),
        D("BM303", "İşletim Sistemleri", "mudek.test", 1, ["PÇ-4"], 0.81m),
        D("BM303", "İşletim Sistemleri", "mudek.test", 2, ["PÇ-6"], 0.69m),
        D("BM301", "Veritabanı Sistemleri", "orn.akademisyen01", 1, ["PÇ-3", "PÇ-7"], 0.74m),
        D("BM301", "Veritabanı Sistemleri", "orn.akademisyen01", 2, ["PÇ-8"], 0.58m),
        D("BM305", "Bilgisayar Ağları", "orn.akademisyen02", 1, ["PÇ-5"], 0.66m),
        D("BM305", "Bilgisayar Ağları", "orn.akademisyen02", 2, ["PÇ-9"], 0.71m),
        D("BM401", "Bitirme Projesi", "orn.akademisyen03", 1, ["PÇ-9", "PÇ-10"], 0.83m),
        D("BM401", "Bitirme Projesi", "orn.akademisyen03", 2, ["PÇ-6"], 0.77m),
        D("BM403", "Yazılım Mühendisliği", "orn.akademisyen01", 1, ["PÇ-8", "PÇ-10"], null),
        D("EEM101", "Devre Teorisi I", "orn.akademisyen05", 1, ["PÇ-1"], 0.70m, ElektrikElektronik),
        D("EEM101", "Devre Teorisi I", "orn.akademisyen05", 2, ["PÇ-2"], 0.62m, ElektrikElektronik),
    ];

    public static readonly IReadOnlyList<MudekBelgesi> Belgeler =
    [
        new("B24-01", Teknoloji, Bilgisayar, "Öz değerlendirme raporu", OzDegerlendirme, "2023-2024", Arsivlendi),
        new("B25-01", Teknoloji, Bilgisayar, "Dönem raporu", DonemRaporu, "2024-2025", Arsivlendi),
        new("B26-01", Teknoloji, Bilgisayar, "Güz dönemi raporu", DonemRaporu, "2025-2026 Güz", Tamamlandi),
        new("B26-02", Teknoloji, Bilgisayar, "Bahar dönemi raporu", DonemRaporu, "2025-2026 Bahar", Hazirlaniyor),
        new("B26-03", Teknoloji, Bilgisayar, "BM101 ders dosyası", DersDosyasi, "2025-2026", Tamamlandi),
        new("B26-04", Teknoloji, Bilgisayar, "Komisyon toplantı tutanağı", Tutanak, "2025-2026", Tamamlandi),
        new("B26-05", Teknoloji, ElektrikElektronik, "Dönem raporu", DonemRaporu, "2025-2026", Hazirlaniyor),
    ];

    private static DersCiktisi D(string kod, string ad, string hoca, int no, IReadOnlyList<string> pc, decimal? oran,
        string bolum = Bilgisayar) =>
        new($"{kod}-{no}", kod, ad, hoca, Hocalar[hoca], Teknoloji, bolum, Donem, $"DÇ-{no}",
            $"Örnek ders çıktısı {no}", pc, oran);
}
