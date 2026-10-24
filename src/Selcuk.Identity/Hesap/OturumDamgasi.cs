using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Kimlik sunucusu oturum çerezindeki güvenlik damgası her istekte veritabanındakiyle karşılaştırılır (Identity'nin
/// varsayılanı 30 dakikada bir). Şifre sıfırlanınca veya değiştirilince damga yenilendiği için kullanıcının diğer
/// tarayıcılardaki oturumları bir sonraki istekte kapanır. Çerez burada yenilenmez; damga eşleşirse Identity'nin olağan
/// doğrulayıcısı çalışır (claim'lerin periyodik tazelenmesi, sid korunur).
/// </summary>
public static class OturumDamgasi
{
    public static async Task DogrulaAsync(CookieValidatePrincipalContext context)
    {
        var signInManager = context.HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
        if (context.Principal is null || await signInManager.ValidateSecurityStampAsync(context.Principal) is null)
        {
            context.RejectPrincipal();
            await signInManager.SignOutAsync();
            return;
        }

        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }
}
