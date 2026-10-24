using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Selcuk.Identity.Data;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Pages.Account;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// TOTP tabanlı iki aşamalı doğrulama: kurulum, kurtarma kodları, giriş ikinci adımı, kilitleme, rol bazlı zorunluluk,
/// kapatma ve id_token'daki amr claim'i.
/// </summary>
public sealed partial class IkiAdimTests : IDisposable
{
    private const string Kurulum = "/Account/IkiAdimDogrulama";
    private const string OturumCerezi = "SelcukSSO.Oturum";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<T> KullaniciIleAsync<T>(SelcukIdentityFactory factory, string kullaniciAdi,
        Func<UserManager<ApplicationUser>, ApplicationUser, Task<T>> islem)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await islem(userManager, (await userManager.FindByNameAsync(kullaniciAdi))!);
    }

    private Task<bool> IkiAdimAcikMiAsync(string kullaniciAdi, SelcukIdentityFactory? factory = null) =>
        KullaniciIleAsync(factory ?? _factory, kullaniciAdi, (um, k) => um.GetTwoFactorEnabledAsync(k));

    private Task<int> HataliDenemeSayisiAsync(string kullaniciAdi) =>
        KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.GetAccessFailedCountAsync(k));

    private static async Task<string> MetinAsync(HttpResponseMessage yanit) =>
        WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());

    private static bool OturumCereziVar(HttpResponseMessage yanit) =>
        yanit.Headers.TryGetValues("Set-Cookie", out var cerezler) &&
        cerezler.Any(c => c.StartsWith(OturumCerezi + "=", StringComparison.Ordinal));

    /// <summary>Kurulum sayfasındaki bir formu (handler) gönderir.</summary>
    private static Task<HttpResponseMessage> KurulumFormuAsync(HttpClient client, string handler,
        Dictionary<string, string>? alanlar = null) =>
        SelcukIdentityFactory.FormGonderAsync(client, Kurulum, alanlar ?? [], $"{Kurulum}?handler={handler}");

    /// <summary>Kurulum sayfasından 2FA'yı açar (başlat → anahtar → kod) ve anahtarla kurtarma kodlarını döndürür.</summary>
    private async Task<(string Anahtar, List<string> Kodlar)> SayfadanIkiAdimAcAsync(
        HttpClient client, string kullaniciAdi, SelcukIdentityFactory? factory = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await KurulumFormuAsync(client, "Baslat")).StatusCode);
        var anahtar = (await KullaniciIleAsync(factory ?? _factory, kullaniciAdi, (um, k) => um.GetAuthenticatorKeyAsync(k)))!;

        var yanit = await KurulumFormuAsync(client, "Etkinlestir", new() { ["Etkinlestirme.Kod"] = Totp.Uret(anahtar) });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return (anahtar, KurtarmaKodlari(await yanit.Content.ReadAsStringAsync()));
    }

    private static List<string> KurtarmaKodlari(string html) =>
        [.. KurtarmaKodu().Matches(html).Select(m => m.Groups[1].Value)];

    [Fact]
    public async Task Totp_yardimcisi_Identity_dogrulayicisinin_kabul_ettigi_kodu_uretiyor()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();
        var (anahtar, _) = await _factory.IkiAdimAcAsync(kullaniciAdi);

        Task<bool> DogrulaAsync(string kod) => KullaniciIleAsync(_factory, kullaniciAdi,
            (um, k) => um.VerifyTwoFactorTokenAsync(k, TokenOptions.DefaultAuthenticatorProvider, kod));

        Assert.True(await DogrulaAsync(Totp.Uret(anahtar)));
        Assert.False(await DogrulaAsync(Totp.Gecersiz(anahtar)));
    }

    [Fact]
    public async Task Kurulum_gecerli_kod_olmadan_acilmiyor_gecerli_kodla_aciliyor_10_kurtarma_kodu_donuyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);

        var ilk = await MetinAsync(await client.GetAsync(Kurulum));
        Assert.Contains("Kurulumu başlat", ilk);

        Assert.Equal(HttpStatusCode.Redirect, (await KurulumFormuAsync(client, "Baslat")).StatusCode);
        var anahtar = (await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.GetAuthenticatorKeyAsync(k)))!;

        // Anahtar metin (4'lü gruplar) ve sunucuda üretilmiş SVG QR kod olarak; issuer "Selcuk SSO (Test)".
        var sayfa = await client.GetAsync(Kurulum);
        Assert.True(sayfa.Headers.CacheControl?.NoStore, "Anahtar içeren sayfa önbelleğe alınmamalı.");
        Assert.Contains("img-src 'self' data:", sayfa.Headers.GetValues("Content-Security-Policy").Single());
        var html = await MetinAsync(sayfa);
        Assert.Contains(IkiAdimIslemleri.AnahtarBicimle(anahtar), html);
        Assert.Matches("^([A-Z2-7]{4} )+[A-Z2-7]{1,4}$", IkiAdimIslemleri.AnahtarBicimle(anahtar));
        Assert.Contains($"otpauth://totp/Selcuk%20SSO%20%28Test%29:{kullaniciAdi}?secret={anahtar}" +
                        "&issuer=Selcuk%20SSO%20%28Test%29&digits=6", html);
        Assert.DoesNotContain("<script", html);
        var qr = QrKod().Match(html).Groups[1].Value;
        Assert.Contains("<svg", Encoding.UTF8.GetString(Convert.FromBase64String(qr)));

        // Kod yok veya yanlış: 2FA açılmaz.
        foreach (var kod in new[] { "", Totp.Gecersiz(anahtar) })
        {
            var red = await KurulumFormuAsync(client, "Etkinlestir", new() { ["Etkinlestirme.Kod"] = kod });
            Assert.Equal(HttpStatusCode.OK, red.StatusCode);
            Assert.Contains(IkiAdimDogrulamaModel.KodHatasiMesaji, await MetinAsync(red));
            Assert.False(await IkiAdimAcikMiAsync(kullaniciAdi));
        }

        var yanit = await KurulumFormuAsync(client, "Etkinlestir", new() { ["Etkinlestirme.Kod"] = Totp.Uret(anahtar) });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.True(yanit.Headers.CacheControl?.NoStore, "Kurtarma kodlarını içeren yanıt önbelleğe alınmamalı.");
        var kodlar = KurtarmaKodlari(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(10, kodlar.Count);
        Assert.Equal(10, kodlar.Distinct().Count());
        Assert.True(await IkiAdimAcikMiAsync(kullaniciAdi));
        Assert.Equal(10, await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.CountRecoveryCodesAsync(k)));
        Assert.Equal(EpostaSablonlari.IkiAdimAcildiKonusu, (await _factory.Eposta.SiradakiAsync()).Konu);

        // Kodlar yalnızca bir kez gösterilir; oturum açık kalır (damga yenilendi, çerez yeniden yazıldı).
        var tekrar = await client.GetAsync(Kurulum);
        Assert.Equal(HttpStatusCode.OK, tekrar.StatusCode);
        var tekrarHtml = await tekrar.Content.ReadAsStringAsync();
        Assert.All(kodlar, kod => Assert.DoesNotContain(kod, tekrarHtml));
        Assert.Contains("Kalan kurtarma kodu", WebUtility.HtmlDecode(tekrarHtml));
    }

    [Fact]
    public async Task Kurtarma_kodlari_veritabaninda_duz_metin_olarak_bulunmuyor()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();
        var (_, kodlar) = await _factory.IkiAdimAcAsync(kullaniciAdi);

        var tokenlar = await _factory.TabloAsync("AspNetUserTokens");
        var kullanicilar = await _factory.TabloAsync("AspNetUsers");
        var degerler = tokenlar.Concat(kullanicilar).SelectMany(satir => satir).OfType<string>().ToList();
        foreach (var kod in kodlar)
        {
            Assert.DoesNotContain(degerler, d => d.Contains(kod, StringComparison.OrdinalIgnoreCase)
                || d.Contains(kod.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
        }

        // Identity'nin düz metin kaydı yok; yerine 10 PBKDF2 özeti (IPasswordHasher v3 biçimi) var.
        Assert.DoesNotContain(tokenlar, satir => satir.Contains("RecoveryCodes"));
        var ozetler = tokenlar.Single(satir => satir.Contains("KurtarmaKoduOzetleri")).Last()!.Split(';');
        Assert.Equal(10, ozetler.Length);
        Assert.All(ozetler, ozet => Assert.Equal(0x01, Convert.FromBase64String(ozet)[0]));

        // Özetlenmiş kod yine de kullanılabilir (küçük harf ve tiresiz yazılsa da) ve tek kullanımlıktır.
        var girilen = kodlar[0].Replace("-", "").ToLowerInvariant();
        Assert.True((await KullaniciIleAsync(_factory, kullaniciAdi,
            (um, k) => um.RedeemTwoFactorRecoveryCodeAsync(k, girilen))).Succeeded);
        Assert.False((await KullaniciIleAsync(_factory, kullaniciAdi,
            (um, k) => um.RedeemTwoFactorRecoveryCodeAsync(k, kodlar[0]))).Succeeded);
        Assert.Equal(9, await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.CountRecoveryCodesAsync(k)));
    }

    [Fact]
    public async Task IkiAdim_acik_kullanici_sifreyle_girince_ikinci_adima_yonleniyor_kod_olmadan_token_alamiyor()
    {
        await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        await _factory.IkiAdimAcAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();

        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        Assert.StartsWith("/Account/IkiAdim?", giris.Headers.Location!.OriginalString);
        Assert.False(OturumCereziVar(giris), "Şifre adımında oturum çerezi verilmemeli.");

        var ikinciAdim = await client.GetAsync(giris.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, ikinciAdim.StatusCode);
        Assert.Contains("name=\"Input.Kod\"", await ikinciAdim.Content.ReadAsStringAsync());

        // Kod girilmeden kimlik sunucusunda oturum yok: authorize kod vermez, girişe yönlendirir.
        var authorize = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni()));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", authorize.Headers.Location!.ToString());
        Assert.DoesNotContain("code=", authorize.Headers.Location.Query);
        Assert.StartsWith("https://localhost/Account/Login", (await client.GetAsync("/")).Headers.Location!.ToString());
    }

    [Fact]
    public async Task Dogru_TOTP_ile_giris_tamamlaniyor_amr_mfa_iceriyor_sadece_sifreyle_amr_pwd()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);

        var (ikiAdimli, sifre1) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var (anahtar, _) = await _factory.IkiAdimAcAsync(ikiAdimli);
        var client1 = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client1, ikiAdimli, sifre1);
        var dogrulama = await SelcukIdentityFactory.IkinciAdimAsync(client1, Totp.Uret(anahtar));
        Assert.Equal(HttpStatusCode.Redirect, dogrulama.StatusCode);
        Assert.Equal("/", dogrulama.Headers.Location!.OriginalString);
        Assert.True(OturumCereziVar(dogrulama));

        var token1 = await OidcYardimcisi.TokenAlAsync(client1, secret);
        var amr1 = OidcYardimcisi.JwtGovdesi(token1.GetProperty("id_token").GetString()!).GetProperty(Claims.AuthenticationMethodReference);
        Assert.Equal(JsonValueKind.Array, amr1.ValueKind);
        Assert.Equal(["pwd", "mfa"], amr1.EnumerateArray().Select(e => e.GetString()));

        var (sadeceSifre, sifre2) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var client2 = _factory.HttpsIstemcisi();
        Assert.Equal("/", (await SelcukIdentityFactory.GirisYapAsync(client2, sadeceSifre, sifre2)).Headers.Location!.OriginalString);
        var token2 = await OidcYardimcisi.TokenAlAsync(client2, secret);
        var amr2 = OidcYardimcisi.JwtGovdesi(token2.GetProperty("id_token").GetString()!).GetProperty(Claims.AuthenticationMethodReference);
        Assert.Equal(JsonValueKind.Array, amr2.ValueKind);
        Assert.Equal(["pwd"], amr2.EnumerateArray().Select(e => e.GetString()));

        // amr yalnızca id_token'da; access token'a yazılmaz.
        Assert.False(OidcYardimcisi.JwtGovdesi(token1.GetProperty("access_token").GetString()!)
            .TryGetProperty(Claims.AuthenticationMethodReference, out _));

        var kullaniciId = (await _factory.KullaniciAsync(ikiAdimli)).Id;
        await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == DenetimOlayi.IkiAdimBasarili && x.KullaniciId == kullaniciId) &&
            k.Any(x => x.OlayTuru == DenetimOlayi.TokenVerildi && x.KullaniciId == kullaniciId
                && x.IstemciId == Istemciler.DemoObisClientId));
    }

    [Fact]
    public async Task Kurtarma_koduyla_giris_calisiyor_ayni_kod_ikinci_kez_reddediliyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var (_, kodlar) = await _factory.IkiAdimAcAsync(kullaniciAdi);

        var client1 = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client1, kullaniciAdi, sifre);
        var ilk = await SelcukIdentityFactory.IkinciAdimAsync(client1, kodlar[0]);
        Assert.Equal(HttpStatusCode.Redirect, ilk.StatusCode);
        Assert.Equal("/", ilk.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client1.GetAsync("/")).StatusCode);

        var eposta = await _factory.Eposta.SiradakiAsync();
        Assert.Equal(EpostaSablonlari.KurtarmaKoduKullanildiKonusu, eposta.Konu);
        Assert.Contains("Kalan kurtarma kodu sayısı: 9", eposta.Govde);

        // Aynı kod başka bir girişte reddedilir ve hatalı deneme sayılır (Identity kurtarma kodunu kendisi saymaz).
        var client2 = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client2, kullaniciAdi, sifre);
        var ikinci = await SelcukIdentityFactory.IkinciAdimAsync(client2, kodlar[0]);
        Assert.Equal(HttpStatusCode.OK, ikinci.StatusCode);
        Assert.Contains(IkiAdimModel.HataMesaji, await MetinAsync(ikinci));
        Assert.False(OturumCereziVar(ikinci));
        Assert.Equal(1, await HataliDenemeSayisiAsync(kullaniciAdi));

        // Başka bir kod küçük harf ve boşlukla yazılsa da geçer.
        var baska = await SelcukIdentityFactory.IkinciAdimAsync(client2, kodlar[1].ToLowerInvariant().Replace('-', ' '));
        Assert.Equal(HttpStatusCode.Redirect, baska.StatusCode);
        Assert.Equal(8, await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.CountRecoveryCodesAsync(k)));
    }

    [Fact]
    public async Task Bes_hatali_TOTP_denemesinde_hesap_kilitleniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var (anahtar, _) = await _factory.IkiAdimAcAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);

        for (var i = 1; i <= 5; i++)
        {
            var yanit = await SelcukIdentityFactory.IkinciAdimAsync(client, Totp.Gecersiz(anahtar));
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.Contains(IkiAdimModel.HataMesaji, await MetinAsync(yanit));
        }

        Assert.True(await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.IsLockedOutAsync(k)));

        // Kilitliyken doğru kod da reddedilir.
        var dogru = await SelcukIdentityFactory.IkinciAdimAsync(client, Totp.Uret(anahtar));
        Assert.Equal(HttpStatusCode.OK, dogru.StatusCode);
        Assert.Contains(IkiAdimModel.HataMesaji, await MetinAsync(dogru));
        Assert.False(OturumCereziVar(dogru));

        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await _factory.DenetimKayitlariAsync(k =>
            k.Count(x => x.OlayTuru == DenetimOlayi.IkiAdimBasarisiz && x.KullaniciId == kullaniciId) == 6 &&
            k.Any(x => x.OlayTuru == DenetimOlayi.HesapKilitlendi && x.KullaniciId == kullaniciId));
    }

    [Fact]
    public async Task Zorunlu_rol_kullanicisi_kuruluma_zorlaniyor_kurulumdan_once_token_alamiyor()
    {
        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            ["IkiAdimZorunluRoller:0"] = Roller.Akademisyen,
        });
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(factory);

        // Rolü listede olmayan kullanıcı zorlanmaz.
        var (ogrenci, ogrenciSifre) = await factory.KullaniciOlusturAsync(Roller.Ogrenci);
        Assert.Equal("/", (await SelcukIdentityFactory.GirisYapAsync(factory.HttpsIstemcisi(), ogrenci, ogrenciSifre))
            .Headers.Location!.OriginalString);

        var (hoca, sifre) = await factory.KullaniciOlusturAsync(Roller.Akademisyen);
        var client = factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, hoca, sifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        Assert.StartsWith(Kurulum, giris.Headers.Location!.OriginalString);
        Assert.Contains("zorunludur", await MetinAsync(await client.GetAsync(giris.Headers.Location)));

        // Authorize kod vermez, kurulum sayfasına (dönüş adresiyle) yönlendirir.
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var authorize = await client.GetAsync(OidcYardimcisi.AuthorizeAdresi(pkce));
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        Assert.StartsWith(Kurulum + "?returnUrl=", authorize.Headers.Location!.OriginalString);

        // Onay formu doğrudan gönderilse de kod verilmez.
        var kurulumHtml = await (await client.GetAsync(Kurulum)).Content.ReadAsStringAsync();
        var form = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), OidcYardimcisi.AuthorizeAdresi(pkce)).Query)
            .Select(p => KeyValuePair.Create(p.Key, p.Value.ToString()))
            .Append(KeyValuePair.Create("__RequestVerificationToken", SelcukIdentityFactory.AntiforgeryTokenAl(kurulumHtml)))
            .Append(KeyValuePair.Create("karar", "kabul"));
        var dogrudan = await client.PostAsync("/connect/authorize", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, dogrudan.StatusCode);
        var dogrudanSorgu = QueryHelpers.ParseQuery(dogrudan.Headers.Location!.Query);
        Assert.False(dogrudanSorgu.ContainsKey(Parameters.Code));
        Assert.Equal(Errors.AccessDenied, dogrudanSorgu[Parameters.Error].ToString());

        // Kurulumdan sonra token alınır; kurulumda TOTP doğrulandığı için oturum çok faktörlüdür.
        await SayfadanIkiAdimAcAsync(client, hoca, factory);
        var token = await OidcYardimcisi.TokenAlAsync(client, secret);
        Assert.Contains("mfa", OidcYardimcisi.JwtGovdesi(token.GetProperty("id_token").GetString()!)
            .GetProperty(Claims.AuthenticationMethodReference).EnumerateArray().Select(e => e.GetString()));

        // Zorunlu rolde 2FA kapatılamaz.
        var anahtar = (await KullaniciIleAsync(factory, hoca, (um, k) => um.GetAuthenticatorKeyAsync(k)))!;
        var kapat = await KurulumFormuAsync(client, "Kapat", new() { ["Onay.Sifre"] = sifre, ["Onay.Kod"] = Totp.Uret(anahtar) });
        Assert.Contains("zorunludur", await MetinAsync(kapat));
        Assert.True(await IkiAdimAcikMiAsync(hoca, factory));
    }

    [Fact]
    public async Task Kurulum_zorunlulugundan_once_alinan_kod_token_ucunda_reddediliyor()
    {
        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            ["IkiAdimZorunluRoller:0"] = Roller.Personel,
        });
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(factory);
        var (kullaniciAdi, sifre) = await factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var client = factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce);

        // Kod alındıktan sonra kullanıcıya 2FA zorunlu bir rol verilir: token uç noktası da kurulumu şart koşar.
        await KullaniciIleAsync(factory, kullaniciAdi, (um, k) => um.AddToRoleAsync(k, Roller.Personel));
        var yanit = await OidcYardimcisi.TokenIsteAsync(client, code, pkce.Verifier, secret);

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var hata = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(Errors.InvalidGrant, hata.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task IkiAdim_kapatma_yanlis_sifre_veya_yanlis_kodla_reddediliyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var (anahtar, _) = await _factory.IkiAdimAcAsync(kullaniciAdi);
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.IkinciAdimAsync(client, Totp.Uret(anahtar))).StatusCode);

        var denemeler = new[]
        {
            ("Kapat", "YanlisSifre123", Totp.Uret(anahtar)),
            ("Kapat", sifre, Totp.Gecersiz(anahtar)),
            ("KodlariYenile", sifre, Totp.Gecersiz(anahtar)),
        };
        foreach (var (handler, denemeSifresi, kod) in denemeler)
        {
            var red = await KurulumFormuAsync(client, handler, new() { ["Onay.Sifre"] = denemeSifresi, ["Onay.Kod"] = kod });
            Assert.Equal(HttpStatusCode.OK, red.StatusCode);
            Assert.Contains(IkiAdimDogrulamaModel.OnayHatasiMesaji, await MetinAsync(red));
            Assert.True(await IkiAdimAcikMiAsync(kullaniciAdi));
        }

        Assert.Equal(3, await HataliDenemeSayisiAsync(kullaniciAdi));
        Assert.Empty(_factory.Eposta.Gonderilenler);

        var kapat = await KurulumFormuAsync(client, "Kapat", new() { ["Onay.Sifre"] = sifre, ["Onay.Kod"] = Totp.Uret(anahtar) });
        Assert.Equal(HttpStatusCode.Redirect, kapat.StatusCode);
        Assert.False(await IkiAdimAcikMiAsync(kullaniciAdi));
        Assert.Equal(0, await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.CountRecoveryCodesAsync(k)));
        Assert.NotEqual(anahtar, await KullaniciIleAsync(_factory, kullaniciAdi, (um, k) => um.GetAuthenticatorKeyAsync(k)));
        Assert.Equal(EpostaSablonlari.IkiAdimKapatildiKonusu, (await _factory.Eposta.SiradakiAsync()).Konu);
        Assert.Contains("kapatıldı", await MetinAsync(await client.GetAsync(kapat.Headers.Location)));

        // Artık giriş tek adımlı.
        var yeniGiris = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, sifre);
        Assert.Equal("/", yeniGiris.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Kurtarma_kodlari_yenilenince_eskiler_gecersiz_diger_oturum_kapaniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var (anahtar, eskiKodlar) = await _factory.IkiAdimAcAsync(kullaniciAdi);

        var bu = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(bu, kullaniciAdi, sifre);
        await SelcukIdentityFactory.IkinciAdimAsync(bu, Totp.Uret(anahtar));
        var diger = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(diger, kullaniciAdi, sifre);
        await SelcukIdentityFactory.IkinciAdimAsync(diger, Totp.Uret(anahtar));
        Assert.Equal(HttpStatusCode.OK, (await diger.GetAsync("/")).StatusCode);

        var yanit = await KurulumFormuAsync(bu, "KodlariYenile", new() { ["Onay.Sifre"] = sifre, ["Onay.Kod"] = Totp.Uret(anahtar) });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var yeniKodlar = KurtarmaKodlari(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(10, yeniKodlar.Count);
        Assert.Empty(yeniKodlar.Intersect(eskiKodlar));
        Assert.Equal(EpostaSablonlari.KurtarmaKodlariYenilendiKonusu, (await _factory.Eposta.SiradakiAsync()).Konu);

        // Bu oturum açık, diğeri kapandı (güvenlik damgası); eski kod geçmez, yenisi geçer.
        Assert.Equal(HttpStatusCode.OK, (await bu.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await diger.GetAsync("/")).StatusCode);
        Assert.False((await KullaniciIleAsync(_factory, kullaniciAdi,
            (um, k) => um.RedeemTwoFactorRecoveryCodeAsync(k, eskiKodlar[0]))).Succeeded);
        Assert.True((await KullaniciIleAsync(_factory, kullaniciAdi,
            (um, k) => um.RedeemTwoFactorRecoveryCodeAsync(k, yeniKodlar[0]))).Succeeded);
    }

    [Fact]
    public async Task Iki_adim_sayfalari_giris_gerektiriyor_ikinci_adim_sifre_adimi_olmadan_acilmiyor()
    {
        foreach (var sayfa in new[] { Kurulum, "/Account/GuvenlikGecmisi" })
        {
            var yanit = await _factory.HttpsIstemcisi().GetAsync(sayfa);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith("https://localhost/Account/Login", yanit.Headers.Location!.ToString());
        }

        var ikinciAdim = await _factory.HttpsIstemcisi().GetAsync("/Account/IkiAdim");
        Assert.Equal(HttpStatusCode.Redirect, ikinciAdim.StatusCode);
        Assert.StartsWith("/Account/Login", ikinciAdim.Headers.Location!.OriginalString);
    }

    [GeneratedRegex("<li><code>([A-Z0-9]{5}-[A-Z0-9]{5})</code></li>")]
    private static partial Regex KurtarmaKodu();

    [GeneratedRegex("src=\"data:image/svg\\+xml;base64,([^\"]+)\"")]
    private static partial Regex QrKod();
}
