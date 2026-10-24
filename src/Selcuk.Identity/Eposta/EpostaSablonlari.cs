using System.Globalization;

namespace Selcuk.Identity.Eposta;

/// <summary>Türkçe, sade, düz metin e-posta şablonları. Logo veya HTML yoktur.</summary>
public static class EpostaSablonlari
{
    public const string Baslik = "Selçuk Üniversitesi (Test) — Tek Şifre Giriş Sistemi";
    public const string SifreSifirlamaKonusu = "Şifre sıfırlama";
    public const string SifreDegistirildiKonusu = "Şifreniz değiştirildi";
    public const string IkiAdimAcildiKonusu = "İki aşamalı doğrulama açıldı";
    public const string IkiAdimKapatildiKonusu = "İki aşamalı doğrulama kapatıldı";
    public const string KurtarmaKodlariYenilendiKonusu = "Kurtarma kodlarınız yenilendi";
    public const string KurtarmaKoduKullanildiKonusu = "Hesabınıza kurtarma koduyla giriş yapıldı";

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

    public static EpostaMesaji IkiAdimAcildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        new(alici, IkiAdimAcildiKonusu, Govde(adSoyad,
            $"Hesabınızda {Bicimle(zaman)} tarihinde iki aşamalı doğrulama açıldı. Bundan sonra girişte şifrenizle",
            "birlikte doğrulayıcı uygulamadaki 6 haneli kod istenecek. Diğer açık oturumlarınız kapatıldı.",
            "",
            BenYapmadimSatiri));

    public static EpostaMesaji IkiAdimKapatildi(string alici, string adSoyad, DateTimeOffset zaman) =>
        new(alici, IkiAdimKapatildiKonusu, Govde(adSoyad,
            $"Hesabınızda {Bicimle(zaman)} tarihinde iki aşamalı doğrulama kapatıldı. Kurtarma kodlarınız ve",
            "hatırlanan cihazlarınız geçersiz oldu. Diğer açık oturumlarınız kapatıldı.",
            "",
            BenYapmadimSatiri));

    public static EpostaMesaji KurtarmaKodlariYenilendi(string alici, string adSoyad, DateTimeOffset zaman) =>
        new(alici, KurtarmaKodlariYenilendiKonusu, Govde(adSoyad,
            $"Hesabınızın kurtarma kodları {Bicimle(zaman)} tarihinde yenilendi; eski kodlar artık geçmez.",
            "Diğer açık oturumlarınız kapatıldı.",
            "",
            BenYapmadimSatiri));

    public static EpostaMesaji KurtarmaKoduKullanildi(string alici, string adSoyad, DateTimeOffset zaman, int kalan) =>
        new(alici, KurtarmaKoduKullanildiKonusu, Govde(adSoyad,
            $"Hesabınıza {Bicimle(zaman)} tarihinde bir kurtarma koduyla giriş yapıldı. Kullanılan kod artık geçmez.",
            $"Kalan kurtarma kodu sayısı: {kalan}. Kodlarınız azaldıysa Hesabım > İki aşamalı doğrulama sayfasından",
            "yenileyebilirsiniz.",
            "",
            BenYapmadimSatiri));

    private const string BenYapmadimSatiri =
        "Bu işlemi siz yapmadıysanız hemen şifrenizi değiştirin ve sistem yöneticisine başvurun.";

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

    /// <summary>Türkiye saatiyle "gg.aa.yyyy SS:dd (TSİ)"; güvenlik geçmişi sayfası da kullanır.</summary>
    internal static string Bicimle(DateTimeOffset zaman) =>
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Istanbul", out var tsi)
            ? TimeZoneInfo.ConvertTime(zaman, tsi).ToString("dd.MM.yyyy HH:mm", Turkce) + " (TSİ)"
            : zaman.UtcDateTime.ToString("dd.MM.yyyy HH:mm", Turkce) + " (UTC)";
}
