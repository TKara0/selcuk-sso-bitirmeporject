using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.BolumKurul.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.BolumKurul.Tests;

/// <summary>
/// Bölüm Kurul Karar'a özgü testler: bölüm süzmesi (başkan, üye, sekreter), fakülte süzmesi (fakülte sekreteri,
/// başka fakülte), oylama göstergeleri ve ayardan gelen yeter sayı ile karar çoğunluğu.
/// </summary>
public sealed class BolumKurulEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = BolumKurulKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "kurul");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-kurul=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Bolum_rolleri_yalnizca_kendi_bolumunu_goruyor()
    {
        Assert.Equal(["T26-04", "T26-03", "T26-02", "T26-01", "T25-03", "T25-02", "T25-01", "T24-03", "T24-02", "T24-01"],
            Kayitlar(await Html(Kullanici(Roller.BolumBaskani), "/Baskan/Toplantilar")));
        Assert.Equal(["T26-03", "T26-04"], Kayitlar(await Html(Kullanici(Roller.BolumKuruluUyesi), "/Uye/Gundem")));

        var arsiv = Kayitlar(await Html(Kullanici(Roller.BolumSekreteri), "/Sekreter/Arsiv"));
        Assert.Contains("T26-02-1", arsiv);
        Assert.Contains("T24-03", arsiv);
        Assert.DoesNotContain("T25-02-2", arsiv);
        Assert.DoesNotContain(arsiv, id => id.StartsWith("TE", StringComparison.Ordinal) || id.StartsWith("TM", StringComparison.Ordinal));

        var eem = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["TE26-02", "TE26-01", "TE25-01"], Kayitlar(await Html(eem, "/Baskan/Toplantilar")));

        var bolumsuz = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Akademisyen, Roller.BolumKuruluUyesi]);
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", await Html(bolumsuz, "/Uye/Gundem"));
    }

    [Fact]
    public async Task Fakulte_sekreteri_yalnizca_kendi_fakultesinin_kararlarini_bolumlere_gore_goruyor()
    {
        var html = await Html(Kullanici(Roller.FakulteSekreteri), "/Fakulte/GelenKararlar");

        var kararlar = Kayitlar(html);
        Assert.Contains("T26-02-1", kararlar);
        Assert.Contains("TE26-01-2", kararlar);
        Assert.DoesNotContain(kararlar, id => id.StartsWith("TM", StringComparison.Ordinal));
        Assert.Contains("<h2 id=\"tablo-bilgisayar-muhendisligi\">Bilgisayar Mühendisliği</h2>", html);
        Assert.Contains("<h2 id=\"tablo-elektrik-elektronik-muhendisligi\">Elektrik-Elektronik Mühendisliği</h2>", html);
        Assert.Contains("data-ozet=\"bolum\">2<", html);

        var muhendislik = _factory.Istemci("muh.sekreter", bolum: null, fakulte: OrnekBolumKurulVerisi.Muhendislik,
            roller: [Roller.Personel, Roller.FakulteSekreteri]);
        Assert.Equal(["TM26-02-1", "TM26-01-1"], Kayitlar(await Html(muhendislik, "/Fakulte/GelenKararlar")));
    }

    [Fact]
    public async Task Oylama_oy_sayilarini_yeter_sayiyi_ve_kabul_cogunlugunu_gosteriyor()
    {
        var html = await Html(Kullanici(Roller.BolumKuruluUyesi), "/Uye/Oylama");

        Assert.Equal(["T26-03-1", "T26-03-2"], Kayitlar(html));
        Assert.Contains("data-ozet=\"katilim-T26-03\">7 / 11<", html);
        Assert.Contains("data-ozet=\"gereken-T26-03\">6<", html);
        Assert.Contains("Toplantı yeter sayısı sağlandı", html);
        Assert.Contains("4 kabul, 2 ret, 1 çekimser", Satir(html, "T26-03-1"));
        Assert.Contains("Kabul çoğunluğu var", Satir(html, "T26-03-1"));
        Assert.Contains("Kabul çoğunluğu yok", Satir(html, "T26-03-2"));
        Assert.Contains("Şart değerleri örnektir.", html);
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_yeter_sayi_ve_cogunluk_gostergesi_degisiyor()
    {
        using var farkli = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["ToplantiSartlari:YeterSayiOrani"] = "0.7",
            ["ToplantiSartlari:KararCogunluguOrani"] = "0.6",
        });

        var html = await Html(Kullanici(Roller.BolumKuruluUyesi, farkli), "/Uye/Oylama");

        // 7 katılan, 11 üyenin %70'inden (7,7) fazla değil; 4 kabul, 7 katılanın %60'ından (4,2) fazla değil.
        Assert.Contains("data-ozet=\"gereken-T26-03\">8<", html);
        Assert.Contains("Toplantı yeter sayısı sağlanmadı", html);
        Assert.Contains("Kabul çoğunluğu yok", Satir(html, "T26-03-1"));
        Assert.Contains("<strong>%70</strong>", html);
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["ToplantiSartlari:YeterSayiOrani"] = "1.5" });

        Assert.Contains("'ToplantiSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [Fact]
    public async Task Baskanin_formlari_yalnizca_planlanan_toplantilara_madde_ekletir()
    {
        var html = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/Toplantilar");

        var secim = Regex.Match(html, "<select id=\"toplanti\"[\\s\\S]*?</select>").Value;
        Assert.Contains("value=\"T26-03\"", secim);
        Assert.Contains("value=\"T26-04\"", secim);
        Assert.DoesNotContain("value=\"T26-02\"", secim);
        Assert.Contains("action=\"/Baskan/Toplantilar?handler=YeniToplanti\"", html);
    }
}
