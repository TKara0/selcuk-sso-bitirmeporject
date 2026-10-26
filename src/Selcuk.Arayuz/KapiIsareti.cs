using System.Globalization;
using System.Text;

namespace Selcuk.Arayuz;

/// <summary>
/// "Taç kapı" işareti: Selçuklu taç kapılarındaki sivri kemer ve sekiz köşeli yıldız örgüsü. Elle çizilmiş satır içi
/// SVG; yalnızca çizgi (stroke), dolgu yok, dekoratif olduğu için aria-hidden. Renkler CSS'ten gelir (currentColor).
/// Büyük hâli giriş ekranının kapı panelinde: dikdörtgen çerçeve, iki çizgili sivri kemer, kemerle kırpılmış "yıldız
/// ve haç" örgüsü (açılışta bir kez çizilir; animasyon selcuk.css'te, prefers-reduced-motion'da yok). Küçük hâli üst
/// çubukta: kemer çizgisi ve ortada tek yıldız. Kapalı hâli hata sayfalarında: kemer ve içi çapraz tarama.
/// Aynı SVG'ler wwwroot/img/ altında dosya olarak da durur (Tek Kapı kopyalar); eşitliği ArayuzTests denetler.
/// </summary>
public static class KapiIsareti
{
    /// <summary>Üst çubuktaki küçük işaret (24 × 28 px).</summary>
    public const string Kucuk =
        """<svg class="kapi-kucuk" viewBox="0 0 24 28" width="24" height="28" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linejoin="round" aria-hidden="true" focusable="false">""" +
        """<path d="M4 27V13A12 12 0 0 1 12 1.7A12 12 0 0 1 20 13V27"/>""" +
        """<path stroke-width="1.5" d="M16.81 17L15.4 18.4V20.4L13.4 20.4L12 21.81L10.6 20.4L8.6 20.4V18.4L7.19 17L8.6 15.6V13.6L10.6 13.6L12 12.19L13.4 13.6L15.4 13.6V15.6Z"/>""" +
        "</svg>";

    /// <summary>Giriş ekranındaki büyük işaret (viewBox 240 × 320).</summary>
    public static string Buyuk { get; } = BuyukOlustur();

    // Kemer: üzengi çizgisi y=150, iki merkezli sivri kemer. Dış kemer 44–196, iç kemer 52–188; merkezler ortak.
    private const double Uzengi = 150;
    private const double SolMerkez = 158;
    private const double SagMerkez = 82;
    private const double DisYaricap = 114;
    private const double IcYaricap = 106;
    private const double Taban = 306;

    /// <summary>Hata sayfalarındaki "kapalı kapı" (48 × 48 px): kemer çizgisi, eşik ve içi ince çapraz tarama.</summary>
    public static string Kapali { get; } = KapaliOlustur();

    // Örgü: kenar yarısı a olan iki karenin birleşimi (sekiz köşeli yıldız). Yıldızlar 2a aralıklı kare ızgaranın
    // dama tahtası konumlarında; satır ve sütunlarda yıldız ile haç dönüşümlü dizilir. Yıldızlar yalnızca çapraz
    // uçlarında birbirine değer; eksen uçları komşu haçın çentiğine oturur. Haçların bütün kenarları yıldız
    // kenarıdır, bu yüzden yıldızların dış çizgileri örgünün tamamını çizer.
    private const double KareYarisi = 14;

    private static string BuyukOlustur()
    {
        var sb = new StringBuilder();
        sb.Append("""<svg class="kapi-buyuk" viewBox="0 0 240 320" fill="none" stroke="currentColor" stroke-linejoin="round" aria-hidden="true" focusable="false">""");
        sb.Append("<defs><clipPath id=\"kapi-kemer\"><path d=\"").Append(Kemer(IcYaricap)).Append("Z\"/></clipPath></defs>");

        // Çerçeve: iki dikdörtgen (bordür), iki çizgili kemer ve köşelerde (kemer omuzları) birer yıldız.
        sb.Append("""<g class="kapi-cerceve" stroke-width="1.5">""");
        sb.Append("""<rect x="4" y="4" width="232" height="312" rx="2"/><rect x="14" y="14" width="212" height="292"/>""");
        sb.Append("<path d=\"").Append(Kemer(DisYaricap)).Append("\"/>");
        sb.Append("<path d=\"").Append(Kemer(IcYaricap)).Append("\"/>");
        sb.Append("<path d=\"").Append(Yildiz(36, 38, 9)).Append("\"/>");
        sb.Append("<path d=\"").Append(Yildiz(204, 38, 9)).Append("\"/>");
        sb.Append("</g>");

        // Örgü: yıldızlar dama tahtası konumlarında (satır + sütun çift), iç kemerle kırpılır. Ortadaki sütunda üzengi
        // hizasında bir yıldız var. Her yıldız ayrı yol (pathLength=1): açılış animasyonunda hepsi aynı anda çizilir.
        var aralik = 2 * KareYarisi;
        var uc = KareYarisi * Math.Sqrt(2);
        sb.Append("""<g class="kapi-orgu" stroke-width="1" clip-path="url(#kapi-kemer)">""");
        for (var satir = -4; satir <= 6; satir++)
        {
            for (var sutun = -3; sutun <= 3; sutun++)
            {
                if ((satir + sutun) % 2 != 0)
                {
                    continue;
                }

                var x = 120 + sutun * aralik;
                var y = Uzengi + satir * aralik;
                if (KemereDegiyor(x, y, uc))
                {
                    sb.Append("<path pathLength=\"1\" d=\"").Append(Yildiz(x, y, KareYarisi)).Append("\"/>");
                }
            }
        }

        sb.Append("</g></svg>");
        return sb.ToString();
    }

    private static string KapaliOlustur()
    {
        // Küçük işaretin kemeri iki katına yakın ölçekte; tarama 45°, 5 birim aralıklı, kemerin içiyle kırpılır.
        const string kemer = "M10 44V22A18 18 0 0 1 24 4.5A18 18 0 0 1 38 22V44";
        var sb = new StringBuilder();
        sb.Append("""<svg class="kapi-kapali" viewBox="0 0 48 48" width="48" height="48" fill="none" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">""");
        sb.Append("<defs><clipPath id=\"kapi-kapali-ic\"><path d=\"").Append(kemer).Append("Z\"/></clipPath></defs>");
        sb.Append("""<g stroke-width="1" clip-path="url(#kapi-kapali-ic)">""");
        for (var k = -40; k <= 44; k += 5)
        {
            sb.Append("<path d=\"M").Append(S(k)).Append(" 48L").Append(S(k + 48)).Append(" 0\"/>");
        }

        sb.Append("</g>");
        sb.Append("<path stroke-width=\"2\" d=\"").Append(kemer).Append("\"/>");
        sb.Append("""<path stroke-width="2" d="M5 44H43"/>""");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// wwwroot/img/ altındaki dosya hâli: satır içi SVG'ye xmlns eklenir (tek başına görsel veya CSS maskesi olarak
    /// kullanılabilsin diye). Tek Kapı bu dosyaları kopyalar (scripts/arayuz-esitle.ps1).
    /// </summary>
    public static string Dosya(string svg) =>
        svg.Replace("<svg ", "<svg xmlns=\"http://www.w3.org/2000/svg\" ", StringComparison.Ordinal) + "\n";

    /// <summary>Tabandan başlayıp tepede birleşen iki yaydan oluşan sivri kemer (açık yol).</summary>
    private static string Kemer(double r)
    {
        var sol = SolMerkez - r;
        var sag = SagMerkez + r;
        var dx = SolMerkez - 120;
        var tepe = Uzengi - Math.Sqrt(r * r - dx * dx);
        return $"M{S(sol)} {S(Taban)}V{S(Uzengi)}A{S(r)} {S(r)} 0 0 1 120 {S(tepe)}A{S(r)} {S(r)} 0 0 1 {S(sag)} {S(Uzengi)}V{S(Taban)}";
    }

    /// <summary>Merkezi (x, y), kare kenar yarısı a olan sekiz köşeli yıldızın dış çizgisi (16 köşe).</summary>
    private static string Yildiz(double x, double y, double a)
    {
        var dis = a * Math.Sqrt(2);
        var ic = a / Math.Cos(Math.PI / 8);
        var sb = new StringBuilder();
        for (var i = 0; i < 16; i++)
        {
            var aci = i * Math.PI / 8;
            var yaricap = i % 2 == 0 ? dis : ic;
            sb.Append(i == 0 ? 'M' : 'L')
              .Append(S(x + yaricap * Math.Cos(aci))).Append(' ')
              .Append(S(y + yaricap * Math.Sin(aci)));
        }

        return sb.Append('Z').ToString();
    }

    /// <summary>Yıldız (yarıçapı r) iç kemerin içine en az kısmen düşüyor mu (kırpılacak olsa da çizilir).</summary>
    private static bool KemereDegiyor(double x, double y, double r)
    {
        if (y - r > Taban || x + r < SolMerkez - IcYaricap || x - r > SagMerkez + IcYaricap)
        {
            return false;
        }

        return y >= Uzengi
            || (Uzaklik(x, y, SolMerkez) < IcYaricap + r && Uzaklik(x, y, SagMerkez) < IcYaricap + r);
    }

    private static double Uzaklik(double x, double y, double merkezX) => Math.Sqrt(
        (x - merkezX) * (x - merkezX) + (y - Uzengi) * (y - Uzengi));

    private static string S(double sayi) => Math.Round(sayi, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
