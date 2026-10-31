using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Danismanlik.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekDanismanlikVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): danışman yalnızca danışmanı olduğu öğrencileri ve kendi
/// notlarını (danışmanlık kayıt verisidir, rol değil), öğrenci yalnızca kendi kaydını, bölüm başkanı kendi bölümünü görür.
/// </summary>
public sealed class MockDanismanlikVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IDanismanlikVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<DanismanlikOgrencisi>> OgrencilerimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekDanismanlikVerisi.Ogrenciler.Where(o => k.Kapsar(KayitKapsami.Kendi, o.DanismanKullaniciAdi, null, null))]);
    }

    public Task<IReadOnlyList<GorusmeNotu>> GorusmeNotlarimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekDanismanlikVerisi.Notlar.Where(n => k.Kapsar(KayitKapsami.Kendi, n.DanismanKullaniciAdi, null, null))
            .OrderByDescending(n => n.Tarih)]);
    }

    public Task<IReadOnlyList<DanismanlikOgrencisi>> KaydimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekDanismanlikVerisi.Ogrenciler.Where(o => k.Kapsar(KayitKapsami.Kendi, o.OgrenciNo, null, null))]);
    }

    public Task<IReadOnlyList<GorusmeTalebi>> TaleplerimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekDanismanlikVerisi.Talepler.Where(t => k.Kapsar(KayitKapsami.Kendi, t.OgrenciNo, null, null))
            .OrderByDescending(t => t.TalepTarihi)]);
    }

    public Task<IReadOnlyList<DanismanlikOgrencisi>> BolumOgrencileriAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekDanismanlikVerisi.Ogrenciler.Where(o => k.Kapsar(KayitKapsami.Bolum, null, o.Bolum, o.Fakulte))]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Öğrenci Danışmanlık API'si (docs/api-sozlesmeleri/danismanlik.md) üzerinden çalışan veri kaynağı. Kapsamı API, access
/// token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiDanismanlikVeriKaynagi(OtomasyonApiIstemcisi api) : IDanismanlikVeriKaynagi
{
    public Task<IReadOnlyList<DanismanlikOgrencisi>> OgrencilerimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DanismanlikOgrencisi>>("api/danismanlik/ogrencilerim", cancellationToken);

    public Task<IReadOnlyList<GorusmeNotu>> GorusmeNotlarimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<GorusmeNotu>>("api/danismanlik/gorusme-notlarim", cancellationToken);

    public Task<IReadOnlyList<DanismanlikOgrencisi>> KaydimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DanismanlikOgrencisi>>("api/danismanlik/kaydim", cancellationToken);

    public Task<IReadOnlyList<GorusmeTalebi>> TaleplerimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<GorusmeTalebi>>("api/danismanlik/taleplerim", cancellationToken);

    public Task<IReadOnlyList<DanismanlikOgrencisi>> BolumOgrencileriAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DanismanlikOgrencisi>>("api/danismanlik/bolum-ogrencileri", cancellationToken);
}
