using System.Globalization;
using Selcuk.Otomasyon.Staj.Sozlesme;
using static Selcuk.Otomasyon.Staj.Sozlesme.StajDurumu;
using static Selcuk.Otomasyon.Staj.Sozlesme.StajTuru;

namespace Selcuk.Otomasyon.Staj.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2026 yıllarına yayılan 26 staj kaydı ve defter girişleri. Öğrenciler kimlik sunucusunun
/// test öğrencileri (203011001, 203011002, 203011003) ve yalnızca burada bulunan uydurma öğrencilerdir (ORN-…);
/// ikisi Elektrik-Elektronik Mühendisliği'ndendir (bölüm kapsamı denensin diye). Firma adları ve adresleri uydurmadır.
/// </summary>
public static class OrnekStajVerisi
{
    public const string Fakulte = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";

    private const string OrnekYazilim = "Örnek Yazılım A.Ş.";
    private const string DenemeElektronik = "Deneme Elektronik Ltd.";
    private const string KurguBilisim = "Kurgu Bilişim Ltd. Şti.";
    private const string HayaliTeknoloji = "Hayali Teknoloji A.Ş.";
    private const string OrnekVeri = "Örnek Veri Hizmetleri A.Ş.";

    private static readonly Dictionary<string, string> Adresler = new()
    {
        [OrnekYazilim] = "Örnek Mah. Deneme Cad. No: 1, Konya",
        [DenemeElektronik] = "Deneme Mah. Kurgu Sok. No: 7, Konya",
        [KurguBilisim] = "Kurgu Mah. Örnek Bulvarı No: 21, Ankara",
        [HayaliTeknoloji] = "Hayali Mah. Deneme Sok. No: 3, İstanbul",
        [OrnekVeri] = "Örnek Mah. Veri Cad. No: 12, İzmir",
    };

    private static readonly Dictionary<string, (string Ad, string Bolum)> Ogrenciler = new()
    {
        ["203011001"] = ("Test Öğrenci", Bilgisayar),
        ["203011002"] = ("Test Öğrenci İki", Bilgisayar),
        ["203011003"] = ("Test Öğrenci Üç", Bilgisayar),
        ["ORN-0101"] = ("Örnek Öğrenci Bir", Bilgisayar),
        ["ORN-0102"] = ("Örnek Öğrenci İki", Bilgisayar),
        ["ORN-0103"] = ("Örnek Öğrenci Üç", Bilgisayar),
        ["ORN-0104"] = ("Örnek Öğrenci Dört", Bilgisayar),
        ["ORN-0105"] = ("Örnek Öğrenci Beş", Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", ElektrikElektronik),
        ["ORN-0202"] = ("Örnek Öğrenci EEM İki", ElektrikElektronik),
    };

    public static readonly IReadOnlyList<StajKaydi> Kayitlar =
    [
        // 2024
        K("S24-01", "ORN-0101", StajI, OrnekYazilim, "2024-07-01", "2024-07-26", 20, Degerlendirildi, "2024-05-10", 20),
        K("S24-02", "ORN-0102", StajI, DenemeElektronik, "2024-07-08", "2024-08-02", 20, Degerlendirildi, "2024-05-14", 18,
            "İki günün defter girişi eksik; 18 gün kabul edildi."),
        K("S24-03", "ORN-0103", StajI, KurguBilisim, "2024-08-05", "2024-08-30", 20, Reddedildi, "2024-06-03", null,
            "Firma bilgileri eksik; başvuru reddedildi."),
        K("S24-04", "ORN-0104", StajII, OrnekYazilim, "2024-07-15", "2024-08-09", 20, Degerlendirildi, "2024-05-20", 20),
        K("S24-05", "ORN-0201", StajI, DenemeElektronik, "2024-07-01", "2024-07-26", 20, Degerlendirildi, "2024-05-08", 20),
        K("S24-06", "ORN-0105", StajI, HayaliTeknoloji, "2024-08-12", "2024-09-06", 20, Degerlendirildi, "2024-06-10", 20),

        // 2025
        K("S25-01", "203011001", StajI, OrnekYazilim, "2025-07-07", "2025-08-01", 20, Degerlendirildi, "2025-05-12", 20,
            "Defter eksiksiz; 20 gün kabul edildi."),
        K("S25-02", "203011002", StajI, DenemeElektronik, "2025-07-14", "2025-08-08", 20, Degerlendirildi, "2025-05-15", 17,
            "Üç günde yapılan çalışma açıklanmamış; 17 gün kabul edildi."),
        K("S25-03", "203011003", StajI, KurguBilisim, "2025-08-04", "2025-08-29", 20, Degerlendirildi, "2025-06-02", 20),
        K("S25-04", "ORN-0101", StajII, HayaliTeknoloji, "2025-07-01", "2025-07-28", 20, Degerlendirildi, "2025-05-06", 20),
        K("S25-05", "ORN-0102", StajII, OrnekVeri, "2025-07-21", "2025-08-15", 20, Reddedildi, "2025-05-26", 0,
            "Defter staj konusuyla ilgili değil; staj kabul edilmedi."),
        K("S25-06", "ORN-0103", StajI, DenemeElektronik, "2025-07-07", "2025-08-01", 20, Degerlendirildi, "2025-05-13", 20),
        K("S25-07", "ORN-0202", StajI, KurguBilisim, "2025-07-14", "2025-08-08", 20, Degerlendirildi, "2025-05-19", 20),
        K("S25-08", "ORN-0104", Ime, OrnekYazilim, "2025-09-15", "2026-01-09", 70, Degerlendirildi, "2025-06-16", 70),

        // 2026
        K("S26-01", "203011001", StajII, DenemeElektronik, "2026-07-06", "2026-07-31", 20, DefterTeslimEdildi, "2026-05-11"),
        K("S26-02", "203011001", Ime, OrnekYazilim, "2027-02-15", "2027-05-28", 70, BasvuruBekliyor, "2026-10-20"),
        K("S26-03", "203011002", StajII, HayaliTeknoloji, "2026-08-03", "2026-08-28", 20, Degerlendirildi, "2026-06-01", 19,
            "Bir günlük giriş eksik; 19 gün kabul edildi."),
        K("S26-04", "203011002", Ime, KurguBilisim, "2026-09-14", "2026-12-25", 70, DevamEdiyor, "2026-06-15"),
        K("S26-05", "203011003", StajII, OrnekVeri, "2026-11-02", "2026-11-27", 20, Onaylandi, "2026-10-05"),
        K("S26-06", "ORN-0105", StajII, DenemeElektronik, "2026-07-20", "2026-08-14", 20, DefterTeslimEdildi, "2026-05-25"),
        K("S26-07", "ORN-0101", Ime, OrnekYazilim, "2026-09-14", "2026-12-25", 70, DevamEdiyor, "2026-06-08"),
        K("S26-08", "ORN-0102", StajII, KurguBilisim, "2026-11-09", "2026-12-04", 20, BasvuruBekliyor, "2026-10-12"),
        K("S26-09", "ORN-0103", StajII, HayaliTeknoloji, "2026-11-16", "2026-12-11", 20, BasvuruBekliyor, "2026-10-14"),
        K("S26-10", "ORN-0201", StajII, DenemeElektronik, "2026-11-02", "2026-11-27", 20, BasvuruBekliyor, "2026-10-07"),
        K("S26-11", "ORN-0202", StajII, OrnekYazilim, "2026-07-06", "2026-07-31", 20, DefterTeslimEdildi, "2026-05-12"),
        K("S26-12", "ORN-0105", Ime, OrnekVeri, "2027-02-15", "2027-05-28", 70, Reddedildi, "2026-10-01", null,
            "Staj II değerlendirmesi tamamlanmadan İME başvurusu yapılamaz."),
    ];

    // DefterGirisleri'nden önce tanımlı olmalı (statik alanlar sırayla başlatılır).
    private static readonly string[] Calismalar =
    [
        "Firmanın yazılım geliştirme süreci ve kullanılan araçlar tanıtıldı.",
        "Geliştirme ortamı kuruldu; örnek proje derlendi ve çalıştırıldı.",
        "Ekip toplantısına katılındı; bir hata kaydı incelendi.",
        "Birim testleri yazıldı ve kod incelemesine gönderildi.",
        "Haftalık çalışma raporu hazırlandı.",
    ];

    /// <summary>Defteri olan (devam eden, teslim edilmiş veya değerlendirilmiş) stajların ilk beş iş günü.</summary>
    public static readonly IReadOnlyList<DefterGirisi> DefterGirisleri =
    [
        .. Kayitlar.Where(k => k.Durum is DevamEdiyor or DefterTeslimEdildi or Degerlendirildi)
            .SelectMany(k => IsGunleri(k.Baslangic).Take(5).Select((gun, i) => new DefterGirisi(k.Id, gun, Calismalar[i]))),
    ];

    private static IEnumerable<DateOnly> IsGunleri(DateOnly baslangic)
    {
        for (var gun = baslangic; ; gun = gun.AddDays(1))
        {
            if (gun.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                yield return gun;
            }
        }
    }

    private static DateOnly Tarih(string deger) => DateOnly.ParseExact(deger, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static StajKaydi K(string id, string no, StajTuru tur, string firma, string baslangic, string bitis, int isGunu,
        StajDurumu durum, string basvuru, int? kabul = null, string? not = null)
    {
        var (ad, bolum) = Ogrenciler[no];
        return new StajKaydi(id, no, ad, Fakulte, bolum, tur, firma, Adresler[firma], Tarih(baslangic), Tarih(bitis),
            isGunu, durum, Tarih(basvuru), kabul, not);
    }
}
