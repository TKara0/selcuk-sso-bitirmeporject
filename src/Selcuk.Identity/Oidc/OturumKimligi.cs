using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Kimlik sunucusundaki her oturum için bir oturum kimliği (sid) üretir ve oturum çerezinde taşır.
/// sid, id_token'a yazılır ve Back-Channel Logout'ta istemcilerin hangi oturumu kapatacağını belirtir.
/// </summary>
public static class OturumKimligi
{
    public static string Yeni() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(16));

    /// <summary>Identity oturum çerezi yazılırken sid yoksa ekler.</summary>
    public static Task OturumAcilirkenAsync(CookieSigningInContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity
            || identity.HasClaim(c => c.Type == OidcKimlik.SidClaim))
        {
            return Task.CompletedTask;
        }

        // Aynı kullanıcı yeniden imzalanıyorsa (ör. RefreshSignInAsync) oturum kimliği korunur;
        // yeni girişte veya farklı kullanıcıda yeni sid üretilir.
        var mevcut = context.HttpContext.User;
        var ayniKullanici = mevcut.FindFirst(Claims.Subject)?.Value is { } sub
            && sub == identity.FindFirst(Claims.Subject)?.Value;
        var sid = ayniKullanici ? mevcut.FindFirst(OidcKimlik.SidClaim)?.Value : null;

        identity.AddClaim(new Claim(OidcKimlik.SidClaim, sid ?? Yeni()));
        return Task.CompletedTask;
    }

    /// <summary>Güvenlik damgası doğrulaması principal'ı yeniden oluşturduğunda sid korunur.</summary>
    public static Task PrincipalYenilenirkenAsync(SecurityStampRefreshingPrincipalContext context)
    {
        var sid = context.CurrentPrincipal?.FindFirst(OidcKimlik.SidClaim)?.Value;
        if (sid is not null && context.NewPrincipal?.Identity is ClaimsIdentity identity
            && !identity.HasClaim(c => c.Type == OidcKimlik.SidClaim))
        {
            identity.AddClaim(new Claim(OidcKimlik.SidClaim, sid));
        }

        return Task.CompletedTask;
    }
}
