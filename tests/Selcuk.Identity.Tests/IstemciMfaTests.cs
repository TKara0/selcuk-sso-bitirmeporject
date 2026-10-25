using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Pages.Account;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// İstemci bazlı 2FA şartı (mfaZorunlu, tek-kapi): yalnızca şifreyle veya "Bu cihazı hatırla" ile açılmış oturumda
/// ek doğrulama (step-up), 2FA'sı kapalı kullanıcıda kurulum, token uç noktasında denetim, Uygulamalarım etiketi ve
/// giriş gerektiren sayfalarda Cache-Control: no-store.
/// </summary>
public sealed class IstemciMfaTests : IDisposable
{
    private const string Kapsam = "openid profile roles";
    private const string EkDogrulama = "/Account/EkDogrulama";
    private const string Kurulum = "/Account/IkiAdimDogrulama";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task TekKapiKaydetAsync(params string[] izinliler)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(Istemciler.TekKapi(izinliler));
    }

    private static string TekKapiAuthorize(OidcYardimcisi.Pkce pkce) =>
        OidcYardimcisi.AuthorizeAdresi(pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri, Kapsam);

    private static async Task<JsonElement> TekKapiTokenAsync(HttpClient client, OidcYardimcisi.Pkce pkce)
    {
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri, Kapsam);
        var yanit = await OidcYardimcisi.PublicTokenIsteAsync(client, code, pkce.Verifier, Istemciler.TekKapiClientId,
            Istemciler.TekKapiRedirectUri);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return JsonDocument.Parse(await yanit.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string[] Amr(JsonElement token) =>
        [.. OidcYardimcisi.JwtGovdesi(token.GetProperty("id_token").GetString()!)
            .GetProperty(Claims.AuthenticationMethodReference).EnumerateArray().Select(e => e.GetString()!)];

    private async Task<int> HataliDenemeSayisiAsync(string kullaniciAdi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.GetAccessFailedCountAsync((await userManager.FindByNameAsync(kullaniciAdi))!);
    }

    /// <summary>
    /// Açık oturumu düşürmeden 2FA'yı açar: anahtar ve 2FA bayrağı depoya yazılır, güvenlik damgası değişmez. Böylece
    /// "yalnızca şifreyle açılmış oturum + 2FA'sı açık kullanıcı" durumu kurulur (ör. 2FA'nın oturum açıldıktan sonra
    /// başka bir yoldan açılması). Kurulum sayfası ve Identity API'si damgayı yenileyip oturumu kapattığı için kullanılmaz.
    /// </summary>
    private async Task<string> IkiAdimiDamgaDegismedenAcAsync(string kullaniciAdi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var user = (await userManager.FindByNameAsync(kullaniciAdi))!;
        var damga = user.SecurityStamp;
        var anahtar = userManager.GenerateNewAuthenticatorKey();
        await ((IUserAuthenticatorKeyStore<ApplicationUser>)store).SetAuthenticatorKeyAsync(user, anahtar, CancellationToken.None);
        await ((IUserTwoFactorStore<ApplicationUser>)store).SetTwoFactorEnabledAsync(user, true, CancellationToken.None);
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        Assert.Equal(damga, (await userManager.FindByNameAsync(kullaniciAdi))!.SecurityStamp);
        return anahtar;
    }

    private static async Task<string> MetinAsync(HttpResponseMessage yanit) =>
        WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());

    [Fact]
    public async Task MfaZorunlu_istemci_sifreyle_acilmis_oturumda_ek_dogrulama_istiyor_TOTP_sonrasi_kod_ve_amr_mfa()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await TekKapiKaydetAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var sid = _factory.OturumSid(giris);
        var anahtar = await IkiAdimiDamgaDegismedenAcAsync(kullaniciAdi);
        var kodlar = Totp.Kodlar(anahtar, 0);

        // Oturum yalnızca şifreyle açık (amr ["pwd"]): authorize ek doğrulamaya, dönüş adresiyle yönlendirir; kod yok.
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var authorize = await client.GetAsync(TekKapiAuthorize(pkce));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var konum = authorize.Headers.Location!.OriginalString;
        Assert.StartsWith(EkDogrulama + "?returnUrl=", konum);
        Assert.Equal(TekKapiAuthorize(pkce), QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), konum).Query)["returnUrl"].ToString());

        var sayfa = await client.GetAsync(konum);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        Assert.Contains("Bu uygulama iki aşamalı doğrulama gerektiriyor", await MetinAsync(sayfa));

        // Yanlış kod: reddedilir ve hatalı deneme sayılır.
        var yanlis = await SelcukIdentityFactory.FormGonderAsync(client, konum, new() { ["Input.Kod"] = Totp.Gecersiz(anahtar) });
        Assert.Equal(HttpStatusCode.OK, yanlis.StatusCode);
        Assert.Contains(EkDogrulamaModel.HataMesaji, await MetinAsync(yanlis));
        Assert.Equal(1, await HataliDenemeSayisiAsync(kullaniciAdi));

        // Doğru TOTP: oturum çok faktörlü olur (aynı sid), authorize isteğine dönülür.
        var dogru = await SelcukIdentityFactory.FormGonderAsync(client, konum, new() { ["Input.Kod"] = kodlar[0] });
        Assert.Equal(HttpStatusCode.Redirect, dogru.StatusCode);
        Assert.Equal(TekKapiAuthorize(pkce), dogru.Headers.Location!.OriginalString);
        Assert.Equal(sid, _factory.OturumSid(dogru));
        Assert.Equal(0, await HataliDenemeSayisiAsync(kullaniciAdi));

        var token = await TekKapiTokenAsync(client, pkce);
        Assert.Equal(["pwd", "mfa"], Amr(token));

        // Oturum artık çok faktörlü: ek doğrulama sayfası tekrar sormadan dönüş adresine gider.
        var tekrar = await client.GetAsync(konum);
        Assert.Equal(HttpStatusCode.Redirect, tekrar.StatusCode);
        Assert.Equal(TekKapiAuthorize(pkce), tekrar.Headers.Location!.OriginalString);

        var kayitlar = await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == DenetimOlayi.EkDogrulama && x.Sonuc == DenetimSonucu.Basarili && x.KullaniciId == kullaniciId) &&
            k.Any(x => x.OlayTuru == DenetimOlayi.TokenVerildi && x.IstemciId == Istemciler.TekKapiClientId));
        var istendi = kayitlar.Single(x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli);
        Assert.Equal((kullaniciId, Istemciler.TekKapiClientId, "uc=authorize; yonlendirme=ek-dogrulama"),
            (istendi.KullaniciId, istendi.IstemciId, istendi.Ayrinti));
        Assert.Contains(kayitlar, x => x.OlayTuru == DenetimOlayi.EkDogrulama && x.Sonuc == DenetimSonucu.Basarisiz
            && x.Ayrinti == "yontem=totp");
        Assert.Equal("yontem=totp", kayitlar.Single(x => x.OlayTuru == DenetimOlayi.EkDogrulama && x.Sonuc == DenetimSonucu.Basarili).Ayrinti);
    }

    [Fact]
    public async Task IkiAdimi_kapali_kullanici_MfaZorunlu_istemci_icin_kuruluma_yonleniyor_kurulumdan_once_token_alamiyor()
    {
        await _factory.IstemcileriKaydetAsync();
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await TekKapiKaydetAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);

        // Uygulamalarım: Tek Kapı kartında "2FA gerekli" etiketi, demo kartlarında yok.
        var html = await MetinAsync(await client.GetAsync("/"));
        var tekKapiKarti = html[html.IndexOf("Tek Kapı", StringComparison.Ordinal)..];
        Assert.Contains("2FA gerekli", tekKapiKarti[..tekKapiKarti.IndexOf("</a>", StringComparison.Ordinal)]);
        Assert.Equal(1, html.Split("2FA gerekli").Length - 1);

        var pkce = OidcYardimcisi.Pkce.Yeni();
        var authorize = await client.GetAsync(TekKapiAuthorize(pkce));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var konum = authorize.Headers.Location!.OriginalString;
        Assert.StartsWith(Kurulum + "?returnUrl=", konum);
        Assert.Contains("Bu uygulama iki aşamalı doğrulama gerektiriyor", await MetinAsync(await client.GetAsync(konum)));

        // Kurulumdan önce: onay formu doğrudan gönderilse de kod verilmez.
        var formHtml = await (await client.GetAsync("/Account/SifreDegistir")).Content.ReadAsStringAsync();
        var form = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), TekKapiAuthorize(pkce)).Query)
            .Select(p => KeyValuePair.Create(p.Key, p.Value.ToString()))
            .Append(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(formHtml)))
            .Append(KeyValuePair.Create("karar", "kabul"));
        var dogrudan = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, dogrudan.StatusCode);
        var sorgu = QueryHelpers.ParseQuery(dogrudan.Headers.Location!.Query);
        Assert.Equal(Errors.AccessDenied, sorgu[Parameters.Error].ToString());
        Assert.False(sorgu.ContainsKey(Parameters.Code));

        // Kurulum: başlat → anahtar → kod. Kurtarma kodlarının yanında "devam et" authorize isteğine döner.
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.FormGonderAsync(client, konum, [],
            Kurulum + "?handler=Baslat&returnUrl=" + Uri.EscapeDataString(TekKapiAuthorize(pkce)))).StatusCode);
        var anahtar = (await _factory.AnahtarAsync(kullaniciAdi))!;
        var acma = await SelcukIdentityFactory.FormGonderAsync(client, konum, new() { ["Etkinlestirme.Kod"] = Totp.Uret(anahtar) },
            Kurulum + "?handler=Etkinlestir&returnUrl=" + Uri.EscapeDataString(TekKapiAuthorize(pkce)));
        Assert.Equal(HttpStatusCode.OK, acma.StatusCode);
        Assert.Contains($"href=\"{TekKapiAuthorize(pkce)}\"", await MetinAsync(acma));

        Assert.Equal(["pwd", "mfa"], Amr(await TekKapiTokenAsync(client, pkce)));
        var kayitlar = await _factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli));
        Assert.Contains(kayitlar, x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli && x.KullaniciId == kullaniciId
            && x.Ayrinti == "uc=authorize; yonlendirme=kurulum");
        Assert.Contains(kayitlar, x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli && x.Ayrinti == "uc=authorize; neden=onay-formu");
    }

    [Fact]
    public async Task Bu_cihazi_hatirla_ile_acilmis_oturumda_ek_dogrulama_isteniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        await TekKapiKaydetAsync(kullaniciAdi);
        var (anahtar, kurtarmaKodlari) = await _factory.IkiAdimAcAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();

        // İlk giriş: şifre + TOTP, "Bu cihazı hatırla". Çıkış.
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.IkinciAdimAsync(client, Totp.Uret(anahtar), cihaziHatirla: true)).StatusCode);
        await SelcukIdentityFactory.FormGonderAsync(client, "/connect/logout", []);

        // İkinci giriş yalnızca şifreyle tamamlanır (cihaz hatırlandı), oturum amr ["pwd"].
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal("/", giris.Headers.Location!.OriginalString);

        var pkce = OidcYardimcisi.Pkce.Yeni();
        var authorize = await client.GetAsync(TekKapiAuthorize(pkce));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var konum = authorize.Headers.Location!.OriginalString;
        Assert.StartsWith(EkDogrulama + "?returnUrl=", konum);

        // Ek doğrulama kurtarma koduyla da yapılabilir.
        var dogru = await SelcukIdentityFactory.FormGonderAsync(client, konum, new() { ["Input.Kod"] = kurtarmaKodlari[0] });
        Assert.Equal(HttpStatusCode.Redirect, dogru.StatusCode);
        Assert.Equal(["pwd", "mfa"], Amr(await TekKapiTokenAsync(client, pkce)));

        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == DenetimOlayi.GirisBasarili && x.KullaniciId == kullaniciId && x.Ayrinti == "amr=pwd; cihaz-hatirlandi") &&
            k.Any(x => x.OlayTuru == DenetimOlayi.EkDogrulama && x.Sonuc == DenetimSonucu.Basarili && x.Ayrinti == "yontem=kurtarma-kodu") &&
            k.Any(x => x.OlayTuru == DenetimOlayi.KurtarmaKoduKullanildi && x.KullaniciId == kullaniciId));
    }

    [Fact]
    public async Task Token_ucu_amr_mfa_icermeyen_yetkilendirmeyi_MfaZorunlu_istemci_icin_reddediyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;

        // Kod, istemci henüz 2FA istemezken yalnızca şifreyle açılmış oturumdan alınır.
        var tanim = Istemciler.TekKapi([kullaniciAdi]);
        tanim.Properties[Istemciler.MfaZorunluOzelligi] = JsonSerializer.SerializeToElement(false);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
        }

        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.TekKapiClientId, Istemciler.TekKapiRedirectUri, Kapsam);

        // Ardından istemci 2FA ister hâle gelir: token uç noktası amr'si "mfa" içermeyen kodu reddeder.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await manager.UpdateAsync((await manager.FindByClientIdAsync(Istemciler.TekKapiClientId))!, Istemciler.TekKapi([kullaniciAdi]));
        }

        var yanit = await OidcYardimcisi.PublicTokenIsteAsync(client, code, pkce.Verifier, Istemciler.TekKapiClientId,
            Istemciler.TekKapiRedirectUri);
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var hata = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(Errors.InvalidGrant, hata.RootElement.GetProperty("error").GetString());
        Assert.False(hata.RootElement.TryGetProperty("access_token", out _));

        var kayitlar = await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli && x.KullaniciId == kullaniciId));
        Assert.Equal("uc=token", kayitlar.Single(x => x.OlayTuru == DenetimOlayi.IstemciMfaGerekli).Ayrinti);
        Assert.DoesNotContain(kayitlar, x => x.OlayTuru == DenetimOlayi.TokenVerildi);
    }

    [Fact]
    public async Task Giris_gerektiren_hesap_sayfalarinda_no_store_var_herkese_acik_sayfa_ve_statik_dosyada_yok()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();

        // Girişsiz: korumalı sayfanın girişe yönlendirmesi de önbelleğe alınmaz.
        Assert.True((await client.GetAsync("/Account/Hesabim")).Headers.CacheControl?.NoStore);

        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        foreach (var sayfa in new[] { "/", "/Account/Hesabim", "/Account/IkiAdimDogrulama", "/Account/SifreDegistir",
                     "/Account/GuvenlikGecmisi", EkDogrulama })
        {
            var yanit = await client.GetAsync(sayfa);
            Assert.True(yanit.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect, sayfa);
            Assert.True(yanit.Headers.CacheControl?.NoStore, sayfa);
        }

        foreach (var acik in new[] { "/Account/AccessDenied", "/css/site.css" })
        {
            var yanit = await client.GetAsync(acik);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.False(yanit.Headers.CacheControl?.NoStore ?? false, acik);
        }
    }
}
