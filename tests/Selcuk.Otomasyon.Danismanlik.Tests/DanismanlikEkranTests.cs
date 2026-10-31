using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Danismanlik.Tests;

/// <summary>
/// Danışmanlık'a özgü testler: danışman yalnızca kendi öğrencilerini ve notlarını, öğrenci yalnızca kendi kaydını ve
/// taleplerini, bölüm başkanı kendi bölümünü görür; ayardan gelen şartlar (görüşme aralığı, danışman başına öğrenci).
/// </summary>
public sealed class DanismanlikEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = DanismanlikKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "danismanlik");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-danismanlik=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Danisman_yalnizca_kendi_ogrencilerini_ve_notlarini_goruyor()
    {
        var hoca = Kullanici(Roller.Danisman);
        Assert.Equal(["203011001", "203011002", "ORN-0101", "ORN-0102", "ORN-0103"], Kayitlar(await Html(hoca, "/Danisman/Ogrencilerim")));
        var notlar = await Html(hoca, "/Danisman/GorusmeNotlari");
        Assert.Equal(["N26-03", "N26-02", "N26-01", "N25-03", "N25-02", "N25-01", "N24-01"], Kayitlar(notlar));
        Assert.DoesNotContain("Test Öğrenci Üç", notlar);

        var koordinator = _factory.Istemci("koordinator.test", roller: [Roller.Akademisyen, Roller.Danisman]);
        Assert.Equal(["203011003", "ORN-0104", "ORN-0105", "ORN-0106"], Kayitlar(await Html(koordinator, "/Danisman/Ogrencilerim")));
    }

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_kaydini_ve_taleplerini_goruyor()
    {
        var ogrenci = Kullanici(Roller.Ogrenci);
        var danisman = await Html(ogrenci, "/Ogrenci/Danismanim");
        Assert.Equal(["203011002"], Kayitlar(danisman));
        Assert.Contains("Test Akademisyen", danisman);

        var talepler = await Html(ogrenci, "/Ogrenci/GorusmeTalebi");
        Assert.Equal(["T26-01", "T25-01"], Kayitlar(talepler));
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Danışman onayı</span>", talepler);

        var reddedilen = await Html(_factory.Istemci("203011003", roller: Roller.Ogrenci), "/Ogrenci/GorusmeTalebi");
        Assert.Equal(["T26-04"], Kayitlar(reddedilen));
        Assert.Contains("data-durum=\"olumsuz\">Reddedildi</span>", reddedilen);

        var danismansiz = await Html(_factory.Istemci("ORN-0111", roller: Roller.Ogrenci), "/Ogrenci/Danismanim");
        Assert.Contains("Henüz atanmadı", danismansiz);
    }

    [Fact]
    public async Task Bolum_baskani_kendi_bolumunu_ve_danisman_kontenjanini_goruyor()
    {
        var html = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/DanismanAtama");

        var kayitlar = Kayitlar(html);
        Assert.Equal(["ORN-0111", "ORN-0112"], kayitlar.Take(2));
        Assert.Equal(15, kayitlar.Count);
        Assert.DoesNotContain("ORN-0201", kayitlar);
        Assert.Matches("<th scope=\"row\">Test Akademisyen</th>\\s*<td class=\"sayi\">5 / 5</td>\\s*<td><span [^>]*>Dolu</span>", html);

        var eem = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["ORN-0201", "ORN-0202"], Kayitlar(await Html(eem, "/Baskan/DanismanAtama")));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_gorusme_uyarisi_ve_kontenjan_degisiyor()
    {
        using var kisa = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["DanismanlikSartlari:GorusmeAraligiHedefiGun"] = "1",
            ["DanismanlikSartlari:DanismanBasinaEnFazlaOgrenci"] = "4",
        });
        var gecikti = await Html(Kullanici(Roller.Danisman, kisa), "/Danisman/Ogrencilerim");
        Assert.Contains("Görüşme aralığı hedefi: <strong>1 gün</strong>", gecikti);
        Assert.Contains("Görüşme gecikti", Satir(gecikti, "203011001"));
        var asildi = await Html(Kullanici(Roller.BolumBaskani, kisa), "/Baskan/DanismanAtama");
        Assert.Matches("<th scope=\"row\">Test Akademisyen</th>\\s*<td class=\"sayi\">5 / 4</td>\\s*<td><span [^>]*>Aşıldı</span>", asildi);

        using var uzun = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["DanismanlikSartlari:GorusmeAraligiHedefiGun"] = "100000" });
        var guncel = await Html(Kullanici(Roller.Danisman, uzun), "/Danisman/Ogrencilerim");
        Assert.Contains("Güncel", Satir(guncel, "203011001"));
        // Hiç görüşmesi olmayan öğrenci her durumda uyarı alır.
        Assert.Contains("Görüşme gecikti", Satir(guncel, "ORN-0103"));
        Assert.Contains("Şart değerleri örnektir.", guncel);
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["DanismanlikSartlari:DanismanBasinaEnFazlaOgrenci"] = "0" });

        Assert.Contains("'DanismanlikSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }
}
