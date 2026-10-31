using Selcuk.Otomasyon.EtikKurul.Sozlesme;
using Selcuk.Otomasyon.EtikKurul.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.EtikKurul.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Etik Kurul'un değerleriyle.</summary>
public sealed class EtikKurulTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => EtikKurulOtomasyonu.Ad;

    protected override string Simge => "EK";

    protected override string ClientId => "etik-kurul-otomasyonu";

    protected override string CerezAdi => "EtikKurulOtomasyonu.Oturum";

    protected override string ApiKaynagi => "etik-kurul-api";

    protected override string OkuKapsami => "etik-kurul.oku";

    protected override string VeriAdi => "etik";

    protected override string AcikVurgu => "#7A2E5C";

    protected override string AnaRol => Roller.Akademisyen;

    protected override IReadOnlyList<MenuOgesi> Menu => EtikKurulOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IEtikKurulVeriKaynagi);

    protected override Type MockTuru => typeof(MockEtikKurulVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiEtikKurulVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => EtikKurulKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Akademisyen/Basvurularim", "api/etik-kurul/basvurularim", new[]
        {
            new EtikBasvurusu("API-1", "hoca.test", "Test Akademisyen", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar,
                "Örnek API projesi", ArastirmaTuru.Anket, "Gönüllüler", "Anket", new DateOnly(2026, 10, 1),
                EtikDurumu.OnIncelemede, []),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Kurul/Gorus", null, Roller.EtikKurulUyesi),
    ];
}

/// <summary>
/// Etik Kurul'a girebilen rollerin test kullanıcıları. Ortak menü testi her kullanıcının yalnızca bir rol başlığı
/// görmesini beklediği için kurul üyesi ve bölüm başkanı yalnızca o rolle verilir; gerçek çok rollü hâl
/// (etik.test: akademisyen + kurul üyesi) EtikKurulEkranTests'te.
/// </summary>
internal static class EtikKurulKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Akademisyen] = new("hoca.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.Danisman),
        [Roller.EtikKurulUyesi] = new("etik.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.EtikKurulUyesi),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
    };
}
