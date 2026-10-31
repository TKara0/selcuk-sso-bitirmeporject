namespace Selcuk.Otomasyon.CapYandal.Sozlesme;

// ÇAP-Yandal API'sinin sözleşmesi (docs/api-sozlesmeleri/cap-yandal.md). Mock ve Api veri kaynakları aynı türleri
// kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "koordinatorDegerlendirmesinde"), tarihler "yyyy-MM-dd".

public enum ProgramTuru
{
    /// <summary>Çift anadal programı.</summary>
    Cap,

    Yandal,
}

/// <summary>Başvurunun durumu. Aşamalar: Başvuru → Koordinatör değerlendirmesi → Bölüm başkanı onayı → Kesin kayıt.</summary>
public enum CapDurumu
{
    KoordinatorDegerlendirmesinde,
    BaskanOnayinda,
    KesinKayitBekliyor,
    KesinKayitYapildi,
    Reddedildi,
}

/// <summary>Başvuruya açık bir ÇAP veya yandal programı; Fakulte ve Bolum programı açan birimdir.</summary>
public sealed record CapProgrami(string Kod, string Ad, ProgramTuru Tur, string Fakulte, string Bolum);

/// <summary>Kesin kayıt evrakları (bölüm sekreterliği).</summary>
public sealed record KesinKayitEvraki(bool Dilekce, bool Transkript, bool AnadalOnayi);

/// <summary>
/// Bir ÇAP veya yandal başvurusu. Fakulte ve Bolum programı açan birimdir (koordinatör, bölüm başkanı ve sekreter
/// kapsamı); OgrenciBolumu öğrencinin anadalıdır. Gno, BasariSirasi ve SinifMevcudu başvuru anındaki değerlerdir.
/// </summary>
public sealed record CapBasvurusu(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string OgrenciBolumu,
    string ProgramKodu,
    string ProgramAdi,
    ProgramTuru Tur,
    string Fakulte,
    string Bolum,
    string Donem,
    decimal Gno,
    int BasariSirasi,
    int SinifMevcudu,
    DateOnly BasvuruTarihi,
    CapDurumu Durum,
    KesinKayitEvraki? Evrak = null,
    string? Not = null);

/// <summary>Öğrencinin anadaldaki GNO'su ve başarı sırası (şart göstergesi).</summary>
public sealed record AkademikBilgi(string OgrenciNo, decimal Gno, int BasariSirasi, int SinifMevcudu);

/// <summary>İkinci programın bir dersi; Not ve Donem null ise ders henüz alınmadı.</summary>
public sealed record ProgramDersi(string Kod, string Ad, int Akts, string? Not = null, string? Donem = null);

public sealed record DonemGnosu(string Donem, decimal Gno);

/// <summary>Kesin kaydı yapılmış bir öğrencinin ikinci programdaki ilerlemesi.</summary>
public sealed record ProgramIlerlemesi(
    CapBasvurusu Basvuru,
    IReadOnlyList<ProgramDersi> AlinanDersler,
    IReadOnlyList<ProgramDersi> KalanDersler,
    IReadOnlyList<DonemGnosu> DonemlikGno)
{
    public decimal? GuncelGno => DonemlikGno.Count > 0 ? DonemlikGno[^1].Gno : null;
}

/// <summary>POST /api/cap-yandal/basvurular gövdesi (öğrencinin başvurusu; öğrenci token'dan belirlenir).</summary>
public sealed record CapBasvuruFormu(ProgramTuru Tur, string ProgramKodu);

/// <summary>
/// ÇAP-Yandal Otomasyonu'nun veri kaynağı (yalnızca okuma). Öğrenci yöntemleri kullanıcının kendi kayıtlarını, diğerleri
/// kullanıcının bölümünün açtığı programların kayıtlarını döndürür.
/// </summary>
public interface ICapYandalVeriKaynagi
{
    /// <summary>GET /api/cap-yandal/programlar (başvuruya açık programlar)</summary>
    Task<IReadOnlyList<CapProgrami>> ProgramlarAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/cap-yandal/akademik-bilgim (öğrencinin GNO'su ve başarı sırası; yoksa boş liste)</summary>
    Task<IReadOnlyList<AkademikBilgi>> AkademikBilgimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/cap-yandal/basvurularim</summary>
    Task<IReadOnlyList<CapBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/cap-yandal/ilerlemem (kesin kaydı yapılmış programlar)</summary>
    Task<IReadOnlyList<ProgramIlerlemesi>> IlerlememAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/cap-yandal/basvurular?durum=… (bölümün programları)</summary>
    Task<IReadOnlyList<CapBasvurusu>> BasvurularAsync(CapDurumu? durum, CancellationToken cancellationToken = default);

    /// <summary>GET /api/cap-yandal/programdaki-ogrenciler (bölümün programlarında kesin kaydı yapılmış öğrenciler)</summary>
    Task<IReadOnlyList<ProgramIlerlemesi>> ProgramdakiOgrencilerAsync(CancellationToken cancellationToken = default);
}
