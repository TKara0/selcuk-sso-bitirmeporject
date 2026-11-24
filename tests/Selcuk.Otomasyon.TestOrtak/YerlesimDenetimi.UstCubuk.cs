using System.Text.RegularExpressions;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.TestYardimcilari;

/// <summary>
/// R12 arayüz düzeltmelerinin otomasyon denetimleri: dar ekranda tek satırlık üst çubuk (A1) ve ana sayfa kısayol
/// listesindeki menü simgeleri (A2). Saf işlevlerdir; Staj ve ortak temel testler aynı denetimleri kullanır.
/// </summary>
public static partial class YerlesimDenetimi
{
    private const string DarEkranSorgusu = "@media (max-width: 599.98px)";

    /// <summary>
    /// Dar ekranda üst çubuğun tek satır olduğunu ve erişilebilir kaldığını denetler: CSS tarafında medya sorgusu
    /// (sarılma yok, uygulama adı ellipsis, "Uygulamalarım" yalnızca simge, hesap düğmesi sağda, 44 px dokunma hedefi),
    /// işaretleme tarafında tam uygulama adı ve "Uygulamalarım" metninin DOM'da kalması. Geniş ekran kuralları değişmez.
    /// </summary>
    public static void DarEkranUstCubuguDogrula(string html, string selcukCss, string otomasyonCss, string uygulamaAdi, string kimlikKoku)
    {
        var bas = otomasyonCss.IndexOf(DarEkranSorgusu, StringComparison.Ordinal);
        Assert.True(bas >= 0, "Dar ekran medya sorgusu yok.");
        var blok = MedyaBlogu(otomasyonCss, bas);

        // Tek satır: sarılma yok; uygulama adı tek satırda kısalır (ellipsis), "Test" rozeti ve simge küçülmez.
        Assert.Matches(@"\.uygulama-cubugu\s*\{[^}]*flex-wrap:\s*nowrap", blok);
        Assert.Matches(@"\.uygulama-cubugu \.marka\s*\{[^}]*flex:\s*1 1 0;[^}]*min-width:\s*0", blok);
        Assert.Matches(@"\.marka > span:not\(\.uygulama-simgesi\):not\(\.etiket\)\s*\{[^}]*overflow:\s*hidden;[^}]*text-overflow:\s*ellipsis;[^}]*white-space:\s*nowrap", blok);
        Assert.Matches(@"\.marka > \.etiket\s*\{[^}]*flex:\s*none", blok);
        // Hesap düğmesi sağa yaslı: bağlantı grubu küçülmez, sola otomatik boşluk.
        Assert.Matches(@"\.ust-baglantilar\s*\{[^}]*flex:\s*none;[^}]*flex-wrap:\s*nowrap;[^}]*margin-left:\s*auto", blok);
        // "Uygulamalarım": yalnızca ızgara simgesi (CSS ile çizilir); metin font-size: 0 ile görsel olarak gizli; 44x44 px.
        Assert.Matches(@"\.uygulamalarim-baglantisi\s*\{[^}]*width:\s*var\(--dokunma\);[^}]*min-width:\s*var\(--dokunma\);[^}]*font-size:\s*0", blok);
        Assert.Matches(@"\.uygulamalarim-baglantisi::before\s*\{[^}]*content:\s*"""";[^}]*width:\s*20px;[^}]*height:\s*20px", blok);
        Assert.DoesNotMatch(@"url\(|data:", blok);
        Assert.Matches(@"--dokunma:\s*44px", selcukCss);
        Assert.Matches(@"\.uygulamalarim-baglantisi\s*\{[^}]*min-height:\s*var\(--dokunma\)", otomasyonCss);

        // Geniş ekran değişmez: metni gizleyen kural ve tek satır zorlaması yalnızca medya bloğunda.
        var disarisi = otomasyonCss.Remove(bas, blok.Length);
        Assert.DoesNotMatch(@"font-size:\s*0\s*;", disarisi);
        Assert.DoesNotContain("text-overflow", disarisi);
        Assert.Matches(@"\.uygulama-cubugu\s*\{\s*flex-wrap:\s*wrap;\s*\}", disarisi);

        // İşaretleme: tam ad ve "Uygulamalarım" metni DOM'da (ekran okuyucu okur); satır içi stil yok.
        Assert.Contains($"<span>{uygulamaAdi}</span>", html);
        Assert.Contains($"<a class=\"uygulamalarim-baglantisi\" href=\"{kimlikKoku}\">Uygulamalarım</a>", html);
        Assert.DoesNotContain(" style=", html);
    }

    /// <summary>
    /// Ana sayfadaki kısayol listesinin her satırının başında menüdeki simgenin (aria-hidden, bağlantının dışında)
    /// bulunduğunu, simgenin sprite'ta tanımlı olduğunu ve bağlantı metinlerinin menü tanımıyla aynı kaldığını denetler.
    /// beklenen: (başlık, adres, açıklama, simge adı) menü sırasıyla.
    /// </summary>
    public static void KisayolSimgeleriniDogrula(string html, IReadOnlyList<(string Baslik, string Adres, string Aciklama, string? Simge)> beklenen, string rol)
    {
        var bas = html.IndexOf("<ul class=\"satirlar kisayollar\"", StringComparison.Ordinal);
        Assert.True(bas >= 0, $"{rol}: kısayol listesi yok.");
        var kisayollar = html[bas..html.LastIndexOf("</ul>", StringComparison.Ordinal)];

        var ogeler = KisayolOgesi().Matches(kisayollar);
        Assert.True(ogeler.Count == beklenen.Count, $"{rol}: {ogeler.Count} kısayol, beklenen {beklenen.Count}.");
        var sprite = Regex.Match(html, "<svg class=\"simge-sprite\".*?</svg>", RegexOptions.Singleline).Value;
        for (var i = 0; i < beklenen.Count; i++)
        {
            Assert.Equal("#" + MenuSimgeleri.SembolKimligi(beklenen[i].Simge), ogeler[i].Groups["simge"].Value);
            Assert.Equal(beklenen[i].Adres, ogeler[i].Groups["adres"].Value);
            Assert.Equal(beklenen[i].Baslik, ogeler[i].Groups["baslik"].Value);
            Assert.Equal(beklenen[i].Aciklama, ogeler[i].Groups["aciklama"].Value);
            Assert.Contains($"<symbol id=\"{ogeler[i].Groups["simge"].Value[1..]}\"", sprite);
        }

        // Her simge aria-hidden ve odaklanamaz; kısayol listesinde başka svg yok (bağlantı içeriği değişmemiş).
        Assert.Equal(ogeler.Count, Regex.Matches(kisayollar, "<svg class=\"menu-simgesi\" aria-hidden=\"true\" focusable=\"false\"").Count);
        Assert.DoesNotContain("<svg", Regex.Replace(kisayollar, "<svg class=\"menu-simgesi\".*?</svg>", "", RegexOptions.Singleline));
        Assert.Single(Regex.Matches(html, "<svg class=\"simge-sprite\""));
    }

    // @media bloğunun tamamı (süslü parantezler dengelenene kadar).
    private static string MedyaBlogu(string css, int bas)
    {
        var derinlik = 0;
        for (var i = css.IndexOf('{', bas); i < css.Length; i++)
        {
            derinlik += css[i] == '{' ? 1 : css[i] == '}' ? -1 : 0;
            if (derinlik == 0)
            {
                return css[bas..(i + 1)];
            }
        }

        throw new InvalidOperationException("Medya bloğu kapanmıyor.");
    }

    [GeneratedRegex(@"<li>\s*<svg class=""menu-simgesi""[^>]*><use href=""(?<simge>#[^""]+)""></use></svg>\s*<a class=""kisayol"" href=""(?<adres>[^""]*)"">\s*<span class=""satir-baslik"">(?<baslik>[^<]*)</span>\s*<span class=""ipucu"">(?<aciklama>[^<]*)</span>\s*</a>\s*</li>")]
    private static partial Regex KisayolOgesi();
}
