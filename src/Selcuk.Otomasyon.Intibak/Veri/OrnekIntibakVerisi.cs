using System.Globalization;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using static Selcuk.Otomasyon.Intibak.Sozlesme.GelisTuru;
using static Selcuk.Otomasyon.Intibak.Sozlesme.IntibakDurumu;

namespace Selcuk.Otomasyon.Intibak.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2026 yıllarına yayılan 21 intibak başvurusu. Öğrenciler kimlik sunucusunun test öğrencileri
/// (203011001, 203011002, 203011003) ve yalnızca burada bulunan uydurma öğrencilerdir (ORN-…). Dördü
/// Elektrik-Elektronik Mühendisliği'nden (bölüm kapsamı), biri Mühendislik Fakültesi'nden (fakülte kapsamı). Önceki
/// kurum adları uydurmadır ("Örnek …", "Deneme …", "Kurgu …", "Hayali …").
/// </summary>
public static class OrnekIntibakVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Muhendislik = "Mühendislik Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string Makine = "Makine Mühendisliği";

    private const string OrnekUni = "Örnek Üniversitesi";
    private const string DenemeUni = "Deneme Teknik Üniversitesi";
    private const string KurguUni = "Kurgu Üniversitesi";
    private const string HayaliMyo = "Hayali Meslek Yüksekokulu";
    private const string AyniUni = "Aynı üniversite (af öncesi kayıt)";

    /// <summary>Bölümlerin müfredatı (eşleştirme seçenekleri). Kodlar ve adlar örnektir.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<MufredatDersi>> Mufredat =
        new Dictionary<string, IReadOnlyList<MufredatDersi>>
        {
            [Bilgisayar] =
            [
                new("BM101", "Programlamaya Giriş", 6, 1),
                new("MAT101", "Matematik I", 5, 1),
                new("FIZ101", "Fizik I", 5, 1),
                new("ING101", "İngilizce I", 3, 1),
                new("BM102", "Nesneye Yönelik Programlama", 6, 2),
                new("MAT102", "Matematik II", 5, 2),
                new("BM201", "Veri Yapıları", 6, 3),
                new("BM203", "Sayısal Tasarım", 5, 3),
                new("BM205", "Ayrık Matematik", 5, 3),
                new("BM301", "Veritabanı Sistemleri", 6, 5),
                new("BM303", "İşletim Sistemleri", 6, 5),
                new("BM305", "Bilgisayar Ağları", 5, 5),
            ],
            [ElektrikElektronik] =
            [
                new("EEM101", "Devre Teorisi I", 6, 1),
                new("MAT101", "Matematik I", 5, 1),
                new("FIZ101", "Fizik I", 5, 1),
                new("EEM201", "Elektronik I", 6, 3),
            ],
            [Makine] =
            [
                new("MAK101", "Statik", 5, 1),
                new("MAT101", "Matematik I", 5, 1),
                new("FIZ101", "Fizik I", 5, 1),
            ],
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
        ["ORN-0112"] = ("Örnek Öğrenci On İki", Teknoloji, Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", Teknoloji, ElektrikElektronik),
        ["ORN-0202"] = ("Örnek Öğrenci EEM İki", Teknoloji, ElektrikElektronik),
        ["ORN-0203"] = ("Örnek Öğrenci EEM Üç", Teknoloji, ElektrikElektronik),
        ["ORN-0204"] = ("Örnek Öğrenci EEM Dört", Teknoloji, ElektrikElektronik),
        ["ORN-0301"] = ("Örnek Öğrenci Makine Bir", Muhendislik, Makine),
    };

    // Önceki kurumda alınan dersler ve komisyonun önerdiği eşdeğer (kod; null: karşılığı yok) ile muafiyet kararı.
    private static readonly (string Ad, int Akts, string Not, string? Kod, bool Muaf)[] YatayDersler =
    [
        ("Programlamaya Giriş", 6, "BA", "BM101", true),
        ("Matematik I", 5, "CB", "MAT101", true),
        ("Fizik I", 5, "DD", "FIZ101", false),
        ("Teknik Resim", 3, "BB", null, false),
    ];

    private static readonly (string Ad, int Akts, string Not, string? Kod, bool Muaf)[] DikeyDersler =
    [
        ("Algoritma ve Programlama", 6, "AA", "BM101", true),
        ("Veritabanı Yönetimi", 6, "BB", "BM301", true),
        ("Temel Matematik", 3, "CC", "MAT101", false),
        ("İşletim Sistemleri", 6, "BA", "BM303", true),
    ];

    private static readonly (string Ad, int Akts, string Not, string? Kod, bool Muaf)[] DigerDersler =
    [
        ("Nesneye Yönelik Programlama", 6, "BB", "BM102", true),
        ("Veri Yapıları", 6, "FF", "BM201", false),
        ("İngilizce I", 3, "AA", "ING101", true),
    ];

    private static readonly (string Ad, int Akts, string Not, string? Kod, bool Muaf)[] EemDersler =
    [
        ("Devre Analizi", 6, "BA", "EEM101", true),
        ("Matematik I", 5, "BB", "MAT101", true),
        ("Fizik I", 5, "DC", "FIZ101", false),
    ];

    private static readonly (string Ad, int Akts, string Not, string? Kod, bool Muaf)[] MakineDersler =
    [
        ("Statik", 5, "BB", "MAK101", true),
        ("Matematik I", 5, "CC", "MAT101", true),
    ];

    public static readonly IReadOnlyList<IntibakBasvurusu> Kayitlar =
    [
        // 2024
        K("I24-01", "ORN-0101", YatayGecis, OrnekUni, "Bilgisayar Mühendisliği", "2024-08-12", Sonuclandi, "2024-09-20"),
        K("I24-02", "ORN-0102", DikeyGecis, HayaliMyo, "Bilgisayar Programcılığı", "2024-08-19", Sonuclandi, "2024-09-27"),
        K("I24-03", "ORN-0103", Af, AyniUni, "Bilgisayar Mühendisliği", "2024-09-02", Reddedildi, "2024-09-30",
            "Transkript ve ders içerikleri teslim edilmedi; başvuru reddedildi."),
        K("I24-04", "ORN-0201", YatayGecis, DenemeUni, "Elektrik-Elektronik Mühendisliği", "2024-08-14", Sonuclandi, "2024-09-25"),
        K("I24-05", "ORN-0105", EkYerlestirme, OrnekUni, "Yazılım Mühendisliği", "2024-09-09", Sonuclandi, "2024-10-11"),

        // 2025
        K("I25-01", "203011001", YatayGecis, KurguUni, "Bilgisayar Mühendisliği", "2025-08-11", Sonuclandi, "2025-09-19",
            "Fizik I notu muafiyet için yetersiz; ders tekrar alınacak."),
        K("I25-02", "ORN-0104", DikeyGecis, HayaliMyo, "Bilgisayar Programcılığı", "2025-08-18", Sonuclandi, "2025-09-26"),
        K("I25-03", "ORN-0106", YatayGecis, DenemeUni, "Bilgisayar Mühendisliği", "2025-08-20", Sonuclandi, "2025-09-26"),
        K("I25-04", "ORN-0202", EkYerlestirme, KurguUni, "Elektrik-Elektronik Mühendisliği", "2025-09-08", Sonuclandi, "2025-10-10"),

        // 2026
        K("I26-01", "203011002", YatayGecis, OrnekUni, "Bilgisayar Mühendisliği", "2026-08-10", BaskanOnayinda),
        K("I26-02", "203011003", DikeyGecis, HayaliMyo, "Bilgisayar Programcılığı", "2026-08-24", KomisyonIncelemesinde),
        K("I26-03", "ORN-0107", YatayGecis, KurguUni, "Bilgisayar Mühendisliği", "2026-09-01", KomisyonIncelemesinde),
        K("I26-04", "ORN-0108", Af, AyniUni, "Bilgisayar Mühendisliği", "2026-08-17", GeriGonderildi, null,
            "Bölüm başkanı: Veri Yapıları dersinin içeriği yeniden incelensin."),
        K("I26-05", "ORN-0109", EkYerlestirme, OrnekUni, "Yazılım Mühendisliği", "2026-08-12", YonetimKurulunda),
        K("I26-06", "ORN-0110", DikeyGecis, HayaliMyo, "Bilgisayar Programcılığı", "2026-08-13", YonetimKurulunda),
        K("I26-07", "ORN-0203", YatayGecis, DenemeUni, "Elektrik-Elektronik Mühendisliği", "2026-08-11", YonetimKurulunda),
        K("I26-08", "ORN-0204", DikeyGecis, HayaliMyo, "Elektronik Teknolojisi", "2026-08-25", KomisyonIncelemesinde),
        K("I26-09", "ORN-0301", YatayGecis, KurguUni, "Makine Mühendisliği", "2026-08-14", YonetimKurulunda),
        K("I26-10", "ORN-0111", YatayGecis, DenemeUni, "Bilgisayar Mühendisliği", "2026-08-27", BaskanOnayinda),
        K("I26-11", "ORN-0112", YatayGecis, OrnekUni, "Bilgisayar Mühendisliği", "2026-08-28", Reddedildi, "2026-09-18",
            "Genel not ortalaması yatay geçiş şartını karşılamıyor; başvuru reddedildi."),
        K("I26-12", "ORN-0201", EkYerlestirme, DenemeUni, "Elektrik-Elektronik Mühendisliği", "2026-09-03", BaskanOnayinda),
    ];

    private static DateOnly Tarih(string deger) => DateOnly.ParseExact(deger, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static IntibakBasvurusu K(string id, string no, GelisTuru tur, string universite, string oncekiBolum,
        string basvuru, IntibakDurumu durum, string? karar = null, string? not = null)
    {
        var (ad, fakulte, bolum) = Ogrenciler[no];
        var dersler = bolum switch
        {
            ElektrikElektronik => EemDersler,
            Makine => MakineDersler,
            _ => tur switch { YatayGecis => YatayDersler, DikeyGecis => DikeyDersler, _ => DigerDersler },
        };

        // Komisyon incelemesindeki başvuruda eşleştirme henüz yok; reddedilen başvuruda ders değerlendirilmedi.
        var kararli = durum is not (KomisyonIncelemesinde or Reddedildi);
        IReadOnlyList<DersEslesmesi> eslesmeler = [.. dersler.Select(d =>
        {
            if (!kararli)
            {
                return new DersEslesmesi(d.Ad, d.Akts, d.Not);
            }

            var esdeger = d.Kod is null ? null : Mufredat[bolum].Single(m => m.Kod == d.Kod);
            return new DersEslesmesi(d.Ad, d.Akts, d.Not, esdeger?.Kod, esdeger?.Ad, esdeger?.Akts, d.Muaf ? d.Not : null, d.Muaf);
        })];

        return new IntibakBasvurusu(id, no, ad, fakulte, bolum, tur, universite, oncekiBolum, Tarih(basvuru), durum,
            eslesmeler, karar is null ? null : Tarih(karar), not);
    }
}
