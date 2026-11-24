using System.Globalization;
using System.Text.RegularExpressions;

namespace Selcuk.TestYardimcilari;

/// <summary>
/// Fakülte sitesinden esinlenen yerleşimin (koyu lacivert üst çubuk, başlık çizgisi, gezinti yolu, ikonlu menü) ortak
/// denetimleri. Saf işlevlerdir (HTML ve CSS metni alırlar); hem OtomasyonTemelTestleri (10
/// otomasyon), kendi fabrikasını kullanan Staj testleri ve kimlik sunucusu testleri çağırır (kimlik sunucusu testleri
/// yalnızca bu dosyayı derler; menü simgesi denetimi <c>YerlesimDenetimi.Menu.cs</c>'dedir).
/// </summary>
public static partial class YerlesimDenetimi
{
    public sealed record GeziniOgesiSonucu(string Metin, string? Adres, bool Gecerli);

    /// <summary>Sayfadaki gezinti yolu; yoksa null. Birden çok varsa veya yapı bozuksa test düşer.</summary>
    public static IReadOnlyList<GeziniOgesiSonucu>? GeziniYolu(string html)
    {
        var navlar = GeziniYoluBloku().Matches(html);
        if (navlar.Count == 0)
        {
            return null;
        }

        Assert.Single(navlar);
        var ogeler = Oge().Matches(navlar[0].Groups[1].Value);
        Assert.NotEmpty(ogeler);
        return [.. ogeler.Select(m => m.Groups["metin"].Success
            ? new GeziniOgesiSonucu(m.Groups["metin"].Value, m.Groups["adres"].Value, false)
            : new GeziniOgesiSonucu(m.Groups["gecerli"].Value, null, true))];
    }

    /// <summary>
    /// Gezinti yolunun yapısını denetler: nav (aria-label="Gezinti yolu") içinde ol, h1'in hemen altında, öğeler beklenen
    /// sırada, yalnızca son öğe aria-current="page" taşır ve bağlantı değildir (öteki öğeler bağlantıdır).
    /// </summary>
    public static void GeziniYoluDogrula(string html, IReadOnlyList<GeziniOgesiSonucu> beklenen, string sayfa)
    {
        var yol = GeziniYolu(html);
        Assert.True(yol is not null, $"{sayfa}: gezinti yolu yok.");
        Assert.Matches(@"</h1>\s*<nav class=""gezinti-yolu"" aria-label=""Gezinti yolu"">\s*<ol>", html);
        Assert.Equal(beklenen, yol);

        var nav = GeziniYoluBloku().Match(html).Groups[1].Value;
        Assert.Single(Regex.Matches(nav, "aria-current=\"page\""));
        Assert.Equal(yol.Count - 1, Regex.Matches(nav, "<a ").Count);
        Assert.True(yol[^1].Gecerli && yol[^1].Adres is null, $"{sayfa}: son öğe geçerli sayfa olmalı.");
    }

    /// <summary>
    /// Otomasyon gezinti yolu: ilk öğe "Uygulamalarım" ve adresi Authority kökü, ikinci öğe otomasyon adı ("/" adresli;
    /// yalnızca ana sayfada geçerli öğe), son öğe ekran adı.
    /// </summary>
    public static void GeziniYoluDogrula(string html, string authorityKok, string otomasyonAdi, string? ekranAdi, string sayfa)
    {
        List<GeziniOgesiSonucu> beklenen = [new("Uygulamalarım", authorityKok, false)];
        if (ekranAdi is null)
        {
            beklenen.Add(new(otomasyonAdi, null, true));
        }
        else
        {
            beklenen.Add(new(otomasyonAdi, "/", false));
            beklenen.Add(new(ekranAdi, null, true));
        }

        GeziniYoluDogrula(html, beklenen, sayfa);
    }

    /// <summary>
    /// Üst çubuk renk çiftlerinin kontrastı (açık ve koyu tema): metin, soluk metin (Test rozeti) ve açık firuze (kimlik
    /// vurgusu) çubuk zeminine karşı en az 4,5:1; çubuk, sayfa zeminine ve yüzeye karşı ayırt edilebilir. yerlesimCss:
    /// otomasyon.css veya kimlik sunucusunun site.css'i; selcukCss: ortak tasarım sistemi.
    /// </summary>
    public static void UstCubukKontrastiniDogrula(string selcukCss, string yerlesimCss)
    {
        static List<string> Renkler(string css, string ad) =>
            [.. Regex.Matches(css, Regex.Escape(ad) + @":\s*(#[0-9A-Fa-f]{6})").Select(m => m.Groups[1].Value)];

        var kapiZemin = Renkler(selcukCss, "--kapi-zemin");
        var kapiMetin = Renkler(selcukCss, "--kapi-metin");
        var kapiSoluk = Renkler(selcukCss, "--kapi-soluk");
        var kapiCizgi = Renkler(selcukCss, "--kapi-cizgi");
        var tas = Renkler(selcukCss, "--tas");
        var yuzey = Renkler(selcukCss, "--yuzey");
        foreach (var liste in new[] { kapiZemin, kapiMetin, kapiSoluk, kapiCizgi, tas, yuzey })
        {
            Assert.Equal(2, liste.Count);
        }

        // Açık temada çubuk zemini kapı paneli zemini, koyu temada bir ton açık lacivert (tek sabit renk).
        Assert.Matches(@":root\s*\{[^}]*--ust-zemin:\s*var\(--kapi-zemin\);", yerlesimCss);
        var koyuZemin = Renkler(yerlesimCss, "--ust-zemin");
        Assert.Single(koyuZemin);
        foreach (var token in new[] { "--ust-metin: var(--kapi-metin)", "--ust-soluk: var(--kapi-soluk)", "--ust-vurgu: var(--kapi-cizgi)" })
        {
            Assert.Contains(token, yerlesimCss);
        }

        var temalar = new[]
        {
            (Ad: "açık", Zemin: kapiZemin[0], Metin: kapiMetin[0], Soluk: kapiSoluk[0], Vurgu: kapiCizgi[0], Tas: tas[0], Yuzey: yuzey[0], Ayirt: 3.0),
            (Ad: "koyu", Zemin: koyuZemin[0], Metin: kapiMetin[1], Soluk: kapiSoluk[1], Vurgu: kapiCizgi[1], Tas: tas[1], Yuzey: yuzey[1], Ayirt: 1.5),
        };
        foreach (var t in temalar)
        {
            foreach (var (ad, renk) in new[] { ("metin", t.Metin), ("soluk metin / Test rozeti", t.Soluk), ("açık firuze", t.Vurgu) })
            {
                var oran = Kontrast(renk, t.Zemin);
                Assert.True(oran >= 4.5, $"{t.Ad} tema, {ad} {renk} / çubuk {t.Zemin}: {oran:0.00}");
            }

            var tasOrani = Kontrast(t.Zemin, t.Tas);
            Assert.True(tasOrani >= t.Ayirt, $"{t.Ad} tema, çubuk {t.Zemin} sayfa zemini {t.Tas}: {tasOrani:0.00}");
            Assert.True(Kontrast(t.Zemin, t.Yuzey) >= Math.Min(t.Ayirt, 1.3), $"{t.Ad} tema, çubuk / yüzey: {Kontrast(t.Zemin, t.Yuzey):0.00}");
        }

        // Kurallar token'ları kullanır: Test rozeti, hesap menüsü düğmesi, marka ve kapı işareti.
        Assert.Matches(@"\.ust-cubuk \.etiket-notr\s*\{[^}]*color:\s*var\(--ust-soluk\)", yerlesimCss);
        Assert.Matches(@"\.ust-cubuk \.hesap-dugmesi\s*\{[^}]*color:\s*var\(--ust-metin\)", yerlesimCss);
        Assert.Matches(@"\.ust-cubuk \.marka\b[^{]*\{[^}]*color:\s*var\(--ust-metin\)", yerlesimCss);
        Assert.Matches(@"\.ust-cubuk \.marka \.kapi-kucuk[^{]*\{[^}]*color:\s*var\(--ust-vurgu\)", yerlesimCss);
        Assert.Matches(@"\.ust-cubuk\s*\{[^}]*background:\s*var\(--ust-zemin\)", yerlesimCss);
    }

    /// <summary>WCAG 2.1 kontrast oranı.</summary>
    public static double Kontrast(string a, string b)
    {
        static double Parlaklik(string renk)
        {
            double Kanal(int i)
            {
                var c = int.Parse(renk.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber) / 255.0;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Kanal(0) + 0.7152 * Kanal(1) + 0.0722 * Kanal(2);
        }

        var (la, lb) = (Parlaklik(a), Parlaklik(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [GeneratedRegex(@"<nav class=""gezinti-yolu"" aria-label=""Gezinti yolu"">(.*?)</nav>", RegexOptions.Singleline)]
    private static partial Regex GeziniYoluBloku();

    [GeneratedRegex(@"<li>(?:<a href=""(?<adres>[^""]*)"">(?<metin>[^<]*)</a>|<span aria-current=""page"">(?<gecerli>[^<]*)</span>)</li>")]
    private static partial Regex Oge();
}
