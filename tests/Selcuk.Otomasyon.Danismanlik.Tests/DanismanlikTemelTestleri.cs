using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Danismanlik.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Danismanlik.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Danışmanlık'ın değerleriyle.</summary>
public sealed class DanismanlikTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => DanismanlikOtomasyonu.Ad;

    protected override string Simge => "DA";

    protected override string ClientId => "danismanlik-otomasyonu";

    protected override string CerezAdi => "DanismanlikOtomasyonu.Oturum";

    protected override string ApiKaynagi => "danismanlik-api";

    protected override string OkuKapsami => "danismanlik.oku";

    protected override string VeriAdi => "danismanlik";

    protected override string AcikVurgu => "#4B4A9C";

    protected override IReadOnlyList<MenuOgesi> Menu => DanismanlikOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IDanismanlikVeriKaynagi);

    protected override Type MockTuru => typeof(MockDanismanlikVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiDanismanlikVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => DanismanlikKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/GorusmeTalebi", "api/danismanlik/taleplerim", new[]
        {
            new GorusmeTalebi("API-1", "203011002", "Test Öğrenci İki", "Test Akademisyen", new DateOnly(2026, 10, 1),
                GorusmeKonusu.DersSecimi, TalepDurumu.Bekliyor),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Ogrenci/GorusmeTalebi", null, Roller.Ogrenci),
        new("/Danisman/GorusmeNotlari", null, Roller.Danisman),
        new("/Baskan/DanismanAtama", null, Roller.BolumBaskani),
    ];
}

/// <summary>Danışmanlık'a girebilen rollerin test kullanıcıları (Development test kullanıcılarındaki gibi).</summary>
internal static class DanismanlikKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.Danisman] = new("hoca.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Akademisyen, Roller.Danisman),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
    };
}
