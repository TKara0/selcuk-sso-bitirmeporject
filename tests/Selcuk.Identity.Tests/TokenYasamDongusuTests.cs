using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Token yaşam döngüsü: küçültülmüş access token (veri minimizasyonu), refresh token (offline_access, kayan, tekrar
/// kullanım tespiti), revocation uç noktası (RFC 7009) ve çıkış/şifre değişikliğinde oturum bazında iptal.
/// </summary>
public sealed class TokenYasamDongusuTests : IDisposable
{
    private const string TumKapsamlar = "openid profile email roles offline_access notlar.oku";
    private readonly LogYakalayici _loglar = new();
    private readonly SelcukIdentityFactory _factory;

    public TokenYasamDongusuTests() =>
        _factory = new SelcukIdentityFactory(services => services.AddSingleton<ILoggerProvider>(_loglar));

    public void Dispose() => _factory.Dispose();

    private sealed record Tokenlar(string Access, string Id, string Refresh, string Code, string Verifier);

    private async Task<HttpClient> GirisAsync(string kullaniciAdi, string sifre)
    {
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        return client;
    }

    private static async Task<Tokenlar> TokenAlAsync(HttpClient client, string secret, string kapsam = TumKapsamlar)
    {
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.DemoObisClientId, Istemciler.DemoObisRedirectUri, kapsam);
        var yanit = await OidcYardimcisi.TokenIsteAsync(client, code, pkce.Verifier, secret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = json.RootElement;
        return new(kok.GetProperty("access_token").GetString()!, kok.GetProperty("id_token").GetString()!,
            kok.TryGetProperty("refresh_token", out var r) ? r.GetString()! : string.Empty, code, pkce.Verifier);
    }

    private static Task<HttpResponseMessage> YenileAsync(HttpClient client, string refreshToken, string secret) =>
        client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.RefreshToken,
            [Parameters.RefreshToken] = refreshToken,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
        }));

    private static async Task<(string Access, string Refresh)> YenileBasariliAsync(HttpClient client, string refreshToken, string secret)
    {
        var yanit = await YenileAsync(client, refreshToken, secret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("access_token").GetString()!, json.RootElement.GetProperty("refresh_token").GetString()!);
    }

    private static async Task ReddedildiAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(Errors.InvalidGrant, json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Access_token_kucultuldu_profil_bilgileri_yalnizca_id_tokenda_aud_demo_api()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, "Teknoloji Fakültesi", "Bilgisayar Mühendisliği");
        var client = await GirisAsync(kullaniciAdi, sifre);
        var tokenlar = await TokenAlAsync(client, secret);

        // Demo OBİS girişte userinfo'yu bu (aud = demo-api) access token'la çağırır; profil bilgisi oradan gelir.
        using (var istek = new HttpRequestMessage(HttpMethod.Get, "/connect/userinfo"))
        {
            istek.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenlar.Access);
            var userinfo = await client.SendAsync(istek);
            Assert.Equal(HttpStatusCode.OK, userinfo.StatusCode);
            using var bilgi = JsonDocument.Parse(await userinfo.Content.ReadAsStringAsync());
            Assert.Equal("Bilgisayar Mühendisliği", bilgi.RootElement.GetProperty("bolum").GetString());
        }

        var access = OidcYardimcisi.JwtGovdesi(tokenlar.Access);
        var adlar = access.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Superset(new HashSet<string> { "iss", "sub", "aud", "exp", "iat", "jti", "client_id", "scope", "role", "sid" }, adlar);
        foreach (var yasak in new[] { "name", "email", "fakulte", "bolum", "preferred_username", "amr" })
        {
            Assert.DoesNotContain(yasak, adlar);
        }

        // Yalnızca bunlar ve OpenIddict'in kişisel veri içermeyen iç tanımlayıcıları var: oi_prst (token'ı alan istemci),
        // oi_au_id (yetkilendirme kimliği), oi_tkn_id (token kaydı). OpenIddict, token kendisine geri geldiğinde
        // (ör. userinfo) iptal durumunu bunlarla denetler.
        Assert.True(adlar.IsSubsetOf(["iss", "sub", "aud", "exp", "iat", "jti", "client_id", "scope", "role", "sid",
                "oi_prst", "oi_au_id", "oi_tkn_id"]),
            "Access token'da beklenmeyen claim: " + string.Join(", ", adlar));
        Assert.Equal(Istemciler.DemoApiKaynagi, access.GetProperty("aud").GetString());
        Assert.Contains(Istemciler.NotlarOkuKapsami, access.GetProperty("scope").GetString()!.Split(' '));
        Assert.InRange(access.GetProperty("exp").GetInt64() - access.GetProperty("iat").GetInt64(), 14 * 60, 15 * 60);

        var id = OidcYardimcisi.JwtGovdesi(tokenlar.Id);
        Assert.Equal("Test Kullanıcısı", id.GetProperty("name").GetString());
        Assert.Equal(kullaniciAdi, id.GetProperty("preferred_username").GetString());
        Assert.Equal("Teknoloji Fakültesi", id.GetProperty("fakulte").GetString());
        Assert.Equal("Bilgisayar Mühendisliği", id.GetProperty("bolum").GetString());
        Assert.EndsWith("@test.local", id.GetProperty("email").GetString());
        Assert.Equal(access.GetProperty("sid").GetString(), id.GetProperty("sid").GetString());
    }

    [Fact]
    public async Task Offline_access_ile_refresh_token_aliniyor_kayan_yenileme_eski_token_ikinci_kez_reddediliyor()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var client = await GirisAsync(kullaniciAdi, sifre);

        // offline_access yoksa refresh token verilmez.
        Assert.Equal(string.Empty, (await TokenAlAsync(client, secret, "openid profile roles")).Refresh);

        var ilk = await TokenAlAsync(client, secret);
        Assert.False(string.IsNullOrEmpty(ilk.Refresh));
        var (yeniAccess, yeniRefresh) = await YenileBasariliAsync(client, ilk.Refresh, secret);
        Assert.NotEqual(ilk.Access, yeniAccess);
        Assert.NotEqual(ilk.Refresh, yeniRefresh);
        Assert.Equal(OidcYardimcisi.JwtGovdesi(ilk.Access).GetProperty("sub").GetString(),
            OidcYardimcisi.JwtGovdesi(yeniAccess).GetProperty("sub").GetString());

        await ReddedildiAsync(await YenileAsync(client, ilk.Refresh, secret));
    }

    [Fact]
    public async Task Kullanilmis_refresh_token_tekrar_gelince_yetkilendirmenin_tum_tokenlari_iptal_ve_denetim_kaydi()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        var client = await GirisAsync(kullaniciAdi, sifre);
        var baskaOturum = await TokenAlAsync(await GirisAsync(kullaniciAdi, sifre), secret);

        var ilk = await TokenAlAsync(client, secret);
        var (_, ikinci) = await YenileBasariliAsync(client, ilk.Refresh, secret);

        // Çalınma belirtisi: kullanılmış refresh token tekrar geliyor.
        await ReddedildiAsync(await YenileAsync(client, ilk.Refresh, secret));
        // Aynı yetkilendirmenin yeni refresh token'ı da artık geçmez.
        await ReddedildiAsync(await YenileAsync(client, ikinci, secret));
        // Başka oturumun (başka yetkilendirmenin) zinciri etkilenmez.
        await YenileBasariliAsync(_factory.HttpsIstemcisi(), baskaOturum.Refresh, secret);

        var kayitlar = await _factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.RefreshTokenTekrarKullanildi));
        var kayit = Assert.Single(kayitlar, x => x.OlayTuru == DenetimOlayi.RefreshTokenTekrarKullanildi);
        Assert.Equal((kullaniciId, Istemciler.DemoObisClientId, DenetimSonucu.Basarisiz), (kayit.KullaniciId, kayit.IstemciId, kayit.Sonuc));
        Assert.Equal("Refresh token tekrar kullanıldı", Denetim.DenetimOlaylari.Ad(DenetimOlayi.RefreshTokenTekrarKullanildi, DenetimSonucu.Basarisiz));
    }

    [Fact]
    public async Task TekKapi_offline_access_isteyemiyor()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(Istemciler.TekKapi(["203011001"]));
        }

        var adres = OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(), Istemciler.TekKapiClientId,
            Istemciler.TekKapiRedirectUri, "openid profile roles offline_access");
        var yanit = await _factory.HttpsIstemcisi().GetAsync(adres);

        // OpenIddict isteği istemciye yönlendirmeden reddeder (refresh_token grant izni olmayan istemci offline_access
        // isteyemez); kod verilmez.
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        var govde = await yanit.Content.ReadAsStringAsync();
        Assert.Contains("offline_access", govde);
        Assert.DoesNotContain("code=", govde);
    }

    [Fact]
    public async Task Revocation_ucu_refresh_tokeni_iptal_ediyor_discovery_ve_onay_ekrani()
    {
        // Discovery'de revocation_endpoint var.
        using (var discovery = JsonDocument.Parse(await _factory.HttpsIstemcisi().GetStringAsync("/.well-known/openid-configuration")))
        {
            // Uç nokta adresleri isteğin adresinden üretilir (testte https://localhost); issuer ayardan gelir.
            Assert.EndsWith("/connect/revoke", discovery.RootElement.GetProperty("revocation_endpoint").GetString());
        }

        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var client = await GirisAsync(kullaniciAdi, sifre);

        // Onay ekranı: no-store ve yeni kapsamların Türkçe açıklamaları.
        var onay = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni(), Istemciler.DemoObisClientId,
            Istemciler.DemoObisRedirectUri, TumKapsamlar));
        Assert.Equal(HttpStatusCode.OK, onay.StatusCode);
        Assert.True(onay.Headers.CacheControl?.NoStore);
        var html = WebUtility.HtmlDecode(await onay.Content.ReadAsStringAsync());
        Assert.Contains("Uygulama, oturumun açıkken erişimini sen tekrar giriş yapmadan yenileyebilir.", html);
        Assert.Contains("Not bilgilerini okuma", html);

        var tokenlar = await TokenAlAsync(client, secret);
        var iptal = await client.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.Token] = tokenlar.Refresh,
            [Parameters.TokenTypeHint] = TokenTypeHints.RefreshToken,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
        }));
        Assert.Equal(HttpStatusCode.OK, iptal.StatusCode);
        await ReddedildiAsync(await YenileAsync(client, tokenlar.Refresh, secret));
    }

    [Fact]
    public async Task Sifre_degistirmede_diger_oturumlarin_refresh_tokenlari_iptal_mevcut_calisiyor_sifirlamada_hepsi()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var bu = await GirisAsync(kullaniciAdi, sifre);
        var diger = await GirisAsync(kullaniciAdi, sifre);
        var buTokenlar = await TokenAlAsync(bu, secret);
        var digerTokenlar = await TokenAlAsync(diger, secret);

        var yeniSifre = $"Cc3{Guid.NewGuid():N}";
        var degistir = await SelcukIdentityFactory.FormGonderAsync(bu, "/Account/SifreDegistir", new()
        {
            ["Input.MevcutSifre"] = sifre,
            ["Input.YeniSifre"] = yeniSifre,
            ["Input.YeniSifreTekrar"] = yeniSifre,
        });
        Assert.Equal(HttpStatusCode.Redirect, degistir.StatusCode);

        await ReddedildiAsync(await YenileAsync(diger, digerTokenlar.Refresh, secret));
        var (_, buYeniRefresh) = await YenileBasariliAsync(bu, buTokenlar.Refresh, secret);

        // Şifre sıfırlama (e-postadaki bağlantının yaptığı gibi): tüm oturumların token'ları iptal.
        var kullanici = await _factory.KullaniciAsync(kullaniciAdi);
        string kod;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            kod = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
                await userManager.GeneratePasswordResetTokenAsync((await userManager.FindByIdAsync(kullanici.Id))!)));
        }

        var sifirlamaSifresi = $"Dd4{Guid.NewGuid():N}";
        var sifirla = await SelcukIdentityFactory.FormGonderAsync(_factory.HttpsIstemcisi(),
            $"/Account/SifreSifirla?kullanici={kullanici.Id}&kod={kod}", new()
            {
                ["Input.Kullanici"] = kullanici.Id,
                ["Input.Kod"] = kod,
                ["Input.YeniSifre"] = sifirlamaSifresi,
                ["Input.YeniSifreTekrar"] = sifirlamaSifresi,
            });
        Assert.Contains("Şifreniz değiştirildi", WebUtility.HtmlDecode(await sifirla.Content.ReadAsStringAsync()));
        await ReddedildiAsync(await YenileAsync(bu, buYeniRefresh, secret));
    }

    [Fact]
    public async Task Kimlik_sunucusundan_cikista_o_side_bagli_refresh_tokenlar_iptal()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var cikan = await GirisAsync(kullaniciAdi, sifre);
        var kalan = await GirisAsync(kullaniciAdi, sifre);
        var cikanTokenlar = await TokenAlAsync(cikan, secret);
        var kalanTokenlar = await TokenAlAsync(kalan, secret);

        await SelcukIdentityFactory.FormGonderAsync(cikan, "/connect/logout", []);

        await ReddedildiAsync(await YenileAsync(cikan, cikanTokenlar.Refresh, secret));
        await YenileBasariliAsync(kalan, kalanTokenlar.Refresh, secret);
    }

    [Fact]
    public async Task Loglarda_ve_denetim_kaydinda_token_degeri_yok()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var client = await GirisAsync(kullaniciAdi, sifre);

        var tokenlar = await TokenAlAsync(client, secret);
        var (yeniAccess, yeniRefresh) = await YenileBasariliAsync(client, tokenlar.Refresh, secret);
        await ReddedildiAsync(await YenileAsync(client, tokenlar.Refresh, secret));
        var ikinci = await TokenAlAsync(client, secret);
        await client.PostAsync("/connect/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.Token] = ikinci.Refresh,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
        }));
        await SelcukIdentityFactory.FormGonderAsync(client, "/connect/logout", []);

        var sirlar = new[] { tokenlar.Access, tokenlar.Id, tokenlar.Refresh, tokenlar.Code, tokenlar.Verifier, yeniAccess, yeniRefresh,
            ikinci.Access, ikinci.Refresh, ikinci.Code, secret, sifre };
        await _factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.RefreshTokenTekrarKullanildi)
            && k.Any(x => x.OlayTuru == DenetimOlayi.Cikis));
        var tablo = (await _factory.TabloAsync("DenetimKayitlari")).SelectMany(s => s).OfType<string>().ToList();
        var loglar = _loglar.Mesajlar.ToList();
        Assert.True(loglar.Count > 10, $"Yakalanan log sayısı az: {loglar.Count}");
        string[] adlar = ["access", "id", "refresh", "code", "verifier", "yeniAccess", "yeniRefresh", "ikinciAccess",
            "ikinciRefresh", "ikinciCode", "secret", "sifre"];
        for (var i = 0; i < sirlar.Length; i++)
        {
            var sir = sirlar[i];
            Assert.DoesNotContain(tablo, d => d.Contains(sir, StringComparison.Ordinal));
            var eslesen = loglar.FirstOrDefault(m => m.Contains(sir, StringComparison.Ordinal));
            Assert.True(eslesen is null, $"Logda {adlar[i]} var; kategori: {eslesen?[..eslesen.IndexOf(':')]}");
        }
    }

    /// <summary>Uygulamanın yazdığı tüm log iletilerini (yapılandırılmış seviyede) bellekte toplar.</summary>
    private sealed class LogYakalayici : ILoggerProvider
    {
        public ConcurrentQueue<string> Mesajlar { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Kaydedici(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class Kaydedici(LogYakalayici sahip, string kategori) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                sahip.Mesajlar.Enqueue($"{kategori}: {formatter(state, exception)} {exception}");
        }
    }
}
