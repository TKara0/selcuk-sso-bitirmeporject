using Microsoft.AspNetCore.Identity;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Kullanıcı yokken veya hesap kilitliyken Identity şifre özetini hiç hesaplamaz. Bu yollarda gerçek doğrulamayla aynı
/// maliyette (aynı hasher, aynı PBKDF2 ayarları) sahte bir doğrulama yapılır; böylece yanıt süresinden hesabın var olup
/// olmadığı veya kilitli olduğu anlaşılmaz. Sonuç kullanılmaz.
/// </summary>
public sealed class SahteSifreDogrulayici(IPasswordHasher<ApplicationUser> hasher)
{
    private static readonly ApplicationUser SahteKullanici = new();
    private static string? _ozet;

    public void Dogrula(string sifre)
    {
        var ozet = LazyInitializer.EnsureInitialized(ref _ozet,
            () => hasher.HashPassword(SahteKullanici, Guid.NewGuid().ToString("N")));
        _ = hasher.VerifyHashedPassword(SahteKullanici, ozet, sifre);
    }
}
