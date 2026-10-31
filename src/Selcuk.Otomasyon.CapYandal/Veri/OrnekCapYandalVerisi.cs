using System.Globalization;
using Selcuk.Otomasyon.CapYandal.Sozlesme;
using static Selcuk.Otomasyon.CapYandal.Sozlesme.CapDurumu;

namespace Selcuk.Otomasyon.CapYandal.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2025'ten 2026-2027 Güz'e 20 ÇAP ve yandal başvurusu. Programlar Bilgisayar ve
/// Elektrik-Elektronik Mühendisliği'nindir; test öğrencileri (203011001-003, anadal Bilgisayar) Elektrik-Elektronik'in
/// programlarına, uydurma öğrenciler (ORN-…) Bilgisayar'ın programlarına başvurmuştur. Böylece Bilgisayar'ın
/// koordinatörü test öğrencilerinin başvurularını görmez (başka bölümün kaydı). Ders kodları ve notlar örnektir.
/// </summary>
public static class OrnekCapYandalVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    private const string Makine = "Makine Mühendisliği";

    public static readonly IReadOnlyList<CapProgrami> Programlar =
    [
        new("CAP-BM", "Bilgisayar Mühendisliği ÇAP", ProgramTuru.Cap, Teknoloji, Bilgisayar),
        new("YAN-BM", "Bilgisayar Mühendisliği Yandal", ProgramTuru.Yandal, Teknoloji, Bilgisayar),
        new("CAP-EEM", "Elektrik-Elektronik Mühendisliği ÇAP", ProgramTuru.Cap, Teknoloji, ElektrikElektronik),
        new("YAN-EEM", "Elektrik-Elektronik Mühendisliği Yandal", ProgramTuru.Yandal, Teknoloji, ElektrikElektronik),
    ];

    private static readonly Dictionary<string, ProgramDersi[]> ProgramDersleri = new()
    {
        ["CAP-BM"] =
        [
            new("BM101", "Programlamaya Giriş", 6), new("BM102", "Nesneye Yönelik Programlama", 6), new("BM201", "Veri Yapıları", 6),
            new("BM203", "Sayısal Tasarım", 5), new("BM301", "Veritabanı Sistemleri", 6), new("BM303", "İşletim Sistemleri", 6),
        ],
        ["YAN-BM"] =
        [
            new("BM101", "Programlamaya Giriş", 6), new("BM102", "Nesneye Yönelik Programlama", 6), new("BM201", "Veri Yapıları", 6),
            new("BM301", "Veritabanı Sistemleri", 6),
        ],
        ["CAP-EEM"] =
        [
            new("EEM101", "Devre Teorisi I", 6), new("EEM102", "Devre Teorisi II", 6), new("EEM103", "Elektrik Ölçmeleri", 4),
            new("EEM201", "Elektronik I", 6), new("EEM203", "Sinyaller ve Sistemler", 5), new("EEM301", "Elektromanyetik Alanlar", 5),
            new("EEM305", "Kontrol Sistemleri", 5),
        ],
        ["YAN-EEM"] = [new("EEM101", "Devre Teorisi I", 6), new("EEM102", "Devre Teorisi II", 6), new("EEM201", "Elektronik I", 6)],
    };

    private static readonly Dictionary<string, (string Ad, string Bolum)> Ogrenciler = new()
    {
        ["203011001"] = ("Test Öğrenci", Bilgisayar),
        ["203011002"] = ("Test Öğrenci İki", Bilgisayar),
        ["203011003"] = ("Test Öğrenci Üç", Bilgisayar),
        ["ORN-0101"] = ("Örnek Öğrenci Bir", Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", ElektrikElektronik),
        ["ORN-0202"] = ("Örnek Öğrenci EEM İki", ElektrikElektronik),
        ["ORN-0203"] = ("Örnek Öğrenci EEM Üç", ElektrikElektronik),
        ["ORN-0204"] = ("Örnek Öğrenci EEM Dört", ElektrikElektronik),
        ["ORN-0205"] = ("Örnek Öğrenci EEM Beş", ElektrikElektronik),
        ["ORN-0206"] = ("Örnek Öğrenci EEM Altı", ElektrikElektronik),
        ["ORN-0207"] = ("Örnek Öğrenci EEM Yedi", ElektrikElektronik),
        ["ORN-0208"] = ("Örnek Öğrenci EEM Sekiz", ElektrikElektronik),
        ["ORN-0209"] = ("Örnek Öğrenci EEM Dokuz", ElektrikElektronik),
        ["ORN-0301"] = ("Örnek Öğrenci Makine Bir", Makine),
        ["ORN-0302"] = ("Örnek Öğrenci Makine İki", Makine),
        ["ORN-0303"] = ("Örnek Öğrenci Makine Üç", Makine),
        ["ORN-0304"] = ("Örnek Öğrenci Makine Dört", Makine),
        ["ORN-0305"] = ("Örnek Öğrenci Makine Beş", Makine),
        ["ORN-0306"] = ("Örnek Öğrenci Makine Altı", Makine),
    };

    /// <summary>Test öğrencilerinin anadaldaki GNO'su ve başarı sırası (sınıf mevcudu 80).</summary>
    public static readonly IReadOnlyList<AkademikBilgi> AkademikBilgiler =
    [
        new("203011001", 3.25m, 12, 80),
        new("203011002", 2.80m, 31, 80),
        new("203011003", 2.35m, 58, 80),
    ];

    private const string Guz2024 = "2024-2025 Güz";
    private const string Guz2025 = "2025-2026 Güz";
    private const string Guz2026 = "2026-2027 Güz";

    public static readonly IReadOnlyList<CapBasvurusu> Basvurular =
    [
        // Bilgisayar'ın programları
        K("C24-01", "ORN-0201", "CAP-BM", Guz2024, 3.40m, 5, "2024-08-26", KesinKayitYapildi),
        K("C24-02", "ORN-0202", "YAN-BM", Guz2024, 2.90m, 22, "2024-08-27", KesinKayitYapildi),
        K("C24-03", "ORN-0203", "CAP-BM", Guz2024, 3.05m, 24, "2024-08-28", Reddedildi,
            not: "Başarı sırası şartı karşılanmadı."),
        K("C25-01", "ORN-0204", "CAP-BM", Guz2025, 3.55m, 3, "2025-08-25", KesinKayitYapildi),
        K("C25-02", "ORN-0301", "YAN-BM", Guz2025, 2.62m, 30, "2025-08-26", KesinKayitYapildi),
        K("C25-03", "ORN-0302", "CAP-BM", Guz2025, 3.10m, 14, "2025-08-27", KesinKayitYapildi),
        K("C26-01", "ORN-0205", "CAP-BM", Guz2026, 3.62m, 2, "2026-08-24", KoordinatorDegerlendirmesinde),
        K("C26-02", "ORN-0206", "CAP-BM", Guz2026, 3.18m, 11, "2026-08-25", KoordinatorDegerlendirmesinde),
        K("C26-03", "ORN-0303", "CAP-BM", Guz2026, 3.44m, 6, "2026-08-24", BaskanOnayinda),
        K("C26-04", "ORN-0304", "CAP-BM", Guz2026, 3.01m, 15, "2026-08-26", KoordinatorDegerlendirmesinde),
        K("C26-05", "ORN-0207", "YAN-BM", Guz2026, 2.75m, 26, "2026-08-25", BaskanOnayinda),
        K("C26-06", "ORN-0208", "YAN-BM", Guz2026, 2.55m, 33, "2026-08-27", KoordinatorDegerlendirmesinde),
        K("C26-07", "ORN-0305", "CAP-BM", Guz2026, 3.70m, 1, "2026-08-24", KesinKayitBekliyor,
            new KesinKayitEvraki(Dilekce: true, Transkript: true, AnadalOnayi: false)),
        K("C26-08", "ORN-0306", "YAN-BM", Guz2026, 3.02m, 13, "2026-08-25", KesinKayitBekliyor,
            new KesinKayitEvraki(Dilekce: true, Transkript: false, AnadalOnayi: false)),
        K("C26-09", "ORN-0209", "CAP-BM", Guz2026, 3.20m, 9, "2026-08-26", BaskanOnayinda),

        // Elektrik-Elektronik'in programları (test öğrencileri)
        K("C25-04", "203011001", "CAP-EEM", Guz2025, 3.25m, 12, "2025-08-25", KesinKayitYapildi),
        K("C26-10", "203011002", "YAN-EEM", Guz2026, 2.80m, 31, "2026-08-24", BaskanOnayinda),
        K("C26-11", "203011003", "CAP-EEM", Guz2026, 2.35m, 58, "2026-08-24", Reddedildi,
            not: "GNO şartı karşılanmadı."),
        K("C26-12", "203011003", "YAN-EEM", Guz2026, 2.35m, 58, "2026-08-25", KoordinatorDegerlendirmesinde),
        K("C26-13", "ORN-0101", "CAP-EEM", Guz2026, 3.15m, 10, "2026-08-26", KoordinatorDegerlendirmesinde),
    ];

    private static readonly string[] Notlar = ["BB", "CB", "BA", "CC", "AA", "DC"];

    // Kesin kaydı yapılmış başvurular: alınan ders sayısı ve dönemlik GNO'lar (dönemler başvuru döneminden başlar).
    private static readonly Dictionary<string, decimal[]> DonemGnolari = new()
    {
        ["C24-01"] = [3.30m, 3.12m, 3.05m, 2.98m],
        ["C24-02"] = [2.80m, 2.55m, 2.40m, 2.35m],
        ["C25-01"] = [3.40m, 3.25m],
        ["C25-02"] = [2.30m, 1.95m],
        ["C25-03"] = [2.70m, 2.41m],
        ["C25-04"] = [3.00m, 2.83m],
    };

    private static readonly string[] Donemler =
        [Guz2024, "2024-2025 Bahar", Guz2025, "2025-2026 Bahar", Guz2026];

    /// <summary>Kesin kaydı yapılmış başvuruların ilerlemesi.</summary>
    public static readonly IReadOnlyList<ProgramIlerlemesi> Ilerlemeler =
    [
        .. Basvurular.Where(b => b.Durum == KesinKayitYapildi).Select(b =>
        {
            var gnolar = DonemGnolari[b.Id];
            var donemler = Donemler.Skip(Array.IndexOf(Donemler, b.Donem)).Take(gnolar.Length).ToArray();
            var dersler = ProgramDersleri[b.ProgramKodu];
            var alinan = Math.Min(dersler.Length - 1, gnolar.Length + 1);
            return new ProgramIlerlemesi(b,
                [.. dersler.Take(alinan).Select((d, i) => d with { Not = Notlar[i % Notlar.Length], Donem = donemler[Math.Min(i / 2, donemler.Length - 1)] })],
                [.. dersler.Skip(alinan)],
                [.. donemler.Select((d, i) => new DonemGnosu(d, gnolar[i]))]);
        }),
    ];

    private static CapBasvurusu K(string id, string no, string programKodu, string donem, decimal gno, int sira,
        string basvuru, CapDurumu durum, KesinKayitEvraki? evrak = null, string? not = null)
    {
        var (ad, bolum) = Ogrenciler[no];
        var program = Programlar.Single(p => p.Kod == programKodu);
        evrak ??= durum == KesinKayitYapildi ? new KesinKayitEvraki(true, true, true) : null;
        return new CapBasvurusu(id, no, ad, bolum, program.Kod, program.Ad, program.Tur, program.Fakulte, program.Bolum, donem,
            gno, sira, 80, DateOnly.ParseExact(basvuru, "yyyy-MM-dd", CultureInfo.InvariantCulture), durum, evrak, not);
    }
}
