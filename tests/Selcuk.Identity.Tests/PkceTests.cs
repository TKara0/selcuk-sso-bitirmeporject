using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

public sealed class PkceTests : IDisposable
{
    private const string ClientId = "gecici-test-istemcisi";
    private const string RedirectUri = "https://istemci.test.local/callback";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task GeciciIstemciKaydetAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        // İstemciye özel PKCE gereksinimi bilinçli olarak eklenmedi: reddin sunucu genelindeki
        // RequireProofKeyForCodeExchange() ayarından geldiği doğrulanır.
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Explicit,
            DisplayName = "Geçici Test İstemcisi",
            RedirectUris = { new Uri(RedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
            },
        });
    }

    private static string AuthorizeAdresi(params (string Ad, string Deger)[] ekParametreler)
    {
        var parametreler = new Dictionary<string, string?>
        {
            [Parameters.ClientId] = ClientId,
            [Parameters.RedirectUri] = RedirectUri,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = "openid profile",
            [Parameters.State] = "durum123",
        };
        foreach (var (ad, deger) in ekParametreler)
        {
            parametreler[ad] = deger;
        }

        return QueryHelpers.AddQueryString("/connect/authorize", parametreler);
    }

    [Fact]
    public async Task Pkce_olmadan_authorize_istegi_reddediliyor()
    {
        await GeciciIstemciKaydetAsync();
        var client = _factory.HttpsIstemcisi();

        // code_challenge yok: OpenIddict isteği istemciye yönlendirmeden, doğrudan 400 invalid_request ile reddeder.
        var yanit = await client.GetAsync(AuthorizeAdresi());

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.Contains(Errors.InvalidRequest, govde);
        Assert.Contains("code_challenge", govde);

        // Karşılaştırma: aynı istek PKCE ile gönderildiğinde reddedilmez, giriş sayfasına yönlendirilir.
        var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var pkceliYanit = await client.GetAsync(AuthorizeAdresi(
            (Parameters.CodeChallenge, challenge),
            (Parameters.CodeChallengeMethod, CodeChallengeMethods.Sha256)));

        Assert.Equal(HttpStatusCode.Redirect, pkceliYanit.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", pkceliYanit.Headers.Location!.ToString());
    }
}
