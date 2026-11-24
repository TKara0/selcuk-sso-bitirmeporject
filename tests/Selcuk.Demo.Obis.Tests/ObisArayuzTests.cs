using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Demo OBİS arayüzünün renkleri (site.css'teki --obis-* token'ları): beyaz metin ve mavi metin için WCAG AA (4,5:1),
/// açık ve koyu temada. Değerler stil dosyasından okunur; başlıktaki kontrast tablosu bu testle tutarlı tutulur.
/// </summary>
public sealed partial class ObisArayuzTests
{
    private static async Task<(string Site, string Ortak)> StillerAsync()
    {
        using var factory = new ObisFactory();
        var istemci = factory.HttpsIstemcisi();
        var site = await istemci.GetAsync("/css/site.css");
        var ortak = await istemci.GetAsync("/_content/Selcuk.Arayuz/css/selcuk.css");
        Assert.Equal(HttpStatusCode.OK, site.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ortak.StatusCode);
        return (await site.Content.ReadAsStringAsync(), await ortak.Content.ReadAsStringAsync());
    }

    /// <summary>Stil dosyasında token'ın iki tanımı (açık, koyu) sırayla; yoksa test başarısız olur.</summary>
    private static (string Acik, string Koyu) Token(string css, string ad)
    {
        var degerler = Regex.Matches(css, $@"(?<![\w-]){Regex.Escape(ad)}:\s*(#[0-9A-Fa-f]{{6}})\b").Select(m => m.Groups[1].Value).ToList();
        Assert.True(degerler.Count == 2, $"{ad}: açık ve koyu tema için tam iki tanım beklenir, {degerler.Count} bulundu.");
        return (degerler[0], degerler[1]);
    }

    private static double Parlaklik(string renk)
    {
        double Kanal(int konum)
        {
            var c = int.Parse(renk.AsSpan(1 + konum * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Kanal(0) + 0.7152 * Kanal(1) + 0.0722 * Kanal(2);
    }

    private static double Kontrast(string a, string b)
    {
        var (acik, koyu) = (Math.Max(Parlaklik(a), Parlaklik(b)), Math.Min(Parlaklik(a), Parlaklik(b)));
        return (acik + 0.05) / (koyu + 0.05);
    }

    [Fact]
    public async Task Mavi_tonlar_beyaz_ve_mavi_metin_icin_her_iki_temada_AA_4_5_orani_sagliyor()
    {
        var (site, ortak) = await StillerAsync();
        string Obis(string ad, bool koyu) => koyu ? Token(site, "--obis-" + ad).Koyu : Token(site, "--obis-" + ad).Acik;
        string Ortak(string ad, bool koyu) => koyu ? Token(ortak, "--" + ad).Koyu : Token(ortak, "--" + ad).Acik;

        foreach (var koyu in new[] { false, true })
        {
            var tema = koyu ? "koyu" : "açık";
            var ciftler = new (string Metin, string Zemin, string Ad)[]
            {
                // Beyaz metin: üst şerit, "Test" rozeti, mavi düğme.
                (Obis("uzerinde", koyu), Obis("serit", koyu), "beyaz / şerit"),
                (Obis("uzerinde", koyu), Obis("serit-koyu", koyu), "beyaz / şerit-koyu (rozet, hesap düğmesi)"),
                (Obis("uzerinde", koyu), Obis("dugme", koyu), "beyaz / düğme"),
                // Mavi metin: sayfa başlığı ve bilgi kutularındaki bağlantılar.
                (Obis("baslik", koyu), Obis("acik", koyu), "başlık / açık mavi"),
                (Obis("baglanti", koyu), Obis("acik", koyu), "bağlantı / açık mavi"),
                (Obis("baglanti", koyu), Obis("sari", koyu), "bağlantı / sarı"),
                (Obis("baglanti", koyu), Obis("yesil", koyu), "bağlantı / yeşil"),
                (Obis("baglanti", koyu), Obis("gri", koyu), "bağlantı / gri (sekme çubuğu)"),
                (Obis("baglanti", koyu), Ortak("yuzey", koyu), "bağlantı / yüzey (etkin sekme)"),
                (Obis("baglanti", koyu), Ortak("tas", koyu), "bağlantı / taş"),
                // Normal metin renkleri açık mavi, sarı, yeşil ve gri zeminlerde.
                (Ortak("murekkep", koyu), Obis("gri", koyu), "mürekkep / gri"),
                (Ortak("murekkep", koyu), Obis("sari", koyu), "mürekkep / sarı"),
                (Ortak("murekkep", koyu), Obis("acik", koyu), "mürekkep / açık mavi"),
                (Ortak("murekkep", koyu), Obis("yesil", koyu), "mürekkep / yeşil"),
                (Ortak("soluk", koyu), Obis("gri", koyu), "soluk (pasif sekme) / gri"),
            };

            foreach (var (metin, zemin, ad) in ciftler)
            {
                var oran = Kontrast(metin, zemin);
                Assert.True(oran >= 4.5, $"{tema} tema, {ad}: {metin} / {zemin} = {oran:0.00} (en az 4,5 olmalı).");
            }

            // Başlık şeridinin kalın dikey çubukları dekoratiftir ama sayfa zemininden ayırt edilir (en az 3:1).
            var cubuk = Kontrast(Obis("cubuk", koyu), Ortak("tas", koyu));
            Assert.True(cubuk >= 3.0, $"{tema} tema, çubuk / taş = {cubuk:0.00}");
        }
    }

    [Fact]
    public async Task Stil_dosyasi_mavi_tonlari_ve_baslik_tablosunu_iceriyor_ortak_css_e_dokunmuyor()
    {
        var (site, ortak) = await StillerAsync();

        Assert.Contains("--uygulama-vurgu:", site);
        Assert.Contains("Kontrast oranları", site);
        Assert.Contains(".obis-sekmeler", site);
        Assert.Contains(".obis-sayfa-basligi", site);
        Assert.Contains("prefers-color-scheme: dark", site);
        // Ortak tasarım sistemi OBİS'e özel token'lar içermez.
        Assert.DoesNotContain("--obis-", ortak);
        Assert.DoesNotContain(".obis-", ortak);
    }
}
