using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.BolumKurul.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekBolumKurulVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): bölüm rolleri kendi bölümünü, fakülte sekreteri kendi
/// fakültesini görür. Veri değişmez.
/// </summary>
public sealed class MockBolumKurulVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IBolumKurulVeriKaynagi
{
    private IEnumerable<Toplanti> Kapsamda(KayitKapsami kapsam)
    {
        var k = KullaniciKapsami.Oku(
            httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));
        return OrnekBolumKurulVerisi.Kayitlar.Where(t => k.Kapsar(kapsam, null, t.Bolum, t.Fakulte));
    }

    public Task<IReadOnlyList<Toplanti>> ToplantilarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Toplanti>>([.. Kapsamda(KayitKapsami.Bolum).OrderByDescending(t => t.Tarih)]);

    public Task<IReadOnlyList<Toplanti>> FakulteKararlariAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Toplanti>>([.. Kapsamda(KayitKapsami.Fakulte).Where(t => t.Durum == ToplantiDurumu.Yapildi)
            .OrderBy(t => t.Bolum, StringComparer.Create(BolumKurulOtomasyonu.Tr, false)).ThenByDescending(t => t.Tarih)]);
}

/// <summary>
/// Bölüm Kurul Karar API'si (docs/api-sozlesmeleri/bolum-kurul.md) üzerinden çalışan veri kaynağı. Kapsamı API, access
/// token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiBolumKurulVeriKaynagi(OtomasyonApiIstemcisi api) : IBolumKurulVeriKaynagi
{
    public Task<IReadOnlyList<Toplanti>> ToplantilarAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<Toplanti>>("api/bolum-kurul/toplantilar", cancellationToken);

    public Task<IReadOnlyList<Toplanti>> FakulteKararlariAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<Toplanti>>("api/bolum-kurul/fakulte-kararlari", cancellationToken);
}
