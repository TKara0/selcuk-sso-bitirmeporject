using System.Globalization;

namespace Selcuk.Identity.Eposta;

/// <summary>Türkçe, sade, düz metin e-posta şablonları. Logo veya HTML yoktur.</summary>
public static class EpostaSablonlari
{
    public const string Baslik = "Selçuk Üniversitesi (Test) — Tek Şifre Giriş Sistemi";
    public const string SifreSifirlamaKonusu = "Şifre sıfırlama";
    public const string SifreDegistirildiKonusu = "Şifreniz değiştirildi";

    private static readonly CultureInfo Turkce = CultureInfo.GetCultureInfo("tr-TR");

    public static EpostaMesaji SifreSifirlama(string alici, string adSoyad, Uri baglanti, TimeSpan gecerlilik) =>
        new(alici, SifreSifirlamaKonusu, Govde(adSoyad,
            "Hesabınız için şifre sıfırlama isteği aldık. Yeni şifre belirlemek için aşağıdaki bağlantıyı açın:",
            "",
            baglanti.AbsoluteUri,
            "",
            $"Bağlantı {gecerlilik.TotalMinutes:0} dakika geçerlidir ve yalnızca bir kez kullanılabilir.",
            "Bu isteği siz yapmadıysanız bu e-postayı dikkate almayın; şifreniz değişmez."));

    public static EpostaMesaji SifreDegistirildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        new(alici, SifreDegistirildiKonusu, Govde(adSoyad,
            $"Hesabınızın şifresi {Bicimle(zaman)} tarihinde değiştirildi. Diğer açık oturumlarınız kapatıldı.",
            "",
            "Bu değişikliği siz yapmadıysanız giriş sayfasındaki \"Şifremi unuttum\" bağlantısıyla hemen yeni bir",
            "şifre belirleyin ve sistem yöneticisine başvurun."));

    private static string Govde(string adSoyad, params string[] satirlar) => string.Join("\r\n",
    [
        Baslik,
        new string('=', Baslik.Length),
        "",
        string.IsNullOrWhiteSpace(adSoyad) ? "Merhaba," : $"Merhaba {adSoyad},",
        "",
        .. satirlar,
        "",
        "Bu e-posta otomatik olarak gönderilmiştir, lütfen yanıtlamayın.",
        "",
    ]);

    private static string Bicimle(DateTimeOffset zaman) =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Istanbul", out var tsi)
            ? TimeZoneInfo.ConvertTime(zaman, tsi).ToString("dd.MM.yyyy HH:mm", Turkce) + " (TSİ)"
            : zaman.UtcDateTime.ToString("dd.MM.yyyy HH:mm", Turkce) + " (UTC)";
}
