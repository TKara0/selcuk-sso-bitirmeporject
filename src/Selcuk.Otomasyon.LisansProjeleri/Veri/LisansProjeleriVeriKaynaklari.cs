using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.LisansProjeleri.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekLisansProjeleriVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yalnızca kendi kaydını, akademisyen yalnızca kendi
/// önerdiği konuları ve hocası olduğu projeleri (kayıt verisi, rol değil), bölüm başkanı kendi bölümünü görür.
/// </summary>
public sealed class MockLisansProjeleriVeriKaynagi(IHttpContextAccessor httpContextAccessor) : ILisansProjeleriVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<ProjeKonusu>> KonularAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekLisansProjeleriVerisi.Konular.Where(x => k.Kapsar(KayitKapsami.Bolum, null, x.Bolum, x.Fakulte))]);
    }

    public Task<IReadOnlyList<LisansProjesi>> ProjelerimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekLisansProjeleriVerisi.Kayitlar.Where(p => k.Kapsar(KayitKapsami.Kendi, p.OgrenciNo, null, null))
            .OrderByDescending(p => p.Donem)]);
    }

    public Task<IReadOnlyList<ProjeKonusu>> KonularimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekLisansProjeleriVerisi.Konular.Where(x => k.Kapsar(KayitKapsami.Kendi, x.HocaKullaniciAdi, null, null))]);
    }

    public Task<IReadOnlyList<LisansProjesi>> DanismanligimdakiProjelerAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekLisansProjeleriVerisi.Kayitlar.Where(p => k.Kapsar(KayitKapsami.Kendi, p.HocaKullaniciAdi, null, null))
            .OrderByDescending(p => p.Donem).ThenBy(p => p.OgrenciNo, StringComparer.Ordinal)]);
    }

    public Task<IReadOnlyList<LisansProjesi>> ProjelerAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekLisansProjeleriVerisi.Kayitlar
            .Where(p => p.Donem == OrnekLisansProjeleriVerisi.GuncelDonem && k.Kapsar(KayitKapsami.Bolum, null, p.Bolum, p.Fakulte))
            .OrderBy(p => p.OgrenciNo, StringComparer.Ordinal)]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Lisans Projeleri API'si (docs/api-sozlesmeleri/lisans-projeleri.md) üzerinden çalışan veri kaynağı. Kapsamı API,
/// access token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiLisansProjeleriVeriKaynagi(OtomasyonApiIstemcisi api) : ILisansProjeleriVeriKaynagi
{
    public Task<IReadOnlyList<ProjeKonusu>> KonularAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<ProjeKonusu>>("api/lisans-projeleri/konular", cancellationToken);

    public Task<IReadOnlyList<LisansProjesi>> ProjelerimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<LisansProjesi>>("api/lisans-projeleri/projelerim", cancellationToken);

    public Task<IReadOnlyList<ProjeKonusu>> KonularimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<ProjeKonusu>>("api/lisans-projeleri/konularim", cancellationToken);

    public Task<IReadOnlyList<LisansProjesi>> DanismanligimdakiProjelerAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<LisansProjesi>>("api/lisans-projeleri/danismanligimdaki-projeler", cancellationToken);

    public Task<IReadOnlyList<LisansProjesi>> ProjelerAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<LisansProjesi>>("api/lisans-projeleri/projeler", cancellationToken);
}
