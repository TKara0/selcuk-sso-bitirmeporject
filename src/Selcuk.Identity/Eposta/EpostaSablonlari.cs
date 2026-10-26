using System.Globalization;
using System.Net;
using System.Text;

namespace Selcuk.Identity.Eposta;

/// <summary>
/// Türkçe, sade e-posta şablonları: düz metin ve aynı içeriğin HTML sürümü (tasarım sisteminin renkleri ve başlığı;
/// harici görsel, yazı tipi veya bağlantılı stil yok, stiller satır içi). Logo yoktur.
/// </summary>
public static class EpostaSablonlari
{
    public const string Baslik = "Selçuk Üniversitesi (Test) — Tek Şifre Giriş Sistemi";
    public const string SifreSifirlamaKonusu = "Şifre sıfırlama";
    public const string SifreDegistirildiKonusu = "Şifren değiştirildi";
    public const string IkiAdimAcildiKonusu = "İki aşamalı doğrulama açıldı";
    public const string IkiAdimKurulumuBaslatildiKonusu = "İki aşamalı doğrulama kurulumu başlatıldı";
    public const string IkiAdimKapatildiKonusu = "İki aşamalı doğrulama kapatıldı";
    public const string KurtarmaKodlariYenilendiKonusu = "Kurtarma kodların yenilendi";
    public const string KurtarmaKoduKullanildiKonusu = "Hesabına kurtarma koduyla giriş yapıldı";

    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static EpostaMesaji SifreSifirlama(string alici, string adSoyad, Uri baglanti, TimeSpan gecerlilik) =>
        Olustur(alici, SifreSifirlamaKonusu, adSoyad,
            Paragraf("Hesabın için şifre sıfırlama isteği aldık. Yeni şifre belirlemek için bu bağlantıyı aç:"),
            Baglanti(baglanti, "Yeni şifre belirle"),
            Paragraf($"Bağlantı {gecerlilik.TotalMinutes:0} dakika geçerli ve yalnızca bir kez kullanılabilir."),
            Paragraf("Bu isteği sen yapmadıysan bu e-postayı dikkate alma. Şifren değişmez."));

    public static EpostaMesaji SifreDegistirildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        Olustur(alici, SifreDegistirildiKonusu, adSoyad,
            Paragraf($"Hesabının şifresi {Bicimle(zaman)} tarihinde değiştirildi. Diğer açık oturumların kapatıldı."),
            Paragraf("Bu değişikliği sen yapmadıysan giriş sayfasındaki \"Şifremi unuttum\" bağlantısıyla hemen yeni bir " +
                "şifre belirle ve sistem yöneticisine başvur."));

    public static EpostaMesaji IkiAdimAcildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        Olustur(alici, IkiAdimAcildiKonusu, adSoyad,
            Paragraf($"Hesabında {Bicimle(zaman)} tarihinde iki aşamalı doğrulama açıldı. Bundan sonra girişte " +
                "şifrenle birlikte doğrulayıcı uygulamadaki 6 haneli kod istenecek. Diğer açık oturumların kapatıldı."),
            Paragraf(BenYapmadimSatiri));

    public static EpostaMesaji IkiAdimKurulumuBaslatildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        Olustur(alici, IkiAdimKurulumuBaslatildiKonusu, adSoyad,
            Paragraf($"Hesabında {Bicimle(zaman)} tarihinde iki aşamalı doğrulama kurulumu başlatıldı ve yeni bir " +
                "doğrulayıcı anahtarı üretildi. Önceki anahtar artık geçmez. Diğer açık oturumların kapatıldı."),
            Paragraf(BenYapmadimSatiri));

    public static EpostaMesaji IkiAdimKapatildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        Olustur(alici, IkiAdimKapatildiKonusu, adSoyad,
            Paragraf($"Hesabında {Bicimle(zaman)} tarihinde iki aşamalı doğrulama kapatıldı. Kurtarma kodların ve " +
                "hatırlanan cihazların geçersiz oldu. Diğer açık oturumların kapatıldı."),
            Paragraf(BenYapmadimSatiri));

    public static EpostaMesaji KurtarmaKodlariYenilendi(string alici, string adSoyad, DateTimeOffset zaman) =>
        Olustur(alici, KurtarmaKodlariYenilendiKonusu, adSoyad,
            Paragraf($"Hesabının kurtarma kodları {Bicimle(zaman)} tarihinde yenilendi, eski kodlar artık geçmez. " +
                "Diğer açık oturumların kapatıldı."),
            Paragraf(BenYapmadimSatiri));

    public static EpostaMesaji KurtarmaKoduKullanildi(string alici, string adSoyad, DateTimeOffset zaman, int kalan) =>
        Olustur(alici, KurtarmaKoduKullanildiKonusu, adSoyad,
            Paragraf($"Hesabına {Bicimle(zaman)} tarihinde bir kurtarma koduyla giriş yapıldı. Kullanılan kod artık geçmez."),
            Paragraf($"Kalan kurtarma kodu sayısı: {kalan}. Kodların azaldıysa Hesabım > İki aşamalı doğrulama " +
                "sayfasından yenileyebilirsin."),
            Paragraf(BenYapmadimSatiri));

    private const string BenYapmadimSatiri =
        "Bu işlemi sen yapmadıysan hemen şifreni değiştir ve sistem yöneticisine başvur.";

    private const string OtomatikSatiri = "Bu e-posta otomatik gönderildi, yanıtlama.";

    /// <summary>Türkiye saatiyle "gg.aa.yyyy SS:dd (TSİ)".</summary>
    internal static string Bicimle(DateTimeOffset zaman) => $"{DilimsizBicimle(zaman)} ({SaatDilimi})";

    /// <summary>
    /// Saat dilimi eki olmadan "gg.aa.yyyy SS:dd"; güvenlik geçmişi sayfası dilimi sütun başlığında yazar
    /// (<see cref="SaatDilimi"/>).
    /// </summary>
    internal static string DilimsizBicimle(DateTimeOffset zaman) =>
        (Tsi is { } tsi ? TimeZoneInfo.ConvertTime(zaman, tsi).DateTime : zaman.UtcDateTime)
            .ToString("dd.MM.yyyy HH:mm", Turkce);

    /// <summary>"TSİ"; sistemde Europe/Istanbul yoksa zamanlar UTC yazılır.</summary>
    internal static string SaatDilimi => Tsi is null ? "UTC" : "TSİ";

    private static TimeZoneInfo? Tsi =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Istanbul", out var tsi) ? tsi : null;

    // Gövde blokları: paragraf veya (yalnızca şifre sıfırlamada) bağlantı.
    private sealed record Blok(string Metin, Uri? Adres = null);

    private static Blok Paragraf(string metin) => new(metin);

    private static Blok Baglanti(Uri adres, string etiket) => new(etiket, adres);

    private static EpostaMesaji Olustur(string alici, string konu, string adSoyad, params Blok[] bloklar)
    {
        var selam = string.IsNullOrWhiteSpace(adSoyad) ? "Merhaba," : $"Merhaba {adSoyad},";
        return new EpostaMesaji(alici, konu, DuzMetin(selam, bloklar), Html(konu, selam, bloklar));
    }

    private static string DuzMetin(string selam, Blok[] bloklar) => string.Join("\r\n",
    [
        Baslik,
        new string('=', Baslik.Length),
        "",
        selam,
        "",
        // Bağlantı kendi satırında, kırılmadan kopyalanabilsin.
        .. bloklar.SelectMany(b => new[] { b.Adres?.AbsoluteUri ?? b.Metin, "" }),
        OtomatikSatiri,
        "",
    ]);

    // Renkler tasarım sisteminin açık tema token'ları: mürekkep #12263A, firuze #0F7B7E, taş #EDF0EF, yüzey #FFFFFF,
    // ikincil metin #4A5C6A, çizgi #D3DADB. E-posta istemcileri harici stil yüklemediği için stiller satır içi.
    private static string Html(string konu, string selam, Blok[] bloklar)
    {
        static string K(string metin) => WebUtility.HtmlEncode(metin);
        const string P = "margin:0 0 16px;";

        var govde = new StringBuilder();
        govde.Append("<p style=\"").Append(P).Append("\">").Append(K(selam)).Append("</p>\r\n");
        foreach (var blok in bloklar)
        {
            if (blok.Adres is { } adres)
            {
                var href = K(adres.AbsoluteUri);
                govde.Append("<p style=\"").Append(P).Append("\"><a href=\"").Append(href)
                    .Append("\" style=\"display:inline-block;padding:10px 20px;background:#0F7B7E;color:#FFFFFF;")
                    .Append("font-weight:700;text-decoration:none;border-radius:6px;\">").Append(K(blok.Metin))
                    .Append("</a></p>\r\n");
                govde.Append("<p style=\"").Append(P).Append("font-size:15px;color:#4A5C6A;word-break:break-all;\">")
                    .Append("Düğme çalışmazsa bu adresi tarayıcına yapıştır:<br>\r\n<a href=\"").Append(href)
                    .Append("\" style=\"color:#0C6B6E;\">").Append(href).Append("</a></p>\r\n");
            }
            else
            {
                govde.Append("<p style=\"").Append(P).Append("\">").Append(K(blok.Metin)).Append("</p>\r\n");
            }
        }

        return $"""
            <!DOCTYPE html>
            <html lang="tr">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="light">
            <title>{K(konu)}</title>
            </head>
            <body style="margin:0;padding:0;background:#EDF0EF;color:#12263A;font-family:'Atkinson Hyperlegible Next',Arial,sans-serif;font-size:17px;line-height:1.55;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#EDF0EF;">
            <tr><td align="center" style="padding:24px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;background:#FFFFFF;border:1px solid #D3DADB;border-radius:12px;">
            <tr><td style="padding:16px 24px;background:#12263A;color:#E6EEF0;font-weight:700;border-radius:12px 12px 0 0;">{K(Baslik)}</td></tr>
            <tr><td style="padding:24px 24px 8px;">
            <h1 style="margin:0 0 16px;font-size:22px;line-height:1.25;">{K(konu)}</h1>
            {govde}</td></tr>
            <tr><td style="padding:16px 24px;border-top:1px solid #D3DADB;color:#4A5C6A;font-size:15px;">{K(OtomatikSatiri)}</td></tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """;
    }
}
