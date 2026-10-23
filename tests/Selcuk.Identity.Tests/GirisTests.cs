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
}
