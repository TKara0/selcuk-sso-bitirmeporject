using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Selcuk.Olcum;

/// <summary>
/// Ölçüm ayarları ("Olcum" bölümü: appsettings.json, komut satırı). Ölçüm hesaplarının şifresi ve demo-obis istemci
/// secret'ı kimlik sunucusunun user-secrets'ından, kimlik sunucusunun kullandığı adlarla okunur
/// (OlcumKullanicilari:Sifre, Istemciler:DemoObis:ClientSecret); rapora yazılmaz.
/// </summary>
public sealed class OlcumAyarlari
{
    public const string SifreAyari = "OlcumKullanicilari:Sifre";
    public const string DemoObisSecretAyari = "Istemciler:DemoObis:ClientSecret";
    public const int EnFazlaKullanici = 50;

    public required Uri Kimlik { get; init; }
    public required Uri Obis { get; init; }
    public required Uri Yemekhane { get; init; }
    public required Uri Api { get; init; }
    public int Tekrar { get; init; } = 100;
    public int Isinma { get; init; } = 5;
    public IReadOnlyList<int> Eszamanlilik { get; init; } = [1, 10, 25, 50];
    public int KullaniciSayisi { get; init; } = EnFazlaKullanici;
    public TimeSpan IstekZamanAsimi { get; init; } = TimeSpan.FromSeconds(30);
    public required string Sifre { get; init; }
    public required string DemoObisSecret { get; init; }

    /// <summary>Ayarları okur ve doğrular. Önce hedefler: yerel olmayan adres varsa sırlara bakılmadan reddedilir.</summary>
    public static OlcumAyarlari Oku(IConfiguration configuration)
    {
        var olcum = configuration.GetSection("Olcum");
        var hedefler = olcum.GetSection("Hedefler");
        var kimlik = YerelAg.Dogrula("Kimlik", hedefler["Kimlik"]);
        var obis = YerelAg.Dogrula("Obis", hedefler["Obis"]);
        var yemekhane = YerelAg.Dogrula("Yemekhane", hedefler["Yemekhane"]);
        var api = YerelAg.Dogrula("Api", hedefler["Api"]);

        var tekrar = olcum.GetValue<int?>("Tekrar") ?? 100;
        var isinma = olcum.GetValue<int?>("Isinma") ?? 5;
        var kullaniciSayisi = olcum.GetValue<int?>("KullaniciSayisi") ?? EnFazlaKullanici;
        var eszamanlilik = EszamanlilikOku(olcum.GetSection("Eszamanlilik"));
        if (tekrar < 1 || isinma < 0)
        {
            throw new OlcumAyarHatasi("Tekrar en az 1, ısınma en az 0 olmalıdır.");
        }

        if (kullaniciSayisi is < 1 or > EnFazlaKullanici)
        {
            throw new OlcumAyarHatasi($"KullaniciSayisi 1 ile {EnFazlaKullanici} arasında olmalıdır.");
        }

        if (eszamanlilik.Any(c => c < 1 || c > kullaniciSayisi))
        {
            throw new OlcumAyarHatasi($"Eşzamanlılık değerleri 1 ile KullaniciSayisi ({kullaniciSayisi}) arasında olmalıdır.");
        }

        var sifre = configuration[SifreAyari];
        var secret = configuration[DemoObisSecretAyari];
        if (string.IsNullOrEmpty(sifre) || string.IsNullOrEmpty(secret))
        {
            throw new OlcumAyarHatasi(
                $"'{SifreAyari}' ve '{DemoObisSecretAyari}' kimlik sunucusunun user-secrets'ında tanımlı olmalıdır (README > Ölçüm).");
        }

        return new OlcumAyarlari
        {
            Kimlik = kimlik,
            Obis = obis,
            Yemekhane = yemekhane,
            Api = api,
            Tekrar = tekrar,
            Isinma = isinma,
            Eszamanlilik = eszamanlilik,
            KullaniciSayisi = kullaniciSayisi,
            IstekZamanAsimi = olcum.GetValue<TimeSpan?>("IstekZamanAsimi") ?? TimeSpan.FromSeconds(30),
            Sifre = sifre,
            DemoObisSecret = secret,
        };
    }

    // JSON'da dizi ([1, 10]), komut satırında virgüllü metin (--eszamanlilik 1,10,25,50); boş metin: (f) atlanır.
    // Ayar hiç yoksa varsayılan 1/10/25/50.
    private static int[] EszamanlilikOku(IConfigurationSection bolum)
    {
        if (!bolum.Exists())
        {
            return [1, 10, 25, 50];
        }

        var degerler = bolum.Value is { } metin
            ? metin.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : bolum.GetChildren().Select(c => c.Value ?? string.Empty).ToArray();
        return [.. degerler.Select(d => int.TryParse(d, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c)
            ? c
            : throw new OlcumAyarHatasi($"Geçersiz eşzamanlılık değeri: '{d}'."))];
    }
}
