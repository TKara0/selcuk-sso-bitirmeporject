using System.Text.RegularExpressions;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.TestYardimcilari;

/// <summary>Otomasyon menüsünün simge denetimi (Selcuk.Otomasyon.Ortak'a bağlıdır; kimlik sunucusu testlerine derlenmez).</summary>
public static partial class YerlesimDenetimi
{
    /// <summary>
    /// Menüdeki her bağlantının önünde aria-hidden bir simge olduğunu, simgenin sprite'ta tanımlı olduğunu ve bağlantı
    /// metinlerinin menü tanımıyla aynı kaldığını denetler. beklenen: (bağlantı metni, simge adı) sırasıyla.
    /// </summary>
    public static void MenuSimgeleriniDogrula(string html, IReadOnlyList<(string Metin, string? Simge)> beklenen, string sayfa)
    {
        var bas = html.IndexOf("id=\"otomasyon-menu-listesi\"", StringComparison.Ordinal);
        Assert.True(bas >= 0, $"{sayfa}: menü yok.");
        var menu = html[bas..html.IndexOf("</nav>", bas, StringComparison.Ordinal)];

        var ogeler = MenuOgesi().Matches(menu);
        Assert.True(ogeler.Count == beklenen.Count, $"{sayfa}: {ogeler.Count} menü öğesi, beklenen {beklenen.Count}.");
        for (var i = 0; i < beklenen.Count; i++)
        {
            Assert.Equal(beklenen[i].Metin, ogeler[i].Groups["metin"].Value);
            Assert.Equal("#" + MenuSimgeleri.SembolKimligi(beklenen[i].Simge), ogeler[i].Groups["simge"].Value);
        }

        // Her simge aria-hidden ve odaklanamaz; bağlantının dışındadır (bağlantı metni yalnızca menü başlığıdır).
        Assert.Equal(ogeler.Count, Regex.Matches(menu, "<svg class=\"menu-simgesi\" aria-hidden=\"true\" focusable=\"false\"").Count);
        Assert.DoesNotContain("<svg", Regex.Replace(menu, "<svg class=\"menu-simgesi\".*?</svg>", "", RegexOptions.Singleline));

        // Sprite sayfada bir kez, gizli ve dekoratif; kullanılan her simge tanımlı.
        Assert.Single(Regex.Matches(html, "<svg class=\"simge-sprite\""));
        var sprite = Regex.Match(html, "<svg class=\"simge-sprite\".*?</svg>", RegexOptions.Singleline).Value;
        Assert.Contains("aria-hidden=\"true\"", sprite);
        foreach (var ad in MenuSimgeleri.Adlar)
        {
            Assert.Contains($"<symbol id=\"ms-{ad}\"", sprite);
        }

        foreach (Match o in ogeler)
        {
            Assert.Contains($"<symbol id=\"{o.Groups["simge"].Value[1..]}\"", sprite);
        }
    }

    [GeneratedRegex(@"<li><svg class=""menu-simgesi""[^>]*><use href=""(?<simge>#[^""]+)""></use></svg><a href=""[^""]*""[^>]*>(?<metin>[^<]*)</a></li>")]
    private static partial Regex MenuOgesi();
}
