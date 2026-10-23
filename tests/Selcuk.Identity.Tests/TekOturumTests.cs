using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Tek giriş (aynı kimlik sunucusu oturumuyla ikinci istemciye şifresiz geçiş) ve OIDC Back-Channel Logout
/// ile tek çıkış testleri. demo-obis ve demo-yemekhane, uygulamanın kendi tanımlarıyla kaydedilir.
/// </summary>
public sealed partial class TekOturumTests : IDisposable
{
    private const string Issuer = "https://localhost:5001/";

    private readonly SelcukIdentityFactory _factory = new();
    private readonly Dictionary<string, string> _secretler = new()
    {
        [Istemciler.DemoObisClientId] = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
        [Istemciler.DemoYemekhaneClientId] = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32)),
    };

    public void Dispose() => _factory.Dispose();

    private static readonly Dictionary<string, (string Redirect, string PostLogout, string BackChannel)> Adresler = new()
    {
        [Istemciler.DemoObisClientId] = (Istemciler.DemoObisRedirectUri,
            Istemciler.DemoObisPostLogoutRedirectUri, Istemciler.DemoObisBackChannelLogoutUri),
        [Istemciler.DemoYemekhaneClientId] = (Istemciler.DemoYemekhaneRedirectUri,
            Istemciler.DemoYemekhanePostLogoutRedirectUri, Istemciler.DemoYemekhaneBackChannelLogoutUri),
    };

    private sealed record Oturum(HttpClient Client, string KullaniciId);

    private async Task<Oturum> HazirlaVeGirisYapAsync()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await manager.CreateAsync(Istemciler.DemoObis(_secretler[Istemciler.DemoObisClientId]));
            await manager.CreateAsync(Istemciler.DemoYemekhane(_secretler[Istemciler.DemoYemekhaneClientId]));
        }

        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, "Teknoloji Fakültesi", "Bilgisayar Mühendisliği");
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);

        await using var s = _factory.Services.CreateAsyncScope();
        var kullanici = await s.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(kullaniciAdi);
        return new Oturum(client, kullanici!.Id);
    }

    private static (string Verifier, string Challenge) YeniPkce()
    {
        var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (verifier, Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    private static string AuthorizeAdresi(string clientId, string challenge) =>
        QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = clientId,
            [Parameters.RedirectUri] = Adresler[clientId].Redirect,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = "openid profile email roles",
            [Parameters.State] = "durum-" + clientId,
            [Parameters.Nonce] = "nonce-" + clientId,
            [Parameters.CodeChallenge] = challenge,
            [Parameters.CodeChallengeMethod] = CodeChallengeMethods.Sha256,
        });

    private static List<KeyValuePair<string, string>> GizliAlanlar(string html) =>
        [.. GizliAlan().Matches(html).Select(m => KeyValuePair.Create(
            WebUtility.HtmlDecode(m.Groups[1].Value), WebUtility.HtmlDecode(m.Groups[2].Value)))];

    private static string KoduAl(HttpResponseMessage yanit, string clientId)
    {
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith(Adresler[clientId].Redirect + "?", konum.OriginalString);
        var sorgu = QueryHelpers.ParseQuery(konum.Query);
        Assert.Equal("durum-" + clientId, sorgu[Parameters.State].ToString());
        return sorgu[Parameters.Code].ToString();
    }

    /// <summary>İlk yetkilendirme: giriş formu değil onay ekranı gelir; "İzin ver" ile kod alınır.</summary>
    private static async Task<string> OnaylaVeKodAlAsync(HttpClient client, string clientId, string challenge, string gorunenAd)
    {
        var onay = await client.GetAsync(AuthorizeAdresi(clientId, challenge));
        Assert.Equal(HttpStatusCode.OK, onay.StatusCode);
        var html = await onay.Content.ReadAsStringAsync();
        Assert.Contains(gorunenAd, WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("Input.Sifre", html);

        var alanlar = GizliAlanlar(html);
        alanlar.Add(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)));
        alanlar.Add(KeyValuePair.Create("karar", "kabul"));
        return KoduAl(await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(alanlar)), clientId);
    }

    private async Task<JsonWebToken> IdTokenAlAsync(HttpClient client, string clientId, string code, string verifier)
    {
        var yanit = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.AuthorizationCode,
            [Parameters.Code] = code,
            [Parameters.RedirectUri] = Adresler[clientId].Redirect,
            [Parameters.ClientId] = clientId,
            [Parameters.ClientSecret] = _secretler[clientId],
            [Parameters.CodeVerifier] = verifier,
        }));
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);

        using var belge = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var sonuc = await new JsonWebTokenHandler().ValidateTokenAsync(
            belge.RootElement.GetProperty("id_token").GetString()!, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = clientId,
                IssuerSigningKeys = await ImzaAnahtarlariAsync(client),
            });
        Assert.True(sonuc.IsValid, sonuc.Exception?.Message);
        return (JsonWebToken)sonuc.SecurityToken;
    }

    private static async Task<IList<SecurityKey>> ImzaAnahtarlariAsync(HttpClient client) =>
        new JsonWebKeySet(await client.GetStringAsync("/.well-known/jwks")).GetSigningKeys();

    /// <summary>Kullanıcıyı iki istemciye de (aynı kimlik sunucusu oturumuyla) sokar; id_token'ları döndürür.</summary>
    private async Task<(JsonWebToken Obis, JsonWebToken Yemekhane)> IkiIstemciyeGirAsync(HttpClient client)
    {
        var obisPkce = YeniPkce();
        var obisKod = await OnaylaVeKodAlAsync(client, Istemciler.DemoObisClientId, obisPkce.Challenge, "Demo OBİS");
        var obis = await IdTokenAlAsync(client, Istemciler.DemoObisClientId, obisKod, obisPkce.Verifier);

        var yemekhanePkce = YeniPkce();
        var yemekhaneKod = await OnaylaVeKodAlAsync(client, Istemciler.DemoYemekhaneClientId, yemekhanePkce.Challenge, "Demo Yemekhane");
        var yemekhane = await IdTokenAlAsync(client, Istemciler.DemoYemekhaneClientId, yemekhaneKod, yemekhanePkce.Verifier);
        return (obis, yemekhane);
    }

    private static async Task<HttpResponseMessage> CikisYapAsync(HttpClient client)
    {
        var adres = QueryHelpers.AddQueryString("/connect/logout", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.DemoYemekhaneClientId,
            [Parameters.PostLogoutRedirectUri] = Istemciler.DemoYemekhanePostLogoutRedirectUri,
            [Parameters.State] = "cikis",
        });
        var sayfa = await client.GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = await sayfa.Content.ReadAsStringAsync();

        var alanlar = GizliAlanlar(html);
        alanlar.Add(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)));
        var yanit = await client.PostAsync("/connect/logout", new FormUrlEncodedContent(alanlar));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(Istemciler.DemoYemekhanePostLogoutRedirectUri, yanit.Headers.Location!.OriginalString);
        return yanit;
    }

    private async Task<List<IstemciOturumu>> IstemciOturumlariAsync(string sid)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.IstemciOturumlari.AsNoTracking().Where(o => o.Sid == sid).ToListAsync();
    }

    [Fact]
    public async Task Oturum_aciksa_ikinci_istemci_sifre_sormadan_kod_aliyor()
    {
        var oturum = await HazirlaVeGirisYapAsync();

        var obisPkce = YeniPkce();
        await OnaylaVeKodAlAsync(oturum.Client, Istemciler.DemoObisClientId, obisPkce.Challenge, "Demo OBİS");

        // İlk kez: giriş formu yok, yalnızca onay ekranı (OnaylaVeKodAlAsync bunu doğrular).
        await OnaylaVeKodAlAsync(oturum.Client, Istemciler.DemoYemekhaneClientId, YeniPkce().Challenge, "Demo Yemekhane");

        // Onay verildikten sonra: ne giriş formu ne onay ekranı, doğrudan koda yönlendirme.
        var yanit = await oturum.Client.GetAsync(AuthorizeAdresi(Istemciler.DemoYemekhaneClientId, YeniPkce().Challenge));
        Assert.False(string.IsNullOrEmpty(KoduAl(yanit, Istemciler.DemoYemekhaneClientId)));
        Assert.DoesNotContain("/Account/Login", yanit.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Iki_istemcinin_id_tokeninda_ayni_sid_var()
    {
        var oturum = await HazirlaVeGirisYapAsync();

        var (obis, yemekhane) = await IkiIstemciyeGirAsync(oturum.Client);

        var sid = obis.GetClaim(OidcKimlik.SidClaim).Value;
        Assert.False(string.IsNullOrEmpty(sid));
        Assert.Equal(sid, yemekhane.GetClaim(OidcKimlik.SidClaim).Value);
        Assert.Equal(oturum.KullaniciId, obis.Subject);

        var kayitlar = await IstemciOturumlariAsync(sid);
        Assert.Equal([Istemciler.DemoObisClientId, Istemciler.DemoYemekhaneClientId],
            kayitlar.Select(k => k.ClientId).Order());
        Assert.All(kayitlar, k => Assert.Null(k.KapanmaZamani));
    }

    [Fact]
    public async Task Cikista_her_istemciye_dogrulanabilir_logout_token_gidiyor()
    {
        var oturum = await HazirlaVeGirisYapAsync();
        var (obis, _) = await IkiIstemciyeGirAsync(oturum.Client);
        var sid = obis.GetClaim(OidcKimlik.SidClaim).Value;

        await CikisYapAsync(oturum.Client);

        var istekler = _factory.BackChannel.Istekler.ToList();
        Assert.Equal(2, istekler.Count);
        var anahtarlar = await ImzaAnahtarlariAsync(oturum.Client);

        foreach (var clientId in new[] { Istemciler.DemoObisClientId, Istemciler.DemoYemekhaneClientId })
        {
            var istek = Assert.Single(istekler, i => i.Adres == new Uri(Adresler[clientId].BackChannel));
            Assert.Equal("application/x-www-form-urlencoded", istek.IcerikTuru);
            var token = istek.Form["logout_token"];

            var sonuc = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = clientId,
                IssuerSigningKeys = anahtarlar,
                ValidTypes = ["logout+jwt"],
            });
            Assert.True(sonuc.IsValid, sonuc.Exception?.Message);

            var jwt = (JsonWebToken)sonuc.SecurityToken;
            Assert.Equal("logout+jwt", jwt.Typ);
            Assert.Equal(sid, jwt.GetClaim("sid").Value);
            Assert.Equal(oturum.KullaniciId, jwt.Subject);
            Assert.False(string.IsNullOrEmpty(jwt.Id), "jti yok.");
            Assert.False(jwt.TryGetClaim("nonce", out _), "logout_token nonce içermemeli.");
            Assert.InRange(jwt.ValidTo - jwt.IssuedAt, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5));

            using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(jwt.EncodedPayload));
            var events = payload.RootElement.GetProperty("events");
            Assert.Equal(JsonValueKind.Object,
                events.GetProperty(BackChannelLogoutGonderici.OlayAdi).ValueKind);
        }

        Assert.All(await IstemciOturumlariAsync(sid), k => Assert.NotNull(k.KapanmaZamani));
    }

    [Fact]
    public async Task Bir_istemciye_ulasilamasa_da_digerine_gonderiliyor()
    {
        var oturum = await HazirlaVeGirisYapAsync();
        var (obis, _) = await IkiIstemciyeGirAsync(oturum.Client);
        _factory.BackChannel.UlasilamayanAdresler[new Uri(Istemciler.DemoObisBackChannelLogoutUri)] = true;

        await CikisYapAsync(oturum.Client);

        var adresler = _factory.BackChannel.Istekler.Select(i => i.Adres.AbsoluteUri).Order().ToList();
        Assert.Equal([Istemciler.DemoObisBackChannelLogoutUri, Istemciler.DemoYemekhaneBackChannelLogoutUri], adresler);
        Assert.All(await IstemciOturumlariAsync(obis.GetClaim(OidcKimlik.SidClaim).Value), k => Assert.NotNull(k.KapanmaZamani));
    }

    [Fact]
    public async Task Discovery_back_channel_logout_ve_claims_supported_iceriyor()
    {
        using var belge = JsonDocument.Parse(
            await _factory.HttpsIstemcisi().GetStringAsync("/.well-known/openid-configuration"));
        var kok = belge.RootElement;

        Assert.True(kok.GetProperty("backchannel_logout_supported").GetBoolean());
        Assert.True(kok.GetProperty("backchannel_logout_session_supported").GetBoolean());

        var claimler = kok.GetProperty("claims_supported").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
        Assert.Superset(new HashSet<string> { "sub", "name", "email", "role", "fakulte", "bolum", "sid" }, claimler);
    }

    [Fact]
    public async Task Uygulamalarim_sayfasi_istemci_kartlarini_gosteriyor()
    {
        // Girişsiz: giriş sayfasına yönlendirilir.
        var girissiz = await _factory.HttpsIstemcisi().GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, girissiz.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", girissiz.Headers.Location!.ToString());

        var oturum = await HazirlaVeGirisYapAsync();
        var yanit = await oturum.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("Demo OBİS", html);
        Assert.Contains("Demo Yemekhane", html);
        Assert.Contains("href=\"https://localhost:5101/Giris\"", html);
        Assert.Contains("href=\"https://localhost:5102/Giris\"", html);
    }

    [GeneratedRegex("<input type=\"hidden\" name=\"([^\"]+)\" value=\"([^\"]*)\" />")]
    private static partial Regex GizliAlan();
}
