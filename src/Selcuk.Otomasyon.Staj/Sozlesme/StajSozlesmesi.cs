namespace Selcuk.Otomasyon.Staj.Sozlesme;

// Staj API'sinin sözleşmesi (docs/api-sozlesmeleri/staj.md). Mock ve Api veri kaynakları aynı türleri kullanır. JSON:
// camelCase alanlar, enum'lar camelCase metin (ör. "basvuruBekliyor"), tarihler "yyyy-MM-dd".

public enum StajTuru
{
    StajI,
    StajII,
    Ime,
}

public enum StajDurumu
{
    BasvuruBekliyor,
    Onaylandi,
    DevamEdiyor,
    DefterTeslimEdildi,
    Degerlendirildi,
    Reddedildi,
}

/// <summary>Bir öğrencinin bir staj kaydı (başvurudan değerlendirmeye).</summary>
public sealed record StajKaydi(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    StajTuru Tur,
    string FirmaAdi,
    string FirmaAdresi,
    DateOnly Baslangic,
    DateOnly Bitis,
    int IsGunu,
    StajDurumu Durum,
    DateOnly BasvuruTarihi,
    int? KabulEdilenGun = null,
    string? KomisyonNotu = null);

/// <summary>Staj defterinin bir günlük girişi.</summary>
public sealed record DefterGirisi(string StajId, DateOnly Tarih, string Calisma);

/// <summary>Öğrencinin güncel stajının defteri; defteri olan staj yoksa Staj null, Girisler boş.</summary>
public sealed record StajDefteri(StajKaydi? Staj, IReadOnlyList<DefterGirisi> Girisler);

/// <summary>Bir stajın evrak durumu (bölüm sekreterliği).</summary>
public sealed record StajEvragi(
    string StajId, string OgrenciNo, string OgrenciAdi, StajTuru Tur, StajDurumu Durum,
    bool KabulBelgesi, bool SgkGirisi, bool DefterTeslim);

/// <summary>Bir yıl ve staj türü için sayılar (yıl: stajın başlangıç yılı).</summary>
public sealed record YillikStajSayisi(int Yil, StajTuru Tur, int Basvuru, int Onay, int Tamamlanan);

/// <summary>
/// Bölüm özeti: aktif staj (onaylanmış veya devam eden), bekleyen başvuru, verilen yılda tamamlanan (değerlendirilmiş,
/// bitişi o yıl) staj sayısı ve bütün yılların tür bazında sayıları.
/// </summary>
public sealed record StajOzeti(int Yil, int AktifStaj, int BekleyenBasvuru, int Tamamlanan, IReadOnlyList<YillikStajSayisi> Yillar);

/// <summary>POST /api/staj/basvurular gövdesi (öğrencinin yeni başvurusu; öğrenci token'dan belirlenir).</summary>
public sealed record StajBasvurusu(
    StajTuru Tur, string FirmaAdi, string FirmaAdresi, DateOnly Baslangic, DateOnly Bitis, int IsGunu);

/// <summary>
/// Staj Otomasyonu'nun veri kaynağı. Mock (örnek veri, kapsamı oturumdaki kullanıcıdan süzer) ve Api (kaynak
/// sunucusu; kapsamı API access token'daki claim'lere göre süzer) uygulamaları vardır. Öğrenci yöntemleri kullanıcının
/// kendi kayıtlarını, diğerleri kullanıcının bölümünün kayıtlarını döndürür.
/// </summary>
public interface IStajVeriKaynagi
{
    /// <summary>GET /api/staj/basvurularim</summary>
    Task<IReadOnlyList<StajKaydi>> BasvurularimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/staj/defterim</summary>
    Task<StajDefteri> DefterimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/staj/basvurular?durum=…</summary>
    Task<IReadOnlyList<StajKaydi>> BasvurularAsync(StajDurumu? durum, CancellationToken cancellationToken = default);

    /// <summary>GET /api/staj/defterler (teslim edilmiş, değerlendirme bekleyen defterler)</summary>
    Task<IReadOnlyList<StajKaydi>> DefterlerAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/staj/ozet?yil=…</summary>
    Task<StajOzeti> OzetAsync(int yil, CancellationToken cancellationToken = default);

    /// <summary>GET /api/staj/evraklar</summary>
    Task<IReadOnlyList<StajEvragi>> EvraklarAsync(CancellationToken cancellationToken = default);
}
