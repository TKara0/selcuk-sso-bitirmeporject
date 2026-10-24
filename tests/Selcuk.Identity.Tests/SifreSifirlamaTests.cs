using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Selcuk.Identity.Data;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Pages.Account;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Şifremi unuttum → e-postadaki bağlantı → yeni şifre akışı. E-postalar sahte göndericiyle, Back-Channel Logout
/// POST'ları sahte alıcıyla yakalanır.
/// </summary>
public sealed partial class SifreSifirlamaTests : IDisposable
{
    private const string OlmayanKullanici = "olmayan.kullanici";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string YeniSifre() => $"Bb2{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> SifremiUnuttumAsync(HttpClient client, string girdi) =>
        SelcukIdentityFactory.FormGonderAsync(client, "/Account/SifremiUnuttum",
            new() { ["Input.KullaniciAdiVeyaEposta"] = girdi });

    private static Uri BaglantiyiAl(EpostaMesaji mesaj)
    {
        var eslesme = SifirlamaBaglantisi().Match(mesaj.Govde);
        Assert.True(eslesme.Success, "E-postada şifre sıfırlama bağlantısı yok.");
        return new Uri(eslesme.Value);
    }

    /// <summary>Şifremi unuttum isteği gönderir ve gelen e-postadaki bağlantıyı döndürür.</summary>
    private static async Task<Uri> BaglantiIsteAsync(SelcukIdentityFactory factory, string kullaniciAdi)
    {
        await SifremiUnuttumAsync(factory.HttpsIstemcisi(), kullaniciAdi);
        var mesaj = await factory.Eposta.SiradakiAsync();
        Assert.Equal(EpostaSablonlari.SifreSifirlamaKonusu, mesaj.Konu);
        return BaglantiyiAl(mesaj);
    }

    /// <summary>Bağlantıdaki sayfayı açar ve yeni şifreyi gönderir.</summary>
    private static Task<HttpResponseMessage> SifirlaAsync(HttpClient client, Uri baglanti, string yeniSifre)
    {
        var sorgu = QueryHelpers.ParseQuery(baglanti.Query);
        return SelcukIdentityFactory.FormGonderAsync(client, baglanti.PathAndQuery, new()
        {
            ["Input.Kullanici"] = sorgu["kullanici"].ToString(),
            ["Input.Kod"] = sorgu["kod"].ToString(),
            ["Input.YeniSifre"] = yeniSifre,
            ["Input.YeniSifreTekrar"] = yeniSifre,
        });
    }

    private static async Task<string> MetinAsync(HttpResponseMessage yanit) =>
        WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());

    private static async Task GirisBasarisizAsync(SelcukIdentityFactory factory, string kullaniciAdi, string sifre)
    {
        var yanit = await SelcukIdentityFactory.GirisYapAsync(factory.HttpsIstemcisi(), kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains(LoginModel.GenelHataMesaji, await MetinAsync(yanit));
    }

    private static async Task GirisBasariliAsync(SelcukIdentityFactory factory, string kullaniciAdi, string sifre)
    {
        var yanit = await SelcukIdentityFactory.GirisYapAsync(factory.HttpsIstemcisi(), kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
    }

    [Fact]
    public async Task Giris_sayfasinda_sifremi_unuttum_baglantisi_var()
    {
        var yanit = await _factory.HttpsIstemcisi().GetAsync("/Account/Login");

        var html = await MetinAsync(yanit);
        Assert.Contains("href=\"/Account/SifremiUnuttum\"", html);
        Assert.Contains("Şifremi unuttum", html);
        Assert.Equal(HttpStatusCode.OK, (await _factory.HttpsIstemcisi().GetAsync("/Account/SifremiUnuttum")).StatusCode);
    }

    [Fact]
    public async Task Var_olan_ve_olmayan_hesap_icin_sifremi_unuttum_yaniti_ayni()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();

        var adla = await SifremiUnuttumAsync(_factory.HttpsIstemcisi(), kullaniciAdi);
        var epostayla = await SifremiUnuttumAsync(_factory.HttpsIstemcisi(), $"{kullaniciAdi}@test.local");
        var olmayan = await SifremiUnuttumAsync(_factory.HttpsIstemcisi(), OlmayanKullanici);
        var olmayanEposta = await SifremiUnuttumAsync(_factory.HttpsIstemcisi(), "olmayan@test.local");

        var beklenen = await adla.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, adla.StatusCode);
        Assert.Contains(SifremiUnuttumModel.GonderildiMesaji, WebUtility.HtmlDecode(beklenen));
        foreach (var yanit in new[] { epostayla, olmayan, olmayanEposta })
        {
            Assert.Equal(adla.StatusCode, yanit.StatusCode);
            Assert.Equal(beklenen, await yanit.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Var_olan_hesaba_baglantili_eposta_gidiyor_olmayana_gitmiyor()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();

        // Önce olmayan hesaplar: e-postalar sırayla gönderildiği için bunlar için e-posta üretilseydi ilk onlar gelirdi.
        await SifremiUnuttumAsync(client, OlmayanKullanici);
        await SifremiUnuttumAsync(client, "olmayan@test.local");
        await SifremiUnuttumAsync(client, kullaniciAdi);

        var mesaj = await _factory.Eposta.SiradakiAsync();
        Assert.Equal($"{kullaniciAdi}@test.local", mesaj.Alici);
        Assert.Equal(EpostaSablonlari.SifreSifirlamaKonusu, mesaj.Konu);
        Assert.StartsWith(EpostaSablonlari.Baslik, mesaj.Govde);
        Assert.Contains("30 dakika", mesaj.Govde);
        Assert.Equal("https://localhost:5001/Account/SifreSifirla", BaglantiyiAl(mesaj).GetLeftPart(UriPartial.Path));
        Assert.Single(_factory.Eposta.Gonderilenler);

        // E-posta adresiyle istek de aynı hesaba bağlantı gönderir.
        await SifremiUnuttumAsync(client, $"{kullaniciAdi}@test.local");
        Assert.Equal($"{kullaniciAdi}@test.local", (await _factory.Eposta.SiradakiAsync()).Alici);
    }

    [Fact]
    public async Task Gecerli_tokenla_sifirlama_calisiyor_eski_sifre_gecmiyor_yeni_geciyor()
    {
        var (kullaniciAdi, eskiSifre) = await _factory.KullaniciOlusturAsync();
        var damgaOnce = (await _factory.KullaniciAsync(kullaniciAdi)).SecurityStamp;
        var baglanti = await BaglantiIsteAsync(_factory, kullaniciAdi);
        var yeniSifre = YeniSifre();

        var yanit = await SifirlaAsync(_factory.HttpsIstemcisi(), baglanti, yeniSifre);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains(SifreSifirlaModel.TamamlandiMesaji, await MetinAsync(yanit));
        Assert.NotEqual(damgaOnce, (await _factory.KullaniciAsync(kullaniciAdi)).SecurityStamp);
        await GirisBasarisizAsync(_factory, kullaniciAdi, eskiSifre);
        await GirisBasariliAsync(_factory, kullaniciAdi, yeniSifre);

        var bilgi = await _factory.Eposta.SiradakiAsync();
        Assert.Equal(EpostaSablonlari.SifreDegistirildiKonusu, bilgi.Konu);
        Assert.Equal($"{kullaniciAdi}@test.local", bilgi.Alici);
    }

    [Fact]
    public async Task Ayni_token_ikinci_kez_kullanilamiyor()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();
        var baglanti = await BaglantiIsteAsync(_factory, kullaniciAdi);
        var ilkSifre = YeniSifre();
        Assert.Contains(SifreSifirlaModel.TamamlandiMesaji,
            await MetinAsync(await SifirlaAsync(_factory.HttpsIstemcisi(), baglanti, ilkSifre)));

        var ikinciSifre = YeniSifre();
        var yanit = await SifirlaAsync(_factory.HttpsIstemcisi(), baglanti, ikinciSifre);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains(SifreSifirlaModel.GecersizBaglantiMesaji, await MetinAsync(yanit));
        await GirisBasarisizAsync(_factory, kullaniciAdi, ikinciSifre);
        await GirisBasariliAsync(_factory, kullaniciAdi, ilkSifre);
    }

    [Fact]
    public void Sifirlama_tokeni_30_dakika_gecerli_ve_ayri_saglayiciyla_uretiliyor()
    {
        var identity = _factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value;
        var token = _factory.Services.GetRequiredService<IOptions<SifreSifirlamaTokenAyarlari>>().Value;

        Assert.Equal(SifreSifirlamaTokenSaglayici.Ad, identity.Tokens.PasswordResetTokenProvider);
        Assert.Equal(TimeSpan.FromMinutes(30), token.TokenLifespan);
        Assert.Equal(SifreSifirlamaTokenSaglayici.Ad, token.Name);
    }

    [Fact]
    public async Task Suresi_dolmus_token_reddediliyor()
    {
        // Identity'nin DataProtectorTokenProvider'ı süreyi sistem saatinden okur (TimeProvider almaz). Bu yüzden
        // zamanı ilerletmek yerine token ömrü 1 ms'ye indirilir ve token kullanılmadan önce süre geçirilir;
        // üretimdeki 30 dakikalık ayar ayrı testte doğrulanır.
        using var factory = new SelcukIdentityFactory(services =>
            services.Configure<SifreSifirlamaTokenAyarlari>(o => o.TokenLifespan = TimeSpan.FromMilliseconds(1)));
        var (kullaniciAdi, eskiSifre) = await factory.KullaniciOlusturAsync();
        var baglanti = await BaglantiIsteAsync(factory, kullaniciAdi);
        await Task.Delay(TimeSpan.FromMilliseconds(50));

        var yeniSifre = YeniSifre();
        var yanit = await SifirlaAsync(factory.HttpsIstemcisi(), baglanti, yeniSifre);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains(SifreSifirlaModel.GecersizBaglantiMesaji, await MetinAsync(yanit));
        await GirisBasarisizAsync(factory, kullaniciAdi, yeniSifre);
        await GirisBasariliAsync(factory, kullaniciAdi, eskiSifre);
    }

    [Fact]
    public async Task Sifirlamadan_sonra_acik_oturumlara_back_channel_gidiyor_ve_kimlik_oturumu_kapaniyor()
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        var (baskaAdi, _) = await _factory.KullaniciOlusturAsync();
        var baskaId = (await _factory.KullaniciAsync(baskaAdi)).Id;
        await _factory.IstemcileriKaydetAsync();

        // Başka bir tarayıcıda kimlik sunucusu oturumu açık.
        var tarayici = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(tarayici, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync("/")).StatusCode);

        await _factory.IstemciOturumuEkleAsync("sid-a", kullaniciId, Istemciler.DemoObisClientId);
        await _factory.IstemciOturumuEkleAsync("sid-a", kullaniciId, Istemciler.DemoYemekhaneClientId);
        await _factory.IstemciOturumuEkleAsync("sid-b", kullaniciId, Istemciler.DemoObisClientId);
        await _factory.IstemciOturumuEkleAsync("sid-kapali", kullaniciId, Istemciler.DemoYemekhaneClientId, kapali: true);
        await _factory.IstemciOturumuEkleAsync("sid-baska", baskaId, Istemciler.DemoObisClientId);

        var baglanti = await BaglantiIsteAsync(_factory, kullaniciAdi);
        await SifirlaAsync(_factory.HttpsIstemcisi(), baglanti, YeniSifre());

        var bildirimler = _factory.BackChannel.Istekler
            .Select(i => $"{i.Adres.AbsoluteUri} {new JsonWebToken(i.Form["logout_token"]).GetClaim("sid").Value}")
            .Order()
            .ToList();
        Assert.Equal(
        [
            $"{Istemciler.DemoObisBackChannelLogoutUri} sid-a",
            $"{Istemciler.DemoObisBackChannelLogoutUri} sid-b",
            $"{Istemciler.DemoYemekhaneBackChannelLogoutUri} sid-a",
        ], bildirimler);
        Assert.All(await _factory.IstemciOturumlariAsync(kullaniciId), k => Assert.NotNull(k.KapanmaZamani));
        Assert.All(await _factory.IstemciOturumlariAsync(baskaId), k => Assert.Null(k.KapanmaZamani));

        // Kimlik sunucusundaki eski oturum da kapandı: güvenlik damgası değişti.
        var sonra = await tarayici.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, sonra.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", sonra.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Sifirlama_kilitli_hesabin_kilidini_aciyor()
    {
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var kullanici = (await userManager.FindByNameAsync(kullaniciAdi))!;
            await userManager.SetLockoutEndDateAsync(kullanici, DateTimeOffset.UtcNow.AddMinutes(15));
            kullanici.AccessFailedCount = 3;
            await userManager.UpdateAsync(kullanici);
            Assert.True(await userManager.IsLockedOutAsync(kullanici));
        }

        var baglanti = await BaglantiIsteAsync(_factory, kullaniciAdi);
        var yeniSifre = YeniSifre();
        Assert.Contains(SifreSifirlaModel.TamamlandiMesaji,
            await MetinAsync(await SifirlaAsync(_factory.HttpsIstemcisi(), baglanti, yeniSifre)));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var kullanici = (await userManager.FindByNameAsync(kullaniciAdi))!;
            Assert.False(await userManager.IsLockedOutAsync(kullanici));
            Assert.Equal(0, await userManager.GetAccessFailedCountAsync(kullanici));
        }

        await GirisBasariliAsync(_factory, kullaniciAdi, yeniSifre);
    }

    [GeneratedRegex(@"https://\S+/Account/SifreSifirla\?\S+")]
    private static partial Regex SifirlamaBaglantisi();
}
