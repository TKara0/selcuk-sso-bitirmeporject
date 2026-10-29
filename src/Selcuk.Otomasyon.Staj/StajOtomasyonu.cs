using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;
using Selcuk.Otomasyon.Staj.Veri;

namespace Selcuk.Otomasyon.Staj;

/// <summary>Staj Otomasyonu'nun adı, menüsü (rol → ekran) ve veri kaynağı.</summary>
public static class StajOtomasyonu
{
    public const string Ad = "Staj Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Stajlarım", "/Ogrenci/Stajlarim", "Staj I, Staj II ve İME kayıtların ve durumları.", Roller.Ogrenci),
        new("Staj başvurusu", "/Ogrenci/Basvuru", "Firma ve tarih bilgileriyle yeni staj başvurusu.", Roller.Ogrenci),
        new("Staj defterim", "/Ogrenci/Defter", "Günlük defter girişlerin ve yeni giriş.", Roller.Ogrenci),
        new("Sonuç", "/Ogrenci/Sonuc", "Komisyonun değerlendirmesi ve kabul edilen gün sayısı.", Roller.Ogrenci),
        new("Başvuru inceleme", "/Komisyon/BasvuruInceleme", "Bekleyen başvuruları onayla veya reddet.", Roller.StajKomisyonu),
        new("Defter değerlendirme", "/Komisyon/DefterDegerlendirme", "Teslim edilen defterler ve kabul gün sayısı.", Roller.StajKomisyonu),
        new("Yıllara göre takip", "/Komisyon/YillaraGoreTakip", "Yıl ve staj türüne göre başvuru, onay ve tamamlanma.", Roller.StajKomisyonu),
        new("Evrak takibi", "/Sekreter/EvrakTakibi", "Kabul belgesi, SGK girişi ve defter teslim durumu.", Roller.BolumSekreteri),
        new("Özet", "/Baskan/Ozet", "Bölümdeki aktif staj, bekleyen başvuru ve tamamlanan staj sayıları.", Roller.BolumBaskani),
    ];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "ST";
        secenekler.Aciklama = "Staj başvurusu, staj defteri, komisyon değerlendirmesi ve evrak takibi.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IStajVeriKaynagi, MockStajVeriKaynagi, ApiStajVeriKaynagi>();
    }

    public static string TurAdi(StajTuru tur) => tur switch
    {
        StajTuru.StajI => "Staj I",
        StajTuru.StajII => "Staj II",
        StajTuru.Ime => "İME",
        _ => tur.ToString(),
    };

    public static string DurumAdi(StajDurumu durum) => durum switch
    {
        StajDurumu.BasvuruBekliyor => "Başvuru bekliyor",
        StajDurumu.Onaylandi => "Onaylandı",
        StajDurumu.DevamEdiyor => "Devam ediyor",
        StajDurumu.DefterTeslimEdildi => "Defter teslim edildi",
        StajDurumu.Degerlendirildi => "Değerlendirildi",
        StajDurumu.Reddedildi => "Reddedildi",
        _ => durum.ToString(),
    };

    /// <summary>Durum etiketinin sınıfı: reddedilen kiremit, sonuçlanan uygulama rengi, diğerleri nötr.</summary>
    public static string DurumSinifi(StajDurumu durum) => durum switch
    {
        StajDurumu.Reddedildi => "etiket etiket-tehlike",
        StajDurumu.Degerlendirildi or StajDurumu.Onaylandi => "etiket etiket-uygulama",
        _ => "etiket etiket-notr",
    };

    public static string Tarih(DateOnly tarih) => tarih.ToString("d MMMM yyyy", Tr);

    public static readonly System.Globalization.CultureInfo Tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
}
