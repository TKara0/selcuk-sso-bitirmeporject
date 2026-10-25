using System.Globalization;
using System.Text.RegularExpressions;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Performans ölçümü (tools/Selcuk.Olcum) için ayrılmış test hesapları: olcum.kullanici.01 … olcum.kullanici.50, rol
/// ogrenci, 2FA kapalı. Yalnızca Development'ta ve şifre user-secrets'ta (OlcumKullanicilari:Sifre) tanımlıysa seed
/// edilir (<see cref="Data.BaslangicVerisi"/>). Ölçüm sırasında aynı IP'den dakikada yüzlerce giriş yapılır; giriş
/// sayfasının istek sınırı yalnızca bu hesaplar için, yalnızca Development'ta ve Olcum:HizSiniriGevset açıkça true ise
/// devre dışı kalır (<see cref="GevsetmeEtkinMi"/>, <see cref="HizSiniri"/>). Hesap kilitleme ve diğer önlemler aynıdır.
/// </summary>
public static partial class OlcumKullanicilari
{
    public const int Sayi = 50;
    public const string OnEk = "olcum.kullanici.";
    public const string SifreAyari = "OlcumKullanicilari:Sifre";
    public const string GevsetmeAyari = "Olcum:HizSiniriGevset";

    public static string KullaniciAdi(int no) => OnEk + no.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Kullanıcı adı olcum.kullanici.01 … olcum.kullanici.50'den biri mi (büyük/küçük harf duyarsız).</summary>
    public static bool OlcumKullanicisiMi(string? kullaniciAdi) =>
        kullaniciAdi is not null
        && Desen().Match(kullaniciAdi.Trim()) is { Success: true } eslesme
        && int.Parse(eslesme.Groups[1].Value, CultureInfo.InvariantCulture) is >= 1 and <= Sayi;

    /// <summary>Gevşetme yalnızca Development ortamında ve ayar açıkça true ise etkindir; varsayılan kapalı.</summary>
    public static bool GevsetmeEtkinMi(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && configuration.GetValue<bool?>(GevsetmeAyari) == true;

    [GeneratedRegex(@"^olcum\.kullanici\.(\d{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Desen();
}
