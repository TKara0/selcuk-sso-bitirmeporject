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
/// 2FA ve denetim kaydı testlerinin ortak OIDC adımları: demo-obis kaydı, authorize (PKCE S256), gerekirse onay,
/// kod ve token isteği.
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
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.RedirectUri] = Istemciler.DemoObisRedirectUri,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = "openid profile email roles",
            [Parameters.State] = "durum-123",
            [Parameters.Nonce] = "nonce-123",
            [Parameters.CodeChallenge] = pkce.Challenge,
            [Parameters.CodeChallengeMethod] = CodeChallengeMethods.Sha256,
        });

    /// <summary>Onay ekranındaki formu (OIDC parametreleri + karar) antiforgery token'ıyla birlikte hazırlar.</summary>
    public static List<KeyValuePair<string, string>> OnayFormu(string html, string karar = "kabul") =>
    [
        .. GizliAlan().Matches(html).Select(m => KeyValuePair.Create(
            WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value))),
        KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)),
        KeyValuePair.Create("karar", karar),
    ];

    /// <summary>Authorize isteği gönderir, onay ekranı gelirse "İzin ver"i seçer ve redirect_uri'deki kodu döndürür.</summary>
    public static async Task<string> KodAlAsync(HttpClient client, Pkce pkce)
    {
        var yanit = await client.GetAsync(AuthorizeAdresi(pkce));
        if (yanit.StatusCode == HttpStatusCode.OK)
        {
            var html = await yanit.Content.ReadAsStringAsync();
            yanit = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(OnayFormu(html)));
        }

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith(Istemciler.DemoObisRedirectUri + "?", konum.OriginalString);
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
