using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Intibak.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Intibak.Tests;

/// <summary>Bütün otomasyonlarda aynı olan testler (<see cref="OtomasyonTemelTestleri{TProgram}"/>), İntibak'ın değerleriyle.</summary>
public sealed class IntibakTemelTestleri : OtomasyonTemelTestleri<Program>
{
    protected override string Ad => IntibakOtomasyonu.Ad;

    protected override string Simge => "İN";

    protected override string ClientId => "intibak-otomasyonu";

    protected override string CerezAdi => "IntibakOtomasyonu.Oturum";

    protected override string ApiKaynagi => "intibak-api";

    protected override string OkuKapsami => "intibak.oku";

    protected override string VeriAdi => "intibak";

    protected override string AcikVurgu => "#6B4C8A";

    protected override IReadOnlyList<MenuOgesi> Menu => IntibakOtomasyonu.Menu;

    protected override Type VeriArayuzu => typeof(IIntibakVeriKaynagi);

    protected override Type MockTuru => typeof(MockIntibakVeriKaynagi);

    protected override Type ApiTuru => typeof(ApiIntibakVeriKaynagi);

    protected override IReadOnlyDictionary<string, TestKullanicisi> Kullanicilar => IntibakKullanicilari.Hepsi;

    protected override (string Sayfa, string ApiYolu, object Yanit, string Kimlik) ApiOrnegi =>
        ("/Ogrenci/Basvurum", "api/intibak/basvurularim", new[]
        {
            new IntibakBasvurusu("API-1", "203011002", "Test Öğrenci İki", OtomasyonFactory.Teknoloji, OtomasyonFactory.Bilgisayar,
                GelisTuru.YatayGecis, "Örnek API Üniversitesi", "Bilgisayar Mühendisliği", new DateOnly(2026, 8, 10),
                IntibakDurumu.BaskanOnayinda, []),
        }, "API-1");

    protected override IReadOnlyList<TemsiliIslemOrnegi> TemsiliIslemler =>
    [
        new("/Baskan/OnayListesi", "Onayla", Roller.BolumBaskani),
        new("/Baskan/OnayListesi", "GeriGonder", Roller.BolumBaskani),
        new("/Komisyon/DersEslestirme", null, Roller.IntibakKomisyonu),
        new("/Sekreter/Raporlar", null, Roller.BolumSekreteri),
    ];
}

/// <summary>İntibak'a girebilen rollerin test kullanıcıları (kimlik sunucusunun Development test kullanıcıları gibi).</summary>
internal static class IntibakKullanicilari
{
    public static readonly IReadOnlyDictionary<string, TestKullanicisi> Hepsi = new Dictionary<string, TestKullanicisi>
    {
        [Roller.Ogrenci] = new("203011002", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji, Roller.Ogrenci),
        [Roller.IntibakKomisyonu] = new("komisyon.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.StajKomisyonu, Roller.MezuniyetKomisyonu, Roller.BolumKuruluUyesi),
        [Roller.BolumBaskani] = new("baskan.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
        [Roller.BolumSekreteri] = new("sekreter.test", OtomasyonFactory.Bilgisayar, OtomasyonFactory.Teknoloji,
            Roller.Personel, Roller.BolumSekreteri),
        [Roller.FakulteSekreteri] = new("fakultesekreter.test", null, OtomasyonFactory.Teknoloji,
            Roller.Personel, Roller.FakulteSekreteri),
    };
}
