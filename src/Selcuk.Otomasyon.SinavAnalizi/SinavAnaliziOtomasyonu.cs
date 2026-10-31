using System.Globalization;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.SinavAnalizi.Sozlesme;
using Selcuk.Otomasyon.SinavAnalizi.Veri;

namespace Selcuk.Otomasyon.SinavAnalizi;

/// <summary>
/// Sınav şartları (appsettings "SinavSartlari"; değerler örnektir, gerçek yönetmelik değildir): geçme notu (bu harf ve
/// üstü geçer) ve başarı hedefi oranı (geçme oranı bunun altındaysa ders hedef altında).
/// </summary>
public sealed class SinavSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "SinavSartlari";

    public string GecmeNotu { get; set; } = "DD";

    public decimal BasariHedefiOrani { get; set; } = 0.75m;

    private int GecmeSirasi => OrnekSinavAnaliziVerisi.Harfler.Select(h => h.Harf).ToList().IndexOf(GecmeNotu);

    public decimal GecmeOrani(DersSonucu d)
    {
        var gecen = d.NotDagilimi.Where(h => SinavAnaliziOtomasyonu.Sira(h.Harf) <= GecmeSirasi).Sum(h => h.Sayi);
        return d.OgrenciSayisi == 0 ? 0 : (decimal)gecen / d.OgrenciSayisi;
    }

    public bool HedefAltinda(DersSonucu d) => GecmeOrani(d) < BasariHedefiOrani;

    public DurumRozeti Rozet(decimal gecmeOrani) =>
        gecmeOrani >= BasariHedefiOrani ? new("Hedefte", DurumTuru.Olumlu) : new("Hedef altında", DurumTuru.Olumsuz);

    public string? Hata() =>
        GecmeSirasi < 0 ? "GecmeNotu bir harf notu olmalı (AA … FF)."
        : BasariHedefiOrani is <= 0 or > 1 ? "BasariHedefiOrani 0 ile 1 arasında olmalı."
        : null;
}

/// <summary>Sınav Başarısı Analizi Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class SinavAnaliziOtomasyonu
{
    public const string Ad = "Sınav Başarısı Analizi Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Kendi derslerim", "/Akademisyen/Derslerim", "Derslerinin not dağılımı, ortalaması ve geçme oranı.", Roller.Akademisyen),
        new("Bölüm geneli", "/Baskan/BolumGeneli", "Bölümdeki derslerin karşılaştırması ve hedef altında kalanlar.", Roller.BolumBaskani),
        new("Çıktı bazlı analiz", "/Mudek/CiktiAnalizi", "Program çıktısına bağlı derslerin ortalama başarısı.", Roller.MudekKomisyonu),
    ];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "SA";
        secenekler.Aciklama = "Derslerin sınav başarısının toplu analizi: not dağılımı, geçme oranı ve hedefler.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<ISinavAnaliziVeriKaynagi, MockSinavAnaliziVeriKaynagi, ApiSinavAnaliziVeriKaynagi>();
        secenekler.Ayarlar<SinavSartlari>(SinavSartlari.Bolum);
    }

    public static int Sira(string harf) => OrnekSinavAnaliziVerisi.Harfler.Select(h => h.Harf).ToList().IndexOf(harf);

    /// <summary>4'lük ortalama.</summary>
    public static decimal Ortalama(DersSonucu d) => d.OgrenciSayisi == 0 ? 0
        : d.NotDagilimi.Sum(h => h.Sayi * OrnekSinavAnaliziVerisi.Harfler[Sira(h.Harf)].Katsayi) / d.OgrenciSayisi;

    public static string Sayi(decimal deger) => deger.ToString("0.00", Tr);

    public static string Yuzde(decimal oran) => "%" + Math.Round(oran * 100).ToString("0", Tr);

    public static int YuzdeSayisi(decimal oran) => (int)Math.Round(oran * 100);

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
