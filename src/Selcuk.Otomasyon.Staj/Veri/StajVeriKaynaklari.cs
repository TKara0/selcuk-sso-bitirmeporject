using System.Text.Json;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;
using static Selcuk.Otomasyon.Staj.Sozlesme.StajDurumu;

namespace Selcuk.Otomasyon.Staj.Veri;

/// <summary>
/// Örnek veri (<see cref="OrnekStajVerisi"/>) üzerinden çalışan veri kaynağı. Kapsamı oturumdaki kullanıcının
/// claim'lerinden süzer (<see cref="KullaniciKapsami"/>): öğrenci yöntemleri yalnızca kullanıcının kendi kayıtlarını
/// (öğrenci numarası = kullanıcı adı), diğerleri yalnızca kullanıcının bölümünün kayıtlarını döndürür. Veri değişmez.
/// </summary>
public sealed class MockStajVeriKaynagi(IHttpContextAccessor httpContextAccessor) : IStajVeriKaynagi
{
    private KullaniciKapsami Kapsam => KullaniciKapsami.Oku(
        httpContextAccessor.HttpContext?.User ?? throw new InvalidOperationException("İstek dışında veri okunamaz."));

    private IEnumerable<StajKaydi> Kendi() =>
        OrnekStajVerisi.Kayitlar.Where(k => Kapsam.Kapsar(KayitKapsami.Kendi, k.OgrenciNo, k.Bolum, k.Fakulte));

    private IEnumerable<StajKaydi> Bolum() =>
        OrnekStajVerisi.Kayitlar.Where(k => Kapsam.Kapsar(KayitKapsami.Bolum, k.OgrenciNo, k.Bolum, k.Fakulte));

    public Task<IReadOnlyList<StajKaydi>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Kendi().OrderBy(k => k.Baslangic)]);

    public Task<StajDefteri> DefterimAsync(CancellationToken cancellationToken = default)
    {
        // Güncel staj: devam eden, yoksa teslim edilmiş, yoksa en son değerlendirilmiş defterli staj.
        var staj = Kendi().Where(k => k.Durum is DevamEdiyor or DefterTeslimEdildi or Degerlendirildi)
            .OrderBy(k => k.Durum switch { DevamEdiyor => 0, DefterTeslimEdildi => 1, _ => 2 })
            .ThenByDescending(k => k.Baslangic)
            .FirstOrDefault();
        IReadOnlyList<DefterGirisi> girisler = staj is null
            ? []
            : [.. OrnekStajVerisi.DefterGirisleri.Where(g => g.StajId == staj.Id).OrderBy(g => g.Tarih)];
        return Task.FromResult(new StajDefteri(staj, girisler));
    }

    public Task<IReadOnlyList<StajKaydi>> BasvurularAsync(StajDurumu? durum, CancellationToken cancellationToken = default) =>
        Sonuc([.. Bolum().Where(k => durum is null || k.Durum == durum).OrderBy(k => k.BasvuruTarihi)]);

    public Task<IReadOnlyList<StajKaydi>> DefterlerAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Bolum().Where(k => k.Durum == DefterTeslimEdildi).OrderBy(k => k.Bitis)]);

    public Task<StajOzeti> OzetAsync(int yil, CancellationToken cancellationToken = default)
    {
        var kayitlar = Bolum().ToList();
        var yillar = kayitlar
            .GroupBy(k => (k.Baslangic.Year, k.Tur))
            .OrderByDescending(g => g.Key.Year).ThenBy(g => g.Key.Tur)
            .Select(g => new YillikStajSayisi(g.Key.Year, g.Key.Tur, g.Count(),
                g.Count(k => k.Durum is not (BasvuruBekliyor or Reddedildi)),
                g.Count(k => k.Durum == Degerlendirildi)))
            .ToList();
        return Task.FromResult(new StajOzeti(yil,
            kayitlar.Count(k => k.Durum is Onaylandi or DevamEdiyor),
            kayitlar.Count(k => k.Durum == BasvuruBekliyor),
            kayitlar.Count(k => k.Durum == Degerlendirildi && k.Bitis.Year == yil),
            yillar));
    }

    public Task<IReadOnlyList<StajEvragi>> EvraklarAsync(CancellationToken cancellationToken = default) =>
        Sonuc([.. Bolum().Where(k => k.Durum is not (BasvuruBekliyor or Reddedildi)).OrderBy(k => k.OgrenciNo)
            .ThenBy(k => k.Baslangic)
            .Select(k => new StajEvragi(k.Id, k.OgrenciNo, k.OgrenciAdi, k.Tur, k.Durum,
                KabulBelgesi: true,
                SgkGirisi: k.Durum is not Onaylandi,
                DefterTeslim: k.Durum is DefterTeslimEdildi or Degerlendirildi))]);

    private static Task<IReadOnlyList<T>> Sonuc<T>(IReadOnlyList<T> liste) => Task.FromResult(liste);
}

/// <summary>
/// Staj API'si (docs/api-sozlesmeleri/staj.md) üzerinden çalışan veri kaynağı. Kapsamı API, access token'daki
/// claim'lere (sub, preferred_username, role; bölüm ve fakülte kullanıcı kaydından) göre kendisi süzer; buradan
/// kullanıcı bilgisi gönderilmez. Hatalar <see cref="VeriKaynagiHatasi"/> olarak düzenin hata sayfasına gider.
/// </summary>
public sealed class ApiStajVeriKaynagi(OtomasyonApiIstemcisi api) : IStajVeriKaynagi
{
    public Task<IReadOnlyList<StajKaydi>> BasvurularimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<StajKaydi>>("api/staj/basvurularim", cancellationToken);

    public Task<StajDefteri> DefterimAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<StajDefteri>("api/staj/defterim", cancellationToken);

    public Task<IReadOnlyList<StajKaydi>> BasvurularAsync(StajDurumu? durum, CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<StajKaydi>>(
            durum is null ? "api/staj/basvurular" : "api/staj/basvurular?durum=" + JsonNamingPolicy.CamelCase.ConvertName(durum.ToString()!),
            cancellationToken);

    public Task<IReadOnlyList<StajKaydi>> DefterlerAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<StajKaydi>>("api/staj/defterler", cancellationToken);

    public Task<StajOzeti> OzetAsync(int yil, CancellationToken cancellationToken = default) =>
        api.GetAsync<StajOzeti>($"api/staj/ozet?yil={yil}", cancellationToken);

    public Task<IReadOnlyList<StajEvragi>> EvraklarAsync(CancellationToken cancellationToken = default) =>
        api.GetAsync<IReadOnlyList<StajEvragi>>("api/staj/evraklar", cancellationToken);
}
