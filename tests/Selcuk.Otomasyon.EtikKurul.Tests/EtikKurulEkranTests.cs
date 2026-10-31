using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.EtikKurul.Tests;

/// <summary>
/// Etik Kurul'a özgü testler: akademisyen yalnızca kendi başvuruları, kurul üyesi yalnızca kendisine atanan dosyalar
/// (bölümden bağımsız), bölüm başkanı yalnızca kendi bölümü; ayardan gelen şartlar (gereken görüş sayısı, süre hedefi);
/// başvuru formu ve çok rollü kullanıcı.
/// </summary>
public sealed class EtikKurulEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = EtikKurulKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "etik");

    private static string Satir(string html, string id) => Regex.Match(html, $"<tr data-etik=\"{id}\">[\\s\\S]*?</tr>").Value;

    [Fact]
    public async Task Akademisyen_yalnizca_kendi_basvurularini_asamalariyla_goruyor()
    {
        var html = await Html(Kullanici(Roller.Akademisyen), "/Akademisyen/Basvurularim");

        Assert.Equal(["E26-02", "E26-01", "E25-02", "E24-03"], Kayitlar(html));
        Assert.DoesNotContain("Örnek Akademisyen", html);
        Assert.Matches("<li class=\"adim-su-an\" aria-current=\"step\">\\s*<span class=\"adim-adi\">Kurul değerlendirmesi</span>", html);
        Assert.Contains("Bilgilendirilmiş onam formu eksik.", html);

        var eem = _factory.Istemci("orn.akademisyen05", bolum: OtomasyonFactory.ElektrikElektronik, roller: Roller.Akademisyen);
        Assert.Equal(["E26-06", "E26-09", "E24-04"], Kayitlar(await Html(eem, "/Akademisyen/Basvurularim")));
    }

    [Fact]
    public async Task Kurul_uyesi_yalnizca_kendisine_atanan_dosyalari_goruyor()
    {
        var uye = Kullanici(Roller.EtikKurulUyesi);

        // Atama kayıt verisidir: etik.test'e atanan Elektrik-Elektronik dosyaları (E26-06, E24-04) da görünür.
        Assert.Equal(["E26-06", "E26-03", "E26-01", "E26-05", "E25-02", "E24-04", "E24-02", "E24-01"],
            Kayitlar(await Html(uye, "/Kurul/AtananDosyalar")));
        var gorus = await Html(uye, "/Kurul/Gorus");
        Assert.Equal(["E26-06", "E26-03", "E26-01"], Kayitlar(gorus));
        Assert.DoesNotContain("E26-08", gorus);

        // Başka bir üye başka dosyaları görür; etik.test'in dosyası (E26-01) ona görünmez.
        var baska = _factory.Istemci("orn.etikuye03", roller: Roller.EtikKurulUyesi);
        var baskaGorus = Kayitlar(await Html(baska, "/Kurul/Gorus"));
        Assert.Equal(["E26-03", "E26-08"], baskaGorus.Order());
        Assert.DoesNotContain("E26-01", baskaGorus);
    }

    [Fact]
    public async Task Gorus_formu_uyenin_mevcut_gorusunu_seciyor()
    {
        var html = await Html(Kullanici(Roller.EtikKurulUyesi), "/Kurul/Gorus");

        // E26-03'te etik.test "Uygun" demiş.
        Assert.Matches("<select id=\"gorus-E26-03\"[\\s\\S]*?<option value=\"Uygun\" selected=\"selected\">Uygun</option>", html);
        Assert.Contains("name=\"gerekce[E26-03]\"", html);
    }

    [Fact]
    public async Task Bolum_baskani_yalnizca_kendi_bolumunun_kararlarini_goruyor()
    {
        var html = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/KararListesi");

        Assert.Equal(["E26-05", "E26-07", "E25-04", "E25-03", "E25-02", "E25-01", "E24-03", "E24-02", "E24-01"], Kayitlar(html));
        Assert.DoesNotContain("E24-04", html);
        Assert.DoesNotContain("E26-09", html);
        Assert.Contains("data-ozet=\"karar\">9<", html);

        var eem = _factory.Istemci("eem.baskan", bolum: OtomasyonFactory.ElektrikElektronik, roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        Assert.Equal(["E26-09", "E24-04"], Kayitlar(await Html(eem, "/Baskan/KararListesi")));

        var bolumsuz = _factory.Istemci("bolumsuz", bolum: null, roller: Roller.BolumBaskani);
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", await Html(bolumsuz, "/Baskan/KararListesi"));
    }

    [Fact]
    public async Task Sartlar_ayardan_geliyor_degisince_gostergeler_degisiyor()
    {
        var uyeVarsayilan = await Html(Kullanici(Roller.EtikKurulUyesi), "/Kurul/AtananDosyalar");
        Assert.Contains("2 / 3", Satir(uyeVarsayilan, "E26-03"));
        Assert.Contains("Görüş bekleniyor", Satir(uyeVarsayilan, "E26-03"));
        Assert.Contains("Şart değerleri örnektir.", uyeVarsayilan);
        var baskanVarsayilan = await Html(Kullanici(Roller.BolumBaskani), "/Baskan/KararListesi");
        Assert.Contains("Hedef içinde", Satir(baskanVarsayilan, "E24-01"));

        using var farkli = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["EtikKurulSartlari:KararIcinGerekenGorusSayisi"] = "1",
            ["EtikKurulSartlari:IncelemeSuresiHedefiGun"] = "20",
        });
        var uye = await Html(Kullanici(Roller.EtikKurulUyesi, farkli), "/Kurul/AtananDosyalar");
        Assert.Contains("Karar için gereken üye görüşü: <strong>1</strong>", uye);
        Assert.Contains("Görüşler yeterli", Satir(uye, "E26-03"));
        var baskan = await Html(Kullanici(Roller.BolumBaskani, farkli), "/Baskan/KararListesi");
        Assert.Contains("İnceleme süresi hedefi: <strong>20 gün</strong>", baskan);
        // E24-01: başvurudan karara 24 gün.
        Assert.Contains("Hedefi aştı", Satir(baskan, "E24-01"));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?> { ["EtikKurulSartlari:KararIcinGerekenGorusSayisi"] = "0" });

        Assert.Contains("'EtikKurulSartlari' ayarı geçersiz", Assert.Throws<OptionsValidationException>(() => factory.CreateClient()).Message);
    }

    [Fact]
    public async Task Basvuru_formu_belge_alanli_temsili_ve_antiforgery_korumali()
    {
        var client = Kullanici(Roller.Akademisyen);
        var html = await Html(client, "/Akademisyen/Basvuru");
        Assert.Contains("<form method=\"post\" enctype=\"multipart/form-data\"", html);
        Assert.Contains("type=\"file\"", html);

        using var eksik = new MultipartFormDataContent { { new StringContent("Anket"), "arastirmaTuru" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Akademisyen/Basvuru", eksik)).StatusCode);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(OtomasyonFactory.AntiforgeryTokenAl(html)), "__RequestVerificationToken" },
            { new StringContent("Anket"), "arastirmaTuru" },
            { new ByteArrayContent("%PDF-örnek"u8.ToArray()), "belge", "onam.pdf" },
        };
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Akademisyen/Basvuru", form)).StatusCode);
        Assert.Contains(TemsiliIslem.Mesaj, await Html(client, "/Akademisyen/Basvuru"));
    }

    [Fact]
    public async Task Akademisyen_olan_kurul_uyesi_iki_rolun_ekranlarini_goruyor()
    {
        // Development'taki etik.test gibi: akademisyen + etik kurul üyesi.
        var client = _factory.Istemci("etik.test", roller: [Roller.Akademisyen, Roller.EtikKurulUyesi]);

        var html = await Html(client, "/");

        Assert.Contains("<p class=\"menu-grubu\" id=\"menu-grubu-0\">Akademisyen</p>", html);
        Assert.Contains("<p class=\"menu-grubu\" id=\"menu-grubu-1\">Etik kurul üyesi</p>", html);
        // Kendi başvurusu yok; atanan dosyaları var.
        Assert.Empty(Kayitlar(await Html(client, "/Akademisyen/Basvurularim")));
        Assert.NotEmpty(Kayitlar(await Html(client, "/Kurul/AtananDosyalar")));
    }
}
