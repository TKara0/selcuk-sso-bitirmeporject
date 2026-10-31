using Selcuk.Otomasyon.CapYandal.Sozlesme;
using Selcuk.Otomasyon.CapYandal.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.CapYandal.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), ÇAP-Yandal'ın değerleriyle.</summary>
public sealed class CapYandalTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => CapYandalOtomasyonu.Ad;

    protected override string Simge => "ÇY";

    protected override string ClientId => "cap-yandal-otomasyonu";

    protected override string CerezAdi => "CapYandalOtomasyonu.Oturum";

    protected override string ApiKaynagi => "cap-yandal-api";

    protected override string OkuKapsami => "cap-yandal.oku";

    protected override string VeriAdi => "cap";

    protected override string AcikVurgu => "#735A00";

    protected override IReadOnlyList<MenuOgesi> Menu => CapYandalOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(ICapYandalVeriKaynagi);

    protected override Type MockTuru => typeof(MockCapYandalVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiCapYandalVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => CapYandalKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/Basvurularim", "api/cap-yandal/basvurularim", new[]
        {
            new CapBasvurusu("API-1", "203011002", "Test Öğrenci İki", OtomasyonFactory.Bilgisayar, "YAN-EEM",
                "Örnek API Yandal Programı", ProgramTuru.Yandal, OtomasyonFactory.Teknoloji, OtomasyonFactory.ElektrikElektronik,
                "2026-2027 Güz", 2.80m, 31, 80, new DateOnly(2026, 8, 24), CapDurumu.BaskanOnayinda),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Baskan/BasvuruOnayi", null, Roller.BolumBaskani),
    ];
}

/// <summary>ÇAP-Yandal'a girebilen rollerin test kullanıcıları (kimlik sunucusunun Development test kullanıcıları gibi).</summary>
internal static class CapYandalKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.CapYandalKoordinatoru] = new("koordinator.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.CapYandalKoordinatoru, Roller.Danisman),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
        [Roller.BolumSekreteri] = new("sekreter.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Personel, Roller.BolumSekreteri),
    };
}
