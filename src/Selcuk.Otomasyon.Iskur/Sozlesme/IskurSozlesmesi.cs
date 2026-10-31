namespace Selcuk.Otomasyon.Iskur.Sozlesme;

// İŞKUR API'sinin sözleşmesi (docs/api-sozlesmeleri/iskur.md). Mock ve Api veri kaynakları aynı türleri kullanır.
// JSON: camelCase alanlar, enum'lar camelCase metin (ör. "giriliyor"), ay "yyyy-MM".

/// <summary>Aylık devam kaydının durumu: girişler sürüyor, sorumlu onayladı, ödeme yapıldı.</summary>
public enum DevamDurumu
{
    Giriliyor,
    Onaylandi,
    Odendi,
}

/// <summary>Bir öğrencinin bir aydaki devam özeti (planlanan ve gerçekleşen saat).</summary>
public sealed record AylikDevam(
    string Id,
    string OgrenciNo,
    string OgrenciAdi,
    string Fakulte,
    string Bolum,
    string Ay,
    int PlanlananSaat,
    int DevamSaati,
    DevamDurumu Durum);

/// <summary>POST /api/iskur/devam gövdesi (sorumlu token'dan belirlenir).</summary>
public sealed record DevamGirisi(string OgrenciNo, DateOnly Tarih, int Saat);

/// <summary>
/// İŞKUR Otomasyonu'nun veri kaynağı (yalnızca okuma). Öğrenci yöntemi kullanıcının kendi kayıtlarını, sorumlu yöntemi
/// kullanıcının bölümünün kayıtlarını döndürür.
/// </summary>
public interface IIskurVeriKaynagi
{
    /// <summary>GET /api/iskur/devamim (öğrenci)</summary>
    Task<IReadOnlyList<AylikDevam>> DevamKayitlarimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/iskur/devam (iskur_sorumlusu; kullanıcının bölümü)</summary>
    Task<IReadOnlyList<AylikDevam>> DevamKayitlariAsync(CancellationToken cancellationToken = default);
}
