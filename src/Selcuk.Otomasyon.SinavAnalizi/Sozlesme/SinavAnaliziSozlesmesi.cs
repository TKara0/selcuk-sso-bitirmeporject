namespace Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

// Sınav Başarısı Analizi API'sinin sözleşmesi (docs/api-sozlesmeleri/sinav-analizi.md). Mock ve Api veri kaynakları
// aynı türleri kullanır. JSON: camelCase alanlar. Yalnızca toplulaştırılmış veri: öğrenci düzeyinde kayıt yoktur.

/// <summary>Bir harf notunu alan öğrenci sayısı.</summary>
public sealed record HarfSayisi(string Harf, int Sayi);

/// <summary>
/// Bir dersin bir dönemdeki toplu sonucu: harf notu dağılımı ve bağlı program çıktıları. Dersin hocası kişiye bağlı
/// kayıt verisidir (rol değil). Öğrenci adı veya numarası içermez.
/// </summary>
public sealed record DersSonucu(
    string Id,
    string DersKodu,
    string DersAdi,
    string HocaKullaniciAdi,
    string HocaAdi,
    string Fakulte,
    string Bolum,
    string Donem,
    IReadOnlyList<HarfSayisi> NotDagilimi,
    IReadOnlyList<string> ProgramCiktilari)
{
    public int OgrenciSayisi => NotDagilimi.Sum(h => h.Sayi);
}

/// <summary>
/// Sınav Başarısı Analizi Otomasyonu'nun veri kaynağı (yalnızca okuma). Akademisyen yöntemi yalnızca hocası olduğu
/// derslerin, bölüm yöntemi kullanıcının bölümünün derslerinin sonuçlarını döndürür.
/// </summary>
public interface ISinavAnaliziVeriKaynagi
{
    /// <summary>GET /api/sinav-analizi/derslerim (akademisyen)</summary>
    Task<IReadOnlyList<DersSonucu>> DerslerimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/sinav-analizi/bolum-dersleri (bölüm başkanı, MÜDEK komisyonu)</summary>
    Task<IReadOnlyList<DersSonucu>> BolumDersleriAsync(CancellationToken cancellationToken = default);
}
