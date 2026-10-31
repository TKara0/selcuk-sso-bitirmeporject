using Selcuk.Otomasyon.Mudek.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mudek.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekMudekVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): bölüm rolleri kendi bölümünü, akademisyen yalnızca hocası
/// olduğu derslerin çıktılarını (kayıt verisi, rol değil) görür.
/// </summary>
public sealed class MockMudekVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IMudekVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    public Task<IReadOnlyList<ProgramCiktisi>> ProgramCiktilariAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekMudekVerisi.ProgramCiktilari.Where(p => k.Kapsar(KayitKapsami.Bolum, null, p.Bolum, p.Fakulte))]);
    }

    public Task<IReadOnlyList<DersCiktisi>> DersCiktilariAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekMudekVerisi.DersCiktilari.Where(d => k.Kapsar(KayitKapsami.Bolum, null, d.Bolum, d.Fakulte))]);
    }

    public Task<IReadOnlyList<DersCiktisi>> DersCiktilarimAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekMudekVerisi.DersCiktilari.Where(d => k.Kapsar(KayitKapsami.Kendi, d.HocaKullaniciAdi, null, null))]);
    }

    public Task<IReadOnlyList<MudekBelgesi>> BelgelerAsync(CancellationToken cancellationToken = default)
    {
        var k = Kapsam;
        return Sonuc([.. OrnekMudekVerisi.Belgeler.Where(b => k.Kapsar(KayitKapsami.Bolum, null, b.Bolum, b.Fakulte))
            .OrderByDescending(b => b.Donem, StringComparer.Ordinal)]);
    }

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// MÜDEK API'si (docs/api-sozlesmeleri/mudek.md) üzerinden çalışan veri kaynağı. Kapsamı API, access token'daki
/// claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiMudekVeriKaynagi(OtomasyonApiIstemcisi api) : IMudekVeriKaynagi
{
    public Task<IReadOnlyList<ProgramCiktisi>> ProgramCiktilariAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<ProgramCiktisi>>("api/mudek/program-ciktilari", cancellationToken);

    public Task<IReadOnlyList<DersCiktisi>> DersCiktilariAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DersCiktisi>>("api/mudek/ders-ciktilari", cancellationToken);

    public Task<IReadOnlyList<DersCiktisi>> DersCiktilarimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<DersCiktisi>>("api/mudek/ders-ciktilarim", cancellationToken);

    public Task<IReadOnlyList<MudekBelgesi>> BelgelerAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<MudekBelgesi>>("api/mudek/belgeler", cancellationToken);
}
