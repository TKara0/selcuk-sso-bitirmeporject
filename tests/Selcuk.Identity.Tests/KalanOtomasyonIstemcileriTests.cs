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
/// Öğrenci Danışmanlık (danismanlik-otomasyonu), İŞKUR (iskur-otomasyonu), MÜDEK (mudek-otomasyonu) ve Sınav Başarısı
/// Analizi (sinav-analizi-otomasyonu) istemcileri: kayıt, izinli olmayan rollerin access_denied'ı, izinli rollerin
/// access token'ı (aud &lt;ad&gt;-api), Uygulamalarım kartları ve simgeler. Kullanıcılar Development başlangıç
/// verisinin tanımlarıyla (rastgele şifreyle) oluşturulur.
/// </summary>
public sealed class KalanOtomasyonIstemcileriTests : IDisposable
{
    public static readonly IReadOnlyDictionary<string, Otomasyon> Otomasyonlar = new Dictionary<string, Otomasyon>
    {
        ["danismanlik"] = new(Istemciler.DanismanlikOtomasyonuClientId, 5306, "Öğrenci Danışmanlık Otomasyonu", Istemciler.DanismanlikOtomasyonu,
            "danismanlik-api", "danismanlik.oku", "danismanlik.yaz", ["danisman", "ogrenci", "bolum_baskani"], "indigo", "DA"),
        ["iskur"] = new(Istemciler.IskurOtomasyonuClientId, 5307, "İŞKUR Otomasyonu", Istemciler.IskurOtomasyonu,
            "iskur-api", "iskur.oku", "iskur.yaz", ["iskur_sorumlusu", "ogrenci"], "petrol", "İŞ"),
        ["mudek"] = new(Istemciler.MudekOtomasyonuClientId, 5308, "MÜDEK Otomasyonu", Istemciler.MudekOtomasyonu,
            "mudek-api", "mudek.oku", "mudek.yaz", ["mudek_komisyonu", "akademisyen", "bolum_baskani", "bolum_sekreteri"], "gulkurusu", "MÜ"),
        ["sinav"] = new(Istemciler.SinavAnaliziOtomasyonuClientId, 5310, "Sınav Başarısı Analizi Otomasyonu", Istemciler.SinavAnaliziOtomasyonu,
            "sinav-analizi-api", "sinav-analizi.oku", "sinav-analizi.yaz", ["akademisyen", "bolum_baskani", "mudek_komisyonu"], "grafit", "SA"),
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
    [InlineData("danismanlik")]
    [InlineData("iskur")]
    [InlineData("mudek")]
    [InlineData("sinav")]
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
    [InlineData("danismanlik", "etik.test")]
    [InlineData("danismanlik", "sekreter.test")]
    [InlineData("danismanlik", "iskur.test")]
    [InlineData("iskur", "hoca.test")]
    [InlineData("iskur", "sekreter.test")]
    [InlineData("iskur", "baskan.test")]
    [InlineData("mudek", "203011001")]
    [InlineData("mudek", "iskur.test")]
    [InlineData("mudek", "fakultesekreter.test")]
    [InlineData("sinav", "203011001")]
    [InlineData("sinav", "sekreter.test")]
    [InlineData("sinav", "iskur.test")]
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
    [InlineData("danismanlik", "hoca.test", "danisman")]
    [InlineData("danismanlik", "koordinator.test", "danisman")]
    [InlineData("danismanlik", "203011002", "ogrenci")]
    [InlineData("danismanlik", "baskan.test", "bolum_baskani")]
    [InlineData("iskur", "iskur.test", "iskur_sorumlusu")]
    [InlineData("iskur", "203011001", "ogrenci")]
    [InlineData("mudek", "mudek.test", "mudek_komisyonu")]
    [InlineData("mudek", "hoca.test", "akademisyen")]
    [InlineData("mudek", "baskan.test", "bolum_baskani")]
    [InlineData("mudek", "sekreter.test", "bolum_sekreteri")]
    [InlineData("sinav", "hoca.test", "akademisyen")]
    [InlineData("sinav", "baskan.test", "bolum_baskani")]
    [InlineData("sinav", "mudek.test", "mudek_komisyonu")]
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

        // Bölüm başkanı Danışmanlık, MÜDEK ve Sınav Analizi kartlarını görür, İŞKUR kartını görmez.
        var baskan = await UygulamalarimAsync("baskan.test");
        foreach (var ad in new[] { "danismanlik", "mudek", "sinav" })
        {
            var o = Otomasyonlar[ad];
            Assert.Contains(Kart(o), baskan);
            Assert.Contains(o.Ad, baskan);
            Assert.Contains($"href=\"https://localhost:{o.Port}/Giris?iss=https%3A%2F%2Flocalhost%3A5001%2F\"", baskan);
        }

        Assert.DoesNotContain("localhost:5307", baskan);

        // İŞKUR sorumlusu yalnızca İŞKUR, bölüm sekreteri yalnızca MÜDEK, öğrenci Danışmanlık ve İŞKUR kartını görür.
        var iskur = await UygulamalarimAsync("iskur.test");
        Assert.Contains(Kart(Otomasyonlar["iskur"]), iskur);
        Assert.DoesNotContain("localhost:5306", iskur);
        Assert.DoesNotContain("localhost:5308", iskur);
        Assert.DoesNotContain("localhost:5310", iskur);
        var sekreter = await UygulamalarimAsync("sekreter.test");
        Assert.Contains(Kart(Otomasyonlar["mudek"]), sekreter);
        Assert.DoesNotContain("localhost:5306", sekreter);
        Assert.DoesNotContain("localhost:5310", sekreter);
        var ogrenci = await UygulamalarimAsync("203011002");
        Assert.Contains(Kart(Otomasyonlar["danismanlik"]), ogrenci);
        Assert.Contains(Kart(Otomasyonlar["iskur"]), ogrenci);
        Assert.DoesNotContain("localhost:5308", ogrenci);
        Assert.DoesNotContain("localhost:5310", ogrenci);
    }
}
