namespace Selcuk.Otomasyon.LisansProjeleri.Sozlesme;

// Lisans Projeleri API'sinin sözleşmesi (docs/api-sozlesmeleri/lisans-projeleri.md). Mock ve Api veri kaynakları aynı
// türleri kullanır. JSON: camelCase alanlar, enum'lar camelCase metin (ör. "araRapor"), tarihler "yyyy-MM-dd".

/// <summary>Aşamalar: Konu seçimi → Önerge → Ara rapor → Teslim → Sunum ve değerlendirme (sonra Tamamlandı).</summary>
public enum ProjeAsamasi
{
    KonuSecimi,
    Onerge,
    AraRapor,
    Teslim,
    SunumDegerlendirme,
    Tamamlandi,
}

/// <summary>Bir akademisyenin önerdiği proje konusu. Konunun hocası kişiye bağlı kayıt verisidir, rol değildir.</summary>
public sealed record ProjeKonusu(
    string Id,
    string Baslik,
    string Aciklama,
    string HocaKullaniciAdi,
    string HocaAdi,
    string Fakulte,
    string Bolum,
    string Donem,
    int Kontenjan,
    bool Acik);

/// <summary>
/// Bir öğrencinin lisans projesi kaydı. Konu seçilmeden (KonuSecimi) konu ve hoca null'dır, öğrencinin tercihleri
/// (konu kimlikleri, sırayla) vardır. Not yalnızca tamamlanan projede bulunur.
/// </summary>
public sealed record LisansProjesi(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    string Donem,
    ProjeAsamasi Asama,
    string? KonuId = null,
    string? KonuBasligi = null,
    string? HocaKullaniciAdi = null,
    string? HocaAdi = null,
    IReadOnlyList<string>? Tercihler = null,
    int? Not = null);

/// <summary>POST /api/lisans-projeleri/tercihlerim gövdesi (öğrenci token'dan belirlenir).</summary>
public sealed record TercihFormu(IReadOnlyList<string> KonuIdleri);

/// <summary>POST /api/lisans-projeleri/konular gövdesi (hoca token'dan belirlenir).</summary>
public sealed record KonuOnerisiFormu(string Baslik, string Aciklama, int Kontenjan);

/// <summary>
/// Lisans Projeleri Otomasyonu'nun veri kaynağı (yalnızca okuma). Öğrenci yöntemi kullanıcının kendi projelerini,
/// akademisyen yöntemleri kullanıcının önerdiği konuları ve danışmanı olduğu projeleri, diğerleri kullanıcının
/// bölümünün kayıtlarını döndürür.
/// </summary>
public interface ILisansProjeleriVeriKaynagi
{
    /// <summary>GET /api/lisans-projeleri/konular (bölüm; içinde bulunulan dönemin konuları)</summary>
    Task<IReadOnlyList<ProjeKonusu>> KonularAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/lisans-projeleri/projelerim (öğrenci)</summary>
    Task<IReadOnlyList<LisansProjesi>> ProjelerimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/lisans-projeleri/konularim (akademisyenin önerdiği konular)</summary>
    Task<IReadOnlyList<ProjeKonusu>> KonularimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/lisans-projeleri/danismanligimdaki-projeler (akademisyenin hocası olduğu projeler)</summary>
    Task<IReadOnlyList<LisansProjesi>> DanismanligimdakiProjelerAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/lisans-projeleri/projeler (bölüm; içinde bulunulan dönem)</summary>
    Task<IReadOnlyList<LisansProjesi>> ProjelerAsync(CancellationToken cancellationToken = default);
}
