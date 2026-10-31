using System.Globalization;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using static Selcuk.Otomasyon.BolumKurul.Sozlesme.MaddeDurumu;
using static Selcuk.Otomasyon.BolumKurul.Sozlesme.ToplantiDurumu;

namespace Selcuk.Otomasyon.BolumKurul.Veri;

/// <summary>
/// Örnek (mock) veri: 2024-2026 yıllarına yayılan 15 bölüm kurulu toplantısı ve gündem maddeleri. Onu Bilgisayar
/// Mühendisliği'nin, üçü Elektrik-Elektronik Mühendisliği'nin (aynı fakülte), ikisi Mühendislik Fakültesi Makine
/// Mühendisliği'nin (başka fakülte). Konular ve yerler uydurmadır.
/// </summary>
public static class OrnekBolumKurulVerisi
{
    public const string Teknoloji = "Teknoloji Fakültesi";
    public const string Muhendislik = "Mühendislik Fakültesi";
    public const string Bilgisayar = "Bilgisayar Mühendisliği";
    public const string ElektrikElektronik = "Elektrik-Elektronik Mühendisliği";
    public const string Makine = "Makine Mühendisliği";

    private static GundemMaddesi M(string konu, MaddeDurumu durum, int kabul = 0, int ret = 0, int cekimser = 0) =>
        new(string.Empty, 0, konu, durum, kabul, ret, cekimser);

    public static readonly IReadOnlyList<Toplanti> Kayitlar =
    [
        // Bilgisayar Mühendisliği
        T("T24-01", "2024/03", Bilgisayar, "2024-03-14", Yapildi, 11, 9, true,
            M("Bahar dönemi ders görevlendirmeleri", KabulEdildi, 8, 0, 1), M("Staj komisyonu üyelikleri", KabulEdildi, 9)),
        T("T24-02", "2024/09", Bilgisayar, "2024-09-12", Yapildi, 11, 9, true,
            M("Seçmeli ders havuzunun güncellenmesi", KabulEdildi, 7, 1, 1), M("Laboratuvar kullanım yönergesi taslağı", Reddedildi, 3, 5, 1)),
        T("T24-03", "2024/12", Bilgisayar, "2024-12-12", ToplantiDurumu.Ertelendi, 11, 5, false,
            M("Bütünleme sınavı takvimi", MaddeDurumu.Ertelendi)),
        T("T25-01", "2025/02", Bilgisayar, "2025-02-13", Yapildi, 11, 10, true,
            M("Bütünleme sınavı takvimi", KabulEdildi, 8, 0, 2), M("Değişim programı kontenjanları", KabulEdildi, 7, 2, 1)),
        T("T25-02", "2025/06", Bilgisayar, "2025-06-12", Yapildi, 11, 8, true,
            M("Bitirme projesi değerlendirme ölçütleri", KabulEdildi, 6, 1, 1), M("Yaz okulunda açılacak dersler", MaddeDurumu.Ertelendi, 3, 3, 2)),
        T("T25-03", "2025/10", Bilgisayar, "2025-10-09", Yapildi, 11, 9, true,
            M("Yaz okulunda açılacak dersler", KabulEdildi, 7, 1, 1), M("Bölüm tanıtım komisyonu kurulması", Reddedildi, 2, 6, 1)),
        T("T26-01", "2026/03", Bilgisayar, "2026-03-12", Yapildi, 11, 10, true,
            M("Güz dönemi ders görevlendirmeleri", KabulEdildi, 9, 0, 1), M("Lisans projesi konu kontenjanları", KabulEdildi, 8, 1, 1)),
        T("T26-02", "2026/06", Bilgisayar, "2026-06-11", Yapildi, 11, 8, false,
            M("Ders içeriklerinin güncellenmesi", KabulEdildi, 6, 1, 1), M("Seminer saatlerinin değiştirilmesi", Reddedildi, 3, 4, 1)),
        T("T26-03", "2026/11", Bilgisayar, "2026-11-05", Planlandi, 11, 7, false,
            M("Bahar dönemi ders programı", OylamaAcik, 4, 2, 1), M("Bahar dönemi ders görevlendirmeleri", OylamaAcik, 3, 3, 1),
            M("Bölüm seminer dizisi önerisi", Gundemde)),
        T("T26-04", "2026/12", Bilgisayar, "2026-12-10", Planlandi, 11, 0, false,
            M("Bütünleme sınavı takvimi", Gundemde), M("Laboratuvar kullanım yönergesi", Gundemde)),

        // Elektrik-Elektronik Mühendisliği (aynı fakülte, başka bölüm)
        T("TE25-01", "2025/05", ElektrikElektronik, "2025-05-15", Yapildi, 9, 7, true,
            M("Laboratuvar cihazı alım önceliği", KabulEdildi, 6, 0, 1)),
        T("TE26-01", "2026/04", ElektrikElektronik, "2026-04-16", Yapildi, 9, 8, true,
            M("Seçmeli ders önerisi", Reddedildi, 2, 5, 1), M("Staj komisyonu üyelikleri", KabulEdildi, 7, 0, 1)),
        T("TE26-02", "2026/11", ElektrikElektronik, "2026-11-12", Planlandi, 9, 3, false,
            M("Bahar dönemi ders programı", OylamaAcik, 2, 1)),

        // Mühendislik Fakültesi Makine Mühendisliği (başka fakülte)
        T("TM26-01", "2026/05", Makine, "2026-05-21", Yapildi, 13, 10, true,
            M("Atölye güvenlik yönergesi", KabulEdildi, 9, 0, 1)),
        T("TM26-02", "2026/10", Makine, "2026-10-08", Yapildi, 13, 11, false,
            M("Ders görevlendirmeleri", KabulEdildi, 10, 0, 1)),
    ];

    private static Toplanti T(string id, string no, string bolum, string tarih, ToplantiDurumu durum, int uye, int katilan,
        bool tutanak, params GundemMaddesi[] maddeler)
    {
        var fakulte = bolum == Makine ? Muhendislik : Teknoloji;
        var yer = bolum == Makine ? "Örnek Derslik B-12" : "Örnek Toplantı Salonu A-101";
        return new Toplanti(id, no, fakulte, bolum, DateOnly.ParseExact(tarih, "yyyy-MM-dd", CultureInfo.InvariantCulture), yer,
            durum, uye, katilan,
            [.. maddeler.Select((m, i) => m with
            {
                Id = $"{id}-{i + 1}",
                Sira = i + 1,
                KararNo = m.Durum is KabulEdildi or Reddedildi ? $"{no}-{i + 1}" : null,
            })],
            tutanak);
    }
}
