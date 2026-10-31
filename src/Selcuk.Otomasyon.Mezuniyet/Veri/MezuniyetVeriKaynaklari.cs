using System.Text.Json;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.Mezuniyet.Sozlesme.MezuniyetDurumu;

namespace Selcuk.Otomasyon.Mezuniyet.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekMezuniyetVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yalnızca kendi kaydını, bölüm rolleri kendi
/// bölümünü, fakülte sekreteri kendi fakültesini görür. Veri değişmez.
/// </summary>
public sealed class MockMezuniyetVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IMezuniyetVeriKaynagi
{
    private IEnumerable<MezuniyetAdayi> Kapsamda(KayitKapsami kapsam)
    {
        var k = KullaniciKapsami.Oku(
            httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));
        return OrnekMezuniyetVerisi.Kayitlar.Where(a => k.Kapsar(kapsam, a.OgrenciNo, a.Bolum, a.Fakulte));
    }

    public Task<IReadOnlyList<MezuniyetAdayi>> DurumumAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Kendi).OrderByDescending(a => a.BasvuruTarihi ?? DateOnly.MaxValue)]);

    public Task<IReadOnlyList<IlisikKesmeBirimi>> IlisikKesmeAsync(CancellationToken cancellationToken = default) =>
        Sonuc(Kapsamda(KayitKapsami.Kendi).OrderByDescending(a => a.BasvuruTarihi ?? DateOnly.MaxValue).FirstOrDefault() is { } aday
            ? OrnekMezuniyetVerisi.IlisikKesme(aday)
            : []);

    public Task<IReadOnlyList<MezuniyetAdayi>> AdaylarAsync(MezuniyetDurumu? durum, CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Bolum).Where(a => durum is null || a.Durum == durum)
            .OrderBy(a => a.BasvuruTarihi ?? DateOnly.MaxValue)]);

    public Task<IReadOnlyList<MezuniyetAdayi>> FakulteListeleriAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Fakulte).Where(a => a.Durum is Dekanlikta or DiplomaHazirlaniyor or DiplomaHazir)
            .OrderBy(a => a.Bolum, StringComparer.Create(MezuniyetOtomasyonu.Tr, false)).ThenBy(a => a.BasvuruTarihi)]);

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Mezuniyet API'si (docs/api-sozlesmeleri/mezuniyet.md) üzerinden çalışan veri kaynağı. Kapsamı API, access token'daki
/// claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiMezuniyetVeriKaynagi(OtomasyonApiIstemcisi api) : IMezuniyetVeriKaynagi
{
    public Task<IReadOnlyList<MezuniyetAdayi>> DurumumAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<MezuniyetAdayi>>("api/mezuniyet/durumum", cancellationToken);

    public Task<IReadOnlyList<IlisikKesmeBirimi>> IlisikKesmeAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<IlisikKesmeBirimi>>("api/mezuniyet/ilisik-kesme", cancellationToken);

    public Task<IReadOnlyList<MezuniyetAdayi>> AdaylarAsync(MezuniyetDurumu? durum, CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<MezuniyetAdayi>>(
            durum is null ? "api/mezuniyet/adaylar" : "api/mezuniyet/adaylar?durum=" + JsonNamingPolicy.CamelCase.ConvertName(durum.ToString()!),
            cancellationToken);

    public Task<IReadOnlyList<MezuniyetAdayi>> FakulteListeleriAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<MezuniyetAdayi>>("api/mezuniyet/fakulte-listeleri", cancellationToken);
}
