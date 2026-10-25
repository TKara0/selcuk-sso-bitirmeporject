using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Uçtan uca protokol testleri: giriş → authorize (PKCE S256) → onay → kod → token.
/// demo-obis istemcisi, uygulamanın kendi tanımıyla (Istemciler.DemoObis) ve test başına üretilen
/// bir secret ile kaydedilir.
/// </summary>
public sealed partial class OidcAkisTests : IDisposable
{
    private const string Issuer = "https://localhost:5001/";
    private const string Fakulte = "Teknoloji Fakültesi";
    private const string Bolum = "Bilgisayar Mühendisliği";

    private readonly SelcukIdentityFactory _factory = new();
    private readonly string _clientSecret = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public void Dispose() => _factory.Dispose();

    private sealed record Pkce(string Verifier, string Challenge)
    {
        public static Pkce Yeni()
        {
            var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            return new(verifier, Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
        }
    }

    private async Task IstemciKaydetAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(Istemciler.DemoObis(_clientSecret));
    }

    private async Task<string> KullaniciIdAsync(string kullaniciAdi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (await userManager.FindByNameAsync(kullaniciAdi))!.Id;
    }

    /// <summary>İstemciyi kaydeder, ogrenci rolünde bir kullanıcıyla giriş yapmış bir HttpClient döndürür.</summary>
    private async Task<(HttpClient Client, string KullaniciAdi)> GirisYapmisIstemciAsync()
    {
        await IstemciKaydetAsync();
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, Fakulte, Bolum);
        var client = _factory.HttpsIstemcisi();

        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        return (client, kullaniciAdi);
    }

    private static string AuthorizeAdresi(string? codeChallenge, string? method,
        string redirectUri = Istemciler.DemoObisRedirectUri, string nonce = "nonce-123")
    {
        var parametreler = new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.RedirectUri] = redirectUri,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = "openid profile email roles",
            [Parameters.State] = "durum-123",
            [Parameters.Nonce] = nonce,
        };
        if (codeChallenge is not null)
        {
            parametreler[Parameters.CodeChallenge] = codeChallenge;
            parametreler[Parameters.CodeChallengeMethod] = method;
        }

        return QueryHelpers.AddQueryString("/connect/authorize", parametreler);
    }

    /// <summary>Authorize isteği gönderir, onay ekranında "İzin ver"i seçer ve dönen kodu çıkarır.</summary>
    private static async Task<string> KodAlAsync(HttpClient client, Pkce pkce)
    {
        var onay = await client.GetAsync(AuthorizeAdresi(pkce.Challenge, CodeChallengeMethods.Sha256));
        Assert.Equal(HttpStatusCode.OK, onay.StatusCode);

        var html = await onay.Content.ReadAsStringAsync();
        var metin = WebUtility.HtmlDecode(html);
        Assert.Contains("Demo OBİS", metin);
        Assert.Contains("fakülte ve bölüm", metin);

        // Onay formu, OIDC parametrelerini gizli alanlarda yeniden gönderir.
        var alanlar = GizliAlan().Matches(html)
            .Select(m => KeyValuePair.Create(WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value)))
            .ToList();
        Assert.Contains(alanlar, a => a.Key == Parameters.CodeChallenge && a.Value == pkce.Challenge);
        alanlar.Add(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)));
        alanlar.Add(KeyValuePair.Create("karar", "kabul"));

        var yanit = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(alanlar));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);

        var konum = yanit.Headers.Location!;
        Assert.StartsWith(Istemciler.DemoObisRedirectUri + "?", konum.OriginalString);
        var sorgu = QueryHelpers.ParseQuery(konum.Query);
        Assert.Equal("durum-123", sorgu[Parameters.State].ToString());
        Assert.Equal(Issuer, sorgu[Parameters.Iss].ToString());
        return sorgu[Parameters.Code].ToString();
    }

    private static Task<HttpResponseMessage> TokenIsteAsync(HttpClient client, string code, string verifier, string secret) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = Istemciler.DemoObisRedirectUri,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
            [Parameters.CodeVerifier] = verifier,
        }));

    private static async Task<string> HataKoduAsync(HttpResponseMessage yanit)
    {
        using var belge = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        return belge.RootElement.GetProperty("error").GetString()!;
    }

    private async Task<IList<SecurityKey>> ImzaAnahtarlariAsync(HttpClient client)
    {
        var jwks = new JsonWebKeySet(await client.GetStringAsync("/.well-known/jwks"));
        var anahtarlar = jwks.GetSigningKeys();
        Assert.NotEmpty(anahtarlar);
        return anahtarlar;
    }

    [Fact]
    public async Task Kod_akisi_PKCE_ile_imzali_JWT_tokenlar_uretiyor()
    {
        var (client, kullaniciAdi) = await GirisYapmisIstemciAsync();
        var pkce = Pkce.Yeni();
        var code = await KodAlAsync(client, pkce);

        var yanit = await TokenIsteAsync(client, code, pkce.Verifier, _clientSecret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);

        using var tokenYaniti = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = tokenYaniti.RootElement;
        Assert.Equal("Bearer", kok.GetProperty("token_type").GetString());
        Assert.False(kok.TryGetProperty("refresh_token", out _), "offline_access istenmediği için refresh token verilmemeli.");
        var accessToken = kok.GetProperty("access_token").GetString()!;
        var idToken = kok.GetProperty("id_token").GetString()!;

        var anahtarlar = await ImzaAnahtarlariAsync(client);
        var handler = new JsonWebTokenHandler();
        var subject = await KullaniciIdAsync(kullaniciAdi);

        // id_token: imza (JWKS), iss, aud = client_id, exp, nonce.
        var idSonuc = await handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Istemciler.DemoObisClientId,
            IssuerSigningKeys = anahtarlar,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        });
        Assert.True(idSonuc.IsValid, idSonuc.Exception?.Message);
        var id = (JsonWebToken)idSonuc.SecurityToken;
        Assert.Equal("nonce-123", id.GetClaim(Claims.Nonce).Value);
        Assert.InRange(id.ValidTo, DateTime.UtcNow, DateTime.UtcNow.AddHours(1).AddMinutes(1));

        // access_token: şifresiz, imzalı JWT (at+jwt). Kaynak sunucusu kapsamı (notlar.oku) istenmediği için aud
        // claim'i yoktur; hangi istemciye verildiği client_id claim'inde taşınır.
        var accessSonuc = await handler.ValidateTokenAsync(accessToken, new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidateAudience = false,
            IssuerSigningKeys = anahtarlar,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        });
        Assert.True(accessSonuc.IsValid, accessSonuc.Exception?.Message);
        var access = (JsonWebToken)accessSonuc.SecurityToken;
        Assert.Equal("at+jwt", access.Typ);
        Assert.Empty(access.Audiences);
        Assert.Equal(Istemciler.DemoObisClientId, access.GetClaim(Claims.ClientId).Value);
        // Access token 15 dakika geçerli (Tokenlar:AccessTokenSuresi).
        Assert.InRange(access.ValidTo, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));

        foreach (var token in new[] { id, access })
        {
            Assert.Equal(subject, token.Subject);
            Assert.Equal(Roller.Ogrenci, token.GetClaim(Claims.Role).Value);
        }

        // Profil bilgileri yalnızca id_token'da (ve userinfo'da); access token'da yok (veri minimizasyonu).
        Assert.Equal("Test Kullanıcısı", id.GetClaim(Claims.Name).Value);
        Assert.Equal(Fakulte, id.GetClaim("fakulte").Value);
        Assert.Equal(Bolum, id.GetClaim("bolum").Value);
        foreach (var ad in new[] { Claims.Name, Claims.Email, "fakulte", "bolum", Claims.PreferredUsername })
        {
            Assert.False(access.TryGetClaim(ad, out _), ad);
        }

        // userinfo: aynı bilgiler access token ile alınabiliyor.
        using var istek = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo");
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userinfo = await client.SendAsync(istek);
        Assert.Equal(HttpStatusCode.OK, userinfo.StatusCode);
        using var bilgi = JsonDocument.Parse(await userinfo.Content.ReadAsStringAsync());
        Assert.Equal(subject, bilgi.RootElement.GetProperty("sub").GetString());
        Assert.Equal(Fakulte, bilgi.RootElement.GetProperty("fakulte").GetString());
        Assert.Equal(Bolum, bilgi.RootElement.GetProperty("bolum").GetString());
        Assert.Equal("Test Kullanıcısı", bilgi.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Ayni_kod_ikinci_kez_kullanilinca_reddediliyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();
        var pkce = Pkce.Yeni();
        var code = await KodAlAsync(client, pkce);

        Assert.Equal(HttpStatusCode.OK, (await TokenIsteAsync(client, code, pkce.Verifier, _clientSecret)).StatusCode);

        var ikinci = await TokenIsteAsync(client, code, pkce.Verifier, _clientSecret);
        Assert.Equal(HttpStatusCode.BadRequest, ikinci.StatusCode);
        Assert.Equal(Errors.InvalidGrant, await HataKoduAsync(ikinci));
    }

    [Fact]
    public async Task Yanlis_code_verifier_reddediliyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();
        var code = await KodAlAsync(client, Pkce.Yeni());

        var yanit = await TokenIsteAsync(client, code, Pkce.Yeni().Verifier, _clientSecret);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Equal(Errors.InvalidGrant, await HataKoduAsync(yanit));
    }

    [Fact]
    public async Task Kayitli_olmayan_redirect_uri_reddediliyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();

        var yanit = await client.GetAsync(AuthorizeAdresi(Pkce.Yeni().Challenge, CodeChallengeMethods.Sha256,
            redirectUri: "https://saldirgan.test.local/signin-oidc"));

        // Kayıtsız adrese asla yönlendirilmez; hata kimlik sunucusunda gösterilir.
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        Assert.Contains(Errors.InvalidRequest, await yanit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Yanlis_client_secret_reddediliyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();
        var pkce = Pkce.Yeni();
        var code = await KodAlAsync(client, pkce);

        var yanit = await TokenIsteAsync(client, code, pkce.Verifier,
            Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32)));

        Assert.Contains(yanit.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized });
        Assert.Equal(Errors.InvalidClient, await HataKoduAsync(yanit));
    }

    [Fact]
    public async Task Plain_code_challenge_method_reddediliyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();

        using (var discovery = JsonDocument.Parse(await client.GetStringAsync("/.well-known/openid-configuration")))
        {
            var yontemler = discovery.RootElement.GetProperty("code_challenge_methods_supported")
                .EnumerateArray().Select(e => e.GetString()!).ToArray();
            Assert.Equal(["S256"], yontemler);
        }

        var verifier = Pkce.Yeni().Verifier;
        var yanit = await client.GetAsync(AuthorizeAdresi(verifier, CodeChallengeMethods.Plain));

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.Contains(Errors.InvalidRequest, govde);
        Assert.Contains("code_challenge_method", govde);
    }

    [Fact]
    public async Task Guvenlik_basliklari_giris_sayfasinda_mevcut()
    {
        var yanit = await _factory.HttpsIstemcisi().GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("nosniff", yanit.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", yanit.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", yanit.Headers.GetValues("X-Frame-Options").Single());
        var csp = yanit.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("default-src 'self'", csp);
    }

    [Fact]
    public async Task Cikis_post_logout_redirect_uri_ile_istemciye_donuyor()
    {
        var (client, _) = await GirisYapmisIstemciAsync();
        var adres = QueryHelpers.AddQueryString("/connect/logout", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.PostLogoutRedirectUri] = Istemciler.DemoObisPostLogoutRedirectUri,
            [Parameters.State] = "cikis-123",
        });

        var sayfa = await client.GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = await sayfa.Content.ReadAsStringAsync();

        var alanlar = GizliAlan().Matches(html)
            .Select(m => KeyValuePair.Create(WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value)))
            .Append(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)));
        var yanit = await client.PostAsync("/connect/logout", new FormUrlEncodedContent(alanlar));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith(Istemciler.DemoObisPostLogoutRedirectUri, konum.OriginalString);
        Assert.Equal("cikis-123", QueryHelpers.ParseQuery(konum.Query)[Parameters.State].ToString());

        // Kimlik sunucusundaki oturum kapandı: authorize tekrar girişe yönlendirir.
        var tekrar = await client.GetAsync(AuthorizeAdresi(Pkce.Yeni().Challenge, CodeChallengeMethods.Sha256));
        Assert.Equal(HttpStatusCode.Redirect, tekrar.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", tekrar.Headers.Location!.ToString());
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\" />")]
    private static partial Regex GizliAlan();
}
