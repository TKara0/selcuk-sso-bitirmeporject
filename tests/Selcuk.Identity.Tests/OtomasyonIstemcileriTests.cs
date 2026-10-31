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
using Selcuk.Identity.Pages;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// İntibak (intibak-otomasyonu), Mezuniyet (mezuniyet-otomasyonu) ve ÇAP-Yandal (cap-yandal-otomasyonu) istemcileri:
/// kayıt (gizli istemci, PKCE, kapsamlar, izinli roller, vurgu ve simge), izinli olmayan rollerin access_denied'ı,
/// izinli rollerin access token'ı (aud &lt;ad&gt;-api), Uygulamalarım kartı ve istemci kaydındaki simge. Kullanıcılar
/// Development başlangıç verisinin tanımlarıyla (rastgele şifreyle) oluşturulur.
/// </summary>
public sealed class OtomasyonIstemcileriTests : IDisposable
{
    /// <summary>Bir otomasyon istemcisinin beklenen değerleri.</summary>
    public sealed record Otomasyon(
        string ClientId, int Port, string Ad, Func<string, OpenIddictApplicationDescriptor> Tanim, string ApiKaynagi,
        string OkuKapsami, string YazKapsami, string[] IzinliRoller, string Vurgu, string Simge)
    {
        public string RedirectUri => $"https://localhost:{Port}/signin-oidc";

        public string Kapsam => $"openid profile email roles offline_access {OkuKapsami} {YazKapsami}";
    }

    public static readonly IReadOnlyDictionary<string, Otomasyon> Otomasyonlar = new Dictionary<string, Otomasyon>
    {
        ["intibak"] = new(Istemciler.IntibakOtomasyonuClientId, 5301, "İntibak Otomasyonu", Istemciler.IntibakOtomasyonu,
            "intibak-api", "intibak.oku", "intibak.yaz",
            ["ogrenci", "intibak_komisyonu", "bolum_baskani", "bolum_sekreteri", "fakulte_sekreteri"], "eflatun", "İN"),
        ["mezuniyet"] = new(Istemciler.MezuniyetOtomasyonuClientId, 5304, "Mezuniyet Otomasyonu", Istemciler.MezuniyetOtomasyonu,
            "mezuniyet-api", "mezuniyet.oku", "mezuniyet.yaz",
            ["ogrenci", "mezuniyet_komisyonu", "bolum_sekreteri", "fakulte_sekreteri"], "bakir", "ME"),
        ["cap"] = new(Istemciler.CapYandalOtomasyonuClientId, 5311, "ÇAP-Yandal Otomasyonu", Istemciler.CapYandalOtomasyonu,
            "cap-yandal-api", "cap-yandal.oku", "cap-yandal.yaz",
            ["ogrenci", "cap_yandal_koordinatoru", "bolum_baskani", "bolum_sekreteri"], "hardal", "ÇY"),
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
    [InlineData("intibak")]
    [InlineData("mezuniyet")]
    [InlineData("cap")]
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
        Assert.Equal($"https://localhost:{o.Port}/signout-callback-oidc", Assert.Single(await manager.GetPostLogoutRedirectUrisAsync(uygulama)));
        var izinler = await manager.GetPermissionsAsync(uygulama);
        Assert.Contains(Permissions.Prefixes.Scope + o.OkuKapsami, izinler);
        Assert.Contains(Permissions.Prefixes.Scope + o.YazKapsami, izinler);
        Assert.Contains(Permissions.GrantTypes.AuthorizationCode, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Implicit, izinler);
        Assert.DoesNotContain(Permissions.GrantTypes.Password, izinler);

        var ozellikler = await manager.GetPropertiesAsync(uygulama);
        Assert.Equal($"https://localhost:{o.Port}/signout-backchannel", ozellikler[Istemciler.BackChannelLogoutUriOzelligi].GetString());
        Assert.Equal(o.IzinliRoller, ozellikler[Istemciler.IzinliRollerOzelligi].EnumerateArray().Select(e => e.GetString()!));
        Assert.Equal(o.Vurgu, ozellikler[Istemciler.VurguOzelligi].GetString());
        Assert.Equal(o.Simge, ozellikler[Istemciler.SimgeOzelligi].GetString());
        Assert.False(ozellikler[Istemciler.MfaZorunluOzelligi].GetBoolean());

        // Kapsamlar açılışta oluşturulur; kaynakları <ad>-api.
        var kapsamlar = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var kapsamAdi in new[] { o.OkuKapsami, o.YazKapsami })
        {
            var kapsam = await kapsamlar.FindByNameAsync(kapsamAdi);
            Assert.NotNull(kapsam);
            Assert.Equal(o.ApiKaynagi, Assert.Single(await kapsamlar.GetResourcesAsync(kapsam)));
        }
    }

    [Theory]
    [InlineData("intibak", "etik.test")]
    [InlineData("intibak", "iskur.test")]
    [InlineData("intibak", "hoca.test")]
    [InlineData("mezuniyet", "baskan.test")]
    [InlineData("mezuniyet", "koordinator.test")]
    [InlineData("mezuniyet", "hoca.test")]
    [InlineData("cap", "komisyon.test")]
    [InlineData("cap", "fakultesekreter.test")]
    [InlineData("cap", "hoca.test")]
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
    [InlineData("intibak", "203011001", "ogrenci")]
    [InlineData("intibak", "komisyon.test", "intibak_komisyonu")]
    [InlineData("intibak", "baskan.test", "bolum_baskani")]
    [InlineData("intibak", "sekreter.test", "bolum_sekreteri")]
    [InlineData("intibak", "fakultesekreter.test", "fakulte_sekreteri")]
    [InlineData("mezuniyet", "203011002", "ogrenci")]
    [InlineData("mezuniyet", "komisyon.test", "mezuniyet_komisyonu")]
    [InlineData("mezuniyet", "sekreter.test", "bolum_sekreteri")]
    [InlineData("mezuniyet", "fakultesekreter.test", "fakulte_sekreteri")]
    [InlineData("cap", "203011003", "ogrenci")]
    [InlineData("cap", "koordinator.test", "cap_yandal_koordinatoru")]
    [InlineData("cap", "baskan.test", "bolum_baskani")]
    [InlineData("cap", "sekreter.test", "bolum_sekreteri")]
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
        Assert.Contains(o.ApiKaynagi, hedefler);
        // Başka otomasyonun API'si hedef değildir.
        Assert.DoesNotContain("staj-api", hedefler);
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

    [Fact]
    public async Task Uygulamalarim_kartlari_izinli_rollere_gorunuyor_kayittaki_simgeyle()
    {
        foreach (var o in Otomasyonlar.Values)
        {
            await KaydetAsync(o.Tanim);
        }

        // Bölüm sekreteri üç otomasyona da girebilir.
        var sekreter = await UygulamalarimAsync("sekreter.test");
        foreach (var o in Otomasyonlar.Values)
        {
            Assert.Contains($"<span class=\"uygulama-simge uygulama-simge-{o.Vurgu}\" aria-hidden=\"true\">{o.Simge}</span>", sekreter);
            Assert.Contains(o.Ad, sekreter);
            Assert.Contains($"href=\"https://localhost:{o.Port}/Giris?iss=https%3A%2F%2Flocalhost%3A5001%2F\"", sekreter);
        }

        // Fakülte sekreteri İntibak ve Mezuniyet'i görür, ÇAP-Yandal'ı görmez; koordinatör yalnızca ÇAP-Yandal'ı.
        var fakulte = await UygulamalarimAsync("fakultesekreter.test");
        Assert.Contains("İntibak Otomasyonu", fakulte);
        Assert.Contains("Mezuniyet Otomasyonu", fakulte);
        Assert.DoesNotContain("localhost:5311", fakulte);
        var koordinator = await UygulamalarimAsync("koordinator.test");
        Assert.Contains("ÇAP-Yandal Otomasyonu", koordinator);
        Assert.DoesNotContain("localhost:5301", koordinator);
        Assert.DoesNotContain("localhost:5304", koordinator);

        // Hiçbirine izni olmayan kullanıcı kartları görmez.
        var etik = await UygulamalarimAsync("etik.test");
        foreach (var o in Otomasyonlar.Values)
        {
            Assert.DoesNotContain($"localhost:{o.Port}", etik);
        }
    }

    [Fact]
    public async Task Simge_kayittan_geliyor_simgesiz_veya_gecersiz_simgede_addan_uretiliyor()
    {
        await KaydetAsync(Istemciler.StajOtomasyonu);
        await KaydetAsync(Istemciler.DemoObis);
        // Geçersiz simge (iki harften uzun): addan üretilir.
        await KaydetAsync(secret =>
        {
            var tanim = Istemciler.IntibakOtomasyonu(secret);
            tanim.Properties[Istemciler.SimgeOzelligi] = JsonSerializer.SerializeToElement("UZUN");
            return tanim;
        });

        var html = await UygulamalarimAsync("203011001");

        Assert.Contains("uygulama-simge-zeytin\" aria-hidden=\"true\">ST</span>", html);
        Assert.Contains("<span class=\"uygulama-simge\" aria-hidden=\"true\">DO</span>", html);
        Assert.Contains("uygulama-simge-eflatun\" aria-hidden=\"true\">İO</span>", html);
        Assert.DoesNotContain(">UZUN<", html);

        Assert.Equal("ÇY", IndexModel.GecerliSimge("ÇY"));
        Assert.Null(IndexModel.GecerliSimge("A1"));
        Assert.Null(IndexModel.GecerliSimge(""));
        Assert.Null(IndexModel.GecerliSimge(null));
    }
}
