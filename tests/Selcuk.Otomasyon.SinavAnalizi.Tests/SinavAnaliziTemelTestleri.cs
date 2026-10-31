using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;
using Selcuk.Otomasyon.SinavAnalizi.Veri;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.SinavAnalizi.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Sınav Başarısı Analizi'nin değerleriyle.</summary>
public sealed class SinavAnaliziTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => SinavAnaliziOtomasyonu.Ad;

    protected override string Simge => "SA";

    protected override string ClientId => "sinav-analizi-otomasyonu";

    protected override string CerezAdi => "SinavAnaliziOtomasyonu.Oturum";

    protected override string ApiKaynagi => "sinav-analizi-api";

    protected override string OkuKapsami => "sinav-analizi.oku";

    protected override string VeriAdi => "sinav";

    protected override string AcikVurgu => "#3E4A52";

    protected override string AnaRol => Roller.Akademisyen;

    protected override IReadOnlyList<MenuOgesi> Menu => SinavAnaliziOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(ISinavAnaliziVeriKaynagi);

    protected override Type MockTuru => typeof(MockSinavAnaliziVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiSinavAnaliziVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => SinavAnaliziKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Akademisyen/Derslerim", "api/sinav-analizi/derslerim", new[]
        {
            new DersSonucu("API-1", "BM999", "Örnek API dersi", "hoca.test", "Test Akademisyen", OtomasyonFactory.Teknoloji,
                OtomasyonFactory.Bilgisayar, "2025-2026 Güz", [new HarfSayisi("AA", 3), new HarfSayisi("FF", 1)], ["PÇ-1"]),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Baskan/BolumGeneli", null, Roller.BolumBaskani),
    ];
}

/// <summary>
/// Sınav Analizi'ne girebilen rollerin test kullanıcıları. Ortak menü testi her kullanıcının yalnızca bir rol başlığı
/// görmesini beklediği için bölüm başkanı ve komisyon üyesi akademisyen rolü olmadan verilir.
/// </summary>
internal static class SinavAnaliziKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Akademisyen] = new("hoca.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.Danisman),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
        [Roller.MudekKomisyonu] = new("mudek.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.MudekKomisyonu),
    };
}
