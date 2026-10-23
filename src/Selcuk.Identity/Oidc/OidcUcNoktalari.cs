using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Arayüzü olmayan OIDC uç noktaları: token ve userinfo. İstek doğrulamasını (PKCE, istemci,
/// redirect_uri, token imzası vb.) OpenIddict yapar; buraya yalnızca geçerli istekler ulaşır.
/// </summary>
public static class OidcUcNoktalari
{
    public static IEndpointRouteBuilder MapOidcUcNoktalari(this IEndpointRouteBuilder app)
    {
        app.MapPost("connect/token", TokenAsync);
        app.MapMethods("connect/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync);
        return app;
    }

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect isteği alınamadı.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("Desteklenmeyen grant türü.");
        }

        // Yetkilendirme kodu veya refresh token içindeki kimlik.
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = result.Principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await userManager.FindByIdAsync(subject);

        if (user is null || !await signInManager.CanSignInAsync(user) || await userManager.IsLockedOutAsync(user))
        {
            return Reddet(Errors.InvalidGrant, "Token artık geçerli değil.");
        }

        // Kapsamlar, yetkilendirme kimliği gibi OpenIddict claim'leri korunur; kullanıcı bilgileri tazelenir.
        var identity = new ClaimsIdentity(result.Principal!.Claims,
            authenticationType: result.Principal.Identity?.AuthenticationType,
            nameType: Claims.Name, roleType: Claims.Role);
        await OidcKimlik.KullaniciClaimleriniEkleAsync(identity, user, userManager);
        identity.SetDestinations(OidcKimlik.Hedefler);

        return Results.SignIn(new ClaimsPrincipal(identity),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> UserInfoAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        var subject = principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await userManager.FindByIdAsync(subject);

        if (principal is null || user is null)
        {
            return Results.Challenge(HataOzellikleri(Errors.InvalidToken, "Access token geçerli bir kullanıcıya ait değil."),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = await userManager.GetUserIdAsync(user),
        };

        if (principal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.AdSoyad;
            claims[Claims.PreferredUsername] = user.UserName!;
        }

        if (principal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email!;
            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (principal.HasScope(Scopes.Roles))
        {
            claims[Claims.Role] = await userManager.GetRolesAsync(user);
        }

        return Results.Ok(claims);
    }

    private static IResult Reddet(string error, string description) =>
        Results.Forbid(HataOzellikleri(error, description), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static AuthenticationProperties HataOzellikleri(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
