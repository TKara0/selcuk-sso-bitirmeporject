namespace Selcuk.Otomasyon.BolumKurul.Sozlesme;

// Bölüm Kurul Karar API'sinin sözleşmesi (docs/api-sozlesmeleri/bolum-kurul.md). Mock ve Api veri kaynakları aynı
// türleri kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "oylamaAcik"), tarihler "yyyy-MM-dd".

public enum ToplantiDurumu
{
    Planlandi,
    Yapildi,
    Ertelendi,
}

public enum MaddeDurumu
{
    /// <summary>Gündemde, oylama henüz açılmadı.</summary>
    Gundemde,
    OylamaAcik,
    KabulEdildi,
    Reddedildi,
    Ertelendi,
}

/// <summary>Bir gündem maddesi ve oy sayıları; karar verilen maddenin karar numarası vardır.</summary>
public sealed record GundemMaddesi(
    string Id,
    int Sira,
    string Konu,
    MaddeDurumu Durum,
    int Kabul = 0,
    int Ret = 0,
    int Cekimser = 0,
    string? KararNo = null);

/// <summary>
/// Bir bölüm kurulu toplantısı. UyeSayisi kurulun üye tamsayısı, KatilanSayisi yoklamaya göre katılan üye sayısıdır
/// (yoklama alınmadıysa 0).
/// </summary>
public sealed record Toplanti(
    string Id,
    string No,
    string Fakulte,
    string Bolum,
    DateOnly Tarih,
    string Yer,
    ToplantiDurumu Durum,
    int UyeSayisi,
    int KatilanSayisi,
    IReadOnlyList<GundemMaddesi> Maddeler,
    bool TutanakHazir = false);

/// <summary>POST /api/bolum-kurul/toplantilar gövdesi (bölüm token'daki kullanıcıdan belirlenir).</summary>
public sealed record ToplantiFormu(DateOnly Tarih, string Yer);

/// <summary>POST /api/bolum-kurul/toplantilar/{id}/maddeler gövdesi.</summary>
public sealed record GundemMaddesiFormu(string Konu);

/// <summary>
/// Bölüm Kurul Karar Otomasyonu'nun veri kaynağı (yalnızca okuma). Toplantılar kullanıcının bölümünün, fakülte kararları
/// kullanıcının fakültesinin kayıtlarıdır.
/// </summary>
public interface IBolumKurulVeriKaynagi
{
    /// <summary>GET /api/bolum-kurul/toplantilar (bölüm)</summary>
    Task<IReadOnlyList<Toplanti>> ToplantilarAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/bolum-kurul/fakulte-kararlari (fakülte; yapılmış toplantılar ve kararları)</summary>
    Task<IReadOnlyList<Toplanti>> FakulteKararlariAsync(CancellationToken cancellationToken = default);
}
