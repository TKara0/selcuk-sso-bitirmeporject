using System.Net;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Pages.Account;

namespace Selcuk.Identity.Tests;

/// <summary>
/// İstek sınırı ve kullanıcı adı tahmini koruması. Yanıt süreleri ölçülmez (kırılgan olurdu); iki yolun aynı işi
/// yaptığı kodda gözden geçirilmiştir (README > Güvenlik önlemleri).
/// </summary>
public sealed class KabaKuvvetTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Giris_POST_siniri_asilinca_429()
    {
        var client = _factory.HttpsIstemcisi();
        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek; i++)
        {
            var deneme = await SelcukIdentityFactory.GirisYapAsync(client, "olmayan.kullanici", "YanlisSifre123");
            Assert.Equal(HttpStatusCode.OK, deneme.StatusCode);
        }

        var yanit = await SelcukIdentityFactory.GirisYapAsync(client, "olmayan.kullanici", "YanlisSifre123");

        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);
        Assert.Equal("text/html", yanit.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Çok fazla deneme", await yanit.Content.ReadAsStringAsync());
        Assert.True(yanit.Headers.RetryAfter is not null, "Retry-After başlığı yok.");

        // Yalnızca POST sayılır: giriş sayfası açılmaya devam eder.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
    }

    [Fact]
    public async Task Olmayan_kullanici_ve_yanlis_sifre_ayni_mesaji_donuyor()
    {
        const string olmayan = "olmayan.kullanici";
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();

        var yanlisSifre = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, "YanlisSifre123");
        var kullaniciYok = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), olmayan, "YanlisSifre123");

        Assert.Equal(HttpStatusCode.OK, yanlisSifre.StatusCode);
        Assert.Equal(yanlisSifre.StatusCode, kullaniciYok.StatusCode);

        // Formda yazılan kullanıcı adı ve antiforgery token'ı dışında sayfalar birebir aynı.
        var a = Normallestir(await yanlisSifre.Content.ReadAsStringAsync(), kullaniciAdi);
        var b = Normallestir(await kullaniciYok.Content.ReadAsStringAsync(), olmayan);
        Assert.Contains(LoginModel.GenelHataMesaji, WebUtility.HtmlDecode(a));
        Assert.Equal(a, b);
    }

    private static string Normallestir(string html, string kullaniciAdi) =>
        html.Replace(SelcukIdentityFactory.AntiforgeryTokenAl(html), "TOKEN").Replace(kullaniciAdi, "KULLANICI");
}
