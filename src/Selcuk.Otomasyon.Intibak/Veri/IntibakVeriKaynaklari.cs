using System.Text.Json;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.Intibak.Sozlesme.IntibakDurumu;

namespace Selcuk.Otomasyon.Intibak.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekIntibakVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yalnızca kendi başvurularını, bölüm rolleri kendi
/// bölümünü, fakülte sekreteri kendi fakültesini görür. Veri değişmez.
/// </summary>
public sealed class MockIntibakVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IIntibakVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    private IEnumerable<IntibakBasvurusu> Kapsamda(KayitKapsami kapsam)
    {
        var k = Kapsam;
        return OrnekIntibakVerisi.Kayitlar.Where(b => k.Kapsar(kapsam, b.OgrenciNo, b.Bolum, b.Fakulte));
    }

    public Task<IReadOnlyList<IntibakBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Kendi).OrderByDescending(b => b.BasvuruTarihi)]);

    public Task<IReadOnlyList<IntibakBasvurusu>> BasvurularAsync(IntibakDurumu? durum, CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Bolum).Where(b => durum is null || b.Durum == durum).OrderBy(b => b.BasvuruTarihi)]);

    public Task<IReadOnlyList<MufredatDersi>> MufredatAsync(CancellationToken cancellationToken = default) =>
        Sonuc(Kapsam.Bolum is { } bolum && OrnekIntibakVerisi.Mufredat.TryGetValue(bolum, out var dersler) ? dersler : []);

    public Task<IReadOnlyList<IntibakBasvurusu>> YonetimKuruluEvrakiAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Kapsamda(KayitKapsami.Fakulte).Where(b => b.Durum == YonetimKurulunda)
            .OrderBy(b => b.Bolum, StringComparer.Create(IntibakOtomasyonu.Tr, false)).ThenBy(b => b.BasvuruTarihi)]);

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// İntibak API'si (docs/api-sozlesmeleri/intibak.md) üzerinden çalışan veri kaynağı. Kapsamı API, access token'daki
/// claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiIntibakVeriKaynagi(OtomasyonApiIstemcisi api) : IIntibakVeriKaynagi
{
    public Task<IReadOnlyList<IntibakBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<IntibakBasvurusu>>("api/intibak/basvurularim", cancellationToken);

    public Task<IReadOnlyList<IntibakBasvurusu>> BasvurularAsync(IntibakDurumu? durum, CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<IntibakBasvurusu>>(
            durum is null ? "api/intibak/basvurular" : "api/intibak/basvurular?durum=" + JsonNamingPolicy.CamelCase.ConvertName(durum.ToString()!),
            cancellationToken);

    public Task<IReadOnlyList<MufredatDersi>> MufredatAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<MufredatDersi>>("api/intibak/mufredat", cancellationToken);

    public Task<IReadOnlyList<IntibakBasvurusu>> YonetimKuruluEvrakiAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<IntibakBasvurusu>>("api/intibak/yonetim-kurulu-evraki", cancellationToken);
}
