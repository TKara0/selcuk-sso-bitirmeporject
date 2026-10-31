namespace Selcuk.Otomasyon.Danismanlik.Sozlesme;

// Öğrenci Danışmanlık API'sinin sözleşmesi (docs/api-sozlesmeleri/danismanlik.md). Mock ve Api veri kaynakları aynı
// türleri kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "dersSecimi"), tarihler "yyyy-MM-dd".

public enum GorusmeKonusu
{
    DersSecimi,
    AkademikIlerleme,
    StajPlani,
    KariyerPlani,
}

/// <summary>Görüşme talebinin durumu. Aşamalar: Talep → Danışman onayı → Görüşme yapıldı.</summary>
public enum TalepDurumu
{
    Bekliyor,
    Onaylandi,
    Yapildi,
    Reddedildi,
}

/// <summary>
/// Bir öğrencinin danışmanlık kaydı. Danışman kişiye bağlı kayıt verisidir (rol değil); atanmamışsa null.
/// </summary>
public sealed record DanismanlikOgrencisi(
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    int Sinif,
    int ToplamAkts,
    decimal Gno,
    string? DanismanKullaniciAdi = null,
    string? DanismanAdi = null,
    DateOnly? SonGorusme = null);

/// <summary>Danışmanın bir görüşme kaydı. Notlar genel ve akademiktir; kişisel konu içermez.</summary>
public sealed record GorusmeNotu(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string DanismanKullaniciAdi,
    string Fakulte,
    string Bolum,
    DateOnly Tarih,
    GorusmeKonusu Konu,
    string Not);

/// <summary>Öğrencinin danışmanından görüşme talebi.</summary>
public sealed record GorusmeTalebi(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string DanismanAdi,
    DateOnly TalepTarihi,
    GorusmeKonusu Konu,
    TalepDurumu Durum,
    DateOnly? GorusmeTarihi = null,
    string? Aciklama = null);

/// <summary>POST /api/danismanlik/talepler gövdesi (öğrenci token'dan belirlenir).</summary>
public sealed record GorusmeTalebiFormu(GorusmeKonusu Konu, string Aciklama);

/// <summary>
/// Öğrenci Danışmanlık Otomasyonu'nun veri kaynağı (yalnızca okuma). Danışman yöntemleri yalnızca danışmanı olduğu
/// öğrencileri ve kendi notlarını, öğrenci yöntemleri yalnızca kendi kaydını, bölüm yöntemi kullanıcının bölümünü döndürür.
/// </summary>
public interface IDanismanlikVeriKaynagi
{
    /// <summary>GET /api/danismanlik/ogrencilerim (danışman)</summary>
    Task<IReadOnlyList<DanismanlikOgrencisi>> OgrencilerimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/danismanlik/gorusme-notlarim (danışman)</summary>
    Task<IReadOnlyList<GorusmeNotu>> GorusmeNotlarimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/danismanlik/kaydim (öğrenci; yoksa boş liste)</summary>
    Task<IReadOnlyList<DanismanlikOgrencisi>> KaydimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/danismanlik/taleplerim (öğrenci)</summary>
    Task<IReadOnlyList<GorusmeTalebi>> TaleplerimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/danismanlik/bolum-ogrencileri (bölüm başkanı)</summary>
    Task<IReadOnlyList<DanismanlikOgrencisi>> BolumOgrencileriAsync(CancellationToken cancellationToken = default);
}
