using Selcuk.Otomasyon.EtikKurul.Sozlesme;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.EtikKurul.Sozlesme.EtikDurumu;

namespace Selcuk.Otomasyon.EtikKurul.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekEtikKurulVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): akademisyen yalnızca kendi başvurularını, kurul üyesi yalnızca
/// kendisine atanan dosyaları (bölümden bağımsız; atama kayıt verisidir), bölüm başkanı kendi bölümünü görür.
/// </summary>
public sealed class MockEtikKurulVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IEtikKurulVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<EtikBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekEtikKurulVerisi.Kayitlar.Where(b => k.Kapsar(KayitKapsami.Kendi, b.BasvuranKullaniciAdi, null, null))
            .OrderByDescending(b => b.BasvuruTarihi)]);
    }

    public Task<IReadOnlyList<EtikBasvurusu>> AtananDosyalarimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekEtikKurulVerisi.Kayitlar
            .Where(b => b.Gorusler.Any(g => k.Kapsar(KayitKapsami.Kendi, g.UyeKullaniciAdi, null, null)))
            .OrderByDescending(b => b.BasvuruTarihi)]);
    }

    public Task<IReadOnlyList<EtikBasvurusu>> KararlarAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekEtikKurulVerisi.Kayitlar
            .Where(b => b.Durum is Onaylandi or Reddedildi or DuzeltmeIstendi && k.Kapsar(KayitKapsami.Bolum, null, b.Bolum, b.Fakulte))
            .OrderByDescending(b => b.KararTarihi)]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Etik Kurul API'si (docs/api-sozlesmeleri/etik-kurul.md) üzerinden çalışan veri kaynağı. Kapsamı API, access
/// token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiEtikKurulVeriKaynagi(OtomasyonApiIstemcisi api) : IEtikKurulVeriKaynagi
{
    public Task<IReadOnlyList<EtikBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<EtikBasvurusu>>("api/etik-kurul/basvurularim", cancellationToken);

    public Task<IReadOnlyList<EtikBasvurusu>> AtananDosyalarimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<EtikBasvurusu>>("api/etik-kurul/atanan-dosyalarim", cancellationToken);

    public Task<IReadOnlyList<EtikBasvurusu>> KararlarAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<EtikBasvurusu>>("api/etik-kurul/kararlar", cancellationToken);
}
