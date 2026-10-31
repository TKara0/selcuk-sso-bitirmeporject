using System.Globalization;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using static Selcuk.Otomasyon.Danismanlik.Sozlesme.GorusmeKonusu;
using static Selcuk.Otomasyon.Danismanlik.Sozlesme.TalepDurumu;

namespace Selcuk.Otomasyon.Danismanlik.Veri;

/// <summary>
/// Örnek (mock) veri: 17 öğrencinin danışmanlık kaydı, 11 görüşme notu ve 7 görüşme talebi (2024-2026). Öğrenciler
/// test öğrencileri (203011001-003) ve uydurma öğrencilerdir (ORN-…); danışmanlar hoca.test, koordinator.test ve
/// uydurma akademisyenlerdir (orn.akademisyen…). İki öğrenci Elektrik-Elektronik Mühendisliği'nden, ikisinin danışmanı
/// yok. Notlar genel ve akademiktir.
/// </summary>
public static class OrnekDanismanlikVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";

    private static readonly Dictionary<string, string> Danismanlar = new()
    {
        ["hoca.test"] = "Test Akademisyen",
        ["koordinator.test"] = "Test ÇAP-Yandal Koordinatörü",
        ["orn.akademisyen01"] = "Örnek Akademisyen Bir",
        ["orn.akademisyen02"] = "Örnek Akademisyen İki",
        ["orn.akademisyen05"] = "Örnek Akademisyen EEM",
    };

    public static readonly IReadOnlyList<DanismanlikOgrencisi> Ogrenciler =
    [
        O("203011001", "Test Öğrenci", 4, 182, 3.12m, "hoca.test", "2026-09-15"),
        O("203011002", "Test Öğrenci İki", 4, 176, 2.65m, "hoca.test", "2026-05-20"),
        O("ORN-0101", "Örnek Öğrenci Bir", 3, 120, 2.40m, "hoca.test", "2025-12-10"),
        O("ORN-0102", "Örnek Öğrenci İki", 2, 60, 2.95m, "hoca.test", "2026-10-05"),
        O("ORN-0103", "Örnek Öğrenci Üç", 1, 30, 1.85m, "hoca.test"),
        O("203011003", "Test Öğrenci Üç", 4, 168, 2.35m, "koordinator.test", "2026-08-28"),
        O("ORN-0104", "Örnek Öğrenci Dört", 3, 118, 3.40m, "koordinator.test", "2026-03-11"),
        O("ORN-0105", "Örnek Öğrenci Beş", 2, 58, 2.10m, "koordinator.test", "2026-10-12"),
        O("ORN-0106", "Örnek Öğrenci Altı", 1, 28, 3.05m, "koordinator.test", "2026-10-20"),
        O("ORN-0107", "Örnek Öğrenci Yedi", 3, 112, 2.75m, "orn.akademisyen01", "2026-04-02"),
        O("ORN-0108", "Örnek Öğrenci Sekiz", 2, 62, 3.20m, "orn.akademisyen01", "2026-09-29"),
        O("ORN-0109", "Örnek Öğrenci Dokuz", 4, 190, 2.55m, "orn.akademisyen01", "2025-11-18"),
        O("ORN-0110", "Örnek Öğrenci On", 2, 56, 2.85m, "orn.akademisyen02", "2026-10-07"),
        O("ORN-0111", "Örnek Öğrenci On Bir", 1, 30, 2.60m),
        O("ORN-0112", "Örnek Öğrenci On İki", 1, 26, 2.20m),
        O("ORN-0201", "Örnek Öğrenci EEM Bir", 3, 116, 2.90m, "orn.akademisyen05", "2026-09-30", ElektrikElektronik),
        O("ORN-0202", "Örnek Öğrenci EEM İki", 2, 60, 3.10m, "orn.akademisyen05", "2026-06-15", ElektrikElektronik),
    ];

    public static readonly IReadOnlyList<GorusmeNotu> Notlar =
    [
        N("N24-01", "ORN-0101", "hoca.test", "2024-09-20", DersSecimi, "Dönem ders yükü ve ön koşullu dersler gözden geçirildi."),
        N("N25-01", "203011001", "hoca.test", "2025-02-14", StajPlani, "Yaz stajı için başvuru takvimi ve gerekli belgeler konuşuldu."),
        N("N25-02", "203011002", "hoca.test", "2025-09-18", AkademikIlerleme, "Tekrar edilecek dersler ve dönem çalışma planı belirlendi."),
        N("N25-03", "ORN-0101", "hoca.test", "2025-12-10", KariyerPlani, "Lisansüstü eğitim seçenekleri hakkında genel bilgi verildi."),
        N("N26-01", "203011002", "hoca.test", "2026-05-20", DersSecimi, "Seçmeli ders havuzundan uygun dersler önerildi."),
        N("N26-02", "203011001", "hoca.test", "2026-09-15", KariyerPlani, "Bitirme projesi konusu ile ilgi alanları birlikte değerlendirildi."),
        N("N26-03", "ORN-0102", "hoca.test", "2026-10-05", AkademikIlerleme, "Ara sınav dönemi için çalışma takvimi önerildi."),
        N("N26-04", "203011003", "koordinator.test", "2026-08-28", StajPlani, "İşletmede mesleki eğitim seçenekleri anlatıldı."),
        N("N26-05", "ORN-0105", "koordinator.test", "2026-10-12", DersSecimi, "Alttan alınacak dersin ders programına yerleşimi konuşuldu."),
        N("N26-06", "ORN-0106", "koordinator.test", "2026-10-20", AkademikIlerleme, "Bölüm olanakları ve öğrenci toplulukları tanıtıldı."),
        N("N26-07", "ORN-0201", "orn.akademisyen05", "2026-09-30", DersSecimi, "Seçmeli laboratuvar dersleri gözden geçirildi.", ElektrikElektronik),
    ];

    public static readonly IReadOnlyList<GorusmeTalebi> Talepler =
    [
        T("T25-01", "203011002", "2025-09-10", AkademikIlerleme, Yapildi, "2025-09-18"),
        T("T26-01", "203011002", "2026-10-22", KariyerPlani, Bekliyor),
        T("T26-02", "203011001", "2026-09-08", KariyerPlani, Yapildi, "2026-09-15"),
        T("T26-03", "203011001", "2026-10-26", StajPlani, Onaylandi, "2026-11-04"),
        T("T26-04", "203011003", "2026-10-02", DersSecimi, Reddedildi, null, "Önerilen saat uygun değil; yeni saat için tekrar talep oluştur."),
        T("T26-05", "ORN-0103", "2026-10-15", AkademikIlerleme, Bekliyor),
        T("T26-06", "ORN-0201", "2026-09-24", DersSecimi, Yapildi, "2026-09-30"),
    ];

    private static DateOnly Tarih(string deger) => DateOnly.ParseExact(deger, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DanismanlikOgrencisi O(string no, string ad, int sinif, int akts, decimal gno, string? danisman = null,
        string? sonGorusme = null, string bolum = Bilgisayar) =>
        new(no, ad, Teknoloji, bolum, sinif, akts, gno, danisman, danisman is null ? null : Danismanlar[danisman],
            sonGorusme is null ? null : Tarih(sonGorusme));

    private static GorusmeNotu N(string id, string no, string danisman, string tarih, GorusmeKonusu konu, string not,
        string bolum = Bilgisayar) =>
        new(id, no, Ogrenciler.Single(o => o.OgrenciNo == no).OgrenciAdi, danisman, Teknoloji, bolum, Tarih(tarih), konu, not);

    private static GorusmeTalebi T(string id, string no, string talep, GorusmeKonusu konu, TalepDurumu durum,
        string? gorusme = null, string? aciklama = null)
    {
        var ogrenci = Ogrenciler.Single(o => o.OgrenciNo == no);
        return new GorusmeTalebi(id, no, ogrenci.OgrenciAdi, ogrenci.DanismanAdi ?? "Atanmadı", Tarih(talep), konu, durum,
            gorusme is null ? null : Tarih(gorusme), aciklama);
    }
}
