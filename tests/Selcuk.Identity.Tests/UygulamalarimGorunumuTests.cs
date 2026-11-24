using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Pages;
using Selcuk.TestYardimcilari;

namespace Selcuk.Identity.Tests;

/// <summary>
/// R12 arayüz düzeltmeleri (kimlik sunucusu tarafı): hesap düğmesinde görünen ad (A3), Uygulamalarım kart açıklamalarında
/// "(temsili, örnek veri)" ekinin olmaması ve bölüm otomasyonları notunun yalnızca otomasyon kartı gören kullanıcıda bir kez,
/// ilk otomasyon grubunun başlığının üstünde görünmesi (A4), Demo OBİS ve Demo Yemekhane kartlarının simgesi ve vurgusu (A5).
/// </summary>
public sealed class UygulamalarimGorunumuTests : IDisposable
{
    private const string Not = "Bölüm otomasyonları temsili ekranlardan oluşur ve örnek veri gösterir.";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Secret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private async Task KaydetAsync(OpenIddictApplicationDescriptor tanim)
    {
        Istemciler.AdresleriDogrula(tanim);
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
    }

    private static List<OpenIddictApplicationDescriptor> Otomasyonlar() =>
    [
        Istemciler.IntibakOtomasyonu(Secret()), Istemciler.StajOtomasyonu(Secret()), Istemciler.MezuniyetOtomasyonu(Secret()),
        Istemciler.CapYandalOtomasyonu(Secret()), Istemciler.DanismanlikOtomasyonu(Secret()),
        Istemciler.LisansProjeleriOtomasyonu(Secret()), Istemciler.IskurOtomasyonu(Secret()),
        Istemciler.EtikKurulOtomasyonu(Secret()), Istemciler.BolumKurulOtomasyonu(Secret()),
        Istemciler.MudekOtomasyonu(Secret()), Istemciler.SinavAnaliziOtomasyonu(Secret()),
    ];

    private async Task<string> SayfaAsync(string kullaniciAdi, string adres = "/")
    {
        var sifre = $"Aa1{Guid.NewGuid():N}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await BaslangicVerisi.TestKullanicisiOlusturAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == kullaniciAdi), sifre, NullLogger.Instance);
        }

        return await SayfaAsync(kullaniciAdi, sifre, adres);
    }

    private async Task<string> SayfaAsync(string kullaniciAdi, string sifre, string adres = "/")
    {
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        var yanit = await client.GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    private async Task<string> CssAsync() =>
        await (await _factory.HttpsIstemcisi().GetAsync("/css/site.css")).Content.ReadAsStringAsync();

    // ---------- A3: hesap düğmesinde görünen ad ----------

    [Fact]
    public async Task Hesap_dugmesi_gorunen_adi_govde_yazi_tipiyle_gosteriyor_kullanici_adi_menude_ikincil_satir()
    {
        var html = await SayfaAsync("baskan.test");

        // Düğme: ad soyad, Mono sınıfı yok.
        Assert.Contains("<span class=\"hesap-adi\">Test Bölüm Başkanı</span>", html);
        Assert.DoesNotContain("hesap-adi mono", html);
        Assert.DoesNotContain("<span class=\"hesap-adi\">baskan.test</span>", html);

        // Menü: önce görünen ad, onun altında kullanıcı adı (Mono, ikincil) ve kullanıcı türü.
        Assert.Matches(
            "<li class=\"hesap-kimligi\"><p><strong>Test Bölüm Başkanı</strong></p></li>\\s*"
            + "<li class=\"hesap-basligi\">\\s*<p><strong class=\"mono\">baskan\\.test</strong></p>\\s*<p class=\"ipucu\">Akademisyen</p>", html);

        // Ad CSS'i: düğmedeki ad uzunsa kısalır, menüdeki kullanıcı adı ikincil (normal ağırlık, soluk).
        var css = await CssAsync();
        Assert.Matches(@"\.ust-cubuk \.hesap-adi\s*\{[^}]*text-overflow:\s*ellipsis", css);
        Assert.Matches(@"\.hesap-kimligi \+ \.hesap-basligi \.mono\s*\{[^}]*color:\s*var\(--soluk\);[^}]*font-weight:\s*400", css);
        // Açılır liste üst çubuğun açık metin rengini miras alır; başlık satırları beyaz zeminde okunsun diye mürekkep rengi.
        Assert.Matches(@"\.hesap-kimligi,\s*\.hesap-basligi\s*\{[^}]*color:\s*var\(--murekkep\)", css);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Gorunen_ad_bossa_hesap_dugmesi_kullanici_adina_dusuyor(string adSoyad)
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, "Teknoloji Fakültesi", "Bilgisayar Mühendisliği");
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var kullanici = (await userManager.FindByNameAsync(kullaniciAdi))!;
            kullanici.AdSoyad = adSoyad;
            Assert.True((await userManager.UpdateAsync(kullanici)).Succeeded);
        }

        var html = await SayfaAsync(kullaniciAdi, sifre, "/Account/Hesabim");

        Assert.Contains($"<span class=\"hesap-adi\">{kullaniciAdi}</span>", html);
        // Görünen ad yoksa ad satırı eklenmez; kullanıcı adı eskisi gibi başlıkta.
        Assert.DoesNotContain("hesap-kimligi", html);
        Assert.Contains($"<li class=\"hesap-basligi\">", html);
        Assert.Contains($"<strong class=\"mono\">{kullaniciAdi}</strong>", html);
    }

    // ---------- A4: kart açıklamaları ve temsili ekran notu ----------

    [Fact]
    public async Task Kart_aciklamalarinda_temsili_ornek_veri_eki_yok()
    {
        List<OpenIddictApplicationDescriptor> tanimlar =
            [Istemciler.DemoObis(Secret()), Istemciler.DemoYemekhane(Secret()), .. Otomasyonlar()];
        foreach (var tanim in tanimlar)
        {
            var aciklama = tanim.Properties[Istemciler.AciklamaOzelligi].GetString()!;
            Assert.DoesNotContain("temsili", aciklama, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("örnek veri", aciklama, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".", aciklama);
            await KaydetAsync(tanim);
        }

        var html = await SayfaAsync("baskan.test");

        Assert.DoesNotContain("(temsili, örnek veri)", html);
        Assert.DoesNotContain("temsilî", html);
        Assert.Contains("Staj başvurusu, staj defteri, komisyon değerlendirmesi ve evrak takibi.", html);
    }

    [Fact]
    public async Task Temsili_ekran_notu_otomasyon_karti_gorende_bir_kez_ilk_otomasyon_grubunun_basliginin_ustunde()
    {
        await KaydetAsync(Istemciler.DemoObis(Secret()));
        await KaydetAsync(Istemciler.DemoYemekhane(Secret()));
        foreach (var tanim in Otomasyonlar())
        {
            await KaydetAsync(tanim);
        }

        var html = await SayfaAsync("baskan.test");

        Assert.Single(Regex.Matches(html, Regex.Escape(Not)));
        // Kampüs uygulamaları grubunun ardından, "Öğrenci işleri" (ilk otomasyon grubu) bölümünün hemen üstünde, tek satırlık p.
        Assert.Matches(
            "</section>\\s*<p class=\"ipucu otomasyon-notu\">" + Regex.Escape(Not) + "</p>\\s*"
            + "<section class=\"uygulama-grubu\" aria-labelledby=\"grup-1\">\\s*<h2 id=\"grup-1\" class=\"grup-baslik\">Öğrenci işleri</h2>", html);
        Assert.Contains("<h2 id=\"grup-0\" class=\"grup-baslik\">Kampüs uygulamaları</h2>", html);
        // Not başlık sırasını bozmaz: tek h1, grup başlıkları h2.
        Assert.Single(Regex.Matches(html, "<h1\\b"));
        Assert.DoesNotContain("<h3", html);
    }

    [Fact]
    public async Task Temsili_ekran_notu_otomasyon_karti_gormeyen_kullanicida_yok()
    {
        // Yalnızca iki demo kayıtlı: otomasyon kartı yok.
        await KaydetAsync(Istemciler.DemoObis(Secret()));
        await KaydetAsync(Istemciler.DemoYemekhane(Secret()));

        var html = await SayfaAsync("203011001");

        Assert.Contains("Demo OBİS", html);
        Assert.DoesNotContain(Not, html);
        Assert.DoesNotContain("otomasyon-notu", html);
    }

    [Fact]
    public async Task Temsili_ekran_notu_yalniz_otomasyon_grubu_olan_kullanicida_ilk_grubun_ustunde()
    {
        // Demo kartı olmayan sayfa: not yine bir kez ve ilk (tek) grubun başlığının üstünde.
        await KaydetAsync(Istemciler.StajOtomasyonu(Secret()));

        var html = await SayfaAsync("203011001");

        Assert.Single(Regex.Matches(html, Regex.Escape(Not)));
        Assert.Matches("<p class=\"ipucu otomasyon-notu\">" + Regex.Escape(Not) + "</p>\\s*<section class=\"uygulama-grubu\" aria-labelledby=\"grup-0\">", html);
    }

    [Fact]
    public void OtomasyonMu_yalnizca_otomasyonu_soneki_olan_istemci_kimliklerinde_dogru()
    {
        Assert.True(Istemciler.OtomasyonMu(Istemciler.StajOtomasyonuClientId));
        Assert.True(Istemciler.OtomasyonMu(Istemciler.CapYandalOtomasyonuClientId));
        Assert.False(Istemciler.OtomasyonMu(Istemciler.DemoObisClientId));
        Assert.False(Istemciler.OtomasyonMu(Istemciler.DemoYemekhaneClientId));
        Assert.False(Istemciler.OtomasyonMu(Istemciler.TekKapiClientId));
        Assert.False(Istemciler.OtomasyonMu(null));
    }

    // ---------- A5: Demo OBİS ve Demo Yemekhane simgesi ve vurgusu ----------

    [Fact]
    public async Task Demo_OBIS_OB_ve_Demo_Yemekhane_YM_simge_ve_vurguyla_kartta_gorunuyor()
    {
        var obis = Istemciler.DemoObis(Secret());
        var yemekhane = Istemciler.DemoYemekhane(Secret());
        Assert.Equal("OB", obis.Properties[Istemciler.SimgeOzelligi].GetString());
        Assert.Equal("obis", obis.Properties[Istemciler.VurguOzelligi].GetString());
        Assert.Equal("YM", yemekhane.Properties[Istemciler.SimgeOzelligi].GetString());
        Assert.Equal("nar", yemekhane.Properties[Istemciler.VurguOzelligi].GetString());
        Assert.Contains("obis", IndexModel.VurguRenkleri);
        Assert.Contains("nar", IndexModel.VurguRenkleri);
        await KaydetAsync(obis);
        await KaydetAsync(yemekhane);

        var html = await SayfaAsync("203011001");

        Assert.Contains("<span class=\"uygulama-simge uygulama-simge-obis\" aria-hidden=\"true\">OB</span>", html);
        Assert.Contains("<span class=\"uygulama-simge uygulama-simge-nar\" aria-hidden=\"true\">YM</span>", html);
        // Varsayılan (vurgusuz, addan üretilen) simgeler artık yok.
        Assert.DoesNotContain("<span class=\"uygulama-simge\" aria-hidden=\"true\">DO</span>", html);
        Assert.DoesNotContain("<span class=\"uygulama-simge\" aria-hidden=\"true\">DY</span>", html);
    }

    [Fact]
    public async Task Obis_ve_nar_simgelerinin_metin_zemin_kontrasti_acik_ve_koyu_temada_en_az_4_5()
    {
        var css = await CssAsync();
        var koyuBas = css.IndexOf("@media (prefers-color-scheme: dark)", css.IndexOf(".uygulama-simge-grafit", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.True(koyuBas > 0, "Koyu tema simge bloğu yok.");
        var acikCss = css[..koyuBas];
        var koyuCss = css[koyuBas..];

        foreach (var (ad, acikRenk, acikZemin, koyuRenk, koyuZemin) in new[]
        {
            ("obis", "#2A6AB0", "#E4EEF8", "#9CC7F2", "#16324D"),
            ("nar", "#9C3D54", "#F7E4E9", "#E99AAE", "#3B1E28"),
        })
        {
            Assert.Equal((acikRenk, acikZemin), Renkler(acikCss, ad));
            Assert.Equal((koyuRenk, koyuZemin), Renkler(koyuCss, ad));
            var acik = YerlesimDenetimi.Kontrast(acikRenk, acikZemin);
            var koyu = YerlesimDenetimi.Kontrast(koyuRenk, koyuZemin);
            Assert.True(acik >= 4.5, $"{ad} açık tema {acik:0.00}:1");
            Assert.True(koyu >= 4.5, $"{ad} koyu tema {koyu:0.00}:1");
        }
    }

    private static (string Renk, string Zemin) Renkler(string css, string ad)
    {
        var m = Regex.Match(css, $@"\.uygulama-simge-{ad}\s*\{{\s*color:\s*(#[0-9A-Fa-f]{{6}});\s*background:\s*(#[0-9A-Fa-f]{{6}});");
        Assert.True(m.Success, $".uygulama-simge-{ad} bloğu bulunamadı.");
        return (m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant());
    }
}
