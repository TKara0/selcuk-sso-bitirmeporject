using System.Globalization;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;
using static Selcuk.Otomasyon.EtikKurul.Sozlesme.ArastirmaTuru;
using static Selcuk.Otomasyon.EtikKurul.Sozlesme.EtikDurumu;
using static Selcuk.Otomasyon.EtikKurul.Sozlesme.GorusTuru;

namespace Selcuk.Otomasyon.EtikKurul.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2026 yıllarına yayılan 17 etik kurul başvurusu. Başvuranlar kimlik sunucusunun test
/// akademisyeni (hoca.test) ve yalnızca burada bulunan uydurma akademisyenlerdir (orn.akademisyen…); biri
/// Elektrik-Elektronik Mühendisliği'nden. Kurul üyeleri test kullanıcısı etik.test ve uydurma üyelerdir (orn.etikuye…).
/// Proje başlıkları uydurmadır.
/// </summary>
public static class OrnekEtikKurulVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";

    private static readonly Dictionary<string, (string Ad, string Bolum)> Akademisyenler = new()
    {
        ["hoca.test"] = ("Test Akademisyen", Bilgisayar),
        ["orn.akademisyen01"] = ("Örnek Akademisyen Bir", Bilgisayar),
        ["orn.akademisyen02"] = ("Örnek Akademisyen İki", Bilgisayar),
        ["orn.akademisyen03"] = ("Örnek Akademisyen Üç", Bilgisayar),
        ["orn.akademisyen04"] = ("Örnek Akademisyen Dört", Bilgisayar),
        ["orn.akademisyen05"] = ("Örnek Akademisyen EEM", ElektrikElektronik),
    };

    private static readonly Dictionary<string, string> Uyeler = new()
    {
        ["etik.test"] = "Test Etik Kurul Üyesi",
        ["orn.etikuye01"] = "Örnek Kurul Üyesi Bir",
        ["orn.etikuye02"] = "Örnek Kurul Üyesi İki",
        ["orn.etikuye03"] = "Örnek Kurul Üyesi Üç",
    };

    private static (string, GorusTuru?) G(string uye, GorusTuru? gorus = null) => (uye, gorus);

    public static readonly IReadOnlyList<EtikBasvurusu> Kayitlar =
    [
        // 2024
        K("E24-01", "orn.akademisyen01", "Öğrencilerin çevrim içi ders alışkanlıkları anketi", Anket, "Lisans öğrencileri (gönüllü)",
            "Çevrim içi anket formu", "2024-03-04", Onaylandi, "2024-03-28", null,
            G("etik.test", Uygun), G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun)),
        K("E24-02", "orn.akademisyen02", "Uyku düzeninin kod okuma hızına etkisi deneyi", Deney, "Gönüllü yetişkinler",
            "Laboratuvar deneyi ve süre ölçümü", "2024-05-06", Reddedildi, "2024-06-17", "Katılımcı güvenliği planı yetersiz.",
            G("etik.test", UygunDegil), G("orn.etikuye01", UygunDegil), G("orn.etikuye03", DuzeltmeGerekli)),
        K("E24-03", "hoca.test", "Yazılım ekiplerinde iletişim üzerine görüşmeler", Gorusme, "Yazılım geliştiriciler (gönüllü)",
            "Yarı yapılandırılmış görüşme", "2024-10-07", Onaylandi, "2024-10-30", null,
            G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),
        K("E24-04", "orn.akademisyen05", "Akıllı ev sensörlerinin kullanıcı deneyimi deneyi", Deney, "Gönüllü yetişkinler",
            "Kullanılabilirlik testi ve gözlem", "2024-11-11", Onaylandi, "2024-12-09", null,
            G("etik.test", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),

        // 2025
        K("E25-01", "orn.akademisyen03", "Açık veri kümelerinde cinsiyet dengesi incelemesi", IkincilVeri, "Kişi katılımı yok",
            "Anonim açık veri kümeleri", "2025-02-10", Onaylandi, "2025-03-21", null,
            G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),
        K("E25-02", "hoca.test", "Programlama dersinde geri bildirim anketi", Anket, "Lisans öğrencileri (gönüllü)",
            "Basılı ve çevrim içi anket", "2025-04-14", DuzeltmeIstendi, "2025-05-08", "Bilgilendirilmiş onam formu eksik.",
            G("etik.test", DuzeltmeGerekli), G("orn.etikuye01", Uygun), G("orn.etikuye02", DuzeltmeGerekli)),
        K("E25-03", "orn.akademisyen04", "Mezunların ilk iş deneyimi görüşmeleri", Gorusme, "Mezunlar (gönüllü)",
            "Çevrim içi görüşme", "2025-06-02", Onaylandi, "2025-06-25", null,
            G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),
        K("E25-04", "orn.akademisyen01", "Göz izleme ile arayüz okunabilirliği deneyi", Deney, "Gönüllü yetişkinler",
            "Göz izleme cihazıyla deney", "2025-09-15", Reddedildi, "2025-10-20", "Kayıtların saklanma süresi belirtilmemiş.",
            G("orn.etikuye01", UygunDegil), G("orn.etikuye02", UygunDegil), G("orn.etikuye03", UygunDegil)),

        // 2026
        K("E26-01", "hoca.test", "Kodlama asistanı kullanımının öğrenmeye etkisi deneyi", Deney, "Lisans öğrencileri (gönüllü)",
            "Ön test, uygulama, son test", "2026-09-14", KurulDegerlendirmesinde, null, null,
            G("etik.test"), G("orn.etikuye01", Uygun), G("orn.etikuye02")),
        K("E26-02", "hoca.test", "Bitirme projesi süreçleri anketi", Anket, "Son sınıf öğrencileri (gönüllü)",
            "Çevrim içi anket formu", "2026-10-19", OnIncelemede, null, null),
        K("E26-03", "orn.akademisyen02", "Uzaktan çalışan yazılımcılarla görüşmeler", Gorusme, "Yazılım geliştiriciler (gönüllü)",
            "Çevrim içi görüşme ve ses kaydı", "2026-09-28", KurulDegerlendirmesinde, null, null,
            G("etik.test", Uygun), G("orn.etikuye03"), G("orn.etikuye02", DuzeltmeGerekli)),
        K("E26-04", "orn.akademisyen03", "Kampüs ulaşımı memnuniyet anketi", Anket, "Öğrenci ve personel (gönüllü)",
            "Çevrim içi anket formu", "2026-10-12", OnIncelemede, null, null),
        K("E26-05", "orn.akademisyen04", "Sosyal medya verisinde konu modelleme", IkincilVeri, "Kişi katılımı yok",
            "Anonimleştirilmiş paylaşımlar", "2026-08-03", DuzeltmeIstendi, "2026-09-01", "Anonimleştirme yöntemi açıklanmalı.",
            G("etik.test", DuzeltmeGerekli), G("orn.etikuye01", DuzeltmeGerekli), G("orn.etikuye03", Uygun)),
        K("E26-06", "orn.akademisyen05", "Giyilebilir sensörlerle yürüyüş analizi deneyi", Deney, "Gönüllü yetişkinler",
            "Sensörlü ölçüm", "2026-10-05", KurulDegerlendirmesinde, null, null,
            G("etik.test"), G("orn.etikuye01"), G("orn.etikuye02", Uygun)),
        K("E26-07", "orn.akademisyen01", "Kütüphane kullanım alışkanlıkları anketi", Anket, "Lisans öğrencileri (gönüllü)",
            "Basılı anket", "2026-06-08", Onaylandi, "2026-07-02", null,
            G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),
        K("E26-08", "orn.akademisyen04", "Staj deneyimi üzerine görüşmeler", Gorusme, "Stajını tamamlamış öğrenciler (gönüllü)",
            "Yüz yüze görüşme", "2026-10-01", KurulDegerlendirmesinde, null, null,
            G("orn.etikuye01", Uygun), G("orn.etikuye02", Uygun), G("orn.etikuye03", Uygun)),
        K("E26-09", "orn.akademisyen05", "Elektrikli araç şarj davranışı anketi", Anket, "Araç sahipleri (gönüllü)",
            "Çevrim içi anket formu", "2026-07-13", Reddedildi, "2026-08-24", "Örneklem seçimi ve veri gizliliği yetersiz.",
            G("orn.etikuye01", UygunDegil), G("orn.etikuye02", UygunDegil), G("orn.etikuye03", DuzeltmeGerekli)),
    ];

    private static DateOnly Tarih(string deger) => DateOnly.ParseExact(deger, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static EtikBasvurusu K(string id, string basvuran, string baslik, ArastirmaTuru tur, string katilimci, string yontem,
        string basvuru, EtikDurumu durum, string? karar, string? not, params (string Uye, GorusTuru? Gorus)[] gorusler)
    {
        var (ad, bolum) = Akademisyenler[basvuran];
        return new EtikBasvurusu(id, basvuran, ad, Teknoloji, bolum, baslik, tur, katilimci, yontem, Tarih(basvuru), durum,
            [.. gorusler.Select(g => new UyeGorusu(g.Uye, Uyeler[g.Uye], g.Gorus,
                g.Gorus is GorusTuru.Uygun or null ? null : "Örnek gerekçe: başvuru belgelerinde eksik veya belirsiz bölüm var."))],
            karar is null ? null : Tarih(karar), not);
    }
}
