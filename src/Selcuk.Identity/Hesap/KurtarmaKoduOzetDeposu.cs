using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Identity'nin EF Core kullanıcı deposu; yalnızca iki aşamalı doğrulama kurtarma kodlarının saklanışı değiştirildi.
/// Identity varsayılan olarak kodları AspNetUserTokens tablosunda düz metin tutar. Burada her kod Identity'nin
/// <see cref="IPasswordHasher{TUser}"/>'ıyla (PBKDF2, kullanıcı şifreleriyle aynı biçim ve ayarlar) özetlenir ve yalnızca
/// özetler saklanır. Kullanımda girilen kod özetlerle tek tek doğrulanır; eşleşen özet silinir (tek kullanımlık).
/// UserManager'ın kurtarma kodu API'si (GenerateNewTwoFactorRecoveryCodesAsync, RedeemTwoFactorRecoveryCodeAsync,
/// CountRecoveryCodesAsync) ve SignInManager.TwoFactorRecoveryCodeSignInAsync değişmeden bu depoyu kullanır.
/// </summary>
public sealed class KurtarmaKoduOzetDeposu(
    ApplicationDbContext context,
    IPasswordHasher<ApplicationUser> hasher,
    IdentityErrorDescriber? describer = null)
    : UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(context, describer)
{
    // Identity'nin düz metin kaydından ("[AspNetUserStore]" / "RecoveryCodes") ayrı bir ad.
    private const string Saglayici = "[SelcukSSO]";
    private const string Ad = "KurtarmaKoduOzetleri";

    public override Task ReplaceCodesAsync(ApplicationUser user, IEnumerable<string> recoveryCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(recoveryCodes);

        var ozetler = recoveryCodes.Select(kod => hasher.HashPassword(user, Normallestir(kod)));
        return SetTokenAsync(user, Saglayici, Ad, string.Join(';', ozetler), cancellationToken);
    }

    public override async Task<bool> RedeemCodeAsync(ApplicationUser user, string code,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(code);

        var ozetler = await OzetlerAsync(user, cancellationToken);
        var girilen = Normallestir(code);
        var eslesen = ozetler.FindIndex(ozet =>
            hasher.VerifyHashedPassword(user, ozet, girilen) != PasswordVerificationResult.Failed);
        if (eslesen < 0)
        {
            return false;
        }

        ozetler.RemoveAt(eslesen);
        await SetTokenAsync(user, Saglayici, Ad, string.Join(';', ozetler), cancellationToken);
        return true;
    }

    public override async Task<int> CountCodesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        return (await OzetlerAsync(user, cancellationToken)).Count;
    }

    private async Task<List<string>> OzetlerAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        [.. ((await GetTokenAsync(user, Saglayici, Ad, cancellationToken)) ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)];

    /// <summary>Kullanıcı kodu küçük harfle, boşlukla veya tiresiz yazabilir; özet normalize edilmiş hâlden alınır.</summary>
    private static string Normallestir(string kod) => string.Concat(kod.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
}
