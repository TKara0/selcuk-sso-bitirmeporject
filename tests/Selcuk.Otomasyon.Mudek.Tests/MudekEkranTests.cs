using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Mudek.Tests;

/// <summary>
/// MÜDEK'e özgü testler: akademisyen yalnızca kendi derslerinin çıktılarını görür; komisyon, başkan ve sekreter kendi
/// bölümünü görür; program çıktısı gerçekleşmesi, eksikler, dönem raporu ve ayardan gelen eşik/hedef.
/// </summary>
public sealed class MudekEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = MudekKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "mudek");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-mudek=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Akademisyen_yalnizca_kendi_derslerinin_ciktilarini_goruyor()
    {
        Assert.Equal(["BM101-1", "BM101-2", "BM101-3", "BM201-1", "BM201-2"],
            Kayitlar(await Html(Kullanici(Roller.Akademisyen), "/Akademisyen/VeriGirisi")));

        // Development'taki mudek.test gibi: akademisyen + komisyon üyesi; iki rol başlığı ve yalnızca kendi dersi.
        var mudek = _factory.Istemci("mudek.test", roller: [Roller.Akademisyen, Roller.MudekKomisyonu, Roller.BolumKuruluUyesi]);
        var anaSayfa = await Html(mudek, "/");
        Assert.Contains("<p class=\"menu-grubu\" id=\"menu-grubu-0\">Akademisyen</p>", anaSayfa);
        Assert.Equal(["BM303-1", "BM303-2"], Kayitlar(await Html(mudek, "/Akademisyen/VeriGirisi")));
    }

    [Fact]
    public async Task Program_ciktilari_gerceklesme_ve_bolum_suzmesi()
    {
        var html = await Html(Kullanici(Roller.MudekKomisyonu), "/Komisyon/ProgramCiktilari");

        Assert.Equal([.. Enumerable.Range(1, 11).Select(i => $"PÇ-{i}")], Kayitlar(html));
        Assert.Contains("%75", Satir(html, "PÇ-1"));
        Assert.Contains("Hedefte", Satir(html, "PÇ-1"));
        Assert.Contains("%69", Satir(html, "PÇ-3"));
        Assert.Contains("Hedef altında", Satir(html, "PÇ-3"));
        Assert.Contains("Veri yok", Satir(html, "PÇ-11"));
        Assert.Contains("Örnek program çıktısı 1", html);

        var eksikler = await Html(Kullanici(Roller.MudekKomisyonu), "/Komisyon/Eksikler");
        Assert.Equal(["BM101-3", "BM201-2", "BM301-2", "BM403-1", "PÇ-3", "PÇ-5", "PÇ-8", "PÇ-11"], Kayitlar(eksikler));
        Assert.DoesNotContain("EEM101", eksikler);

        var arsiv = Kayitlar(await Html(Kullanici(Roller.BolumSekreteri), "/Sekreter/Arsiv"));
        Assert.Equal(["B24-01", "B25-01", "B26-01", "B26-02", "B26-03", "B26-04"], arsiv.Order());

        var eem = _factory.Istemci("eem.komisyon", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.MudekKomisyonu]);
        var eemHtml = await Html(eem, "/Komisyon/ProgramCiktilari");
        Assert.Contains("%70", Satir(eemHtml, "PÇ-1"));
        Assert.Equal(["B26-05"], Kayitlar(await Html(_factory.Istemci("eem.sekreter", bolum: OtomasyonFactory.ElektrikElektronik,
            roller: [Roller.Personel, Roller.BolumSekreteri]), "/Sekreter/Arsiv")));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_rapor_ve_eksikler_degisiyor()
    {
        var varsayilan = await Html(Kullanici(Roller.BolumBaskani), "/Rapor/Donem");
        Assert.Contains("data-ozet=\"hedefte\">7 / 11<", varsayilan);
        Assert.Contains("data-ozet=\"eksik-ders\">4<", varsayilan);
        Assert.Contains("Şart değerleri örnektir.", varsayilan);

        using var gevsek = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["MudekSartlari:ProgramCiktisiHedefOrani"] = "0.65",
            ["MudekSartlari:DersCiktisiBasariEsigi"] = "0.5",
        });
        var rapor = await Html(Kullanici(Roller.BolumBaskani, gevsek), "/Rapor/Donem");
        // PÇ-3 (%69) hedefte; BM101-3 (%55) ve BM301-2 (%58) eşikte, yalnızca verisi girilmemiş iki çıktı eksik.
        Assert.Contains("data-ozet=\"hedefte\">8 / 11<", rapor);
        Assert.Contains("data-ozet=\"eksik-ders\">2<", rapor);
        Assert.Contains("Hedefte", Satir(await Html(Kullanici(Roller.MudekKomisyonu, gevsek), "/Komisyon/ProgramCiktilari"), "PÇ-3"));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["MudekSartlari:ProgramCiktisiHedefOrani"] = "2" });

        Assert.Contains("'MudekSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }
}
