using System.Globalization;
using System.Security.Claims;

namespace Selcuk.Demo.Obis.Data;

/// <summary>Müfredattaki bir ders (örnek; genel bilgisayar mühendisliği dersleri).</summary>
public sealed record DersTanimi(string Kod, string Ad, int Akts);

/// <summary>Transkriptteki bir ders: tamamlanmış ve harf notu verilmiş.</summary>
public sealed record DersKaydi(string Kod, string Ad, int Akts, string Harf)
{
    /// <summary>FF ve FD başarısızdır; diğerleri geçer.</summary>
    public bool Gecti => Harf is not ("FF" or "FD");
}

/// <summary>Bir dönemin dersleri ve ortalamaları. <see cref="Ad"/> "2025-2026 Güz" biçimindedir.</summary>
public sealed record DonemKaydi(string Ad, IReadOnlyList<DersKaydi> Dersler)
{
    /// <summary>Akademik yıl ("2025-2026").</summary>
    public string Yil => Ad[..9];

    public int ToplamAkts => Dersler.Sum(d => d.Akts);

    public decimal? Ortalama => TranskriptOzeti.Ortalama(Dersler);
}

public enum AdimDurumu
{
    Tamamlandi,
    Bekliyor,
    Baslamadi,
}

public sealed record OnayAdimi(string Ad, AdimDurumu Durum, string Aciklama);

public sealed record HarcKalemi(string Donem, string Kalem, decimal Tutar, DateOnly SonOdeme, bool Odendi);

public sealed record SinavKaydi(string DersKodu, string DersAdi, string Tur, DateOnly Tarih, TimeOnly Saat, string Salon);

public sealed record KurulKarari(string No, DateOnly Tarih, string Konu, string Sonuc);

/// <summary>
/// Bir öğrencinin örnek kaydı. Hepsi uydurmadır: gerçek kişi, öğrenci numarası veya e-posta yoktur. Ad, fakülte, bölüm
/// ve e-posta burada değil, kimlik sunucusunun id_token'ındadır (Anasayfa onları claim'lerden okur).
/// </summary>
/// <param name="OnayTamamlanan">Ders kayıt onayında tamamlanan adım sayısı (0–4).</param>
public sealed record OgrenciKaydi(
    string KullaniciAdi,
    string Danisman,
    int Sinif,
    IReadOnlyList<DonemKaydi> Donemler,
    IReadOnlyList<DersTanimi> SecilenDersler,
    int OnayTamamlanan,
    IReadOnlyList<HarcKalemi> Harclar,
    IReadOnlyList<SinavKaydi> Sinavlar,
    IReadOnlyList<KurulKarari> Kararlar)
{
    public const string OnayDonemi = "2026-2027 Güz";

    public static readonly string[] OnayAdimlari = ["Ders seçimi", "Danışman onayı", "Bölüm kontrolü", "Öğrenci İşleri kaydı"];

    public IReadOnlyList<DersKaydi> TumDersler => [.. Donemler.SelectMany(d => d.Dersler)];

    public decimal? GenelOrtalama => TranskriptOzeti.Ortalama(TumDersler);

    public int ToplamAkts => TumDersler.Where(d => d.Gecti).Sum(d => d.Akts);

    /// <summary>Son akademik yılın ("2025-2026") dönemleri.</summary>
    public IReadOnlyList<DonemKaydi> SonYil => [.. Donemler.Where(d => d.Yil == "2025-2026")];

    public IReadOnlyList<OnayAdimi> Adimlar()
    {
        var adimlar = new List<OnayAdimi>();
        for (var i = 0; i < OnayAdimlari.Length; i++)
        {
            var durum = i < OnayTamamlanan ? AdimDurumu.Tamamlandi
                : i == OnayTamamlanan ? AdimDurumu.Bekliyor
                : AdimDurumu.Baslamadi;
            adimlar.Add(new OnayAdimi(OnayAdimlari[i], durum, durum switch
            {
                AdimDurumu.Tamamlandi => "Tamamlandı",
                AdimDurumu.Bekliyor => "Bekliyor",
                _ => "Henüz başlamadı",
            }));
        }

        return adimlar;
    }

    /// <summary>Danışman onayından sonra seçilen dersler onaylı sayılır.</summary>
    public bool DersOnayli => OnayTamamlanan >= 2;
}

/// <summary>Harf notu katsayısına göre AKTS ağırlıklı ortalama (4'lük sistem; katsayılar <see cref="NotOzeti.Katsayilar"/>).</summary>
public static class TranskriptOzeti
{
    public static decimal? Ortalama(IEnumerable<DersKaydi> dersler)
    {
        var sayilan = dersler.Where(d => NotOzeti.Katsayilar.ContainsKey(d.Harf)).ToList();
        var akts = sayilan.Sum(d => d.Akts);
        return akts == 0 ? null : sayilan.Sum(d => d.Akts * NotOzeti.Katsayilar[d.Harf]) / akts;
    }
}

/// <summary>
/// Demo OBİS'in öğrenci ekranlarının örnek verisi. Test öğrencileri (203011001-003) için ayrı ayrı; tanınmayan
/// kullanıcı adı için boş bir varsayılan kayıt (ekranlar "Kayıt yok." gösterir). Öğrenci yalnızca kendi kaydını görür:
/// kayıt, oturumdaki id_token'ın preferred_username claim'ine göre seçilir. Notlar (Not Durumu) bundan bağımsız olarak
/// Demo API'den gelir.
/// </summary>
public static class OrnekOgrenciVerisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    // Müfredat: dönem sırasıyla (örnek ders adları).
    private static readonly DersTanimi[][] Mufredat =
    [
        [
            new("BIL101", "Programlamaya Giriş", 6), new("MAT101", "Matematik I", 6), new("FIZ101", "Fizik I", 6),
            new("BIL103", "Bilgisayar Mühendisliğine Giriş", 4), new("ING101", "İngilizce I", 4), new("TDB101", "Türk Dili I", 4),
        ],
        [
            new("BIL102", "Nesne Yönelimli Programlama", 6), new("MAT102", "Matematik II", 6), new("FIZ102", "Fizik II", 6),
            new("BIL104", "Ayrık Matematik", 6), new("ING102", "İngilizce II", 3), new("TDB102", "Türk Dili II", 3),
        ],
        [
            new("BIL203", "Veri Yapıları", 6), new("MAT201", "Lineer Cebir", 5), new("BIL201", "Mantık Devreleri", 6),
            new("BIL207", "Programlama Dilleri", 5), new("MAT203", "Olasılık ve İstatistik", 4),
            new("ATA201", "Atatürk İlkeleri ve İnkılap Tarihi I", 4),
        ],
        [
            new("BIL202", "İşletim Sistemleri", 6), new("BIL204", "Bilgisayar Organizasyonu", 6),
            new("BIL206", "Yazılım Mühendisliği", 6), new("BIL208", "Sayısal Yöntemler", 5), new("BIL210", "Web Programlama", 5),
            new("ATA202", "Atatürk İlkeleri ve İnkılap Tarihi II", 2),
        ],
        [
            new("BIL305", "Veritabanı Sistemleri", 6), new("BIL307", "Bilgisayar Ağları", 6), new("BIL301", "Algoritma Analizi", 6),
            new("BIL303", "Biçimsel Diller ve Otomata", 6), new("SEC301", "Seçmeli Ders I", 6),
        ],
        [
            new("BIL302", "Yapay Zekâya Giriş", 6), new("BIL304", "Mikroişlemciler", 6), new("BIL306", "Yazılım Test ve Kalite", 6),
            new("BIL308", "Mobil Programlama", 6), new("SEC302", "Seçmeli Ders II", 6),
        ],
    ];

    private static readonly DersTanimi[] DorduncuSinifGuz =
    [
        new("BIL401", "Yazılım Mimarisi", 6), new("BIL403", "Bilgisayar Grafikleri", 5), new("BIL412", "Bilgi Güvenliği", 6),
        new("BIL498", "Bitirme Projesi", 8), new("SEC401", "Seçmeli Ders III", 5),
    ];

    /// <summary>Başlangıç yılından itibaren her dönem için bir harf dizisi ("BA CB …"; müfredat sırasıyla).</summary>
    private static List<DonemKaydi> Donemler(int baslangicYili, params string[] harfler) =>
    [
        .. harfler.Select((satir, i) =>
        {
            var yil = baslangicYili + i / 2;
            var notlar = satir.Split(' ');
            var dersler = Mufredat[i].Zip(notlar, (d, harf) => new DersKaydi(d.Kod, d.Ad, d.Akts, harf)).ToList();
            return new DonemKaydi($"{yil}-{yil + 1} {(i % 2 == 0 ? "Güz" : "Bahar")}", dersler);
        }),
    ];

    private static DateOnly T(int ay, int gun, int yil = 2026) => new(yil, ay, gun);

    private static SinavKaydi Sinav(DersTanimi ders, string tur, DateOnly tarih, int saat, string salon) =>
        new(ders.Kod, ders.Ad, tur, tarih, new TimeOnly(saat, 0), salon);

    public static readonly OgrenciKaydi Ogrenci1 = new(
        "203011001", "Örnek Akademisyen", 3,
        Donemler(2024, "BA AA BB CB CC BA", "BB BA CB BB AA BA", "CB BB BA CC BB AA", "BB CB BA BB BA AA"),
        Mufredat[4], OnayTamamlanan: 1,
        [
            new("2025-2026 Bahar", "Öğrenim katkı payı (örnek)", 1250m, T(2, 20, 2026), true),
            new("2026-2027 Güz", "Öğrenim katkı payı (örnek)", 1250m, T(10, 2), true),
        ],
        [
            Sinav(Mufredat[4][0], "Ara sınav", T(11, 30), 10, "Örnek Salon A1"),
            Sinav(Mufredat[4][1], "Ara sınav", T(12, 1), 13, "Örnek Salon A2"),
            Sinav(Mufredat[4][2], "Ara sınav", T(12, 3), 9, "Örnek Salon B1"),
            Sinav(Mufredat[4][3], "Ara sınav", T(12, 4), 14, "Örnek Salon B1"),
            Sinav(Mufredat[4][0], "Dönem sonu", T(1, 12, 2027), 10, "Örnek Salon A1"),
        ],
        [
            new("2026/14-3", T(9, 18), "Örnek karar: ders kaydı yenileme talebi", "Kabul"),
            new("2026/17-1", T(10, 29), "Örnek karar: muafiyet incelemesi", "İnceleniyor"),
        ]);

    public static readonly OgrenciKaydi Ogrenci2 = new(
        "203011002", "Örnek Akademisyen İki", 2,
        Donemler(2025, "AA AA BA BA AA AA", "BA AA AA BA BA AA"),
        Mufredat[2], OnayTamamlanan: 4,
        [
            new("2025-2026 Bahar", "Öğrenim katkı payı (örnek)", 1250m, T(2, 20, 2026), true),
            new("2026-2027 Güz", "Öğrenim katkı payı (örnek)", 1250m, T(12, 4), false),
        ],
        [
            Sinav(Mufredat[2][0], "Ara sınav", T(11, 27), 10, "Örnek Salon C1"),
            Sinav(Mufredat[2][1], "Ara sınav", T(11, 30), 14, "Örnek Salon C2"),
            Sinav(Mufredat[2][2], "Ara sınav", T(12, 2), 9, "Örnek Salon C1"),
        ],
        []);

    public static readonly OgrenciKaydi Ogrenci3 = new(
        "203011003", "Örnek Akademisyen Üç", 4,
        Donemler(2023, "CC DD CC CB BB BB", "DC CC DD CC CB BB", "CC CB DC CC BB CB", "CB CC CB BB BB BA", "BB CB CC CB BA", "CB BB BB CB BA"),
        DorduncuSinifGuz, OnayTamamlanan: 2,
        [
            new("2025-2026 Bahar", "Öğrenim katkı payı (örnek)", 1250m, T(2, 20, 2026), true),
            new("2026-2027 Güz", "Öğrenim katkı payı (örnek)", 1250m, T(10, 2), true),
        ],
        [
            Sinav(DorduncuSinifGuz[0], "Ara sınav", T(11, 26), 13, "Örnek Salon D1"),
            Sinav(DorduncuSinifGuz[2], "Ara sınav", T(12, 2), 10, "Örnek Salon D2"),
            Sinav(DorduncuSinifGuz[1], "Ara sınav", T(12, 5), 15, "Örnek Salon D1"),
        ],
        [new("2026/12-2", T(9, 4), "Örnek karar: bitirme projesi danışman değişikliği", "Kabul")]);

    /// <summary>Tanınmayan kullanıcı adı: boş kayıt (ders seçimi dışında veri yok).</summary>
    public static readonly OgrenciKaydi Varsayilan = new(
        "ornek", "Örnek Akademisyen", 1, [], Mufredat[0], OnayTamamlanan: 1, [], [], []);

    private static readonly IReadOnlyDictionary<string, OgrenciKaydi> Kayitlar =
        new[] { Ogrenci1, Ogrenci2, Ogrenci3 }.ToDictionary(k => k.KullaniciAdi, StringComparer.Ordinal);

    /// <summary>Kullanıcının kendi örnek kaydı (preferred_username claim'i); yoksa <see cref="Varsayilan"/>.</summary>
    public static OgrenciKaydi Bul(ClaimsPrincipal kullanici) =>
        kullanici.FindFirst("preferred_username")?.Value is { } ad && Kayitlar.TryGetValue(ad, out var kayit) ? kayit : Varsayilan;

    public static string Tarih(DateOnly tarih) => tarih.ToString("dd.MM.yyyy", Tr);

    public static string Saat(TimeOnly saat) => saat.ToString("HH:mm", Tr);

    public static string Tutar(decimal tutar) => tutar.ToString("N2", Tr) + " TL";

    public static string Ortalama(decimal? ortalama) => ortalama?.ToString("0.00", Tr) ?? "—";
}
