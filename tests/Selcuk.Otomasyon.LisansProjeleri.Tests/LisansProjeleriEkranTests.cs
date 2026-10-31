using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.LisansProjeleri.Tests;

/// <summary>
/// Lisans Projeleri'ne özgü testler: öğrenci yalnızca kendi kaydı, akademisyen yalnızca kendi konuları ve hocası olduğu
/// projeler, bölüm başkanı kendi bölümü; ayardan gelen şartlar (tercih sayısı, hoca başına en fazla proje).
/// </summary>
public sealed class LisansProjeleriEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = LisansProjeleriKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html, string veriAdi = "proje") => OtomasyonFactory.Kayitlar(html, veriAdi);

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_kaydini_ve_asamalarini_goruyor()
    {
        var ogrenci1 = _factory.Istemci("203011001", roller: Roller.Ogrenci);
        var html = await Html(ogrenci1, "/Ogrenci/Asamalarim");
        Assert.Equal(["P26-01"], Kayitlar(html));
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Ara rapor</span>", html);
        Assert.DoesNotContain("Test Öğrenci İki", html);

        // Konusu belli öğrencide tercih formu yok.
        var secim1 = await Html(ogrenci1, "/Ogrenci/KonuSecimi");
        Assert.DoesNotContain("id=\"tercih-1\"", secim1);
        Assert.Contains("Web tabanlı kütüphane salon rezervasyonu (Test Akademisyen)", secim1);

        // Konu seçimindeki öğrencinin tercihleri seçili; açık konular yalnızca kendi bölümünden.
        var secim2 = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/KonuSecimi");
        Assert.Equal(["P26-02"], Kayitlar(secim2));
        Assert.Matches("<select id=\"tercih-1\"[\\s\\S]*?<option value=\"L-K03\" selected=\"selected\">", secim2);
        Assert.Equal(["L-K01", "L-K03", "L-K04", "L-K06"], Kayitlar(secim2, "konu"));
    }

    [Fact]
    public async Task Akademisyen_yalnizca_kendi_konularini_ve_hocasi_oldugu_projeleri_goruyor()
    {
        var hoca = Kullanici(Roller.Akademisyen);

        Assert.Equal(["P26-01", "P26-04", "P26-05", "P25-01", "P24-02"], Kayitlar(await Html(hoca, "/Akademisyen/Degerlendirme")));
        Assert.Equal(["L-K01", "L-K02"], Kayitlar(await Html(hoca, "/Akademisyen/KonuOnerisi"), "konu"));

        var baska = _factory.Istemci("orn.akademisyen01", roller: Roller.Akademisyen);
        var baskaProjeler = Kayitlar(await Html(baska, "/Akademisyen/Degerlendirme"));
        Assert.Equal(["P26-06", "P26-09", "P24-01"], baskaProjeler);
        Assert.DoesNotContain("P26-01", baskaProjeler);

        var eem = _factory.Istemci("orn.akademisyen05", bolum: OtomasyonFactory.ElektrikElektronik, roller: Roller.Akademisyen);
        Assert.Equal(["P26-11", "P24-03"], Kayitlar(await Html(eem, "/Akademisyen/Degerlendirme")));
    }

    [Fact]
    public async Task Bolum_baskani_kendi_bolumunun_eslestirmesini_ve_hoca_kontenjanini_goruyor()
    {
        var html = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/Eslestirme");

        Assert.Equal(["P26-02", "P26-10", "P26-01", "P26-03", "P26-04", "P26-05", "P26-06", "P26-07", "P26-08", "P26-09"], Kayitlar(html));
        Assert.DoesNotContain("P26-11", html);
        Assert.Matches("<th scope=\"row\">Test Akademisyen</th>\\s*<td class=\"sayi\">3 / 3</td>\\s*<td><span [^>]*>Dolu</span>", html);
        Assert.Matches("<th scope=\"row\">Örnek Akademisyen Bir</th>\\s*<td class=\"sayi\">2 / 3</td>\\s*<td><span [^>]*>Yer var</span>", html);

        var eem = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["P26-11"], Kayitlar(await Html(eem, "/Baskan/Eslestirme")));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_tercih_sayisi_ve_hoca_kontenjani_degisiyor()
    {
        var varsayilan = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/KonuSecimi");
        Assert.Equal(3, Regex.Matches(varsayilan, "<select id=\"tercih-\\d\"").Count);
        Assert.Contains("Şart değerleri örnektir.", varsayilan);

        using var farkli = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["ProjeSartlari:OgrenciBasinaTercihSayisi"] = "5",
            ["ProjeSartlari:HocaBasinaEnFazlaProje"] = "2",
        });
        var tercih = await Html(Kullanici(Roller.Ogrenci, farkli), "/Ogrenci/KonuSecimi");
        Assert.Equal(5, Regex.Matches(tercih, "<select id=\"tercih-\\d\"").Count);
        Assert.Contains("En fazla <strong>5</strong> tercih", tercih);

        var eslestirme = await Html(Kullanici(Roller.BolumBaskani, farkli), "/Baskan/Eslestirme");
        Assert.Matches("<th scope=\"row\">Test Akademisyen</th>\\s*<td class=\"sayi\">3 / 2</td>\\s*<td><span [^>]*>Aşıldı</span>", eslestirme);
        Assert.Matches("<th scope=\"row\">Örnek Akademisyen Bir</th>\\s*<td class=\"sayi\">2 / 2</td>\\s*<td><span [^>]*>Dolu</span>", eslestirme);
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["ProjeSartlari:OgrenciBasinaTercihSayisi"] = "0" });

        Assert.Contains("'ProjeSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [Fact]
    public async Task Konu_onerisi_formu_temsili()
    {
        var client = Kullanici(Roller.Akademisyen);
        var html = await Html(client, "/Akademisyen/KonuOnerisi");

        var yanit = await client.PostAsync("/Akademisyen/KonuOnerisi", new FormUrlEncodedContent(
        [
            KeyValuePair.Create("baslik", "Örnek konu"),
            KeyValuePair.Create("__RequestVerificationToken", OtomasyonFactory.AntiforgeryTokenAl(html)),
        ]));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var sonra = await Html(client, "/Akademisyen/KonuOnerisi");
        Assert.Contains(TemsiliIslem.Mesaj, sonra);
        Assert.Equal(["L-K01", "L-K02"], Kayitlar(sonra, "konu"));
    }
}
