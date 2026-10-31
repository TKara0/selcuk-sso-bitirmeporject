using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using static Selcuk.Otomasyon.Mezuniyet.Sozlesme.BirimDurumu;
using static Selcuk.Otomasyon.Mezuniyet.Sozlesme.MezuniyetDurumu;
using static Selcuk.Otomasyon.Mezuniyet.Sozlesme.StajSonucu;

namespace Selcuk.Otomasyon.Mezuniyet.Veri;

/// <summary>
/// Örnek (mock) veri: 2023-2024'ten 2026-2027 Güz'e 19 mezuniyet kaydı. Öğrenciler kimlik sunucusunun test öğrencileri
/// (203011001, 203011002, 203011003) ve yalnızca burada bulunan uydurma öğrencilerdir (ORN-…). Üçü
/// Elektrik-Elektronik Mühendisliği'nden (bölüm kapsamı), ikisi Mühendislik Fakültesi'nden (fakülte kapsamı). Staj
/// durumu da örnektir (ileride Staj API'sinden alınacak).
/// </summary>
public static class OrnekMezuniyetVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Muhendislik = "Mühendislik Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string Makine = "Makine Mühendisliği";

    /// <summary>Bölümlerin zorunlu dersleri (kodlar ve adlar örnektir).</summary>
    public static readonly IReadOnlyDictionary<string, (string Kod, string Ad)[]> ZorunluDersler =
        new Dictionary<string, (string Kod, string Ad)[]>
        {
            [Bilgisayar] =
            [
                ("BM101", "Programlamaya Giriş"), ("BM201", "Veri Yapıları"), ("BM301", "Veritabanı Sistemleri"),
                ("BM303", "İşletim Sistemleri"), ("BM403", "Yazılım Mühendisliği"), ("BM401", "Bitirme Projesi"),
            ],
            [ElektrikElektronik] = [("EEM101", "Devre Teorisi I"), ("EEM201", "Elektronik I"), ("EEM401", "Bitirme Projesi")],
            [Makine] = [("MAK101", "Statik"), ("MAK401", "Bitirme Projesi")],
        };

    private static readonly Dictionary<string, (string Ad, string Fakulte, string Bolum)> Ogrenciler = new()
    {
        ["203011001"] = ("Test Öğrenci", Teknoloji, Bilgisayar),
        ["203011002"] = ("Test Öğrenci İki", Teknoloji, Bilgisayar),
        ["203011003"] = ("Test Öğrenci Üç", Teknoloji, Bilgisayar),
        ["ORN-0101"] = ("Örnek Öğrenci Bir", Teknoloji, Bilgisayar),
        ["ORN-0102"] = ("Örnek Öğrenci İki", Teknoloji, Bilgisayar),
        ["ORN-0103"] = ("Örnek Öğrenci Üç", Teknoloji, Bilgisayar),
        ["ORN-0104"] = ("Örnek Öğrenci Dört", Teknoloji, Bilgisayar),
        ["ORN-0105"] = ("Örnek Öğrenci Beş", Teknoloji, Bilgisayar),
        ["ORN-0106"] = ("Örnek Öğrenci Altı", Teknoloji, Bilgisayar),
        ["ORN-0107"] = ("Örnek Öğrenci Yedi", Teknoloji, Bilgisayar),
        ["ORN-0108"] = ("Örnek Öğrenci Sekiz", Teknoloji, Bilgisayar),
        ["ORN-0109"] = ("Örnek Öğrenci Dokuz", Teknoloji, Bilgisayar),
        ["ORN-0110"] = ("Örnek Öğrenci On", Teknoloji, Bilgisayar),
        ["ORN-0111"] = ("Örnek Öğrenci On Bir", Teknoloji, Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", Teknoloji, ElektrikElektronik),
        ["ORN-0202"] = ("Örnek Öğrenci EEM İki", Teknoloji, ElektrikElektronik),
        ["ORN-0203"] = ("Örnek Öğrenci EEM Üç", Teknoloji, ElektrikElektronik),
        ["ORN-0301"] = ("Örnek Öğrenci Makine Bir", Muhendislik, Makine),
        ["ORN-0302"] = ("Örnek Öğrenci Makine İki", Muhendislik, Makine),
    };

    private const string Guz2026 = "2026-2027 Güz";

    public static readonly IReadOnlyList<MezuniyetAdayi> Kayitlar =
    [
        // Mezun olanlar (diploması hazır)
        K("M24-01", "ORN-0101", "2023-2024 Bahar", 242, 3.05m, Tamamlandi, DiplomaHazir, "2024-06-10"),
        K("M24-02", "ORN-0102", "2023-2024 Bahar", 240, 2.48m, Tamamlandi, DiplomaHazir, "2024-06-12"),
        K("M25-01", "ORN-0103", "2024-2025 Bahar", 244, 3.41m, Tamamlandi, DiplomaHazir, "2025-06-09"),
        K("M25-02", "ORN-0104", "2024-2025 Bahar", 240, 2.21m, Tamamlandi, DiplomaHazir, "2025-06-11"),
        K("M25-03", "ORN-0203", "2024-2025 Bahar", 241, 2.87m, Tamamlandi, DiplomaHazir, "2025-06-13"),
        K("M26-01", "ORN-0105", "2025-2026 Bahar", 240, 2.66m, Tamamlandi, DiplomaHazirlaniyor, "2026-06-08"),
        K("M26-02", "ORN-0302", "2025-2026 Bahar", 240, 2.54m, Tamamlandi, DiplomaHazirlaniyor, "2026-06-15"),

        // 2026-2027 Güz
        K("M27-01", "203011001", Guz2026, 242, 3.12m, Tamamlandi, BolumOnayinda, "2026-10-05"),
        K("M27-02", "203011002", Guz2026, 236, 2.65m, DevamEdiyor, KomisyonKontrolunde, "2026-10-08", eksikDers: "BM401"),
        K("M27-03", "203011003", Guz2026, 198, 1.94m, Eksik, BasvuruYapilmadi, null, eksikDers: "BM403"),
        K("M27-04", "ORN-0106", Guz2026, 243, 2.92m, Tamamlandi, Dekanlikta, "2026-09-21"),
        K("M27-05", "ORN-0107", Guz2026, 240, 2.35m, Tamamlandi, Dekanlikta, "2026-09-23"),
        K("M27-06", "ORN-0108", Guz2026, 241, 3.58m, Tamamlandi, BolumOnayinda, "2026-10-02"),
        K("M27-07", "ORN-0109", Guz2026, 240, 2.77m, Tamamlandi, KomisyonKontrolunde, "2026-10-12"),
        K("M27-08", "ORN-0110", Guz2026, 240, 1.98m, Tamamlandi, EksikBildirildi, "2026-10-06",
            not: "GNO mezuniyet şartının altında; not yükseltme sınavı sonrası yeniden başvurulmalı."),
        K("M27-09", "ORN-0111", Guz2026, 238, 2.41m, Eksik, KomisyonKontrolunde, "2026-10-14"),
        K("M27-10", "ORN-0201", Guz2026, 242, 2.83m, Tamamlandi, Dekanlikta, "2026-09-22"),
        K("M27-11", "ORN-0202", Guz2026, 230, 2.30m, DevamEdiyor, KomisyonKontrolunde, "2026-10-09", eksikDers: "EEM401"),
        K("M27-12", "ORN-0301", Guz2026, 240, 2.95m, Tamamlandi, Dekanlikta, "2026-09-24"),
    ];

    /// <summary>Öğrencinin ilişik kesme durumu: dekanlığa ulaşmış adayda bütün birimler tamam.</summary>
    public static IReadOnlyList<IlisikKesmeBirimi> IlisikKesme(MezuniyetAdayi aday) => aday.OgrenciNo switch
    {
        "203011002" =>
        [
            new("Kütüphane", Borclu, "İade edilmemiş 1 kitap."),
            new("Bölüm sekreterliği", Tamam),
            new("Laboratuvar", Bekliyor, "Dolap anahtarı teslim edilmedi."),
            new("Öğrenci işleri (harç)", Tamam),
            new("Sağlık, kültür ve spor", Tamam),
        ],
        _ when aday.Durum is Dekanlikta or DiplomaHazirlaniyor or DiplomaHazir =>
        [
            new("Kütüphane", Tamam), new("Bölüm sekreterliği", Tamam), new("Laboratuvar", Tamam),
            new("Öğrenci işleri (harç)", Tamam), new("Sağlık, kültür ve spor", Tamam),
        ],
        _ =>
        [
            new("Kütüphane", Bekliyor), new("Bölüm sekreterliği", Bekliyor), new("Laboratuvar", Bekliyor),
            new("Öğrenci işleri (harç)", Tamam), new("Sağlık, kültür ve spor", Bekliyor),
        ],
    };

    private static MezuniyetAdayi K(string id, string no, string donem, int akts, decimal gno, StajSonucu staj,
        MezuniyetDurumu durum, string? basvuru = null, string? eksikDers = null, string? not = null)
    {
        var (ad, fakulte, bolum) = Ogrenciler[no];
        IReadOnlyList<ZorunluDers> dersler = [.. ZorunluDersler[bolum].Select(d => new ZorunluDers(d.Kod, d.Ad, d.Kod != eksikDers))];
        return new MezuniyetAdayi(id, no, ad, fakulte, bolum, donem, akts, gno, dersler, staj, durum,
            basvuru is null ? null : DateOnly.ParseExact(basvuru, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), not);
    }
}
