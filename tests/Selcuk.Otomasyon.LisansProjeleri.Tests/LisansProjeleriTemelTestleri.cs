using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.LisansProjeleri.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.LisansProjeleri.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Lisans Projeleri'nin değerleriyle.</summary>
public sealed class LisansProjeleriTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => LisansProjeleriOtomasyonu.Ad;

    protected override string Simge => "LP";

    protected override string ClientId => "lisans-projeleri-otomasyonu";

    protected override string CerezAdi => "LisansProjeleriOtomasyonu.Oturum";

    protected override string ApiKaynagi => "lisans-projeleri-api";

    protected override string OkuKapsami => "lisans-projeleri.oku";

    protected override string VeriAdi => "proje";

    protected override string AcikVurgu => "#2E6B58";

    protected override IReadOnlyList<MenuOgesi> Menu => LisansProjeleriOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(ILisansProjeleriVeriKaynagi);

    protected override Type MockTuru => typeof(MockLisansProjeleriVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiLisansProjeleriVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => LisansProjeleriKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/Asamalarim", "api/lisans-projeleri/projelerim", new[]
        {
            new LisansProjesi("API-1", "203011002", "Test Öğrenci İki", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar,
                "2026-2027", ProjeAsamasi.Onerge, "API-K", "Örnek API konusu", "hoca.test", "Test Akademisyen", []),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Ogrenci/KonuSecimi", null, Roller.Ogrenci),
        new("/Akademisyen/Degerlendirme", null, Roller.Akademisyen),
        new("/Baskan/Eslestirme", null, Roller.BolumBaskani),
    ];
}

/// <summary>
/// Lisans Projeleri'ne girebilen rollerin test kullanıcıları. Ortak menü testi her kullanıcının yalnızca bir rol başlığı
/// görmesini beklediği için bölüm başkanı yalnızca o rolle verilir (Development'taki baskan.test akademisyendir de).
/// </summary>
internal static class LisansProjeleriKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.Akademisyen] = new("hoca.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.Danisman),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
    };
}
