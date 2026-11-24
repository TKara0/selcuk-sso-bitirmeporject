using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>Gezinti yolunun bir öğesi; son (geçerli) öğenin adresi yoktur.</summary>
public sealed record GeziniOgesi(string Metin, string? Adres = null);

/// <summary>
/// Otomasyonların gezinti yolu: "Uygulamalarım / Otomasyon adı / Ekran adı". Ekran adı menü tanımından gelir; ilk öğenin
/// adresi üst çubuktaki "Uygulamalarım" bağlantısıyla aynıdır (<see cref="OtomasyonBilgisi.KimlikSunucusuAdresi"/>,
/// Kimlik:Authority kökü; geçerli değilse ilk öğe yazılmaz). Ana sayfada yalnızca ilk iki öğe. Menüde olmayan ve
/// Profilim olmayan sayfalarda (giriş, çıkış, hata, erişim reddedildi) gezinti yolu yoktur.
/// </summary>
public static class GeziniYolu
{
    public const string UygulamalarimMetni = "Uygulamalarım";

    private const string AnaSayfa = "/Index";
    private const string Profilim = "/Profilim";

    public static IReadOnlyList<GeziniOgesi>? Olustur(string? sayfa, OtomasyonBilgisi bilgi, OtomasyonMenusu menu)
    {
        string? ekran;
        if (string.Equals(sayfa, AnaSayfa, StringComparison.OrdinalIgnoreCase))
        {
            ekran = null;
        }
        else if (string.Equals(sayfa, Profilim, StringComparison.OrdinalIgnoreCase))
        {
            ekran = "Profilim";
        }
        else if (menu.Ogeler.FirstOrDefault(o => string.Equals(o.Sayfa, sayfa, StringComparison.OrdinalIgnoreCase)) is { } oge)
        {
            ekran = oge.Baslik;
        }
        else
        {
            return null;
        }

        var yol = new List<GeziniOgesi>();
        if (bilgi.KimlikSunucusuAdresi is { } uygulamalarim)
        {
            yol.Add(new GeziniOgesi(UygulamalarimMetni, uygulamalarim));
        }

        yol.Add(ekran is null ? new GeziniOgesi(bilgi.Ad) : new GeziniOgesi(bilgi.Ad, "/"));
        if (ekran is not null)
        {
            yol.Add(new GeziniOgesi(ekran));
        }

        return yol;
    }

    /// <summary>
    /// Gezinti yolunu sayfa gövdesindeki ilk h1'in hemen altına yerleştirir (h1 yoksa gövdenin başına). Ekran içerikleri
    /// (68 sayfa) değişmesin diye ortak düzen ekler; gövde zaten üretilmiş ve kodlanmış HTML'dir, yalnızca araya
    /// girilir.
    /// </summary>
    public static IHtmlContent BasligaEkle(IHtmlContent govde, IHtmlContent gezinti)
    {
        using var yazici = new StringWriter();
        govde.WriteTo(yazici, HtmlEncoder.Default);
        var html = yazici.ToString();
        using var gezintiYazici = new StringWriter();
        gezinti.WriteTo(gezintiYazici, HtmlEncoder.Default);
        var gezintiHtml = gezintiYazici.ToString();

        const string Kapanis = "</h1>";
        var konum = html.IndexOf(Kapanis, StringComparison.Ordinal);
        return new HtmlString(konum < 0
            ? gezintiHtml + html
            : html.Insert(konum + Kapanis.Length, gezintiHtml));
    }
}
