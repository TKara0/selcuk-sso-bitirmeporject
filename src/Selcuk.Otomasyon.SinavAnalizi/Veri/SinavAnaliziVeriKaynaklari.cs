using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;

namespace Selcuk.Otomasyon.SinavAnalizi.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekSinavAnaliziVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): akademisyen yalnızca hocası olduğu dersleri (kayıt verisi),
/// bölüm başkanı ve MÜDEK komisyonu kendi bölümünü görür.
/// </summary>
public sealed class MockSinavAnaliziVeriKaynagi(IHttpContextAccessor httpContextAccessor) : ISinavAnaliziVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<DersSonucu>> DerslerimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekSinavAnaliziVerisi.Kayitlar.Where(d => k.Kapsar(KayitKapsami.Kendi, d.HocaKullaniciAdi, null, null))]);
    }

    public Task<IReadOnlyList<DersSonucu>> BolumDersleriAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekSinavAnaliziVerisi.Kayitlar.Where(d => k.Kapsar(KayitKapsami.Bolum, null, d.Bolum, d.Fakulte))]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Sınav Başarısı Analizi API'si (docs/api-sozlesmeleri/sinav-analizi.md) üzerinden çalışan veri kaynağı. Kapsamı API,
/// access token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiSinavAnaliziVeriKaynagi(OtomasyonApiIstemcisi api) : ISinavAnaliziVeriKaynagi
{
    public Task<IReadOnlyList<DersSonucu>> DerslerimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DersSonucu>>("api/sinav-analizi/derslerim", cancellationToken);

    public Task<IReadOnlyList<DersSonucu>> BolumDersleriAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DersSonucu>>("api/sinav-analizi/bolum-dersleri", cancellationToken);
}
