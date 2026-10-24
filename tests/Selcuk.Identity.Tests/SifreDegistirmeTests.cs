using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Selcuk.Identity.Data;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Pages.Account;

namespace Selcuk.Identity.Tests;

public sealed class SifreDegistirmeTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> DegistirAsync(HttpClient client, string mevcut, string yeni) =>
        SelcukIdentityFactory.FormGonderAsync(client, "/Account/SifreDegistir", new()
        {
            ["Input.MevcutSifre"] = mevcut,
            ["Input.YeniSifre"] = yeni,
            ["Input.YeniSifreTekrar"] = yeni,
        });

    private async Task<bool> SifreDogruMuAsync(string kullaniciAdi, string sifre)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.CheckPasswordAsync((await userManager.FindByNameAsync(kullaniciAdi))!, sifre);
    }

    [Fact]
    public async Task Yanlis_mevcut_sifre_reddediliyor_dogru_sifreyle_degisiyor_diger_oturumlar_kapaniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await _factory.IstemcileriKaydetAsync();

        // Aynı kullanıcı iki tarayıcıda: "bu" şifreyi değiştirecek, "diger" kapanmalı.
        var bu = _factory.HttpsIstemcisi();
        var buSid = _factory.OturumSid(await SelcukIdentityFactory.GirisYapAsync(bu, kullaniciAdi, sifre));
        var diger = _factory.HttpsIstemcisi();
        var digerSid = _factory.OturumSid(await SelcukIdentityFactory.GirisYapAsync(diger, kullaniciAdi, sifre));
        Assert.NotEqual(buSid, digerSid);

        await _factory.IstemciOturumuEkleAsync(buSid, kullaniciId, Istemciler.DemoObisClientId);
        await _factory.IstemciOturumuEkleAsync(digerSid, kullaniciId, Istemciler.DemoObisClientId);
        await _factory.IstemciOturumuEkleAsync(digerSid, kullaniciId, Istemciler.DemoYemekhaneClientId);

        // Yanlış mevcut şifre: değişiklik yok, kimse çıkış yapmaz.
        var yanlis = await DegistirAsync(bu, "YanlisSifre123", $"Cc3{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, yanlis.StatusCode);
        Assert.Contains("Mevcut şifre yanlış.", WebUtility.HtmlDecode(await yanlis.Content.ReadAsStringAsync()));
        Assert.Empty(_factory.BackChannel.Istekler);
        Assert.True(await SifreDogruMuAsync(kullaniciAdi, sifre));
        Assert.Equal(HttpStatusCode.OK, (await diger.GetAsync("/")).StatusCode);

        var yeniSifre = $"Cc3{Guid.NewGuid():N}";
        var yanit = await DegistirAsync(bu, sifre, yeniSifre);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith("/Account/Hesabim", yanit.Headers.Location!.OriginalString);
        Assert.True(await SifreDogruMuAsync(kullaniciAdi, yeniSifre));
        Assert.False(await SifreDogruMuAsync(kullaniciAdi, sifre));

        // Mevcut oturum açık kaldı: çerez yeni damgayla aynı sid'le yeniden yazıldı.
        Assert.Equal(buSid, _factory.OturumSid(yanit));
        var hesabim = await bu.GetAsync(yanit.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, hesabim.StatusCode);
        Assert.Contains("Şifreniz değiştirildi", WebUtility.HtmlDecode(await hesabim.Content.ReadAsStringAsync()));

        // Diğer oturum kapandı ve onun istemcilerine (yalnızca onlara) Back-Channel Logout gitti.
        var digerSonra = await diger.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, digerSonra.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", digerSonra.Headers.Location!.ToString());

        var bildirimler = _factory.BackChannel.Istekler.ToList();
        Assert.Equal(
            [Istemciler.DemoObisBackChannelLogoutUri, Istemciler.DemoYemekhaneBackChannelLogoutUri],
            bildirimler.Select(i => i.Adres.AbsoluteUri).Order());
        Assert.All(bildirimler, i => Assert.Equal(digerSid, new JsonWebToken(i.Form["logout_token"]).GetClaim("sid").Value));

        var kayitlar = await _factory.IstemciOturumlariAsync(kullaniciId);
        Assert.All(kayitlar.Where(k => k.Sid == buSid), k => Assert.Null(k.KapanmaZamani));
        Assert.All(kayitlar.Where(k => k.Sid == digerSid), k => Assert.NotNull(k.KapanmaZamani));

        var bilgi = await _factory.Eposta.SiradakiAsync();
        Assert.Equal(EpostaSablonlari.SifreDegistirildiKonusu, bilgi.Konu);
        Assert.Equal($"{kullaniciAdi}@test.local", bilgi.Alici);
    }

    [Fact]
    public async Task Yanlis_mevcut_sifre_hatali_deneme_sayacini_artiriyor_kilitliyken_degisiklik_yapilmiyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);

        async Task<int> SayacAsync()
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await userManager.GetAccessFailedCountAsync((await userManager.FindByNameAsync(kullaniciAdi))!);
        }

        Assert.Equal(0, await SayacAsync());
        var yanlis = await DegistirAsync(client, "YanlisSifre123", $"Cc3{Guid.NewGuid():N}");
        Assert.Contains("Mevcut şifre yanlış.", WebUtility.HtmlDecode(await yanlis.Content.ReadAsStringAsync()));
        Assert.Equal(1, await SayacAsync());

        // 5. yanlış denemede hesap kilitlenir; kilitliyken doğru mevcut şifreyle de değişiklik yapılmaz.
        for (var i = 2; i <= 5; i++)
        {
            await DegistirAsync(client, $"YanlisSifre12{i}", $"Cc3{Guid.NewGuid():N}");
        }

        var kilitli = await DegistirAsync(client, sifre, $"Cc3{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, kilitli.StatusCode);
        Assert.Contains(SifreDegistirModel.KilitliMesaji,
            WebUtility.HtmlDecode(await kilitli.Content.ReadAsStringAsync()));
        Assert.True(await SifreDogruMuAsync(kullaniciAdi, sifre));

        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        await _factory.DenetimKayitlariAsync(k =>
            k.Count(x => x.OlayTuru == DenetimOlayi.SifreDegisti && x.Sonuc == DenetimSonucu.Basarisiz
                && x.KullaniciId == kullaniciId) == 6 &&
            k.Any(x => x.OlayTuru == DenetimOlayi.HesapKilitlendi && x.KullaniciId == kullaniciId));
    }

    [Fact]
    public async Task Sifre_degistirme_ve_hesabim_giris_gerektiriyor()
    {
        foreach (var sayfa in new[] { "/Account/SifreDegistir", "/Account/Hesabim" })
        {
            var yanit = await _factory.HttpsIstemcisi().GetAsync(sayfa);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith("https://localhost/Account/Login", yanit.Headers.Location!.ToString());
        }
    }
}
