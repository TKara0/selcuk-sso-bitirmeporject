namespace Selcuk.Otomasyon.Intibak.Sozlesme;

// İntibak API'sinin sözleşmesi (docs/api-sozlesmeleri/intibak.md). Mock ve Api veri kaynakları aynı türleri kullanır.
// JSON: camelCase alanlar, enum'lar camelCase metin (ör. "yatayGecis"), tarihler "yyyy-MM-dd".

public enum GelisTuru
{
    YatayGecis,
    DikeyGecis,
    EkYerlestirme,
    Af,
}

/// <summary>Başvurunun durumu. Aşamalar: Başvuru → Komisyon incelemesi → Bölüm başkanı onayı → Yönetim kurulu → Sonuçlandı.</summary>
public enum IntibakDurumu
{
    KomisyonIncelemesinde,
    GeriGonderildi,
    BaskanOnayinda,
    YonetimKurulunda,
    Sonuclandi,
    Reddedildi,
}

/// <summary>
/// Önceki kurumda alınan bir ders ve komisyonun eşleştirmesi. Komisyon karar vermeden önce eşdeğer ders ve muafiyet
/// null'dır; karşılığı olmayan ders muaf sayılmaz (EsdegerKod null, Muaf false).
/// </summary>
public sealed record DersEslesmesi(
    string OncekiDersAdi,
    int OncekiAkts,
    string OncekiNot,
    string? EsdegerKod = null,
    string? EsdegerAd = null,
    int? EsdegerAkts = null,
    string? HarfNotu = null,
    bool? Muaf = null);

/// <summary>Bir öğrencinin intibak başvurusu (geliş türü, önceki kurum, dersler ve karar).</summary>
public sealed record IntibakBasvurusu(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    GelisTuru GelisTuru,
    string OncekiUniversite,
    string OncekiBolum,
    DateOnly BasvuruTarihi,
    IntibakDurumu Durum,
    IReadOnlyList<DersEslesmesi> Dersler,
    DateOnly? KararTarihi = null,
    string? KomisyonNotu = null);

/// <summary>Bölüm müfredatındaki bir ders (eşleştirme seçenekleri).</summary>
public sealed record MufredatDersi(string Kod, string Ad, int Akts, int Yariyil);

/// <summary>POST /api/intibak/basvurular gövdesi (öğrencinin yeni başvurusu; öğrenci token'dan belirlenir).</summary>
public sealed record IntibakBasvuruFormu(GelisTuru GelisTuru, string OncekiUniversite, string OncekiBolum);

/// <summary>
/// İntibak Otomasyonu'nun veri kaynağı (yalnızca okuma). Öğrenci yöntemi kullanıcının kendi başvurularını, bölüm
/// yöntemleri kullanıcının bölümünün, yönetim kurulu evrakı kullanıcının fakültesinin kayıtlarını döndürür.
/// </summary>
public interface IIntibakVeriKaynagi
{
    /// <summary>GET /api/intibak/basvurularim</summary>
    Task<IReadOnlyList<IntibakBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/intibak/basvurular?durum=… (bölüm)</summary>
    Task<IReadOnlyList<IntibakBasvurusu>> BasvurularAsync(IntibakDurumu? durum, CancellationToken cancellationToken = default);

    /// <summary>GET /api/intibak/mufredat (kullanıcının bölümü)</summary>
    Task<IReadOnlyList<MufredatDersi>> MufredatAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/intibak/yonetim-kurulu-evraki (fakülte; yönetim kuruluna gidecek başvurular)</summary>
    Task<IReadOnlyList<IntibakBasvurusu>> YonetimKuruluEvrakiAsync(CancellationToken cancellationToken = default);
}
