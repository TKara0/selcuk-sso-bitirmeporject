using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Mezuniyet.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Mezuniyet.Tests;

/// <summary>
/// Mezuniyet'e özgü testler: kapsam (öğrenci kendi kaydı, bölüm ve fakülte süzmesi), ayardan gelen şartların
/// eksikler listesine ve kontrol sonuçlarına etkisi, başvuru aşamaları ve ilişik kesme.
/// </summary>
public sealed class MezuniyetEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = MezuniyetKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "mezuniyet");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-mezuniyet=\"{id}\">[\\s\\S]*?</tr>").Value;

    // ---------- Öğrenci ve şartlar ----------

    [Fact]
    public async Task Ogrenci_kendi_durumunu_ve_ayardaki_sartlara_gore_eksiklerini_goruyor()
    {
        var html = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/Durumum");

        Assert.Equal(["M27-02"], Kayitlar(html));
        Assert.Contains("data-ozet=\"akts\">236 / 240<", html);
        Assert.Contains("data-ozet=\"gno\">2,65<", html);
        Assert.Contains("data-ozet=\"zorunlu\">5 / 6<", html);
        Assert.Contains("<li>4 AKTS eksik (236 / 240).</li>", html);
        Assert.Contains("<li>Zorunlu ders tamamlanmadı: BM401 Bitirme Projesi.</li>", html);
        Assert.Contains("<li>Staj devam ediyor.</li>", html);
        Assert.DoesNotContain("GNO 2,65", html);
        Assert.Contains("Şart değerleri örnektir.", html);
        Assert.DoesNotContain("Test Öğrenci Üç", html);

        var tamam = await Html(_factory.Istemci("203011001", roller: Roller.Ogrenci), "/Ogrenci/Durumum");
        Assert.Equal(["M27-01"], Kayitlar(tamam));
        Assert.Contains("Eksiğin yok; mezuniyet şartlarını karşılıyorsun.", tamam);
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_degisince_gostergeler_degisiyor()
    {
        using var farkli = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["MezuniyetSartlari:GerekenAkts"] = "230",
            ["MezuniyetSartlari:GnoAltSiniri"] = "2.70",
        });

        var html = await Html(Kullanici(Roller.Ogrenci, farkli), "/Ogrenci/Durumum");
        Assert.Contains("data-ozet=\"akts\">236 / 230<", html);
        Assert.DoesNotContain("AKTS eksik", html);
        Assert.Contains("<li>GNO 2,65, en az 2,70 olmalı.</li>", html);
        Assert.Matches("GNO alt sınırı:\\s*<strong>2,70</strong>", html);
        Assert.Matches("data-kontrol=\"gno\"><span [^>]*data-durum=\"olumsuz\">Karşılamıyor</span>", html);

        // Komisyonun kontrol sonuçları da aynı ayardan.
        var varsayilan = Satir(await Html(Kullanici(Roller.MezuniyetKomisyonu), "/Komisyon/Adaylar"), "M27-02");
        var siki = Satir(await Html(Kullanici(Roller.MezuniyetKomisyonu, farkli), "/Komisyon/Adaylar"), "M27-02");
        Assert.Contains("236 / 240", varsayilan);
        Assert.Contains("data-durum=\"olumlu\">Karşılıyor</span>", varsayilan);
        Assert.Contains("236 / 230", siki);
        Assert.Contains("data-durum=\"olumsuz\">Karşılamıyor</span>", siki);
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["MezuniyetSartlari:GnoAltSiniri"] = "5" });

        Assert.Contains("'MezuniyetSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [Fact]
    public async Task Basvuru_asamalari_ve_ilisik_kesme()
    {
        var basvuru = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/Basvuru");
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Komisyon kontrolü</span>", basvuru);
        Assert.Contains("data-durum=\"bekliyor\">Komisyon kontrolünde</span>", basvuru);

        var yapilmadi = await Html(_factory.Istemci("203011003", roller: Roller.Ogrenci), "/Ogrenci/Basvuru");
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Başvuru</span>", yapilmadi);

        var ilisik = await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/IlisikKesme");
        Assert.Equal(5, OtomasyonFactory.Kayitlar(ilisik, "birim").Count);
        Assert.Contains("data-durum=\"olumsuz\">Borcu var</span>", ilisik);
        Assert.Contains("İade edilmemiş 1 kitap.", ilisik);

        // Mezuniyet kaydı olmayan öğrenci: tablonun boş durumu.
        var kayitsiz = await Html(_factory.Istemci("ORN-9999", roller: Roller.Ogrenci), "/Ogrenci/IlisikKesme");
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", kayitsiz);
    }

    // ---------- Kapsam ----------

    [Fact]
    public async Task Komisyon_ve_sekreter_yalnizca_kendi_bolumunu_goruyor()
    {
        Assert.Equal(["M27-08", "M27-02", "M27-07", "M27-09"], Kayitlar(await Html(Kullanici(Roller.MezuniyetKomisyonu), "/Komisyon/Adaylar")));
        Assert.Equal(["M27-06", "M27-01", "M27-04", "M27-05"], Kayitlar(await Html(Kullanici(Roller.BolumSekreteri), "/Sekreter/DekanlikListesi")));

        var eem = _factory.Istemci("eem.komisyon", bolum: OtomasyonFactory.ElektrikElektronik,
            roller: [Roller.Akademisyen, Roller.MezuniyetKomisyonu]);
        Assert.Equal(["M27-11"], Kayitlar(await Html(eem, "/Komisyon/Adaylar")));

        var bolumsuz = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Personel, Roller.BolumSekreteri]);
        Assert.Empty(Kayitlar(await Html(bolumsuz, "/Sekreter/DekanlikListesi")));
    }

    [Fact]
    public async Task Fakulte_sekreteri_yalnizca_kendi_fakultesini_bolumlere_gore_goruyor()
    {
        var html = await Html(Kullanici(Roller.FakulteSekreteri), "/Fakulte/GelenListeler");

        Assert.Equal(["M24-01", "M24-02", "M25-01", "M25-02", "M26-01", "M27-04", "M27-05", "M25-03", "M27-10"], Kayitlar(html));
        Assert.DoesNotContain("M27-12", html);
        Assert.DoesNotContain("M26-02", html);
        Assert.Contains("data-ozet=\"dekanlikta\">3<", html);
        Assert.Contains("data-ozet=\"hazirlaniyor\">1<", html);
        Assert.Contains("data-ozet=\"hazir\">5<", html);
        Assert.Contains("<h2 id=\"tablo-elektrik-elektronik-muhendisligi\">Elektrik-Elektronik Mühendisliği</h2>", html);

        var muhendislik = _factory.Istemci("muh.sekreter", bolum: null, fakulte: OrnekMezuniyetVerisi.Muhendislik,
            roller: [Roller.Personel, Roller.FakulteSekreteri]);
        Assert.Equal(["M26-02", "M27-12"], Kayitlar(await Html(muhendislik, "/Fakulte/GelenListeler")));
    }
}
