using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Staj Otomasyonu istemcisi (staj-otomasyonu): kayıt, izinli roller (ogrenci, staj_komisyonu, bolum_sekreteri,
/// bolum_baskani), access token'ın staj-api kaynağı ve staj.oku/staj.yaz kapsamları, Uygulamalarım kartı.
/// Kullanıcılar Development başlangıç verisinin tanımlarıyla (rastgele şifreyle) oluşturulur.
/// </summary>
public sealed class StajOtomasyonuIstemciTests : IDisposable
{
    private const string Kapsam = "openid profile email roles offline_access staj.oku staj.yaz";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<string> KaydetAsync()
    {
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await using var scope = _factory.Services.CreateAsyncScope();
        var tanim = Istemciler.StajOtomasyonu(secret);
        Istemciler.AdresleriDogrula(tanim);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
        return secret;
    }

    /// <summary>Seed tanımındaki kullanıcıyı (rolleri, bölümü ve fakültesiyle) rastgele şifreyle oluşturur.</summary>
    private async Task<string> SeedKullanicisiAsync(string kullaniciAdi)
    {
        var sifre = $"Aa1{Guid.NewGuid():N}";
        await using var scope = _factory.Services.CreateAsyncScope();
        await BaslangicVerisi.TestKullanicisiOlusturAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == kullaniciAdi), sifre, NullLogger.Instance);
        return sifre;
    }

    private async Task<HttpClient> GirisYapmisIstemciAsync(string kullaniciAdi)
    {
        var sifre = await SeedKullanicisiAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        return client;
    }

    [Fact]
    public async Task Kayit_gizli_istemci_PKCE_adresler_kapsamlar_ve_izinli_roller()
    {
        await KaydetAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var uygulama = (await manager.FindByClientIdAsync(Istemciler.StajOtomasyonuClientId))!;

        Assert.Equal(ClientTypes.Confidential, await manager.GetClientTypeAsync(uygulama));
        Assert.True(await manager.HasRequirementAsync(uygulama, Requirements.Features.ProofKeyForCodeExchange));
        Assert.Equal("https://localhost:5302/signin-oidc", Assert.Single(await manager.GetRedirectUrisAsync(uygulama)));
        Assert.Equal("https://localhost:5302/signout-callback-oidc", Assert.Single(await manager.GetPostLogoutRedirectUrisAsync(uygulama)));
        var izinler = await manager.GetPermissionsAsync(uygulama);
        Assert.Contains(Permissions.Prefixes.Scope + "staj.oku", izinler);
        Assert.Contains(Permissions.Prefixes.Scope + "staj.yaz", izinler);
        Assert.Contains(Permissions.GrantTypes.AuthorizationCode, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Implicit, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Password, izinler);

        var ozellikler = await manager.GetPropertiesAsync(uygulama);
        Assert.Equal("https://localhost:5302/signout-backchannel", ozellikler[Istemciler.BackChannelLogoutUriOzelligi].GetString());
        Assert.Equal(["ogrenci", "staj_komisyonu", "bolum_sekreteri", "bolum_baskani"],
            ozellikler[Istemciler.IzinliRollerOzelligi].EnumerateArray().Select(e => e.GetString()!));

        // Kapsamlar açılışta oluşturulur; kaynakları staj-api.
        var kapsamlar = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var ad in new[] { "staj.oku", "staj.yaz" })
        {
            var kapsam = await kapsamlar.FindByNameAsync(ad);
            Assert.NotNull(kapsam);
            Assert.Equal("staj-api", Assert.Single(await kapsamlar.GetResourcesAsync(kapsam)));
        }
    }

    [Theory]
    [InlineData("etik.test")]
    [InlineData("iskur.test")]
    [InlineData("hoca.test")]
    public async Task Izinli_rolu_olmayan_kullanici_access_denied_aliyor(string kullaniciAdi)
    {
        await KaydetAsync();
        var client = await GirisYapmisIstemciAsync(kullaniciAdi);

        var yanit = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(),
            Istemciler.StajOtomasyonuClientId, Istemciler.StajOtomasyonuRedirectUri, Kapsam));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(Istemciler.StajOtomasyonuRedirectUri + "?", yanit.Headers.Location!.OriginalString);
        var sorgu = QueryHelpers.ParseQuery(yanit.Headers.Location.Query);
        Assert.Equal(Errors.AccessDenied, sorgu[Parameters.Error].ToString());
        Assert.False(sorgu.ContainsKey(Parameters.Code));
    }

    [Theory]
    [InlineData("203011001", "ogrenci")]
    [InlineData("komisyon.test", "staj_komisyonu")]
    [InlineData("sekreter.test", "bolum_sekreteri")]
    [InlineData("baskan.test", "bolum_baskani")]
    public async Task Izinli_dort_rol_giris_yapiyor_access_token_staj_api_icin(string kullaniciAdi, string rol)
    {
        var secret = await KaydetAsync();
        var client = await GirisYapmisIstemciAsync(kullaniciAdi);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.StajOtomasyonuClientId,
            Istemciler.StajOtomasyonuRedirectUri, Kapsam);

        var yanit = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = Istemciler.StajOtomasyonuRedirectUri,
            [Parameters.ClientId] = Istemciler.StajOtomasyonuClientId,
            [Parameters.ClientSecret] = secret,
            [Parameters.CodeVerifier] = pkce.Verifier,
        }));

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var accessToken = OidcYardimcisi.JwtGovdesi(json.RootElement.GetProperty("access_token").GetString()!);
        var aud = accessToken.GetProperty(Claims.Audience);
        Assert.Contains("staj-api", aud.ValueKind == JsonValueKind.Array
            ? aud.EnumerateArray().Select(e => e.GetString()) : [aud.GetString()]);
        var kapsamlar = accessToken.GetProperty(Claims.Scope).GetString()!.Split(' ');
        Assert.Contains("staj.oku", kapsamlar);
        Assert.Contains("staj.yaz", kapsamlar);
        var roller = accessToken.GetProperty(Claims.Role);
        Assert.Contains(rol, roller.ValueKind == JsonValueKind.Array
            ? roller.EnumerateArray().Select(e => e.GetString()) : [roller.GetString()]);
        Assert.True(json.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task Uygulamalarimda_Staj_karti_izinli_rollere_gorunuyor_digerlerine_gorunmuyor()
    {
        await KaydetAsync();

        async Task<string> SayfaAsync(string kullaniciAdi)
        {
            var yanit = await (await GirisYapmisIstemciAsync(kullaniciAdi)).GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        }

        foreach (var izinli in new[] { "203011002", "komisyon.test", "sekreter.test", "baskan.test" })
        {
            var html = await SayfaAsync(izinli);
            Assert.Contains("Staj Otomasyonu", html);
            Assert.Contains("Staj başvurusu, staj defteri, komisyon değerlendirmesi ve evrak takibi", html);
            Assert.Contains("class=\"uygulama-simge uygulama-simge-zeytin\"", html);
            Assert.Contains("href=\"https://localhost:5302/Giris?iss=https%3A%2F%2Flocalhost%3A5001%2F\"", html);
        }

        foreach (var izinsiz in new[] { "etik.test", "iskur.test" })
        {
            var html = await SayfaAsync(izinsiz);
            Assert.DoesNotContain("Staj Otomasyonu", html);
            Assert.DoesNotContain("localhost:5302", html);
        }
    }
}
