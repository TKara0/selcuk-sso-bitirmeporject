namespace Selcuk.Otomasyon.EtikKurul.Sozlesme;

// Etik Kurul API'sinin sözleşmesi (docs/api-sozlesmeleri/etik-kurul.md). Mock ve Api veri kaynakları aynı türleri
// kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "kurulDegerlendirmesinde"), tarihler "yyyy-MM-dd".

public enum ArastirmaTuru
{
    Anket,
    Gorusme,
    Deney,
    IkincilVeri,
}

/// <summary>Başvurunun durumu. Aşamalar: Başvuru → Ön inceleme → Kurul değerlendirmesi → Karar.</summary>
public enum EtikDurumu
{
    OnIncelemede,
    KurulDegerlendirmesinde,
    DuzeltmeIstendi,
    Onaylandi,
    Reddedildi,
}

public enum GorusTuru
{
    Uygun,
    DuzeltmeGerekli,
    UygunDegil,
}

/// <summary>
/// Dosyaya atanan bir kurul üyesi ve görüşü (null: henüz bildirmedi). Atama kişiye bağlı kayıt verisidir, rol değildir:
/// üye yalnızca kendisine atanan dosyaları görür.
/// </summary>
public sealed record UyeGorusu(string UyeKullaniciAdi, string UyeAdi, GorusTuru? Gorus = null, string? Gerekce = null);

/// <summary>Bir akademisyenin etik kurul başvurusu.</summary>
public sealed record EtikBasvurusu(
    string Id,
    string BasvuranKullaniciAdi,
    string BasvuranAdi,
    string Fakulte,
    string Bolum,
    string ProjeBasligi,
    ArastirmaTuru ArastirmaTuru,
    string KatilimciGrubu,
    string VeriToplamaYontemi,
    DateOnly BasvuruTarihi,
    EtikDurumu Durum,
    IReadOnlyList<UyeGorusu> Gorusler,
    DateOnly? KararTarihi = null,
    string? KararNotu = null);

/// <summary>POST /api/etik-kurul/basvurular gövdesi (başvuran token'dan belirlenir; belge multipart/form-data).</summary>
public sealed record EtikBasvuruFormu(string ProjeBasligi, ArastirmaTuru ArastirmaTuru, string KatilimciGrubu, string VeriToplamaYontemi);

/// <summary>
/// Etik Kurul Otomasyonu'nun veri kaynağı (yalnızca okuma). Akademisyen yöntemi kullanıcının kendi başvurularını, üye
/// yöntemi kullanıcıya atanan dosyaları, karar listesi kullanıcının bölümünün kararlarını döndürür.
/// </summary>
public interface IEtikKurulVeriKaynagi
{
    /// <summary>GET /api/etik-kurul/basvurularim</summary>
    Task<IReadOnlyList<EtikBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/etik-kurul/atanan-dosyalarim (kullanıcının görüşü istenen dosyalar, bölümden bağımsız)</summary>
    Task<IReadOnlyList<EtikBasvurusu>> AtananDosyalarimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/etik-kurul/kararlar (bölüm; kurulun karar verdiği başvurular)</summary>
    Task<IReadOnlyList<EtikBasvurusu>> KararlarAsync(CancellationToken cancellationToken = default);
}
