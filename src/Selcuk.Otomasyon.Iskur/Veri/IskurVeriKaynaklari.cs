using Selcuk.Otomasyon.Iskur.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Iskur.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekIskurVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yalnızca kendi kayıtlarını, İŞKUR sorumlusu kendi
/// bölümünü görür (Development'taki iskur.test'in bölümü dolu olduğu için bölüm kapsamı).
/// </summary>
public sealed class MockIskurVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IIskurVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<AylikDevam>> DevamKayitlarimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekIskurVerisi.Kayitlar.Where(d => k.Kapsar(KayitKapsami.Kendi, d.OgrenciNo, null, null))
            .OrderByDescending(d => d.Ay, StringComparer.Ordinal)]);
    }

    public Task<IReadOnlyList<AylikDevam>> DevamKayitlariAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekIskurVerisi.Kayitlar.Where(d => k.Kapsar(KayitKapsami.Bolum, null, d.Bolum, d.Fakulte))
            .OrderByDescending(d => d.Ay, StringComparer.Ordinal).ThenBy(d => d.OgrenciNo, StringComparer.Ordinal)]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// İŞKUR API'si (docs/api-sozlesmeleri/iskur.md) üzerinden çalışan veri kaynağı. Kapsamı API, access token'daki
/// claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiIskurVeriKaynagi(OtomasyonApiIstemcisi api) : IIskurVeriKaynagi
{
    public Task<IReadOnlyList<AylikDevam>> DevamKayitlarimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<AylikDevam>>("api/iskur/devamim", cancellationToken);

    public Task<IReadOnlyList<AylikDevam>> DevamKayitlariAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<AylikDevam>>("api/iskur/devam", cancellationToken);
}
