using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Selcuk.Identity.Data;
using Selcuk.Identity.Pages.Account;

namespace Selcuk.Identity.Tests;

public sealed class GirisTests : IDisposable
{
    private const string OturumCerezi = "SelcukSSO.Oturum";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<string> GenelHataGosterildiAsync(HttpResponseMessage yanit)
    {
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains(LoginModel.GenelHataMesaji, html);
        return html;
    }

    private static bool OturumCereziVar(HttpResponseMessage yanit) =>
        yanit.Headers.TryGetValues("Set-Cookie", out var cerezler) &&
        cerezler.Any(c => c.StartsWith(OturumCerezi + "=", StringComparison.Ordinal));

    [Fact]
    public async Task Giris_sayfasi_aciliyor()
    {
        var yanit = await _factory.HttpsIstemcisi().GetAsync("/Account/Login");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("name=\"Input.KullaniciAdi\"", html);
        Assert.Contains("name=\"Input.Sifre\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Fact]
    public async Task Dogru_sifreyle_giris_basarili()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();

        var yanit = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, sifre);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/", yanit.Headers.Location?.OriginalString);
        Assert.True(OturumCereziVar(yanit), "Başarılı girişte oturum çerezi verilmedi.");
    }

    [Fact]
    public async Task Yanlis_sifreyle_giris_basarisiz()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();

        var yanit = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, "YanlisSifre123");

        await GenelHataGosterildiAsync(yanit);
        Assert.False(OturumCereziVar(yanit));
    }

    [Fact]
    public async Task Bes_hatali_denemeden_sonra_hesap_kilitleniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();

        for (var i = 1; i <= 5; i++)
        {
            await GenelHataGosterildiAsync(await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, $"YanlisSifre{i}x"));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var kullanici = await userManager.FindByNameAsync(kullaniciAdi);
            Assert.NotNull(kullanici);
            Assert.True(await userManager.IsLockedOutAsync(kullanici));

            var bitis = await userManager.GetLockoutEndDateAsync(kullanici);
            Assert.NotNull(bitis);
            Assert.InRange(bitis.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        }

        // Kilitliyken doğru şifre de reddedilir ve mesaj, yanlış şifreyle aynıdır (hesap durumu sızdırılmaz).
        var yanit = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        await GenelHataGosterildiAsync(yanit);
        Assert.False(OturumCereziVar(yanit));
    }

    [Fact]
    public async Task Kilitli_hesapta_yeni_giris_reddediliyor_kilitten_once_acilan_oturum_kod_ve_token_alabiliyor()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var acik = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(acik, kullaniciAdi, sifre)).StatusCode);

        // Başka biri yanlış şifre deneyerek hesabı kilitler.
        var saldirgan = _factory.HttpsIstemcisi();
        for (var i = 1; i <= 5; i++)
        {
            await GenelHataGosterildiAsync(await SelcukIdentityFactory.GirisYapAsync(saldirgan, kullaniciAdi, $"YanlisSifre{i}x"));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await userManager.IsLockedOutAsync((await userManager.FindByNameAsync(kullaniciAdi))!));
        }

        // Yeni giriş doğru şifreyle de reddedilir; yeni tarayıcı authorize'dan kod alamaz, girişe yönlenir.
        var yeni = _factory.HttpsIstemcisi();
        var yeniGiris = await SelcukIdentityFactory.GirisYapAsync(yeni, kullaniciAdi, sifre);
        await GenelHataGosterildiAsync(yeniGiris);
        Assert.False(OturumCereziVar(yeniGiris));
        var yeniAuthorize = await yeni.GetAsync(OidcYardimcisi.AuthorizeAdresi(OidcYardimcisi.Pkce.Yeni()));
        Assert.Equal(HttpStatusCode.Redirect, yeniAuthorize.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", yeniAuthorize.Headers.Location!.ToString());

        // Kilitten önce açılmış oturum düşmez: authorize kod verir, token uç noktası token verir.
        Assert.Equal(HttpStatusCode.OK, (await acik.GetAsync("/")).StatusCode);
        var token = await OidcYardimcisi.TokenAlAsync(acik, secret);
        Assert.False(string.IsNullOrEmpty(token.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrEmpty(token.GetProperty("id_token").GetString()));

        // Aynı oturumdan ikinci bir kod/token akışı da çalışır (onay kalıcı, kilit hâlâ sürüyor).
        Assert.False(string.IsNullOrEmpty((await OidcYardimcisi.TokenAlAsync(acik, secret)).GetProperty("access_token").GetString()));
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await userManager.IsLockedOutAsync((await userManager.FindByNameAsync(kullaniciAdi))!));
        }
    }
}
