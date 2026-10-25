using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Pages.Connect;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Tek Kapı (public, PKCE, http://127.0.0.1 loopback adresli istemci) ve istemci bazlı erişim izni (izinliRoller,
/// izinliKullaniciAdlari): authorize, token uç noktası, Uygulamalarım kartları, denetim kaydı ve Back-Channel Logout.
/// </summary>
public sealed class IstemciErisimiTests : IDisposable
{
    private const string TekKapiKapsam = "openid profile roles";
    private const string RolTestClientId = "rol-kisitli-test";
    private const string RolTestRedirectUri = "http://127.0.0.1:4646/auth/callback";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task TekKapiKaydetAsync(params string[] izinliKullaniciAdlari)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tanim = Istemciler.TekKapi(izinliKullaniciAdlari);
        Istemciler.AdresleriDogrula(tanim);
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
    }

    private async Task<HttpClient> GirisYapmisIstemciAsync(string kullaniciAdi, string sifre)
    {
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect,
            (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> TekKapiTokenAlAsync(HttpClient client)
    {
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri,
            TekKapiKapsam);
        return await OidcYardimcisi.PublicTokenIsteAsync(client, code, pkce.Verifier, Istemciler.TekKapiClientId,
            Istemciler.TekKapiRedirectUri);
    }

    /// <summary>Authorize isteğinin istemciye error=access_denied ile döndüğünü doğrular (kod yok, state korunur).</summary>
    private static void ErisimReddedildi(HttpResponseMessage yanit, string redirectUri)
    {
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith(redirectUri + "?", konum.OriginalString);
        var sorgu = QueryHelpers.ParseQuery(konum.Query);
        Assert.Equal(Errors.AccessDenied, sorgu[Parameters.Error].ToString());
        Assert.Equal(AuthorizeModel.IzinYokAciklamasi, sorgu[Parameters.ErrorDescription].ToString());
        Assert.Equal("durum-123", sorgu[Parameters.State].ToString());
        Assert.False(sorgu.ContainsKey(Parameters.Code));
    }

    private Task<List<DenetimKaydi>> ErisimReddiKayitlariAsync(string kullaniciId, string clientId, int adet) =>
        _factory.DenetimKayitlariAsync(k => k.Count(x => x.OlayTuru == DenetimOlayi.IstemciErisimiReddedildi
            && x.KullaniciId == kullaniciId && x.IstemciId == clientId) == adet);

    [Fact]
    public async Task TekKapi_public_istemci_secret_olmadan_PKCE_ile_kod_ve_token_aliyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        await TekKapiKaydetAsync(kullaniciAdi);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var uygulama = (await manager.FindByClientIdAsync(Istemciler.TekKapiClientId))!;
            Assert.Equal(ClientTypes.Public, await manager.GetClientTypeAsync(uygulama));
            Assert.Equal(ConsentTypes.Explicit, await manager.GetConsentTypeAsync(uygulama));
            Assert.True(await manager.HasRequirementAsync(uygulama, Requirements.Features.ProofKeyForCodeExchange));
            Assert.Equal(Istemciler.TekKapiRedirectUri, Assert.Single(await manager.GetRedirectUrisAsync(uygulama)));
            // Public istemcide secret doğrulaması her zaman başarısız: kayıtta secret yok.
            Assert.False(await manager.ValidateClientSecretAsync(uygulama, "herhangi-bir-deger"));
        }

        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);
        var yanit = await TekKapiTokenAlAsync(client);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var token = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var idToken = OidcYardimcisi.JwtGovdesi(token.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(Istemciler.TekKapiClientId, idToken.GetProperty(Claims.Audience).GetString());
        Assert.Equal(Roller.Ogrenci, idToken.GetProperty(Claims.Role).GetString());
        Assert.False(string.IsNullOrEmpty(token.RootElement.GetProperty("access_token").GetString()));
        // Kapsamda e-posta yok (yalnızca openid, profile, roles izinli).
        Assert.False(idToken.TryGetProperty(Claims.Email, out _));
    }

    [Fact]
    public async Task TekKapi_PKCE_olmadan_authorize_reddediliyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        await TekKapiKaydetAsync(kullaniciAdi);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);

        var adres = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.TekKapiClientId,
            [Parameters.RedirectUri] = Istemciler.TekKapiRedirectUri,
            [Parameters.ResponseType] = ResponseTypes.Code,
            [Parameters.Scope] = TekKapiKapsam,
            [Parameters.State] = "durum-123",
        });
        var yanit = await client.GetAsync(adres);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.Contains(Errors.InvalidRequest, govde);
        Assert.Contains("code_challenge", govde);

        // plain yöntemi de kabul edilmez (yalnızca S256).
        var plain = await client.GetAsync(QueryHelpers.AddQueryString(adres, new Dictionary<string, string?>
        {
            [Parameters.CodeChallenge] = OidcYardimcisi.Pkce.Yeni().Verifier,
            [Parameters.CodeChallengeMethod] = CodeChallengeMethods.Plain,
        }));
        Assert.DoesNotContain("code=", plain.Headers.Location?.Query ?? string.Empty);
        Assert.NotEqual(HttpStatusCode.OK, plain.StatusCode);
    }

    [Fact]
    public async Task Izinsiz_kullanici_TekKapi_authorize_isteginde_access_denied_ile_donuyor_ve_denetim_kaydi_olusuyor()
    {
        var (izinli, _) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var (hoca, sifre) = await _factory.KullaniciOlusturAsync(Roller.Akademisyen);
        var hocaId = (await _factory.KullaniciAsync(hoca)).Id;
        await TekKapiKaydetAsync(izinli);
        var client = await GirisYapmisIstemciAsync(hoca, sifre);

        // Onay ekranı gösterilmeden istemciye error=access_denied ile döner.
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var adres = OidcYardimcisi.AuthorizeAdresi(pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri,
            TekKapiKapsam);
        ErisimReddedildi(await client.GetAsync(adres), Istemciler.TekKapiRedirectUri);

        // Onay formu doğrudan gönderilse de kod verilmez.
        var html = await (await client.GetAsync("/Account/SifreDegistir")).Content.ReadAsStringAsync();
        var form = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), adres).Query)
            .Select(p => KeyValuePair.Create(p.Key, p.Value.ToString()))
            .Append(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(html)))
            .Append(KeyValuePair.Create("karar", "kabul"));
        ErisimReddedildi(await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(form)),
            Istemciler.TekKapiRedirectUri);

        var kayitlar = await ErisimReddiKayitlariAsync(hocaId, Istemciler.TekKapiClientId, 2);
        Assert.All(kayitlar.Where(k => k.OlayTuru == DenetimOlayi.IstemciErisimiReddedildi), k =>
        {
            Assert.Equal(DenetimSonucu.Basarisiz, k.Sonuc);
            Assert.Equal("uc=authorize", k.Ayrinti);
        });
        Assert.Equal("İstemciye erişim reddedildi",
            DenetimOlaylari.Ad(DenetimOlayi.IstemciErisimiReddedildi, DenetimSonucu.Basarisiz));
        Assert.DoesNotContain(kayitlar, k => k.OlayTuru == DenetimOlayi.OnayVerildi);

        // Kalıcı yetkilendirme (onay) oluşturulmadı.
        await using var scope = _factory.Services.CreateAsyncScope();
        var yetkilendirmeler = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        Assert.Empty(await yetkilendirmeler.FindBySubjectAsync(hocaId).ToListAsync());
    }

    [Fact]
    public async Task Izinsiz_kullanici_Uygulamalarimda_TekKapi_kartini_gormuyor_izinli_goruyor()
    {
        await _factory.IstemcileriKaydetAsync();
        var (izinli, izinliSifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var (hoca, hocaSifre) = await _factory.KullaniciOlusturAsync(Roller.Akademisyen);
        await TekKapiKaydetAsync(izinli);
        const string TekKapiKarti = "href=\"http://127.0.0.1:4545/auth/login?iss=https%3A%2F%2Flocalhost%3A5001%2F\"";

        async Task<string> SayfaAsync(string kullaniciAdi, string sifre)
        {
            var yanit = await (await GirisYapmisIstemciAsync(kullaniciAdi, sifre)).GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        }

        var izinliSayfa = await SayfaAsync(izinli, izinliSifre);
        Assert.Contains("Tek Kapı — gerçek Selçuk sistemleri (yerel)", izinliSayfa);
        Assert.Contains("Üniversite şifren yalnızca bu bilgisayarda, şifreli saklanır.", izinliSayfa);
        Assert.Contains(TekKapiKarti, izinliSayfa);

        var hocaSayfa = await SayfaAsync(hoca, hocaSifre);
        Assert.DoesNotContain("Tek Kapı", hocaSayfa);
        Assert.DoesNotContain("127.0.0.1", hocaSayfa);
        // Kısıtsız istemciler herkese görünür.
        Assert.All(new[] { izinliSayfa, hocaSayfa }, html =>
        {
            Assert.Contains("Demo OBİS", html);
            Assert.Contains("Demo Yemekhane", html);
        });
    }

    [Fact]
    public async Task IzinliRoller_ile_kisitli_istemcide_rolu_uymayan_reddediliyor_uyan_kabul_ediliyor()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(
                new OpenIddictApplicationDescriptor
                {
                    ClientId = RolTestClientId,
                    ClientType = ClientTypes.Public,
                    ApplicationType = ApplicationTypes.Native,
                    ConsentType = ConsentTypes.Explicit,
                    DisplayName = "Rol Kısıtlı Test",
                    RedirectUris = { new Uri(RolTestRedirectUri) },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Scopes.Profile,
                        Permissions.Scopes.Roles,
                    },
                    Requirements = { Requirements.Features.ProofKeyForCodeExchange },
                    Properties =
                    {
                        // Büyük/küçük harf duyarsız.
                        [Istemciler.IzinliRollerOzelligi] = JsonSerializer.SerializeToElement(new[] { "AKADEMISYEN" }),
                    },
                });
        }

        var (ogrenci, ogrenciSifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var ogrenciId = (await _factory.KullaniciAsync(ogrenci)).Id;
        var (hoca, hocaSifre) = await _factory.KullaniciOlusturAsync(Roller.Akademisyen);

        var ogrenciIstemcisi = await GirisYapmisIstemciAsync(ogrenci, ogrenciSifre);
        ErisimReddedildi(await ogrenciIstemcisi.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(),
            RolTestClientId, RolTestRedirectUri, TekKapiKapsam)), RolTestRedirectUri);
        await ErisimReddiKayitlariAsync(ogrenciId, RolTestClientId, 1);

        var hocaIstemcisi = await GirisYapmisIstemciAsync(hoca, hocaSifre);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(hocaIstemcisi, pkce, RolTestClientId, RolTestRedirectUri, TekKapiKapsam);
        var token = await OidcYardimcisi.PublicTokenIsteAsync(hocaIstemcisi, code, pkce.Verifier, RolTestClientId,
            RolTestRedirectUri);
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
    }

    [Fact]
    public async Task Token_ucu_da_izin_kontrol_ediyor_kod_alindiktan_sonra_izin_kaldirilinca_token_reddediliyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await TekKapiKaydetAsync(kullaniciAdi);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri,
            TekKapiKapsam);

        // Kod alındıktan sonra izin listesinden çıkarılır.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await manager.UpdateAsync((await manager.FindByClientIdAsync(Istemciler.TekKapiClientId))!,
                Istemciler.TekKapi(["baska.kullanici"]));
        }

        var yanit = await OidcYardimcisi.PublicTokenIsteAsync(client, code, pkce.Verifier, Istemciler.TekKapiClientId,
            Istemciler.TekKapiRedirectUri);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var hata = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(Errors.InvalidGrant, hata.RootElement.GetProperty("error").GetString());
        Assert.False(hata.RootElement.TryGetProperty("access_token", out _));

        var kayitlar = await ErisimReddiKayitlariAsync(kullaniciId, Istemciler.TekKapiClientId, 1);
        Assert.Equal("uc=token", kayitlar.Single(k => k.OlayTuru == DenetimOlayi.IstemciErisimiReddedildi).Ayrinti);
        Assert.DoesNotContain(kayitlar, k => k.OlayTuru == DenetimOlayi.TokenVerildi);
    }

    [Fact]
    public async Task Cikista_TekKapinin_backchannel_logout_uri_adresine_de_logout_token_gidiyor()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        await TekKapiKaydetAsync(kullaniciAdi);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.OK, (await TekKapiTokenAlAsync(client)).StatusCode);
        await OidcYardimcisi.TokenAlAsync(client, secret);

        // RP-initiated logout: Tek Kapı'nın http loopback post_logout_redirect_uri'sine döner.
        var sayfa = await client.GetAsync(QueryHelpers.AddQueryString("/connect/logout", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = Istemciler.TekKapiClientId,
            [Parameters.PostLogoutRedirectUri] = Istemciler.TekKapiPostLogoutRedirectUri,
            [Parameters.State] = "cikis",
        }));
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var cikis = await client.PostAsync("/connect/logout",
            new FormUrlEncodedContent(OidcYardimcisi.FormAlanlari(await sayfa.Content.ReadAsStringAsync())));
        Assert.Equal(HttpStatusCode.Redirect, cikis.StatusCode);
        Assert.StartsWith(Istemciler.TekKapiPostLogoutRedirectUri + "?state=cikis", cikis.Headers.Location!.OriginalString);

        var bildirimler = _factory.BackChannel.Istekler.ToList();
        Assert.Equal(new[] { Istemciler.DemoObisBackChannelLogoutUri, Istemciler.TekKapiBackChannelLogoutUri }.Order(),
            bildirimler.Select(b => b.Adres.AbsoluteUri).Order());
        var tekKapi = bildirimler.Single(b => b.Adres.AbsoluteUri == Istemciler.TekKapiBackChannelLogoutUri);
        Assert.Equal("application/x-www-form-urlencoded", tekKapi.IcerikTuru);
        var govde = OidcYardimcisi.JwtGovdesi(tekKapi.Form["logout_token"]);
        Assert.Equal(Istemciler.TekKapiClientId, govde.GetProperty(Claims.Audience).GetString());
        Assert.Equal((await _factory.KullaniciAsync(kullaniciAdi)).Id, govde.GetProperty(Claims.Subject).GetString());
    }

    [Fact]
    public async Task TekKapi_calismiyorsa_cikis_tamamlaniyor_diger_istemciler_yine_bildiriliyor()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await TekKapiKaydetAsync(kullaniciAdi);
        _factory.BackChannel.UlasilamayanAdresler[new Uri(Istemciler.TekKapiBackChannelLogoutUri)] = true;
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.OK, (await TekKapiTokenAlAsync(client)).StatusCode);
        await OidcYardimcisi.TokenAlAsync(client, secret);

        await SelcukIdentityFactory.FormGonderAsync(client, "/connect/logout", []);

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).StatusCode);
        var kayitlar = await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == DenetimOlayi.BackChannelLogoutBasarisiz && x.IstemciId == Istemciler.TekKapiClientId) &&
            k.Any(x => x.OlayTuru == DenetimOlayi.BackChannelLogoutGonderildi && x.IstemciId == Istemciler.DemoObisClientId));
        Assert.Equal("hata=HttpRequestException", kayitlar.Single(x => x.OlayTuru == DenetimOlayi.BackChannelLogoutBasarisiz).Ayrinti);
        Assert.All(await _factory.IstemciOturumlariAsync(kullaniciId), o => Assert.NotNull(o.KapanmaZamani));
    }

    [Fact]
    public async Task Kayit_kurali_disinda_eklenmis_http_redirect_adresi_authorize_ve_cikista_reddediliyor()
    {
        const string ClientId = "http-uzak-test";
        const string UzakAdres = "http://istemci.test.local/callback";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            // OpenIddict http adresini sunucu adına bakmadan kabul eder; tanım AdresleriDogrula'dan geçirilmeden eklenir.
            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(
                new OpenIddictApplicationDescriptor
                {
                    ClientId = ClientId,
                    ClientType = ClientTypes.Public,
                    ApplicationType = ApplicationTypes.Native,
                    RedirectUris = { new Uri(UzakAdres) },
                    PostLogoutRedirectUris = { new Uri(UzakAdres) },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.EndSession,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                    },
                });
        }

        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);

        var authorize = await client.GetAsync(
            OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(), ClientId, UzakAdres, "openid"));
        Assert.Equal(HttpStatusCode.BadRequest, authorize.StatusCode);
        Assert.Null(authorize.Headers.Location);
        Assert.Contains(Errors.InvalidRequest, await authorize.Content.ReadAsStringAsync());

        var cikis = await client.GetAsync(QueryHelpers.AddQueryString("/connect/logout", new Dictionary<string, string?>
        {
            [Parameters.ClientId] = ClientId,
            [Parameters.PostLogoutRedirectUri] = UzakAdres,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, cikis.StatusCode);
        Assert.Null(cikis.Headers.Location);
    }

    [Fact]
    public void Istemci_adresleri_https_veya_yalnizca_http_127_0_0_1_olabiliyor()
    {
        // Tanımlı istemciler kurala uyuyor.
        Istemciler.AdresleriDogrula(Istemciler.DemoObis("secret"));
        Istemciler.AdresleriDogrula(Istemciler.DemoYemekhane("secret"));
        Istemciler.AdresleriDogrula(Istemciler.TekKapi(["203011001"]));
        Assert.True(Istemciler.HttpLoopbackMi(new Uri(Istemciler.TekKapiRedirectUri)));

        foreach (var adres in new[]
        {
            "http://localhost:4545/auth/callback", "http://192.168.1.10:4545/auth/callback",
            "http://127.0.0.1.evil.example/auth/callback", "http://istemci.test.local/callback",
        })
        {
            var tanim = Istemciler.TekKapi(["203011001"]);
            tanim.RedirectUris.Clear();
            tanim.RedirectUris.Add(new Uri(adres));
            Assert.Throws<InvalidOperationException>(() => Istemciler.AdresleriDogrula(tanim));
            Assert.False(Istemciler.HttpLoopbackMi(new Uri(adres)));
        }

        // Özel ayarlardaki adresler de denetlenir.
        var backChannel = Istemciler.TekKapi(["203011001"]);
        backChannel.Properties[Istemciler.BackChannelLogoutUriOzelligi] =
            JsonSerializer.SerializeToElement("http://localhost:4545/auth/backchannel-logout");
        Assert.Throws<InvalidOperationException>(() => Istemciler.AdresleriDogrula(backChannel));

        // İzin listesi boş verilirse Tek Kapı herkese açılmaz: varsayılan kullanıcı.
        Assert.Equal(["203011001"], Istemciler.TekKapi([])
            .Properties[Istemciler.IzinliKullaniciAdlariOzelligi].EnumerateArray().Select(e => e.GetString()));
    }
}
