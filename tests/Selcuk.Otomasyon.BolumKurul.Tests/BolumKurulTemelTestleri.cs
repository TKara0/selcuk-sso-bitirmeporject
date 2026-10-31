using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.BolumKurul.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.BolumKurul.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Bölüm Kurul Karar'ın değerleriyle.</summary>
public sealed class BolumKurulTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => BolumKurulOtomasyonu.Ad;

    protected override string Simge => "BK";

    protected override string ClientId => "bolum-kurul-otomasyonu";

    protected override string CerezAdi => "BolumKurulOtomasyonu.Oturum";

    protected override string ApiKaynagi => "bolum-kurul-api";

    protected override string OkuKapsami => "bolum-kurul.oku";

    protected override string VeriAdi => "kurul";

    protected override string AcikVurgu => "#4A5F78";

    protected override string AnaRol => Roller.BolumKuruluUyesi;

    protected override IReadOnlyList<MenuOgesi> Menu => BolumKurulOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IBolumKurulVeriKaynagi);

    protected override Type MockTuru => typeof(MockBolumKurulVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiBolumKurulVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => BolumKurulKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Uye/Gundem", "api/bolum-kurul/toplantilar", new[]
        {
            new Toplanti("API-1", "2026/99", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar, new DateOnly(2026, 11, 20),
                "Örnek API salonu", ToplantiDurumu.Planlandi, 11, 0, [new GundemMaddesi("API-1-1", 1, "Örnek API maddesi", MaddeDurumu.Gundemde)]),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Uye/Oylama", "Kabul", Roller.BolumKuruluUyesi),
        new("/Uye/Oylama", "Ret", Roller.BolumKuruluUyesi),
        new("/Uye/Oylama", "Cekimser", Roller.BolumKuruluUyesi),
        new("/Baskan/Toplantilar", "YeniToplanti", Roller.BolumBaskani),
        new("/Baskan/Toplantilar", "MaddeEkle", Roller.BolumBaskani),
        new("/Sekreter/Arsiv", null, Roller.BolumSekreteri),
    ];
}

/// <summary>
/// Bölüm Kurul Karar'a girebilen rollerin test kullanıcıları. Ortak menü testi her kullanıcının yalnızca bir rol başlığı
/// görmesini beklediği için bölüm başkanı yalnızca o rolle verilir (Development'taki baskan.test kurul üyesidir de).
/// </summary>
internal static class BolumKurulKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.BolumBaskani),
        [Roller.BolumKuruluUyesi] = new("komisyon.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.BolumKuruluUyesi),
        [Roller.BolumSekreteri] = new("sekreter.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Personel, Roller.BolumSekreteri),
        [Roller.FakulteSekreteri] = new("fakultesekreter.test", null, OtomasyonFactory.Teknoloji, Roller.Personel, Roller.FakulteSekreteri),
    };
}
