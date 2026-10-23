using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Data;

/// <summary>
/// Kimlik sunucusuna bağlanan istemci uygulamaların tanımları. Development'ta açılışta kaydedilir;
/// testler de aynı tanımı kullanır.
/// </summary>
public static class Istemciler
{
    public const string DemoObisClientId = "demo-obis";
    public const string DemoObisRedirectUri = "https://localhost:5101/signin-oidc";
    public const string DemoObisPostLogoutRedirectUri = "https://localhost:5101/signout-callback-oidc";

    public static OpenIddictApplicationDescriptor DemoObis(string clientSecret) => new()
    {
        ClientId = DemoObisClientId,
        ClientSecret = clientSecret,
        ClientType = ClientTypes.Confidential,
        ApplicationType = ApplicationTypes.Web,
        ConsentType = ConsentTypes.Explicit,
        DisplayName = "Demo OBİS",
        RedirectUris = { new Uri(DemoObisRedirectUri) },
        PostLogoutRedirectUris = { new Uri(DemoObisPostLogoutRedirectUri) },
        Permissions =
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.Endpoints.EndSession,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Profile,
            Permissions.Scopes.Email,
            Permissions.Scopes.Roles,
        },
        Requirements =
        {
            Requirements.Features.ProofKeyForCodeExchange,
        },
    };
}
