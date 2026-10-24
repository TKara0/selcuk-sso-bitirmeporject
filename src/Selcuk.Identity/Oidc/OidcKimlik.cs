using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
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
    /// <summary>Kuruma özel claim'ler; profile kapsamıyla birlikte verilir.</summary>
    public const string FakulteClaim = "fakulte";
    public const string BolumClaim = "bolum";

    /// <summary>Kimlik sunucusu oturum kimliği (OIDC Back-Channel Logout 1.0, "sid").</summary>
    public const string SidClaim = "sid";

    /// <summary>Kimlik doğrulama yöntemi değerleri (RFC 8176): şifre ve çok faktörlü doğrulama.</summary>
    public const string AmrSifre = "pwd";
    public const string AmrCokFaktorlu = "mfa";

    public static ClaimsIdentity YeniKimlik() =>
        new(TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

    /// <summary>
    /// Oturum çerezindeki Identity "amr" claim'inden id_token'a yazılacak değerler: yalnızca şifreyle ["pwd"], şifre ve
    /// ikinci adımla (TOTP veya kurtarma kodu) ["pwd", "mfa"]. Identity ikinci adımdan sonra çereze yalnızca "mfa"
    /// yazar; ilk faktör her zaman şifredir. "Bu cihazı hatırla" ile ikinci adım atlandıysa Identity "pwd" yazar.
    /// </summary>
    public static string[] AmrDegerleri(ClaimsPrincipal oturum) =>
        oturum.HasClaim(Claims.AuthenticationMethodReference, AmrCokFaktorlu) ? [AmrSifre, AmrCokFaktorlu] : [AmrSifre];

    /// <summary>
    /// amr tek değerli olsa da JSON dizisi olarak yazılır (OIDC Core 1.0 bölüm 2). Ayrı claim'ler tek değerde metne
    /// dönüşeceği için değer türü JSON_ARRAY olan tek bir claim eklenir; JWT'ye dizi olarak yazılır.
    /// </summary>
    public static void AmrAyarla(ClaimsIdentity identity, IEnumerable<string> degerler)
    {
        var dizi = degerler.Distinct(StringComparer.Ordinal).ToArray();
        identity.RemoveClaims(Claims.AuthenticationMethodReference);
        identity.AddClaim(new Claim(Claims.AuthenticationMethodReference, JsonSerializer.Serialize(dizi),
            JsonClaimValueTypes.JsonArray));
    }

    /// <summary>Yetkilendirme kodundan okunan amr değerleri (JSON dizisi veya ayrı claim'ler hâlinde gelebilir).</summary>
    public static IEnumerable<string> AmrOku(ClaimsIdentity identity) =>
        identity.FindAll(Claims.AuthenticationMethodReference).SelectMany(claim =>
            claim.ValueType == JsonClaimValueTypes.JsonArray
                ? JsonSerializer.Deserialize<string[]>(claim.Value) ?? []
                : [claim.Value]);

    /// <summary>Kullanıcının güncel bilgilerini kimliğe yazar (mevcut değerlerin üzerine yazar).</summary>
    public static async Task KullaniciClaimleriniEkleAsync(
        ClaimsIdentity identity, ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Name, user.AdSoyad)
                .SetClaim(Claims.PreferredUsername, await userManager.GetUserNameAsync(user))
                .SetClaim(FakulteClaim, user.Fakulte)
                .SetClaim(BolumClaim, user.Bolum)
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

            case Claims.Name or Claims.PreferredUsername or FakulteClaim or BolumClaim:
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

            // sid yalnızca id_token'da: istemci, Back-Channel Logout bildirimini oturumuyla eşler.
            // amr (kimlik doğrulama yöntemleri) de bir id_token claim'idir.
            case SidClaim or Claims.AuthenticationMethodReference:
                yield return Destinations.IdentityToken;
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
