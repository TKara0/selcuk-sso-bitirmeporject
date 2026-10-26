using System.Net;
using System.Text.RegularExpressions;

namespace Selcuk.Demo.Yemekhane.Tests;

/// <summary>
/// Demo Yemekhane ortak tasarım sistemini (Selcuk.Arayuz) kullanır: stil, betik ve yazı tipleri _content altından aynı kökenden
/// sunulur; sayfalar ortak düzenle (uygulama simgesi, başlık şeridi) ve uygulamanın kendi vurgu rengiyle çizilir.
/// </summary>
public sealed partial class ArayuzTests
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

    [Fact]
    public async Task Ortak_css_betik_ve_yazi_tipleri_content_altindan_200_donuyor()
    {
        using var factory = new YemekhaneFactory();
        var client = factory.HttpsIstemcisi();

        foreach (var (dosya, tur) in new[] { ("css/selcuk.css", "text/css"), ("js/selcuk.js", "text/javascript") })
        {
            var yanit = await client.GetAsync(Kok + dosya);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Equal(tur, yanit.Content.Headers.ContentType?.MediaType);
        }

        foreach (var dosya in YaziTipleri)
        {
            var yanit = await client.GetAsync(Kok + "fonts/" + dosya);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Equal("font/woff2", yanit.Content.Headers.ContentType?.MediaType);
            Assert.Equal("wOF2"u8.ToArray(), (await yanit.Content.ReadAsByteArrayAsync())[..4]);
        }

        // Uygulamanın kendi CSS'i yalnızca vurgu rengini ve yerleşimleri tanımlar.
        var site = await client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, site.StatusCode);
        Assert.Contains("--uygulama-vurgu:", await site.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sayfalar_ortak_duzenle_ve_ayni_kokenden_kaynaklarla_ciziliyor()
    {
        using var factory = new YemekhaneFactory(sahteKimlik: true);

        foreach (var (rol, sayfa) in new[] { ((string?)null, "/"), ("personel", "/YemekhaneYonetimi"), ("personel", "/Profilim") })
        {
            var html = await (await factory.HttpsIstemcisi(rol).GetAsync(sayfa)).Content.ReadAsStringAsync();
            var kaynaklar = SayfaKaynagi().Matches(html).Select(m => m.Groups[1].Value).ToList();
            Assert.Contains(kaynaklar, k => k.StartsWith(Kok + "css/selcuk.css", StringComparison.Ordinal));
            Assert.Contains(kaynaklar, k => k.StartsWith("/css/site.css", StringComparison.Ordinal));
            Assert.All(kaynaklar, k => Assert.StartsWith("/", k));
            Assert.All(kaynaklar, k => Assert.False(k.StartsWith("//", StringComparison.Ordinal), k));
            Assert.Contains("class=\"ust-cubuk uygulama-cubugu\"", html);
            Assert.Single(H1().Matches(html));
        }
    }

    [GeneratedRegex("<(?:link|script)\\b[^>]*\\b(?:href|src)=\"([^\"]+)\"")]
    private static partial Regex SayfaKaynagi();

    [GeneratedRegex("<h1\\b")]
    private static partial Regex H1();
}
