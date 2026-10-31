using Selcuk.Otomasyon.Mudek.Sozlesme;
using Selcuk.Otomasyon.Mudek.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Mudek.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), MÜDEK'in değerleriyle.</summary>
public sealed class MudekTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => MudekOtomasyonu.Ad;

    protected override string Simge => "MÜ";

    protected override string ClientId => "mudek-otomasyonu";

    protected override string CerezAdi => "MudekOtomasyonu.Oturum";

    protected override string ApiKaynagi => "mudek-api";

    protected override string OkuKapsami => "mudek.oku";

    protected override string VeriAdi => "mudek";

    protected override string AcikVurgu => "#8A5163";

    protected override string AnaRol => Roller.Akademisyen;

    protected override IReadOnlyList<MenuOgesi> Menu => MudekOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IMudekVeriKaynagi);

    protected override Type MockTuru => typeof(MockMudekVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiMudekVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => MudekKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Akademisyen/VeriGirisi", "api/mudek/ders-ciktilarim", new[]
        {
            new DersCiktisi("API-1", "BM999", "Örnek API dersi", "hoca.test", "Test Akademisyen", OtomasyonFactory.Teknoloji,
                OtomasyonFactory.Bilgisayar, "2025-2026", "DÇ-1", "Örnek ders çıktısı", ["PÇ-1"], 0.7m),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Akademisyen/VeriGirisi", null, Roller.Akademisyen),
        new("/Rapor/Donem", null, Roller.MudekKomisyonu),
    ];
}

/// <summary>
/// MÜDEK'e girebilen rollerin test kullanıcıları. Ortak menü testi her kullanıcının yalnızca bir rol başlığı görmesini
/// beklediği için komisyon üyesi ve bölüm başkanı akademisyen rolü olmadan verilir (Development'taki mudek.test ve
/// baskan.test akademisyendir de; çok rollü hâl MudekEkranTests'te).
/// </summary>
internal static class MudekKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Akademisyen] = new("hoca.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.Danisman),
        [Roller.MudekKomisyonu] = new("mudek.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.MudekKomisyonu),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
        [Roller.BolumSekreteri] = new("sekreter.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Personel, Roller.BolumSekreteri),
    };
}
