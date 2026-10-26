using System.Net;
using System.Text.RegularExpressions;
using Selcuk.Arayuz;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Pages.Account;
using Selcuk.Identity.Pages.Connect;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Ortak tasarım sistemi (Selcuk.Arayuz): statik dosyalar _content altından aynı kökenden sunulur (CSP font-src
/// default-src 'self''e düşer, gevşetilmez), giriş sayfasının erişilebilirlik temeli ve Uygulamalarım boş durumu.
/// </summary>
public sealed partial class ArayuzTests : IDisposable
{
    private const string Kok = "/_content/Selcuk.Arayuz/";

    private static readonly string[] YaziTipleri =
    [
        "atkinson-hyperlegible-next-latin-400-normal.woff2",
        "atkinson-hyperlegible-next-latin-ext-400-normal.woff2",
        "atkinson-hyperlegible-next-latin-700-normal.woff2",
        "atkinson-hyperlegible-next-latin-ext-700-normal.woff2",
        "atkinson-hyperlegible-mono-latin-400-normal.woff2",
        "atkinson-hyperlegible-mono-latin-ext-400-normal.woff2",
    ];

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Yazi_tipleri_css_ve_betik_content_altindan_200_donuyor_ve_csp_disinda_kaynak_yok()
    {
        var client = _factory.HttpsIstemcisi();

        // CSP gevşetilmedi: font-src ve style-src ayrıca tanımlı değil, default-src 'self' geçerli.
        var giris = await client.GetAsync("/Account/Login");
        var csp = giris.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Equal(GuvenlikBasliklari.ContentSecurityPolicy, csp);
        Assert.Contains("default-src 'self'", csp);
        Assert.DoesNotContain("font-src", csp);
        Assert.DoesNotContain("style-src", csp);
        Assert.DoesNotContain("script-src", csp);

        var css = await client.GetAsync(Kok + "css/selcuk.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        var cssMetni = await css.Content.ReadAsStringAsync();

        // CSS'teki her url() göreli (aynı köken) ve sunuluyor; harici adres veya @import yok.
        var adresler = CssAdresi().Matches(cssMetni).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(YaziTipleri.Order(), adresler.Select(a => a.Replace("../fonts/", "")).Distinct().Order());
        Assert.DoesNotContain("@import", cssMetni);
        Assert.DoesNotMatch("https?://", cssMetni);

        foreach (var dosya in YaziTipleri)
        {
            var yanit = await client.GetAsync(Kok + "fonts/" + dosya);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Equal("font/woff2", yanit.Content.Headers.ContentType?.MediaType);
            var bas = (await yanit.Content.ReadAsByteArrayAsync())[..4];
            Assert.Equal("wOF2"u8.ToArray(), bas);
        }

        foreach (var dosya in new[] { "fonts/OFL-AtkinsonHyperlegibleNext.txt", "fonts/OFL-AtkinsonHyperlegibleMono.txt", "js/selcuk.js" })
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Kok + dosya)).StatusCode);
        }

        // Sayfadaki stil, betik ve ön yükleme bağlantıları da aynı kökenden.
        var html = await giris.Content.ReadAsStringAsync();
        var kaynaklar = SayfaKaynagi().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.Contains(kaynaklar, k => k.StartsWith(Kok + "css/selcuk.css", StringComparison.Ordinal));
        Assert.Contains(kaynaklar, k => k.StartsWith(Kok + "js/selcuk.js", StringComparison.Ordinal));
        Assert.All(kaynaklar, k => Assert.StartsWith("/", k));
        Assert.All(kaynaklar, k => Assert.False(k.StartsWith("//", StringComparison.Ordinal), k));
    }

    [Fact]
    public async Task Giris_sayfasinda_her_alanin_etiketi_ve_tek_h1_var()
    {
        var html = await (await _factory.HttpsIstemcisi().GetAsync("/Account/Login")).Content.ReadAsStringAsync();

        Assert.Single(H1().Matches(html));
        Assert.Contains("<title>Giriş yap — Tek Şifre Giriş</title>", WebUtility.HtmlDecode(html));

        var alanlar = GorunurAlan().Matches(html).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(["Input_KullaniciAdi", "Input_Sifre"], alanlar);
        foreach (var id in alanlar)
        {
            Assert.Matches($"<label[^>]* for=\"{id}\"[^>]*>[^<]+</label>", html);
        }

        // Göster/gizle düğmesi erişilebilir adlı ve aria-pressed'li; dekoratif kapı işareti ekran okuyucudan gizli.
        Assert.Matches("<button type=\"button\" class=\"sifre-goster\" aria-label=\"Şifreyi göster\" aria-pressed=\"false\"",
            WebUtility.HtmlDecode(html));
        Assert.Contains("<svg class=\"kapi-buyuk\"", html);
        Assert.Matches("<svg class=\"kapi-buyuk\"[^>]* aria-hidden=\"true\"", html);
    }

    [Fact]
    public async Task Uygulamalarim_uygulama_yokken_bos_durum_metnini_gosteriyor()
    {
        // İstemci kaydedilmedi: kullanıcının görebileceği uygulama yok.
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);

        var yanit = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("Henüz erişebildiğin bir uygulama yok. Yetki için birim yöneticine başvur.", html);
        Assert.DoesNotContain("class=\"satirlar uygulamalar\"", html);
        Assert.Single(H1().Matches(html));
    }

    [Fact]
    public void Eposta_html_surumu_ayni_baslikla_harici_kaynaksiz_ve_kodlanmis()
    {
        var baglanti = new Uri("https://localhost:5001/Account/SifreSifirla?kullanici=1&kod=abc");
        var mesaj = EpostaSablonlari.SifreSifirlama("ogrenci@test.local", "<b>Ad</b> Soyad", baglanti, TimeSpan.FromMinutes(30));

        Assert.StartsWith(EpostaSablonlari.Baslik, mesaj.Govde);
        var html = Assert.IsType<string>(mesaj.HtmlGovde);
        Assert.Contains(WebUtility.HtmlEncode(EpostaSablonlari.Baslik), html);
        Assert.Contains("href=\"https://localhost:5001/Account/SifreSifirla?kullanici=1&amp;kod=abc\"", html);
        Assert.Contains("&lt;b&gt;Ad&lt;/b&gt; Soyad", html);
        Assert.DoesNotContain("<b>Ad", html);
        // Harici görsel, stil dosyası veya betik yok; tek dış adres sıfırlama bağlantısı.
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<link", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("url(", html);
        Assert.All(DisAdres().Matches(html), m => Assert.StartsWith("https://localhost:5001/Account/SifreSifirla?", m.Value));
    }

    [Fact]
    public async Task Kapi_isaretlerinin_svg_dosyalari_koddaki_isaretlerle_ayni_ve_sunuluyor()
    {
        // Tek Kapı bu dosyaları kopyalar (scripts/arayuz-esitle.ps1); kaynak KapiIsareti'dir.
        var client = _factory.HttpsIstemcisi();
        foreach (var (dosya, svg) in new[]
        {
            ("kapi-kucuk.svg", KapiIsareti.Kucuk), ("kapi-buyuk.svg", KapiIsareti.Buyuk), ("kapi-kapali.svg", KapiIsareti.Kapali),
        })
        {
            var yanit = await client.GetAsync(Kok + "img/" + dosya);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Equal("image/svg+xml", yanit.Content.Headers.ContentType?.MediaType);
            Assert.Equal(KapiIsareti.Dosya(svg), (await yanit.Content.ReadAsStringAsync()).ReplaceLineEndings("\n"));
        }
    }

    [Fact]
    public async Task Giris_onay_ve_hata_sayfalari_D1_duzeltmeleriyle_ciziliyor()
    {
        var client = _factory.HttpsIstemcisi();

        var giris = WebUtility.HtmlDecode(await (await client.GetAsync("/Account/Login")).Content.ReadAsStringAsync());
        Assert.Contains("<p class=\"kapi-cumlesi\">Fakültedeki bütün uygulamalar için tek hesap.</p>", giris);
        Assert.Equal(("Profil", "Adın, fakülte ve bölüm bilgilerin"), AuthorizeModel.KapsamBilgisi("profile"));

        // Hata sayfalarında başlığın üstünde 48px kapalı kapı işareti.
        foreach (var sayfa in new[] { "/Account/AccessDenied", "/Hata" })
        {
            var html = await (await client.GetAsync(sayfa)).Content.ReadAsStringAsync();
            Assert.Matches(new Regex(
                @"<div class=""durum-sayfasi"">\s*<svg class=""kapi-kapali"" viewBox=""0 0 48 48"" width=""48"" height=""48""[^>]* aria-hidden=""true"""), html);
        }
    }

    [Fact]
    public async Task Iki_adim_sayfasinda_yardim_cumlesi_yok_kod_alani_yer_tutuculu()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        await _factory.IkiAdimAcAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();

        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var html = WebUtility.HtmlDecode(await (await client.GetAsync(giris.Headers.Location)).Content.ReadAsStringAsync());

        Assert.DoesNotContain("Telefonun yanında değilse", html);
        Assert.Contains("placeholder=\"000 000\"", html);
        Assert.Contains("data-yer-tutucu-kurtarma=\"XXXXX-XXXXX\"", html);
        Assert.Contains(">Kurtarma kodu kullan</button>", html);
    }

    [Fact]
    public async Task Guvenlik_gecmisinde_saat_dilimi_yalnizca_sutun_basliginda()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await _factory.DenetimKayitlariAsync(k => k.Any(x => x.KullaniciId == kullaniciId && x.OlayTuru == DenetimOlayi.GirisBasarili));

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/Account/GuvenlikGecmisi")).Content.ReadAsStringAsync());

        var dilim = GuvenlikGecmisiModel.SaatDilimi;
        Assert.Contains($"<th scope=\"col\" role=\"columnheader\">Zaman ({dilim})</th>", html);
        Assert.Matches(new Regex(@"<td role=""cell"" data-etiket=""Zaman"">\d{2}\.\d{2}\.\d{4} \d{2}:\d{2}</td>"), html);
        Assert.Single(Regex.Matches(GecmisTablosu().Match(html).Value, Regex.Escape($"({dilim})")));
    }

    [GeneratedRegex("<table class=\"gecmis\".*?</table>", RegexOptions.Singleline)]
    private static partial Regex GecmisTablosu();

    [GeneratedRegex(@"https?://[^\s""<]+")]
    private static partial Regex DisAdres();

    [GeneratedRegex(@"url\(""?([^"")]+)""?\)")]
    private static partial Regex CssAdresi();

    [GeneratedRegex("<(?:link|script)\\b[^>]*\\b(?:href|src)=\"([^\"]+)\"")]
    private static partial Regex SayfaKaynagi();

    [GeneratedRegex("<h1\\b")]
    private static partial Regex H1();

    // Gizli olmayan input'lar (type="hidden" hariç).
    [GeneratedRegex("<input\\b(?![^>]*type=\"hidden\")[^>]*\\bid=\"([^\"]+)\"")]
    private static partial Regex GorunurAlan();
}
