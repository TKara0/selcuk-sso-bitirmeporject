namespace Selcuk.Otomasyon.Mudek.Sozlesme;

// MÜDEK API'sinin sözleşmesi (docs/api-sozlesmeleri/mudek.md). Mock ve Api veri kaynakları aynı türleri kullanır.
// JSON: camelCase alanlar, enum'lar camelCase metin (ör. "donemRaporu"), oranlar 0-1 arası ondalık.

public enum BelgeTuru
{
    DonemRaporu,
    OzDegerlendirme,
    DersDosyasi,
    Tutanak,
}

public enum BelgeDurumu
{
    Hazirlaniyor,
    Tamamlandi,
    Arsivlendi,
}

/// <summary>Bölümün bir program çıktısı (adlar örnektir; gerçek akreditasyon metni değildir).</summary>
public sealed record ProgramCiktisi(string Kod, string Ad, string Fakulte, string Bolum);

/// <summary>
/// Bir dersin bir ders çıktısı: bağlı olduğu program çıktıları ve ölçülen başarı oranı (null: veri girilmedi). Dersin
/// hocası kişiye bağlı kayıt verisidir (rol değil).
/// </summary>
public sealed record DersCiktisi(
    string Id,
    string DersKodu,
    string DersAdi,
    string HocaKullaniciAdi,
    string HocaAdi,
    string Fakulte,
    string Bolum,
    string Donem,
    string CiktiKodu,
    string Aciklama,
    IReadOnlyList<string> ProgramCiktilari,
    decimal? OlculenOran = null);

/// <summary>Bölümün MÜDEK raporu veya belgesi.</summary>
public sealed record MudekBelgesi(string Id, string Fakulte, string Bolum, string Ad, BelgeTuru Tur, string Donem, BelgeDurumu Durum);

/// <summary>POST /api/mudek/ders-ciktilari/{id}/olcum gövdesi (yalnızca dersin hocası).</summary>
public sealed record OlcumFormu(decimal OlculenOran);

/// <summary>
/// MÜDEK Otomasyonu'nun veri kaynağı (yalnızca okuma). Bölüm yöntemleri kullanıcının bölümünün kayıtlarını,
/// akademisyen yöntemi yalnızca hocası olduğu derslerin çıktılarını döndürür.
/// </summary>
public interface IMudekVeriKaynagi
{
    /// <summary>GET /api/mudek/program-ciktilari (bölüm)</summary>
    Task<IReadOnlyList<ProgramCiktisi>> ProgramCiktilariAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/mudek/ders-ciktilari (bölüm)</summary>
    Task<IReadOnlyList<DersCiktisi>> DersCiktilariAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/mudek/ders-ciktilarim (akademisyen; yalnızca kendi dersleri)</summary>
    Task<IReadOnlyList<DersCiktisi>> DersCiktilarimAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/mudek/belgeler (bölüm)</summary>
    Task<IReadOnlyList<MudekBelgesi>> BelgelerAsync(CancellationToken cancellationToken = default);
}
