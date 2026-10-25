using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Identity'nin TOTP sağlayıcısı (AuthenticatorTokenProvider) + tekrar oynatma koruması. Kod doğrulaması değişmeden
/// Identity'de kalır; geçerli bir kod ayrıca kullanıcının son <see cref="TekrarSuresi"/> içinde kabul edilmiş kodlarıyla
/// karşılaştırılır. Aynı kod tekrar gelirse reddedilir ve denetim kaydına yazılır; çağıran taraf bunu hatalı kod gibi
/// sayaca ekler (girişte SignInManager kendisi, onaylarda <see cref="IkiAdimIslemleri"/>). Kodlar düz metin değil,
/// kullanıcı kimliğiyle birlikte anahtarlı özetle (<see cref="AnahtarliOzet"/>, ayrı amaç etiketi) ve kabul zamanıyla
/// AspNetUserTokens'ta saklanır. Yazım Identity'nin iyimser eşzamanlılık denetiminden (ConcurrencyStamp) geçer: aynı
/// kodla eşzamanlı iki istekten yalnızca biri kabul edilir.
/// Girişin ikinci adımı ve 2FA kapatma/kurtarma kodu yenileme onayları <see cref="ValidateAsync"/> (UserManager
/// üzerinden) ile kodu "kullanır"; kurulumdaki doğrulama <see cref="TuketmedenDogrulaAsync"/> ile kodu kaydetmez.
/// </summary>
public sealed class TekKullanimlikTotpSaglayici(
    AnahtarliOzet ozet,
    DenetimKaydedici denetim,
    TimeProvider zaman) : AuthenticatorTokenProvider<ApplicationUser>
{
    public static readonly TimeSpan TekrarSuresi = TimeSpan.FromMinutes(3);

    // Kurtarma kodu özetleriyle aynı sağlayıcı adı (KurtarmaKoduOzetDeposu).
    public const string Saglayici = "[SelcukSSO]";
    public const string Ad = "KullanilanTotpKodlari";

    public override async Task<bool> ValidateAsync(string purpose, string token,
        UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        if (!await base.ValidateAsync(purpose, token, manager, user))
        {
            return false;
        }

        var simdi = zaman.GetUtcNow();
        var kodOzeti = ozet.Hesapla(AnahtarliOzet.TotpTekrarAmaci, user.Id + "\0" + KanonikKod(token));
        var kayitlar = KayitlariCoz(await manager.GetAuthenticationTokenAsync(user, Saglayici, Ad))
            .Where(k => simdi - k.Zaman < TekrarSuresi)
            .ToList();

        if (kayitlar.Any(k => k.Ozet == kodOzeti))
        {
            denetim.Ekle(DenetimOlayi.IkiAdimKoduTekrarKullanildi, DenetimSonucu.Basarisiz, user.Id,
                ayrinti: "yontem=totp");
            return false;
        }

        kayitlar.Add((simdi, kodOzeti));
        var sonuc = await manager.SetAuthenticationTokenAsync(user, Saglayici, Ad,
            string.Join(';', kayitlar.Select(k => $"{k.Zaman.ToUnixTimeSeconds()}|{k.Ozet}")));
        if (!sonuc.Succeeded)
        {
            // Kullanıcı kaydı bu arada değişti (ör. aynı kod eşzamanlı başka bir istekte kabul edildi).
            denetim.Ekle(DenetimOlayi.IkiAdimKoduTekrarKullanildi, DenetimSonucu.Basarisiz, user.Id,
                ayrinti: "yontem=totp; neden=eszamanli-kullanim");
            return false;
        }

        return true;
    }

    /// <summary>Kodu Identity'nin TOTP doğrulamasıyla kontrol eder; kullanılmış sayıp kaydetmez.</summary>
    public Task<bool> TuketmedenDogrulaAsync(string token, UserManager<ApplicationUser> manager, ApplicationUser user) =>
        base.ValidateAsync("TwoFactor", token, manager, user);

    /// <summary>Identity kodu sayı olarak karşılaştırır ("012345", "12345", "+12345" aynı kod); özet sayıdan alınır.</summary>
    private static string KanonikKod(string token) =>
        int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sayi)
            ? sayi.ToString("D6", CultureInfo.InvariantCulture)
            : token.Trim();

    private static IEnumerable<(DateTimeOffset Zaman, string Ozet)> KayitlariCoz(string? deger)
    {
        foreach (var kayit in (deger ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parcalar = kayit.Split('|', 2);
            if (parcalar.Length == 2 && long.TryParse(parcalar[0], CultureInfo.InvariantCulture, out var saniye))
            {
                yield return (DateTimeOffset.FromUnixTimeSeconds(saniye), parcalar[1]);
            }
        }
    }
}
