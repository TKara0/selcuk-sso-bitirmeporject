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
using Otomasyon = Selcuk.Identity.Tests.OtomasyonIstemcileriTests.Otomasyon;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Etik Kurul (etik-kurul-otomasyonu), Bölüm Kurul Karar (bolum-kurul-otomasyonu) ve Lisans Projeleri
/// (lisans-projeleri-otomasyonu) istemcileri: kayıt, izinli olmayan rollerin access_denied'ı, izinli rollerin access
/// token'ı (aud &lt;ad&gt;-api), Uygulamalarım kartları ve simgeler. Kullanıcılar Development başlangıç verisinin
/// tanımlarıyla (rastgele şifreyle) oluşturulur.
/// </summary>
public sealed class KurulVeProjeIstemcileriTests : IDisposable
{
    public static readonly IReadOnlyDictionary<string, Otomasyon> Otomasyonlar = new Dictionary<string, Otomasyon>
    {
        ["etik"] = new(Istemciler.EtikKurulOtomasyonuClientId, 5303, "Etik Kurul Otomasyonu", Istemciler.EtikKurulOtomasyonu,
            "etik-kurul-api", "etik-kurul.oku", "etik-kurul.yaz", ["akademisyen", "etik_kurul_uyesi", "bolum_baskani"], "murdum", "EK"),
        ["kurul"] = new(Istemciler.BolumKurulOtomasyonuClientId, 5305, "Bölüm Kurul Karar Otomasyonu", Istemciler.BolumKurulOtomasyonu,
            "bolum-kurul-api", "bolum-kurul.oku", "bolum-kurul.yaz",
            ["bolum_baskani", "bolum_kurulu_uyesi", "bolum_sekreteri", "fakulte_sekreteri"], "arduvaz", "BK"),
        ["proje"] = new(Istemciler.LisansProjeleriOtomasyonuClientId, 5309, "Lisans Projeleri Otomasyonu", Istemciler.LisansProjeleriOtomasyonu,
            "lisans-projeleri-api", "lisans-projeleri.oku", "lisans-projeleri.yaz", ["ogrenci", "akademisyen", "bolum_baskani"], "cam", "LP"),
    };

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<string> KaydetAsync(Func<string, OpenIddictApplicationDescriptor> tanimla)
    {
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        await using var scope = _factory.Services.CreateAsyncScope();
        var tanim = tanimla(secret);
        Istemciler.AdresleriDogrula(tanim);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
        return secret;
    }

    private async Task<HttpClient> GirisYapmisIstemciAsync(string kullaniciAdi)
    {
        var sifre = $"Aa1{Guid.NewGuid():N}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await BaslangicVerisi.TestKullanicisiOlusturAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == kullaniciAdi), sifre, NullLogger.Instance);
        }

        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        return client;
    }

    [Theory]
    [InlineData("etik")]
    [InlineData("kurul")]
    [InlineData("proje")]
    public async Task Kayit_gizli_istemci_PKCE_adresler_kapsamlar_izinli_roller_vurgu_simge(string ad)
    {
        var o = Otomasyonlar[ad];
        await KaydetAsync(o.Tanim);
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var uygulama = (await manager.FindByClientIdAsync(o.ClientId))!;

        Assert.Equal(ClientTypes.Confidential, await manager.GetClientTypeAsync(uygulama));
        Assert.True(await manager.HasRequirementAsync(uygulama, Requirements.Features.ProofKeyForCodeExchange));
        Assert.Equal(o.RedirectUri, Assert.Single(await manager.GetRedirectUrisAsync(uygulama)));
        var izinler = await manager.GetPermissionsAsync(uygulama);
        Assert.Contains(Permissions.Prefixes.Scope + o.OkuKapsami, izinler);
        Assert.Contains(Permissions.Prefixes.Scope + o.YazKapsami, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Implicit, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Password, izinler);

        var ozellikler = await manager.GetPropertiesAsync(uygulama);
        Assert.Equal($"https://localhost:{o.Port}/signout-backchannel", ozellikler[Istemciler.BackChannelLogoutUriOzelligi].GetString());
        Assert.Equal(o.IzinliRoller, ozellikler[Istemciler.IzinliRollerOzelligi].EnumerateArray().Select(e => e.GetString()!));
        Assert.Equal(o.Vurgu, ozellikler[Istemciler.VurguOzelligi].GetString());
        Assert.Equal(o.Simge, ozellikler[Istemciler.SimgeOzelligi].GetString());

        var kapsamlar = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var kapsamAdi in new[] { o.OkuKapsami, o.YazKapsami })
        {
            var kapsam = await kapsamlar.FindByNameAsync(kapsamAdi);
            Assert.NotNull(kapsam);
            Assert.Equal(o.ApiKaynagi, Assert.Single(await kapsamlar.GetResourcesAsync(kapsam)));
        }
    }

    [Theory]
    [InlineData("etik", "203011001")]
    [InlineData("etik", "sekreter.test")]
    [InlineData("etik", "iskur.test")]
    [InlineData("kurul", "hoca.test")]
    [InlineData("kurul", "203011001")]
    [InlineData("kurul", "etik.test")]
    [InlineData("proje", "sekreter.test")]
    [InlineData("proje", "fakultesekreter.test")]
    [InlineData("proje", "iskur.test")]
    public async Task Izinli_rolu_olmayan_kullanici_access_denied_aliyor(string ad, string kullaniciAdi)
    {
        var o = Otomasyonlar[ad];
        await KaydetAsync(o.Tanim);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi);

        var yanit = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(), o.ClientId, o.RedirectUri, o.Kapsam));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(o.RedirectUri + "?", yanit.Headers.Location!.OriginalString);
        var sorgu = QueryHelpers.ParseQuery(yanit.Headers.Location.Query);
        Assert.Equal(Errors.AccessDenied, sorgu[Parameters.Error].ToString());
        Assert.False(sorgu.ContainsKey(Parameters.Code));
    }

    [Theory]
    [InlineData("etik", "hoca.test", "akademisyen")]
    [InlineData("etik", "etik.test", "etik_kurul_uyesi")]
    [InlineData("etik", "baskan.test", "bolum_baskani")]
    [InlineData("kurul", "baskan.test", "bolum_baskani")]
    [InlineData("kurul", "komisyon.test", "bolum_kurulu_uyesi")]
    [InlineData("kurul", "sekreter.test", "bolum_sekreteri")]
    [InlineData("kurul", "fakultesekreter.test", "fakulte_sekreteri")]
    [InlineData("proje", "203011002", "ogrenci")]
    [InlineData("proje", "hoca.test", "akademisyen")]
    [InlineData("proje", "baskan.test", "bolum_baskani")]
    public async Task Izinli_roller_giris_yapiyor_access_token_kendi_API_kaynagi_icin(string ad, string kullaniciAdi, string rol)
    {
        var o = Otomasyonlar[ad];
        var secret = await KaydetAsync(o.Tanim);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, o.ClientId, o.RedirectUri, o.Kapsam);

        var yanit = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = o.RedirectUri,
            [Parameters.ClientId] = o.ClientId,
            [Parameters.ClientSecret] = secret,
            [Parameters.CodeVerifier] = pkce.Verifier,
        }));

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var accessToken = OidcYardimcisi.JwtGovdesi(json.RootElement.GetProperty("access_token").GetString()!);
        var aud = accessToken.GetProperty(Claims.Audience);
        var hedefler = aud.ValueKind == JsonValueKind.Array ? aud.EnumerateArray().Select(e => e.GetString()).ToList() : [aud.GetString()];
        Assert.Equal([o.ApiKaynagi], hedefler);
        var kapsamlar = accessToken.GetProperty(Claims.Scope).GetString()!.Split(' ');
        Assert.Contains(o.OkuKapsami, kapsamlar);
        Assert.Contains(o.YazKapsami, kapsamlar);
        var roller = accessToken.GetProperty(Claims.Role);
        Assert.Contains(rol, roller.ValueKind == JsonValueKind.Array ? roller.EnumerateArray().Select(e => e.GetString()) : [roller.GetString()]);
    }

    private async Task<string> UygulamalarimAsync(string kullaniciAdi)
    {
        var yanit = await (await GirisYapmisIstemciAsync(kullaniciAdi)).GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    private static string Kart(Otomasyon o) => $"<span class=\"uygulama-simge uygulama-simge-{o.Vurgu}\" aria-hidden=\"true\">{o.Simge}</span>";

    [Fact]
    public async Task Uygulamalarim_kartlari_izinli_rollere_kayittaki_simgeyle_gorunuyor()
    {
        foreach (var o in Otomasyonlar.Values)
        {
            await KaydetAsync(o.Tanim);
        }

        // Bölüm başkanı (akademisyen, bölüm kurulu üyesi) üçünü de görür.
        var baskan = await UygulamalarimAsync("baskan.test");
        foreach (var o in Otomasyonlar.Values)
        {
            Assert.Contains(Kart(o), baskan);
            Assert.Contains(o.Ad, baskan);
            Assert.Contains($"href=\"https://localhost:{o.Port}/Giris?iss=https%3A%2F%2Flocalhost%3A5001%2F\"", baskan);
        }

        // Akademisyen Etik Kurul ve Lisans Projeleri'ni, bölüm sekreteri yalnızca Bölüm Kurul Karar'ı, öğrenci yalnızca
        // Lisans Projeleri'ni görür.
        var hoca = await UygulamalarimAsync("hoca.test");
        Assert.Contains(Kart(Otomasyonlar["etik"]), hoca);
        Assert.Contains(Kart(Otomasyonlar["proje"]), hoca);
        Assert.DoesNotContain("localhost:5305", hoca);
        var sekreter = await UygulamalarimAsync("sekreter.test");
        Assert.Contains(Kart(Otomasyonlar["kurul"]), sekreter);
        Assert.DoesNotContain("localhost:5303", sekreter);
        Assert.DoesNotContain("localhost:5309", sekreter);
        var ogrenci = await UygulamalarimAsync("203011002");
        Assert.Contains(Kart(Otomasyonlar["proje"]), ogrenci);
        Assert.DoesNotContain("localhost:5303", ogrenci);
        Assert.DoesNotContain("localhost:5305", ogrenci);
    }
}
