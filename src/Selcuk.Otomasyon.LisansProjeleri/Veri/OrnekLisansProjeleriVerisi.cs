using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using static Selcuk.Otomasyon.LisansProjeleri.Sozlesme.ProjeAsamasi;

namespace Selcuk.Otomasyon.LisansProjeleri.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2025'ten 2026-2027'ye 17 lisans projesi kaydı ve içinde bulunulan dönemin 7 konusu.
/// Öğrenciler test öğrencileri (203011001-003) ve uydurma öğrencilerdir (ORN-…); hocalar test akademisyeni (hoca.test)
/// ve uydurma akademisyenlerdir (orn.akademisyen…). İkisi Elektrik-Elektronik Mühendisliği'nden (bir konu ve proje).
/// Konu başlıkları uydurmadır.
/// </summary>
public static class OrnekLisansProjeleriVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string GuncelDonem = "2026-2027";

    private static readonly Dictionary<string, (string Ad, string Bolum)> Hocalar = new()
    {
        ["hoca.test"] = ("Test Akademisyen", Bilgisayar),
        ["orn.akademisyen01"] = ("Örnek Akademisyen Bir", Bilgisayar),
        ["orn.akademisyen02"] = ("Örnek Akademisyen İki", Bilgisayar),
        ["orn.akademisyen03"] = ("Örnek Akademisyen Üç", Bilgisayar),
        ["orn.akademisyen05"] = ("Örnek Akademisyen EEM", ElektrikElektronik),
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
        ["ORN-0106"] = ("Örnek Öğrenci Altı", Bilgisayar),
        ["ORN-0107"] = ("Örnek Öğrenci Yedi", Bilgisayar),
        ["ORN-0108"] = ("Örnek Öğrenci Sekiz", Bilgisayar),
        ["ORN-0109"] = ("Örnek Öğrenci Dokuz", Bilgisayar),
        ["ORN-0110"] = ("Örnek Öğrenci On", Bilgisayar),
        ["ORN-0111"] = ("Örnek Öğrenci On Bir", Bilgisayar),
        ["ORN-0112"] = ("Örnek Öğrenci On İki", Bilgisayar),
        ["ORN-0201"] = ("Örnek Öğrenci EEM Bir", ElektrikElektronik),
        ["ORN-0202"] = ("Örnek Öğrenci EEM İki", ElektrikElektronik),
    };

    public static readonly IReadOnlyList<ProjeKonusu> Konular =
    [
        Konu("L-K01", "Web tabanlı kütüphane salon rezervasyonu", "hoca.test", 2, true),
        Konu("L-K02", "Örnek veri kümesiyle görüntüden metin okuma", "hoca.test", 1, false),
        Konu("L-K03", "Mobil kampüs rehberi uygulaması", "orn.akademisyen01", 2, true),
        Konu("L-K04", "Ders soru bankası için metin sınıflandırma", "orn.akademisyen02", 2, true),
        Konu("L-K05", "Sensörlerle sera ortamı izleme", "orn.akademisyen03", 2, false),
        Konu("L-K06", "Oyunlaştırılmış algoritma eğitimi", "orn.akademisyen01", 2, true),
        Konu("L-K07", "Güneş paneli verim izleme kartı", "orn.akademisyen05", 2, true),
    ];

    public static readonly IReadOnlyList<LisansProjesi> Kayitlar =
    [
        // Tamamlanan projeler
        P("P24-01", "ORN-0101", "2024-2025", Tamamlandi, "Kantin sipariş sistemi", "orn.akademisyen01", not: 85),
        P("P24-02", "ORN-0102", "2024-2025", Tamamlandi, "Ders programı çakışma denetleyicisi", "hoca.test", not: 78),
        P("P24-03", "ORN-0201", "2024-2025", Tamamlandi, "Akıllı priz prototipi", "orn.akademisyen05", not: 90),
        P("P25-01", "ORN-0103", "2025-2026", Tamamlandi, "Laboratuvar envanter takibi", "hoca.test", not: 72),
        P("P25-02", "ORN-0104", "2025-2026", Tamamlandi, "Otopark doluluk tahmini", "orn.akademisyen02", not: 88),
        P("P25-03", "ORN-0105", "2025-2026", Tamamlandi, "Etkinlik duyuru botu", "orn.akademisyen03", not: 65),

        // 2026-2027
        P("P26-01", "203011001", GuncelDonem, AraRapor, konuId: "L-K01"),
        P("P26-02", "203011002", GuncelDonem, KonuSecimi, tercihler: ["L-K03", "L-K01", "L-K06"]),
        P("P26-03", "203011003", GuncelDonem, Onerge, konuId: "L-K04"),
        P("P26-04", "ORN-0106", GuncelDonem, Onerge, konuId: "L-K01"),
        P("P26-05", "ORN-0107", GuncelDonem, Teslim, konuId: "L-K02"),
        P("P26-06", "ORN-0108", GuncelDonem, AraRapor, konuId: "L-K03"),
        P("P26-07", "ORN-0109", GuncelDonem, SunumDegerlendirme, konuId: "L-K05"),
        P("P26-08", "ORN-0110", GuncelDonem, AraRapor, konuId: "L-K05"),
        P("P26-09", "ORN-0111", GuncelDonem, Onerge, konuId: "L-K06"),
        P("P26-10", "ORN-0112", GuncelDonem, KonuSecimi, tercihler: ["L-K04", "L-K06"]),
        P("P26-11", "ORN-0202", GuncelDonem, Onerge, konuId: "L-K07"),
    ];

    private static ProjeKonusu Konu(string id, string baslik, string hoca, int kontenjan, bool acik)
    {
        var (hocaAdi, bolum) = Hocalar[hoca];
        return new ProjeKonusu(id, baslik, "Örnek açıklama: konunun amacı, beklenen çıktılar ve kullanılacak araçlar.",
            hoca, hocaAdi, Teknoloji, bolum, GuncelDonem, kontenjan, acik);
    }

    private static LisansProjesi P(string id, string no, string donem, ProjeAsamasi asama, string? eskiKonu = null,
        string? eskiHoca = null, string? konuId = null, IReadOnlyList<string>? tercihler = null, int? not = null)
    {
        var (ad, bolum) = Ogrenciler[no];
        var konu = konuId is null ? null : Konular.Single(k => k.Id == konuId);
        var hoca = konu?.HocaKullaniciAdi ?? eskiHoca;
        return new LisansProjesi(id, no, ad, Teknoloji, bolum, donem, asama, konuId, konu?.Baslik ?? eskiKonu,
            hoca, hoca is null ? null : Hocalar[hoca].Ad, tercihler ?? [], not);
    }
}
