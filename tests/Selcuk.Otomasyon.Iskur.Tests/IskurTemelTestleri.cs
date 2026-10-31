using Selcuk.Otomasyon.Iskur.Sozlesme;
using Selcuk.Otomasyon.Iskur.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Iskur.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), İŞKUR'un değerleriyle.</summary>
public sealed class IskurTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => IskurOtomasyonu.Ad;

    protected override string Simge => "İŞ";

    protected override string ClientId => "iskur-otomasyonu";

    protected override string CerezAdi => "IskurOtomasyonu.Oturum";

    protected override string ApiKaynagi => "iskur-api";

    protected override string OkuKapsami => "iskur.oku";

    protected override string VeriAdi => "iskur";

    protected override string AcikVurgu => "#1F5F73";

    /// <summary>Ortak testlerin varsayılan izinsiz kullanıcısı İŞKUR sorumlusudur; burada izinli olduğu için yalnızca personel.</summary>
    protected override string[] IzinsizRoller => [Roller.Personel];

    protected override IReadOnlyList<MenuOgesi> Menu => IskurOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IIskurVeriKaynagi);

    protected override Type MockTuru => typeof(MockIskurVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiIskurVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => IskurKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/DevamDurumum", "api/iskur/devamim", new[]
        {
            new AylikDevam("API-1", "203011002", "Test Öğrenci İki", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar,
                "2026-10", 60, 48, DevamDurumu.Giriliyor),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Sorumlu/DevamGirisi", null, Roller.IskurSorumlusu),
    ];
}

/// <summary>İŞKUR'a girebilen rollerin test kullanıcıları (Development test kullanıcılarındaki gibi).</summary>
internal static class IskurKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.IskurSorumlusu] = new("iskur.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Personel, Roller.IskurSorumlusu),
    };
}
