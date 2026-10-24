using System.Net;

namespace Selcuk.Demo.Yemekhane.Tests;

/// <summary>
/// /signin-oidc dönüşü başarısız olduğunda (geri tuşu, eşleştirme çerezi yok) geliştirici hata sayfası yerine
/// yönlendirme yapıldığını doğrular.
/// </summary>
public sealed class GirisDonusHatasiTests
{
    private const string GeriTusuAdresi = "/signin-oidc?code=x&state=y";

    [Fact]
    public async Task Giris_yapmis_kullanici_eslestirme_cerezi_olmadan_donuste_ana_sayfaya_yonlendiriliyor()
    {
        using var factory = new YemekhaneFactory(sahteKimlik: true);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync(GeriTusuAdresi);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/", yanit.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Girissiz_kullanici_eslestirme_cerezi_olmadan_donuste_GirisHatasi_sayfasina_yonlendiriliyor()
    {
        using var factory = new YemekhaneFactory();
        var client = factory.HttpsIstemcisi();

        var yanit = await client.GetAsync(GeriTusuAdresi);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!.OriginalString;
        Assert.Equal("/GirisHatasi?neden=oturum-suresi", konum);

        var sayfa = await client.GetAsync(konum);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = WebUtility.HtmlDecode(await sayfa.Content.ReadAsStringAsync());
        Assert.Contains("Giriş işlemi tamamlanamadı.", html);
        Assert.Contains("Tekrar giriş yap", html);
    }
}
