using System.Net;
using System.Text.RegularExpressions;
using Selcuk.TestYardimcilari;
using static Selcuk.TestYardimcilari.YerlesimDenetimi;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Kimlik sunucusunda fakülte sitesinden esinlenen yerleşim: koyu lacivert üst çubuk (Test rozeti dahil) ve başlık altı
/// çizgi; gezinti yolu yalnızca Hesabım sayfalarında ("Uygulamalarım / Hesabım / Sayfa"); giriş, girişin ikinci adımı,
/// onay, çıkış, hata ve Uygulamalarım sayfalarında gezinti yolu yok; giriş sayfasında harici görsel, logo veya resmî
/// site menü metni yok.
/// </summary>
public sealed class YerlesimTests : IDisposable
{
    private const string Teknoloji = "Teknoloji Fakültesi";
    private const string Bilgisayar = "Bilgisayar Mühendisliği";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> GirisYapmisAsync()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, Teknoloji, Bilgisayar);
        var client = _factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        return client;
    }

    private static async Task<string> MetinAsync(HttpClient client, string adres)
    {
        var yanit = await client.GetAsync(adres);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    private async Task<string> CssAsync(string adres) =>
        await (await _factory.HttpsIstemcisi().GetAsync(adres)).Content.ReadAsStringAsync();

    [Fact]
    public async Task Hesabim_ve_alt_sayfalarinda_gezinti_yolu_h1in_altinda_Uygulamalarim_Hesabim_Sayfa()
    {
        var client = await GirisYapmisAsync();

        GeziniYoluDogrula(await MetinAsync(client, "/Account/Hesabim"),
            [new("Uygulamalarım", "/", false), new("Hesabım", null, true)], "/Account/Hesabim");

        foreach (var (adres, ad) in new[]
                 {
                     ("/Account/SifreDegistir", "Şifre değiştir"),
                     ("/Account/GuvenlikGecmisi", "Güvenlik geçmişi"),
                     ("/Account/IkiAdimDogrulama", "İki aşamalı doğrulama"),
                 })
        {
            GeziniYoluDogrula(await MetinAsync(client, adres),
                [new("Uygulamalarım", "/", false), new("Hesabım", "/Account/Hesabim", false), new(ad, null, true)], adres);
        }
    }

    [Fact]
    public async Task Uygulamalarim_giris_ikinci_adim_onay_cikis_ve_hata_sayfalarinda_gezinti_yolu_yok()
    {
        // Girişsiz sayfalar.
        var anonim = _factory.HttpsIstemcisi();
        foreach (var adres in new[] { "/Account/Login", "/Account/SifremiUnuttum", "/Account/AccessDenied", "/Hata" })
        {
            Assert.Null(GeziniYolu(await MetinAsync(anonim, adres)));
        }

        // Uygulamalarım (ana sayfa), hata, çıkış ve onay sayfaları.
        var client = await GirisYapmisAsync();
        var ana = await MetinAsync(client, "/");
        Assert.Contains("<h1 class=\"baslik-buyuk\">Merhaba", ana);
        Assert.Null(GeziniYolu(ana));
        Assert.Null(GeziniYolu(await MetinAsync(client, "/Hata")));
        Assert.Null(GeziniYolu(await MetinAsync(client, "/Account/AccessDenied")));
        var cikis = await client.GetAsync("/connect/logout");
        Assert.Equal(HttpStatusCode.OK, cikis.StatusCode);
        Assert.Null(GeziniYolu(WebUtility.HtmlDecode(await cikis.Content.ReadAsStringAsync())));

        await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var onay = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni()));
        Assert.Equal(HttpStatusCode.OK, onay.StatusCode);
        var onayHtml = WebUtility.HtmlDecode(await onay.Content.ReadAsStringAsync());
        Assert.Contains("<h1>Demo OBİS</h1>", onayHtml);
        Assert.Null(GeziniYolu(onayHtml));

        // Girişin ikinci adımı (2FA kodu): şifre doğru, kod bekleniyor.
        var (ikiAdimKullanici, ikiAdimSifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, Teknoloji, Bilgisayar);
        await _factory.IkiAdimAcAsync(ikiAdimKullanici);
        var ikinci = _factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(ikinci, ikiAdimKullanici, ikiAdimSifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        Assert.StartsWith("/Account/IkiAdim", giris.Headers.Location!.OriginalString);
        var ikiAdim = await MetinAsync(ikinci, giris.Headers.Location!.OriginalString);
        Assert.Contains("<h1>İki aşamalı doğrulama</h1>", ikiAdim);
        Assert.Null(GeziniYolu(ikiAdim));
    }

    [Fact]
    public async Task Ust_cubuk_ve_Test_rozeti_kontrasti_acik_ve_koyu_tema()
    {
        UstCubukKontrastiniDogrula(await CssAsync("/_content/Selcuk.Arayuz/css/selcuk.css"), await CssAsync("/css/site.css"));

        // Test rozeti ve hesap menüsü üst çubuktadır (kimlik sunucusu sayfalarında).
        var client = await GirisYapmisAsync();
        var html = (await MetinAsync(client, "/Account/Hesabim")).Replace("\r\n", " ").Replace("\n", " ");
        Assert.Matches(@"<header class=""ust-cubuk"">.*?<span class=""etiket etiket-notr"">Test</span>.*?class=""hesap-menusu""", html);
        Assert.DoesNotContain(" style=", html);
    }

    [Fact]
    public async Task Baslik_cizgisi_CSS_ile_cizilir_murekkep_renginde_ve_golge_yok()
    {
        var css = await CssAsync("/css/site.css");

        Assert.Matches(@"main h1::after,\s*\.grup-baslik::after\s*\{[^}]*content:\s*"""";[^}]*width:\s*3\.5rem;[^}]*height:\s*4px;[^}]*background:\s*var\(--murekkep\)", css);
        Assert.DoesNotMatch(@"box-shadow:\s+(?!inset\b|none\b)\S", css);
    }

    [Fact]
    public async Task Giris_sayfasinda_harici_gorsel_logo_ve_resmi_site_menusu_yok_resmi_degil_ibaresi_duruyor()
    {
        var html = await MetinAsync(_factory.HttpsIstemcisi(), "/Account/Login");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<picture", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("background-image", html, StringComparison.OrdinalIgnoreCase);
        // Bütün src, href ve action değerleri aynı kökenden (göreli) ya da sayfa içi bağlantı.
        foreach (Match m in Regex.Matches(html, @"\b(?:src|href|action)=""([^""]*)"""))
        {
            var deger = m.Groups[1].Value;
            Assert.True(deger.StartsWith('/') || deger.StartsWith('#') || deger.Length == 0, m.Value);
        }

        // Gerçek kurum sitesinin menü metinleri ve kurum amblemi yok.
        foreach (var yasak in new[] { "ÜNİVERSİTEMİZ", "AKADEMİK", "KURUMSAL", "ENSTİTÜLER", "logo" })
        {
            Assert.DoesNotContain(yasak, html, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("resmî sistem değildir", html);

        // Giriş düzeninde üst çubuk (ve gezinti yolu) yok; kapı paneli zaten koyu lacivert.
        Assert.DoesNotContain("class=\"ust-cubuk", html);
        Assert.Null(GeziniYolu(html));

        // CSS'te de harici adres yok.
        var css = await CssAsync("/css/site.css");
        Assert.DoesNotMatch(@"https?://", css);
        Assert.DoesNotContain("@import", css);
    }
}
