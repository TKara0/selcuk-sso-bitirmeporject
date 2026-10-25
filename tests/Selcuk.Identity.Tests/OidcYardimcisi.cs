using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// 2FA, denetim kaydı ve istemci erişimi testlerinin ortak OIDC adımları: demo-obis kaydı, authorize (PKCE S256),
/// gerekirse onay, kod ve token isteği. Varsayılan istemci demo-obis'tir; diğer istemciler (ör. public tek-kapi) için
/// client_id, redirect_uri ve kapsam verilir.
/// </summary>
public static partial class OidcYardimcisi
{
    public sealed record Pkce(string Verifier, string Challenge)
    {
        public static Pkce Yeni()
        {
            var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            return new(verifier, Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        }
    }

    /// <summary>demo-obis'i uygulamanın kendi tanımıyla, test içinde üretilen bir secret'la kaydeder.</summary>
    public static async Task<string> DemoObisKaydetAsync(SelcukIdentityFactory factory)
    {
        var secret = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>()
            .CreateAsync(Istemciler.DemoObis(secret));
        return secret;
    }

    public static string AuthorizeAdresi(Pkce pkce) =>
        AuthorizeAdresi(pkce, Istemciler.DemoObisClientId, Istemciler.DemoObisRedirectUri, "openid profile email roles");

    public static string AuthorizeAdresi(Pkce pkce, string clientId, string redirectUri, string scope) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = clientId,
            [Parameters.RedirectUri] = redirectUri,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = scope,
            [Parameters.State] = "durum-123",
            [Parameters.Nonce] = "nonce-123",
            [Parameters.CodeChallenge] = pkce.Challenge,
            [Parameters.CodeChallengeMethod] = CodeChallengeMethods.Sha256,
        });

    /// <summary>Onay ekranındaki formu (OIDC parametreleri + karar) antiforgery token'ıyla birlikte hazırlar.</summary>
    public static List<KeyValuePair<string, string>> OnayFormu(string html, string karar = "kabul") =>
    [
        .. FormAlanlari(html),
        KeyValuePair.Create("karar", karar),
    ];

    /// <summary>Sayfadaki formun gizli alanları ve antiforgery token'ı (onay ve çıkış formları).</summary>
    public static List<KeyValuePair<string, string>> FormAlanlari(string html) =>
    [
        .. GizliAlan().Matches(html).Select(m => KeyValuePair.Create(
            WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value))),
        KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)),
    ];

    /// <summary>Authorize isteği gönderir, onay ekranı gelirse "İzin ver"i seçer ve redirect_uri'deki kodu döndürür.</summary>
    public static Task<string> KodAlAsync(HttpClient client, Pkce pkce) =>
        KodAlAsync(client, pkce, Istemciler.DemoObisClientId, Istemciler.DemoObisRedirectUri, "openid profile email roles");

    public static async Task<string> KodAlAsync(HttpClient client, Pkce pkce, string clientId, string redirectUri,
        string scope)
    {
        var yanit = await client.GetAsync(AuthorizeAdresi(pkce, clientId, redirectUri, scope));
        if (yanit.StatusCode == HttpStatusCode.OK)
        {
            var html = await yanit.Content.ReadAsStringAsync();
            yanit = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(OnayFormu(html)));
        }

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith(redirectUri + "?", konum.OriginalString);
        return QueryHelpers.ParseQuery(konum.Query)[Parameters.Code].ToString();
    }

    public static Task<HttpResponseMessage> TokenIsteAsync(HttpClient client, string code, string verifier, string secret) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = Istemciler.DemoObisRedirectUri,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
            [Parameters.CodeVerifier] = verifier,
        }));

    /// <summary>Public istemci için token isteği: client_secret yok, istemci yalnızca client_id ve PKCE ile tanınır.</summary>
    public static Task<HttpResponseMessage> PublicTokenIsteAsync(HttpClient client, string code, string verifier,
        string clientId, string redirectUri) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = redirectUri,
            [Parameters.ClientId] = clientId,
            [Parameters.CodeVerifier] = verifier,
        }));

    /// <summary>Kod + token: başarılı token yanıtının JSON'unu döndürür.</summary>
    public static async Task<JsonElement> TokenAlAsync(HttpClient client, string secret)
    {
        var pkce = Pkce.Yeni();
        var code = await KodAlAsync(client, pkce);
        var yanit = await TokenIsteAsync(client, code, pkce.Verifier, secret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return JsonDocument.Parse(await yanit.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>JWT'nin payload kısmını ham JSON olarak döndürür (claim'in dizi mi metin mi olduğu görülsün).</summary>
    public static JsonElement JwtGovdesi(string jwt) =>
        JsonDocument.Parse(Base64UrlEncoder.Decode(jwt.Split('.')[1])).RootElement.Clone();

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\" />")]
    private static partial Regex GizliAlan();
}
