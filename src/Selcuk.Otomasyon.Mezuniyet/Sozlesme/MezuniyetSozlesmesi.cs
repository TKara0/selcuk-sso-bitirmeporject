namespace Selcuk.Otomasyon.Mezuniyet.Sozlesme;

// Mezuniyet API'sinin sözleşmesi (docs/api-sozlesmeleri/mezuniyet.md). Mock ve Api veri kaynakları aynı türleri
// kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "komisyonKontrolunde"), tarihler "yyyy-MM-dd".

/// <summary>
/// Mezuniyet sürecinin durumu. Aşamalar: Başvuru → Komisyon kontrolü → Bölüm onayı → Dekanlık → Diploma hazırlığı.
/// </summary>
public enum MezuniyetDurumu
{
    BasvuruYapilmadi,
    KomisyonKontrolunde,
    EksikBildirildi,
    BolumOnayinda,
    Dekanlikta,
    DiplomaHazirlaniyor,
    DiplomaHazir,
}

/// <summary>Zorunlu stajların durumu. Şimdilik örnek veridir; ileride Staj API'sinden (staj-api) alınacak.</summary>
public enum StajSonucu
{
    Tamamlandi,
    DevamEdiyor,
    Eksik,
}

public enum BirimDurumu
{
    Tamam,
    Bekliyor,
    Borclu,
}

/// <summary>Programın zorunlu bir dersi ve öğrencinin tamamlayıp tamamlamadığı.</summary>
public sealed record ZorunluDers(string Kod, string Ad, bool Tamamlandi);

/// <summary>Bir mezuniyet adayı: AKTS, GNO, zorunlu dersler, staj ve sürecin durumu. Şartlar ayardan karşılaştırılır.</summary>
public sealed record MezuniyetAdayi(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    string Donem,
    int ToplamAkts,
    decimal Gno,
    IReadOnlyList<ZorunluDers> ZorunluDersler,
    StajSonucu Staj,
    MezuniyetDurumu Durum,
    DateOnly? BasvuruTarihi = null,
    string? KomisyonNotu = null);

/// <summary>İlişik kesmede bir birimin (kütüphane, bölüm, laboratuvar…) durumu.</summary>
public sealed record IlisikKesmeBirimi(string Birim, BirimDurumu Durum, string? Aciklama = null);

/// <summary>POST /api/mezuniyet/basvurular gövdesi (öğrencinin başvurusu; öğrenci token'dan belirlenir).</summary>
public sealed record MezuniyetBasvuruFormu(string Donem, string IletisimAdresi, string DiplomaTeslimi);

/// <summary>
/// Mezuniyet Otomasyonu'nun veri kaynağı (yalnızca okuma). Öğrenci yöntemleri kullanıcının kendi kaydını, adaylar
/// kullanıcının bölümünü, gelen listeler kullanıcının fakültesini döndürür.
/// </summary>
public interface IMezuniyetVeriKaynagi
{
    /// <summary>GET /api/mezuniyet/durumum (öğrencinin mezuniyet kaydı; yoksa boş liste)</summary>
    Task<IReadOnlyList<MezuniyetAdayi>> DurumumAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/mezuniyet/ilisik-kesme (öğrencinin birim durumları)</summary>
    Task<IReadOnlyList<IlisikKesmeBirimi>> IlisikKesmeAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/mezuniyet/adaylar?durum=… (bölüm)</summary>
    Task<IReadOnlyList<MezuniyetAdayi>> AdaylarAsync(MezuniyetDurumu? durum, CancellationToken cancellationToken = default);

    /// <summary>GET /api/mezuniyet/fakulte-listeleri (fakülte; dekanlığa gelen ve diploması hazırlanan adaylar)</summary>
    Task<IReadOnlyList<MezuniyetAdayi>> FakulteListeleriAsync(CancellationToken cancellationToken = default);
}
