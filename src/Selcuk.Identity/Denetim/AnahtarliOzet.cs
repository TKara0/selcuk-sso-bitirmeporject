using System.Security.Cryptography;
using System.Text;

namespace Selcuk.Identity.Denetim;

/// <summary>
/// Saklanması gereken ama düz metin tutulmaması gereken değerlerin (var olmayan kullanıcı adı, kullanılmış TOTP kodu)
/// anahtarlı özeti: HMAC-SHA256 (System.Security.Cryptography). Anahtar yapılandırmadan gelir
/// (<see cref="AyarAdi"/>, base64, en az 32 bayt); Development'ta user-secrets'ta, diğer ortamlarda ortam değişkeni veya
/// sır deposunda tutulur. Düz SHA-256'dan farkı: anahtarı bilmeyen biri olası kullanıcı adlarını veya 6 haneli kodların
/// tamamını deneyerek özeti geri çeviremez. Her kullanım kendi amaç etiketiyle özetlenir (aynı değer farklı amaçlarda
/// farklı özet verir). Çıktı sürüm önekiyle yazılır (<see cref="Onek"/>); eski kayıtlardaki önekli olmayan düz SHA-256
/// özetleri değiştirilmez.
/// </summary>
public sealed class AnahtarliOzet
{
    public const string AyarAdi = "Denetim:KullaniciAdiOzetAnahtari";
    public const string Onek = "h1:";
    public const int EnAzAnahtarUzunlugu = 32;

    public const string KullaniciAdiAmaci = "denetim-kullanici-adi";
    public const string TotpTekrarAmaci = "totp-tekrar";

    private readonly byte[] _anahtar;

    public AnahtarliOzet(byte[] anahtar)
    {
        if (anahtar.Length < EnAzAnahtarUzunlugu)
        {
            throw new InvalidOperationException(
                $"'{AyarAdi}' en az {EnAzAnahtarUzunlugu} baytlık bir anahtar olmalı (base64).");
        }

        _anahtar = [.. anahtar];
    }

    /// <summary>"h1:" + HMAC-SHA256(anahtar, amaç ‖ 0x00 ‖ değer) (UTF-8, küçük harf onaltılık).</summary>
    public string Hesapla(string amac, string deger) =>
        Onek + Convert.ToHexStringLower(HMACSHA256.HashData(_anahtar, Encoding.UTF8.GetBytes(amac + "\0" + deger)));

    /// <summary>
    /// Anahtarı yapılandırmadan okur. Development dışında anahtar yoksa veya geçersizse uygulama açılmaz. Development'ta
    /// anahtar yoksa her açılışta yeni bir geçici anahtar üretilir (özetler yeniden başlatmalar arasında eşleşmez).
    /// </summary>
    public static AnahtarliOzet Olustur(IConfiguration configuration, IHostEnvironment ortam, ILogger logger)
    {
        var deger = configuration[AyarAdi];
        if (string.IsNullOrWhiteSpace(deger))
        {
            if (!ortam.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"'{AyarAdi}' ayarı tanımlı değil. Ortam değişkeni veya sır deposuyla en az {EnAzAnahtarUzunlugu} " +
                    "baytlık rastgele bir anahtar (base64) verilmeden kimlik sunucusu başlatılmaz.");
            }

            logger.LogWarning("'{Ayar}' tanımlı değil; geçici bir anahtar üretildi (README > Kurulum).", AyarAdi);
            return new AnahtarliOzet(RandomNumberGenerator.GetBytes(EnAzAnahtarUzunlugu));
        }

        byte[] anahtar;
        try
        {
            anahtar = Convert.FromBase64String(deger);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"'{AyarAdi}' geçerli bir base64 değeri değil.");
        }

        return new AnahtarliOzet(anahtar);
    }
}
