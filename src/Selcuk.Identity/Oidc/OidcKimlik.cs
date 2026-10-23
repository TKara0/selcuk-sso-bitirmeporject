using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Authorize ve token uç noktalarının ortak kullandığı claim üretimi ve claim hedefleri.
/// </summary>
public static class OidcKimlik
{
    public static ClaimsIdentity YeniKimlik() =>
        new(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

    /// <summary>Kullanıcının güncel bilgilerini kimliğe yazar (mevcut değerlerin üzerine yazar).</summary>
    public static async Task KullaniciClaimleriniEkleAsync(
        ClaimsIdentity identity, ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Name, user.AdSoyad)
                .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user))
                .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                .SetClaims(Claims.Role, [.. await userManager.GetRolesAsync(user)]);
    }

    /// <summary>
    /// Bir claim'in hangi tokena gireceğini belirler. Profil, e-posta ve rol bilgileri yalnızca
    /// kullanıcının onay verdiği kapsam varsa tokenlara yazılır.
    /// </summary>
    public static IEnumerable<string> Hedefler(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            case Claims.Name or Claims.PreferredUsername:
                if (claim.Subject!.HasScope(Scopes.Profile))
                {
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case Claims.Email:
                if (claim.Subject!.HasScope(Scopes.Email))
                {
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                }
                yield break;

            case Claims.Role:
                if (claim.Subject!.HasScope(Scopes.Roles))
                {
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                }
                yield break;

            // Identity'nin güvenlik damgası hiçbir tokena yazılmaz.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }
}
