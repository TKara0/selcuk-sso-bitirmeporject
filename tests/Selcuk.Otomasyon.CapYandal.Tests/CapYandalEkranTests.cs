using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.CapYandal.Tests;

/// <summary>
/// ÇAP-Yandal'a özgü testler: kapsam (öğrenci kendi kaydı, programı açan bölüme göre süzme), GNO sıralaması ve
/// kontenjan göstergesi, devam GNO uyarısı, ayardan gelen şartlar (GNO alt sınırı, başarı sırası, kontenjan) ve
/// öğrenci formu.
/// </summary>
public sealed class CapYandalEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = CapYandalKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "cap");

    private static string Satir(string html, string oznitelik) =>
        Regex.Match(html, $"<tr {oznitelik}>[\\s\\S]*?</tr>").Value;

    // ---------- Öğrenci ----------

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_basvurularini_ve_ilerlemesini_goruyor()
    {
        var ogrenci2 = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/Basvurularim");
        Assert.Equal(["C26-10"], Kayitlar(ogrenci2));
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Bölüm başkanı onayı</span>", ogrenci2);
        Assert.Empty(Kayitlar(await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/Ilerleme")));

        var ogrenci3 = _factory.Istemci("203011003", roller: Roller.Ogrenci);
        var basvurular3 = await Html(ogrenci3, "/Ogrenci/Basvurularim");
        Assert.Equal(["C26-12", "C26-11"], Kayitlar(basvurular3));
        Assert.Contains("data-durum=\"olumsuz\">Reddedildi</span>", basvurular3);

        var ilerleme = await Html(_factory.Istemci("203011001", roller: Roller.Ogrenci), "/Ogrenci/Ilerleme");
        Assert.Equal(["C25-04"], Kayitlar(ilerleme));
        Assert.Contains("data-ozet=\"alinan\">3<", ilerleme);
        Assert.Contains("data-ozet=\"kalan\">4<", ilerleme);
        Assert.Contains("data-ozet=\"gno\">2,83<", ilerleme);
        Assert.Contains("<h3 id=\"kalan-C25-04\">Kalan dersler</h3>", ilerleme);
    }

    [Fact]
    public async Task Basvuru_sart_gostergesi_ve_temsili_form()
    {
        var client = Kullanici(Roller.Ogrenci);
        var html = await Html(client, "/Ogrenci/Basvuru");

        // 203011002: GNO 2,80, sıra 31 / 80 (%38,8). ÇAP: GNO ≥ 3,00 ve ilk %20; yandal: GNO ≥ 2,50.
        Assert.Contains("data-ozet=\"gno\">2,80<", html);
        Assert.Contains("data-ozet=\"sira\">31 / 80<", html);
        var cap = Satir(html, "data-cap-sart=\"cap\"");
        Assert.Contains("En az 3,00", cap);
        Assert.Contains("İlk %20 (sen %38,8)", cap);
        Assert.Contains("Şartları karşılamıyorsun", cap);
        Assert.Contains("Başvurabilirsin", Satir(html, "data-cap-sart=\"yandal\""));
        Assert.Contains("Şart değerleri örnektir.", html);
        // Kendi anadal bölümünün programları listelenmez.
        Assert.DoesNotContain("value=\"CAP-BM\"", html);
        Assert.Contains("value=\"CAP-EEM\"", html);

        var yanit = await client.PostAsync("/Ogrenci/Basvuru", new FormUrlEncodedContent(
            [KeyValuePair.Create("tur", "Cap"), KeyValuePair.Create("__RequestVerificationToken", OtomasyonFactory.AntiforgeryTokenAl(html))]));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Contains(TemsiliIslem.Mesaj, await Html(client, "/Ogrenci/Basvuru"));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_GNO_alt_siniri_ve_kontenjan_degisince_gosterge_degisiyor()
    {
        using var farkli = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["CapYandalSartlari:Cap:BasvuruGnoAltSiniri"] = "2.75",
            ["CapYandalSartlari:Cap:BasariYuzdesiUstSiniri"] = "40",
            ["CapYandalSartlari:Cap:Kontenjan"] = "2",
        });

        var cap = Satir(await Html(Kullanici(Roller.Ogrenci, farkli), "/Ogrenci/Basvuru"), "data-cap-sart=\"cap\"");
        Assert.Contains("En az 2,75", cap);
        Assert.Contains("İlk %40", cap);
        Assert.Contains("Başvurabilirsin", cap);

        // Kontenjan 4 iken GNO'su üçüncü olan C26-03 kontenjan içinde, 2 iken yedek.
        var varsayilan = await Html(Kullanici(Roller.CapYandalKoordinatoru), "/Koordinator/GelenBasvurular");
        Assert.Contains("data-kontenjan=\"cap\">4<", varsayilan);
        Assert.Contains("Kontenjan içinde", Satir(varsayilan, "data-cap=\"C26-03\""));
        var dar = await Html(Kullanici(Roller.CapYandalKoordinatoru, farkli), "/Koordinator/GelenBasvurular");
        Assert.Contains("data-kontenjan=\"cap\">2<", dar);
        Assert.Contains("data-ozet=\"kontenjan-CAP-BM\">2<", dar);
        Assert.Contains("Yedek", Satir(dar, "data-cap=\"C26-03\""));
        Assert.Contains("Kontenjan içinde", Satir(dar, "data-cap=\"C26-01\""));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["CapYandalSartlari:Yandal:Kontenjan"] = "0" });

        Assert.Contains("'CapYandalSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    // ---------- Koordinatör, başkan, sekreter ----------

    [Fact]
    public async Task Koordinator_kendi_bolumunun_programlarini_GNO_sirasiyla_kontenjan_gostergesiyle_goruyor()
    {
        var html = await Html(Kullanici(Roller.CapYandalKoordinatoru), "/Koordinator/GelenBasvurular");

        Assert.Equal(["C26-07", "C26-01", "C26-03", "C26-09", "C26-02", "C26-04", "C26-08", "C26-05", "C26-06"], Kayitlar(html));
        Assert.DoesNotContain("C26-13", html);
        Assert.Contains("Yedek", Satir(html, "data-cap=\"C26-02\""));
        Assert.Contains("data-ozet=\"basvuru-CAP-BM\">6<", html);

        // Elektrik-Elektronik'in koordinatörü test öğrencilerinin başvurularını görür.
        var eem = _factory.Istemci("eem.koordinator", bolum: OtomasyonFactory.ElektrikElektronik,
            roller: [Roller.Akademisyen, Roller.CapYandalKoordinatoru]);
        Assert.Equal(["C26-13", "C26-10", "C26-12"], Kayitlar(await Html(eem, "/Koordinator/GelenBasvurular")));
    }

    [Fact]
    public async Task Ogrenci_takibi_devam_sartinin_altindakileri_uyariyor()
    {
        var html = await Html(Kullanici(Roller.CapYandalKoordinatoru), "/Koordinator/OgrenciTakibi");

        Assert.Equal(["C24-01", "C25-01", "C25-03", "C24-02", "C25-02"], Kayitlar(html));
        Assert.Contains("data-ozet=\"uyari\">2<", html);
        Assert.Contains("Uyarı: GNO şartın altında", Satir(html, "data-cap=\"C25-03\""));
        Assert.Contains("Uyarı: GNO şartın altında", Satir(html, "data-cap=\"C25-02\""));
        Assert.Contains("Şartı karşılıyor", Satir(html, "data-cap=\"C24-02\""));
        Assert.DoesNotContain("C25-04", html);
    }

    [Fact]
    public async Task Baskan_ve_sekreter_yalnizca_kendi_bolumunu_goruyor()
    {
        Assert.Equal(["C26-03", "C26-09", "C26-05"], Kayitlar(await Html(Kullanici(Roller.BolumBaskani), "/Baskan/BasvuruOnayi")));
        var eemBaskan = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik,
            roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["C26-10"], Kayitlar(await Html(eemBaskan, "/Baskan/BasvuruOnayi")));

        var evrak = await Html(Kullanici(Roller.BolumSekreteri), "/Sekreter/Evrak");
        Assert.Equal(["C26-07", "C26-08", "C24-01", "C24-02", "C25-01", "C25-02", "C25-03"], Kayitlar(evrak));
        Assert.Equal(2, Regex.Matches(Satir(evrak, "data-cap=\"C26-08\""), "data-durum=\"olumsuz\">Eksik</span>").Count);

        var bolumsuz = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Akademisyen, Roller.CapYandalKoordinatoru]);
        var bos = await Html(bolumsuz, "/Koordinator/OgrenciTakibi");
        Assert.Empty(Kayitlar(bos));
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", bos);
    }
}
