using System.Net;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Data;

/// <summary>
/// Kimlik sunucusuna bağlanan istemci uygulamaların tanımları. Development'ta açılışta kaydedilir;
/// testler de aynı tanımları kullanır. Adresler https olmalıdır; tek istisna RFC 8252'deki yerel (native)
/// uygulamalar için http://127.0.0.1 loopback adresleridir (<see cref="AdresleriDogrula"/>).
/// </summary>
public static class Istemciler
{
    // İstemcinin özel ayarları (OpenIddict "Properties"). Adlar OIDC istemci meta verisi adlarıdır.
    public const string BackChannelLogoutUriOzelligi = "backchannel_logout_uri";
    public const string ClientUriOzelligi = "client_uri";
    public const string InitiateLoginUriOzelligi = "initiate_login_uri";
    public const string AciklamaOzelligi = "aciklama";
    // İstemci bazlı erişim izni (IstemciErisimi): ikisi de boşsa herkes, doluysa en az birine uyan kullanıcı girebilir.
    public const string IzinliRollerOzelligi = "izinliRoller";
    public const string IzinliKullaniciAdlariOzelligi = "izinliKullaniciAdlari";
    // true ise bu istemciye yalnızca iki aşamalı doğrulanmış (amr "mfa") oturumdan kod ve token verilir.
    public const string MfaZorunluOzelligi = "mfaZorunlu";

    // Demo API (kaynak sunucusu): notlar.oku kapsamıyla verilen access token'ın aud'u demo-api olur.
    public const string DemoApiKaynagi = "demo-api";
    public const string NotlarOkuKapsami = "notlar.oku";

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

    public const string TekKapiClientId = "tek-kapi";
    private const string TekKapiAdres = "http://127.0.0.1:4545";
    public const string TekKapiRedirectUri = TekKapiAdres + "/auth/callback";
    public const string TekKapiPostLogoutRedirectUri = TekKapiAdres + "/";
    public const string TekKapiBackChannelLogoutUri = TekKapiAdres + "/auth/backchannel-logout";
    public const string TekKapiInitiateLoginUri = TekKapiAdres + "/auth/login";
    public const string TekKapiClientUri = TekKapiAdres + "/";
    public const string TekKapiAyarlari = "Istemciler:TekKapi";

    /// <summary>Yapılandırmada liste yoksa veya boşsa Tek Kapı'ya yalnızca bu kullanıcı girebilir (herkese açılmaz).</summary>
    public static readonly string[] TekKapiVarsayilanIzinliKullaniciAdlari = ["203011001"];

    public static OpenIddictApplicationDescriptor DemoObis(string clientSecret)
    {
        var tanim = Tanim(DemoObisClientId, "Demo OBİS", DemoObisAdres,
            "Notlarınızı görüntüleyin; akademisyenler için ders yönetimi.", clientSecret);
        // Notlarım sayfası notları Demo API'den (notlar.oku) çeker.
        tanim.Permissions.Add(Permissions.Prefixes.Scope + NotlarOkuKapsami);
        return tanim;
    }

    /// <summary>Demo API'nin kapsamı: kimlik sunucusunda açılışta oluşturulur veya güncellenir (tüm ortamlarda).</summary>
    public static OpenIddictScopeDescriptor NotlarOku() => new()
    {
        Name = NotlarOkuKapsami,
        DisplayName = "Not bilgilerini okuma",
        Resources = { DemoApiKaynagi },
    };

    public static OpenIddictApplicationDescriptor DemoYemekhane(string clientSecret) => Tanim(
        DemoYemekhaneClientId, "Demo Yemekhane", DemoYemekhaneAdres,
        "Haftalık menü ve yemek rezervasyonu; personel için günlük sayılar.", clientSecret);

    /// <summary>
    /// Tek Kapı: kullanıcının kendi bilgisayarında çalışan yerel köprü uygulaması (Node.js). Public istemci (RFC 8252
    /// native app): client_secret yok, çünkü yerel uygulamada gizli tutulamaz; bunun yerine PKCE zorunlu ve adresler
    /// yalnızca 127.0.0.1 loopback. Explicit onay; yalnızca izinliKullaniciAdlari listesindeki kullanıcılar girebilir.
    /// Gerçek sistem şifrelerinin kilidini açtığı için iki aşamalı doğrulama zorunludur (mfaZorunlu).
    /// </summary>
    public static OpenIddictApplicationDescriptor TekKapi(IEnumerable<string> izinliKullaniciAdlari)
    {
        var izinliler = izinliKullaniciAdlari.Where(ad => !string.IsNullOrWhiteSpace(ad)).Select(ad => ad.Trim()).ToArray();
        return new OpenIddictApplicationDescriptor
        {
            ClientId = TekKapiClientId,
            ClientType = ClientTypes.Public,
            ApplicationType = ApplicationTypes.Native,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "Tek Kapı — gerçek Selçuk sistemleri (yerel)",
            RedirectUris = { new Uri(TekKapiRedirectUri) },
            PostLogoutRedirectUris = { new Uri(TekKapiPostLogoutRedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange,
            },
            Properties =
            {
                [BackChannelLogoutUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiBackChannelLogoutUri),
                [ClientUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiClientUri),
                [InitiateLoginUriOzelligi] = JsonSerializer.SerializeToElement(TekKapiInitiateLoginUri),
                [AciklamaOzelligi] = JsonSerializer.SerializeToElement(
                    "Kendi üniversite hesabınla gerçek OBİS, UZEM ve e-postaya bu bilgisayardan otomatik giriş. " +
                    "Üniversite şifren yalnızca bu bilgisayarda, şifreli saklanır."),
                [IzinliKullaniciAdlariOzelligi] = JsonSerializer.SerializeToElement(
                    izinliler.Length > 0 ? izinliler : TekKapiVarsayilanIzinliKullaniciAdlari),
                [MfaZorunluOzelligi] = JsonSerializer.SerializeToElement(true),
            },
        };
    }

    /// <summary>
    /// Tanımdaki tüm adresler (redirect, post-logout, back-channel logout, client_uri, initiate_login_uri) https
    /// olmalı; http yalnızca 127.0.0.1 loopback adresinde kabul edilir (RFC 8252 bölüm 7.3, 8.3). "localhost" adı
    /// da kabul edilmez: ad çözümlemesi değiştirilerek başka bir arayüze yönlendirilebilir.
    /// </summary>
    public static void AdresleriDogrula(OpenIddictApplicationDescriptor tanim)
    {
        var adresler = tanim.RedirectUris.Concat(tanim.PostLogoutRedirectUris)
            .Concat(new[] { BackChannelLogoutUriOzelligi, ClientUriOzelligi, InitiateLoginUriOzelligi }
                .Where(tanim.Properties.ContainsKey)
                .Select(ad => new Uri(tanim.Properties[ad].GetString()!)));
        foreach (var adres in adresler)
        {
            if (adres.Scheme != Uri.UriSchemeHttps && !HttpLoopbackMi(adres))
            {
                throw new InvalidOperationException(
                    $"'{tanim.ClientId}' istemcisinde '{adres}' kabul edilmez: adres https veya http://127.0.0.1 olmalı.");
            }
        }
    }

    /// <summary>http şemalı ve sunucusu 127.0.0.1 IP adresi olan adres (RFC 8252 loopback).</summary>
    public static bool HttpLoopbackMi(Uri adres) =>
        adres.Scheme == Uri.UriSchemeHttp
        && IPAddress.TryParse(adres.Host, out var ip)
        && ip.Equals(IPAddress.Loopback);

    /// <summary>
    /// Gizli web istemcisi: authorization_code + PKCE zorunlu, explicit onay; redirect, post-logout ve
    /// back-channel logout adresleri uygulamanın kök adresinden türetilir. offline_access ile refresh token alabilir
    /// (kayan, tek kullanımlık) ve çıkışta refresh token'ını revocation uç noktasıyla iptal edebilir.
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
            Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Scopes.Profile,
            Permissions.Scopes.Email,
            Permissions.Scopes.Roles,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess,
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
            [MfaZorunluOzelligi] = JsonSerializer.SerializeToElement(false),
        },
    };
}
