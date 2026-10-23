using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Data;

/// <summary>
/// Kimlik sunucusuna bağlanan istemci uygulamaların tanımları. Development'ta açılışta kaydedilir;
/// testler de aynı tanımları kullanır.
/// </summary>
public static class Istemciler
{
    // İstemcinin özel ayarları (OpenIddict "Properties"). Adlar OIDC istemci meta verisi adlarıdır.
    public const string BackChannelLogoutUriOzelligi = "backchannel_logout_uri";
    public const string ClientUriOzelligi = "client_uri";
    public const string InitiateLoginUriOzelligi = "initiate_login_uri";
    public const string AciklamaOzelligi = "aciklama";

    public const string DemoObisClientId = "demo-obis";
    private const string DemoObisAdres = "https://localhost:5101";
    public const string DemoObisRedirectUri = DemoObisAdres + "/signin-oidc";
    public const string DemoObisPostLogoutRedirectUri = DemoObisAdres + "/signout-callback-oidc";
    public const string DemoObisBackChannelLogoutUri = DemoObisAdres + "/signout-backchannel";

    public const string DemoYemekhaneClientId = "demo-yemekhane";
    private const string DemoYemekhaneAdres = "https://localhost:5102";
    public const string DemoYemekhaneRedirectUri = DemoYemekhaneAdres + "/signin-oidc";
    public const string DemoYemekhanePostLogoutRedirectUri = DemoYemekhaneAdres + "/signout-callback-oidc";
    public const string DemoYemekhaneBackChannelLogoutUri = DemoYemekhaneAdres + "/signout-backchannel";

    public static OpenIddictApplicationDescriptor DemoObis(string clientSecret) => Tanim(
        DemoObisClientId, "Demo OBİS", DemoObisAdres,
        "Notlarınızı görüntüleyin; akademisyenler için ders yönetimi.", clientSecret);

    public static OpenIddictApplicationDescriptor DemoYemekhane(string clientSecret) => Tanim(
        DemoYemekhaneClientId, "Demo Yemekhane", DemoYemekhaneAdres,
        "Haftalık menü ve yemek rezervasyonu; personel için günlük sayılar.", clientSecret);

    /// <summary>
    /// Gizli web istemcisi: authorization_code + PKCE zorunlu, explicit onay; redirect, post-logout ve
    /// back-channel logout adresleri uygulamanın kök adresinden türetilir.
    /// </summary>
    private static OpenIddictApplicationDescriptor Tanim(
        string clientId, string gorunenAd, string adres, string aciklama, string clientSecret) => new()
    {
        ClientId = clientId,
        ClientSecret = clientSecret,
        ClientType = ClientTypes.Confidential,
        ApplicationType = ApplicationTypes.Web,
        ConsentType = ConsentTypes.Explicit,
        DisplayName = gorunenAd,
        RedirectUris = { new Uri(adres + "/signin-oidc") },
        PostLogoutRedirectUris = { new Uri(adres + "/signout-callback-oidc") },
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
        Properties =
        {
            [BackChannelLogoutUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/signout-backchannel"),
            [ClientUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/"),
            [InitiateLoginUriOzelligi] = JsonSerializer.SerializeToElement(adres + "/Giris"),
            [AciklamaOzelligi] = JsonSerializer.SerializeToElement(aciklama),
        },
    };
}
