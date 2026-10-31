using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Iskur.Tests;

/// <summary>
/// İŞKUR'a özgü testler: öğrenci yalnızca kendi devam kayıtlarını görür; İŞKUR sorumlusu kendi bölümünü görür (bölüm
/// kapsamı); ayardan gelen şartlar (aylık en fazla saat, devamsızlık uyarı eşiği).
/// </summary>
public sealed class IskurEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = IskurKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "iskur");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-iskur=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_devam_kayitlarini_ve_toplamini_goruyor()
    {
        var html = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/DevamDurumum");

        Assert.Equal(["I26-10", "I26-06", "I26-03"], Kayitlar(html));
        Assert.Contains("data-ozet=\"toplam\">147<", html);
        Assert.DoesNotContain("Test Öğrenci</", html);

        var baska = await Html(_factory.Istemci("ORN-0103", roller: Roller.Ogrenci), "/Ogrenci/DevamDurumum");
        Assert.Equal(["I26-12", "I26-04"], Kayitlar(baska));
    }

    [Fact]
    public async Task Sorumlu_yalnizca_kendi_bolumunu_goruyor()
    {
        var html = await Html(Kullanici(Roller.IskurSorumlusu), "/Sorumlu/Takip");

        var kayitlar = Kayitlar(html);
        Assert.Equal(15, kayitlar.Count);
        Assert.DoesNotContain("I25-05", kayitlar);
        Assert.DoesNotContain("I26-08", kayitlar);
        Assert.Contains("data-ozet=\"uyari\">6<", html);
        Assert.Equal(["I26-09", "I26-10", "I26-11", "I26-12"], Kayitlar(await Html(Kullanici(Roller.IskurSorumlusu), "/Sorumlu/DevamGirisi")));

        var eem = _factory.Istemci("eem.iskur", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Personel, Roller.IskurSorumlusu]);
        Assert.Equal(["I26-08", "I25-05"], Kayitlar(await Html(eem, "/Sorumlu/Takip")));

        var bolumsuz = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Personel, Roller.IskurSorumlusu]);
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", await Html(bolumsuz, "/Sorumlu/Takip"));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_uyari_rozetleri_degisiyor()
    {
        var varsayilan = await Html(Kullanici(Roller.IskurSorumlusu), "/Sorumlu/Takip");
        Assert.Contains("Uygun", Satir(varsayilan, "I26-06"));
        Assert.Contains("Uygun", Satir(varsayilan, "I26-02"));
        Assert.Contains("Aylık sınır aşıldı", Satir(varsayilan, "I26-04"));
        Assert.Contains("Devamsızlık uyarısı", Satir(varsayilan, "I26-07"));
        Assert.Contains("Şart değerleri örnektir.", varsayilan);

        using var siki = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["IskurSartlari:DevamsizlikUyariEsigi"] = "0.9",
            ["IskurSartlari:AylikEnFazlaDevamSaati"] = "50",
        });
        var html = await Html(Kullanici(Roller.IskurSorumlusu, siki), "/Sorumlu/Takip");
        // I26-06: 50 / 60 saat (%83) eşik %90'ın altında; I26-02: 60 saat sınır 50'nin üstünde.
        Assert.Contains("Devamsızlık uyarısı", Satir(html, "I26-06"));
        Assert.Contains("Aylık sınır aşıldı", Satir(html, "I26-02"));
        Assert.Contains("<strong>%90</strong>", html);
        Assert.Contains("Aylık en fazla devam saati: <strong>50</strong>", await Html(Kullanici(Roller.IskurSorumlusu, siki), "/Sorumlu/DevamGirisi"));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["IskurSartlari:DevamsizlikUyariEsigi"] = "1.5" });

        Assert.Contains("'IskurSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }
}
