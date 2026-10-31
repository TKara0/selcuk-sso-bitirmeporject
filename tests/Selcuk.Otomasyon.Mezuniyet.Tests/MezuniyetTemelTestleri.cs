using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Mezuniyet.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Mezuniyet.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), Mezuniyet'in değerleriyle.</summary>
public sealed class MezuniyetTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => MezuniyetOtomasyonu.Ad;

    protected override string Simge => "ME";

    protected override string ClientId => "mezuniyet-otomasyonu";

    protected override string CerezAdi => "MezuniyetOtomasyonu.Oturum";

    protected override string ApiKaynagi => "mezuniyet-api";

    protected override string OkuKapsami => "mezuniyet.oku";

    protected override string VeriAdi => "mezuniyet";

    protected override string AcikVurgu => "#8A4B1F";

    protected override IReadOnlyList<MenuOgesi> Menu => MezuniyetOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IMezuniyetVeriKaynagi);

    protected override Type MockTuru => typeof(MockMezuniyetVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiMezuniyetVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => MezuniyetKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/Durumum", "api/mezuniyet/durumum", new[]
        {
            new MezuniyetAdayi("API-1", "203011002", "Test Öğrenci İki", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar,
                "2026-2027 Güz", 240, 2.50m, [new ZorunluDers("BM401", "Bitirme Projesi", true)], StajSonucu.Tamamlandi,
                MezuniyetDurumu.KomisyonKontrolunde, new DateOnly(2026, 10, 8)),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Komisyon/Adaylar", "Onayla", Roller.MezuniyetKomisyonu),
        new("/Komisyon/Adaylar", "EksikBildir", Roller.MezuniyetKomisyonu),
        new("/Sekreter/DekanlikListesi", null, Roller.BolumSekreteri),
        new("/Ogrenci/Basvuru", null, Roller.Ogrenci),
    ];
}

/// <summary>Mezuniyet'e girebilen rollerin test kullanıcıları (kimlik sunucusunun Development test kullanıcıları gibi).</summary>
internal static class MezuniyetKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.MezuniyetKomisyonu] = new("komisyon.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.StajKomisyonu, Roller.MezuniyetKomisyonu, Roller.BolumKuruluUyesi),
        [Roller.BolumSekreteri] = new("sekreter.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Personel, Roller.BolumSekreteri),
        [Roller.FakulteSekreteri] = new("fakultesekreter.test", null, OtomasyonFactory.Teknoloji,
            Roller.Personel, Roller.FakulteSekreteri),
    };
}
