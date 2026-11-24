using Microsoft.AspNetCore.Html;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Ortak.Tests;

/// <summary>
/// Fakülte sitesinden esinlenen yerleşimin ortak parçaları: menü simgesi kataloğu (MenuSimgeleri, MenuOgesi.Simge) ve
/// gezinti yolu mantığı (GeziniYolu). Sayfa üzerindeki davranış otomasyonların test projelerindedir.
/// </summary>
public sealed class YerlesimBilesenleriTests
{
    private static readonly OtomasyonMenusu Menu = new(
    [
        new MenuOgesi("Başvuru", "/Ogrenci/Basvuru", "Açıklama", Roller.Ogrenci) { Simge = "form" },
        new MenuOgesi("Simgesiz", "/Ogrenci/Simgesiz", "Açıklama", Roller.Ogrenci),
    ]);

    private static OtomasyonBilgisi Bilgi(string? kimlikSunucusu = "https://localhost:5001/") =>
        new("Deneme Otomasyonu", "DO", "Açıklama", kimlikSunucusu);

    private static string Yaz(IHtmlContent icerik)
    {
        using var yazici = new StringWriter();
        icerik.WriteTo(yazici, System.Text.Encodings.Web.HtmlEncoder.Default);
        return yazici.ToString();
    }

    [Fact]
    public void Simge_katalogu_genel_ikonlari_ve_varsayilan_noktayi_iceriyor()
    {
        string[] gerekli = ["ana-sayfa", "form", "liste", "onay", "takvim", "belge", "arsiv", "kullanicilar", "grafik", "rapor", "bilgi", "kurul"];
        Assert.All(gerekli, ad => Assert.Contains(ad, MenuSimgeleri.Adlar));
        Assert.Contains(MenuSimgeleri.Varsayilan, MenuSimgeleri.Adlar);
        // 12-16 genel ikon + varsayılan nokta.
        Assert.InRange(MenuSimgeleri.Adlar.Count - 1, 12, 16);
        Assert.Equal(MenuSimgeleri.Adlar.Count, MenuSimgeleri.Adlar.Distinct().Count());
        Assert.All(MenuSimgeleri.Semboller, s => Assert.StartsWith("<", s.Yol));
        Assert.All(MenuSimgeleri.Semboller, s => Assert.DoesNotContain("<script", s.Yol, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null, "nokta")]
    [InlineData("", "nokta")]
    [InlineData("olmayan", "nokta")]
    [InlineData("Form", "nokta")]
    [InlineData("form", "form")]
    public void Ikonsuz_veya_bilinmeyen_simge_varsayilan_noktaya_duser(string? ad, string beklenen)
    {
        Assert.Equal(beklenen, MenuSimgeleri.Gecerli(ad));
        Assert.Equal("ms-" + beklenen, MenuSimgeleri.SembolKimligi(ad));
    }

    [Fact]
    public void Menu_ogesinin_simgesi_istege_bagli_ve_yetkiyi_degistirmiyor()
    {
        var simgeli = new MenuOgesi("A", "/A", "", Roller.Ogrenci) { Simge = "liste" };
        var simgesiz = new MenuOgesi("A", "/A", "", Roller.Ogrenci);

        Assert.Null(simgesiz.Simge);
        Assert.Equal("liste", simgeli.Simge);
        Assert.Equal(simgesiz.Roller, simgeli.Roller);
    }

    [Fact]
    public void Gezinti_yolu_ana_sayfada_iki_menu_sayfasinda_uc_ogeli()
    {
        var ana = GeziniYolu.Olustur("/Index", Bilgi(), Menu)!;
        Assert.Equal([new("Uygulamalarım", "https://localhost:5001/"), new("Deneme Otomasyonu", null)], ana);

        var ekran = GeziniYolu.Olustur("/Ogrenci/Basvuru", Bilgi(), Menu)!;
        Assert.Equal([new("Uygulamalarım", "https://localhost:5001/"), new("Deneme Otomasyonu", "/"), new("Başvuru", null)], ekran);

        // Sayfa yolu büyük/küçük harfe duyarsız eşleşir; ekran adı menü tanımından gelir.
        Assert.Equal("Simgesiz", GeziniYolu.Olustur("/ogrenci/simgesiz", Bilgi(), Menu)![^1].Metin);
        Assert.Equal("Profilim", GeziniYolu.Olustur("/Profilim", Bilgi(), Menu)![^1].Metin);
    }

    [Theory]
    [InlineData("/Hata")]
    [InlineData("/Giris")]
    [InlineData("/GirisHatasi")]
    [InlineData("/Cikis")]
    [InlineData("/ErisimReddedildi")]
    [InlineData("/Olmayan")]
    [InlineData(null)]
    public void Menude_olmayan_ve_hata_giris_cikis_sayfalarinda_gezinti_yolu_yok(string? sayfa)
    {
        Assert.Null(GeziniYolu.Olustur(sayfa, Bilgi(), Menu));
    }

    [Fact]
    public void Kimlik_sunucusu_adresi_gecerli_degilse_ilk_oge_yazilmaz()
    {
        var yol = GeziniYolu.Olustur("/Ogrenci/Basvuru", Bilgi(null), Menu)!;

        Assert.Equal(["Deneme Otomasyonu", "Başvuru"], yol.Select(o => o.Metin));
    }

    [Fact]
    public void Gezinti_yolu_ilk_h1in_hemen_altina_girer_h1_yoksa_basa()
    {
        var gezinti = new HtmlString("<nav>G</nav>");

        Assert.Equal("<h1>A</h1><nav>G</nav><p>x</p><h1>B</h1>",
            Yaz(GeziniYolu.BasligaEkle(new HtmlString("<h1>A</h1><p>x</p><h1>B</h1>"), gezinti)));
        Assert.Equal("<nav>G</nav><p>x</p>", Yaz(GeziniYolu.BasligaEkle(new HtmlString("<p>x</p>"), gezinti)));
        // Gövde kodlanmış metin içerse de bozulmaz.
        Assert.Equal("<h1>Ö &amp; Ş</h1><nav>G</nav>", Yaz(GeziniYolu.BasligaEkle(new HtmlString("<h1>Ö &amp; Ş</h1>"), gezinti)));
    }
}
