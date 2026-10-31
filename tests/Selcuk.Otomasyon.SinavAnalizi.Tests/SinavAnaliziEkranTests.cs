using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.SinavAnalizi.Tests;

/// <summary>
/// Sınav Başarısı Analizi'ne özgü testler: akademisyen yalnızca kendi derslerini, bölüm başkanı ve MÜDEK komisyonu
/// kendi bölümünü görür; ekranlarda öğrenci düzeyinde veri yoktur; çubuklar metinli ve satır içi stilsizdir; ayardan
/// gelen geçme notu ve hedef değişince göstergeler değişir.
/// </summary>
public sealed partial class SinavAnaliziEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = SinavAnaliziKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "sinav");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-sinav=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Akademisyen_yalnizca_kendi_derslerini_goruyor()
    {
        var html = await Html(Kullanici(Roller.Akademisyen), "/Akademisyen/Derslerim");

        Assert.Equal(["S02", "S01", "S04", "S03"], Kayitlar(html));
        Assert.DoesNotContain("BM303", html);
        Assert.Contains("BM101 not dağılımı, 2025-2026 Güz", html);
        // Dağılım çubuğu <meter> ile, değeri yanında metin olarak.
        Assert.Matches("<meter min=\"0\" max=\"11\" value=\"8\" aria-hidden=\"true\"></meter> 8</span>", html);

        var mudek = _factory.Istemci("mudek.test", roller: [Roller.Akademisyen, Roller.MudekKomisyonu]);
        Assert.Equal(["S06", "S05"], Kayitlar(await Html(mudek, "/Akademisyen/Derslerim")));
    }

    [Fact]
    public async Task Bolum_baskani_kendi_bolumunu_ve_hedef_altindakileri_goruyor()
    {
        var html = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/BolumGeneli");

        var kayitlar = Kayitlar(html);
        Assert.Equal(["S04", "S12", "S11", "S09"], kayitlar.Take(4));
        Assert.Equal(4 + 14, kayitlar.Count);
        Assert.DoesNotContain("S15", kayitlar);
        Assert.Contains("data-ozet=\"hedef-altinda\">4<", html);
        Assert.Contains("%71", Satir(html, "S04"));

        var eem = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["S16", "S15"], Kayitlar(await Html(eem, "/Baskan/BolumGeneli")));
    }

    [Fact]
    public async Task Cikti_bazli_analiz_bolumun_program_ciktilarini_ozetliyor()
    {
        var html = await Html(Kullanici(Roller.MudekKomisyonu), "/Mudek/CiktiAnalizi");

        Assert.Equal([.. Enumerable.Range(1, 10).Select(i => $"PÇ-{i}")], Kayitlar(html));
        // PÇ-1: BM101, BM201 ve BM205'in ikişer dönemi.
        Assert.Matches("<td class=\"sayi\">6</td>", Satir(html, "PÇ-1"));
    }

    [Fact]
    public async Task Ekranlarda_ogrenci_duzeyinde_veri_ve_satir_ici_stil_yok()
    {
        foreach (var (rol, sayfa) in new[]
                 {
                     (Roller.Akademisyen, "/Akademisyen/Derslerim"), (Roller.BolumBaskani, "/Baskan/BolumGeneli"),
                     (Roller.MudekKomisyonu, "/Mudek/CiktiAnalizi"),
                 })
        {
            var html = await Html(Kullanici(rol), sayfa);
            Assert.DoesNotMatch(OgrenciNumarasi(), html);
            Assert.DoesNotContain("ORN-", html);
            Assert.DoesNotContain("Öğrenci İki", html);
            Assert.DoesNotContain("style=", html);
            Assert.Contains("<meter", html);
        }
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_gecme_notu_ve_hedef_degisince_durum_degisiyor()
    {
        var varsayilan = await Html(Kullanici(Roller.Akademisyen), "/Akademisyen/Derslerim");
        Assert.Contains("%83", Satir(varsayilan, "S01"));
        Assert.Contains("Hedefte", Satir(varsayilan, "S01"));
        Assert.Contains("Şart değerleri örnektir.", varsayilan);

        using var siki = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["SinavSartlari:GecmeNotu"] = "CC" });
        var html = await Html(Kullanici(Roller.Akademisyen, siki), "/Akademisyen/Derslerim");
        // S01: CC ve üstü 41 / 60 öğrenci (%68), hedef %75'in altında.
        Assert.Contains("Geçme notu: <strong>CC</strong>", html);
        Assert.Contains("%68", Satir(html, "S01"));
        Assert.Contains("Hedef altında", Satir(html, "S01"));

        using var dusuk = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["SinavSartlari:BasariHedefiOrani"] = "0.6" });
        Assert.Contains("data-ozet=\"hedef-altinda\">0<", await Html(Kullanici(Roller.BolumBaskani, dusuk), "/Baskan/BolumGeneli"));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["SinavSartlari:GecmeNotu"] = "XX" });

        Assert.Contains("'SinavSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [GeneratedRegex("\\b20301100\\d\\b")]
    private static partial Regex OgrenciNumarasi();
}
