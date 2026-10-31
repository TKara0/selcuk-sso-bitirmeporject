using System.Text.Json;
using Selcuk.Otomasyon.CapYandal.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.CapYandal.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekCapYandalVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yalnızca kendi kayıtlarını, koordinatör, bölüm
/// başkanı ve sekreteri yalnızca kendi bölümünün açtığı programları görür. Veri değişmez.
/// </summary>
public sealed class MockCapYandalVeriKaynagi(IHttpContextAccessor httpContextAccessor) : ICapYandalVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    private bool Kapsar(KayitKapsami kapsam, CapBasvurusu b) => Kapsam.Kapsar(kapsam, b.OgrenciNo, b.Bolum, b.Fakulte);

    public Task<IReadOnlyList<CapProgrami>> ProgramlarAsync(CancellationToken cancellationToken = default) =>
        Sonuc(OrnekCapYandalVerisi.Programlar);

    public Task<IReadOnlyList<AkademikBilgi>> AkademikBilgimAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. OrnekCapYandalVerisi.AkademikBilgiler.Where(a => Kapsam.Kapsar(KayitKapsami.Kendi, a.OgrenciNo, null, null))]);

    public Task<IReadOnlyList<CapBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. OrnekCapYandalVerisi.Basvurular.Where(b => Kapsar(KayitKapsami.Kendi, b)).OrderByDescending(b => b.BasvuruTarihi)]);

    public Task<IReadOnlyList<ProgramIlerlemesi>> IlerlememAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. OrnekCapYandalVerisi.Ilerlemeler.Where(i => Kapsar(KayitKapsami.Kendi, i.Basvuru))]);

    public Task<IReadOnlyList<CapBasvurusu>> BasvurularAsync(CapDurumu? durum, CancellationToken cancellationToken = default) =>
        Sonuc([.. OrnekCapYandalVerisi.Basvurular.Where(b => Kapsar(KayitKapsami.Bolum, b) && (durum is null || b.Durum == durum))
            .OrderBy(b => b.BasvuruTarihi)]);

    public Task<IReadOnlyList<ProgramIlerlemesi>> ProgramdakiOgrencilerAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. OrnekCapYandalVerisi.Ilerlemeler.Where(i => Kapsar(KayitKapsami.Bolum, i.Basvuru))
            .OrderBy(i => i.Basvuru.ProgramKodu).ThenBy(i => i.Basvuru.OgrenciNo)]);

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// ÇAP-Yandal API'si (docs/api-sozlesmeleri/cap-yandal.md) üzerinden çalışan veri kaynağı. Kapsamı API, access
/// token'daki claim'lere göre kendisi süzer; buradan kullanıcı bilgisi gönderilmez.
/// </summary>
public sealed class ApiCapYandalVeriKaynagi(OtomasyonApiIstemcisi api) : ICapYandalVeriKaynagi
{
    public Task<IReadOnlyList<CapProgrami>> ProgramlarAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<CapProgrami>>("api/cap-yandal/programlar", cancellationToken);

    public Task<IReadOnlyList<AkademikBilgi>> AkademikBilgimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<AkademikBilgi>>("api/cap-yandal/akademik-bilgim", cancellationToken);

    public Task<IReadOnlyList<CapBasvurusu>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<CapBasvurusu>>("api/cap-yandal/basvurularim", cancellationToken);

    public Task<IReadOnlyList<ProgramIlerlemesi>> IlerlememAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<ProgramIlerlemesi>>("api/cap-yandal/ilerlemem", cancellationToken);

    public Task<IReadOnlyList<CapBasvurusu>> BasvurularAsync(CapDurumu? durum, CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<CapBasvurusu>>(
            durum is null ? "api/cap-yandal/basvurular" : "api/cap-yandal/basvurular?durum=" + JsonNamingPolicy.CamelCase.ConvertName(durum.ToString()!),
            cancellationToken);

    public Task<IReadOnlyList<ProgramIlerlemesi>> ProgramdakiOgrencilerAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<ProgramIlerlemesi>>("api/cap-yandal/programdaki-ogrenciler", cancellationToken);
}
