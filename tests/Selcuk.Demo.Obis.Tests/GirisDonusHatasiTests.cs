using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// /signin-oidc dönüşü başarısız olduğunda (geri tuşu, eşleştirme çerezi yok, onay reddedildi) geliştirici hata
/// sayfası yerine yönlendirme yapıldığını doğrular. Hiçbir istekte eşleştirme çerezi gönderilmez.
/// </summary>
public sealed class GirisDonusHatasiTests
{
    private const string GeriTusuAdresi = "/signin-oidc?code=x&state=y";

    /// <summary>Uygulamanın kendi state formatıyla korunmuş, eşleştirme anahtarı içeren geçerli bir state.</summary>
    private static string GecerliState(ObisFactory factory, string redirectUri)
    {
        var ayarlar = factory.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties { RedirectUri = redirectUri };
        properties.Items[".xsrf"] = "eslestirme-cerezi-yok";
        return ayarlar.StateDataFormat.Protect(properties);
    }

    private static async Task GirisHatasiSayfasiAsync(HttpClient client, HttpResponseMessage yanit,
        string beklenenNeden, string beklenenMesaj)
    {
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.Equal("/GirisHatasi", konum.OriginalString.Split('?')[0]);
        // Hata ayrıntısı URL'ye konmaz; yalnızca kısa neden kodu.
        var sorgu = QueryHelpers.ParseQuery(new Uri(new Uri("https://localhost"), konum).Query);
        Assert.Equal(["neden"], sorgu.Keys);
        Assert.Equal(beklenenNeden, sorgu["neden"].ToString());

        var sayfa = await client.GetAsync(konum);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = WebUtility.HtmlDecode(await sayfa.Content.ReadAsStringAsync());
        Assert.Contains(beklenenMesaj, html);
        Assert.Contains("Tekrar giriş yap", html);
    }

    [Fact]
    public async Task Giris_yapmis_kullanici_eslestirme_cerezi_olmadan_donuste_ana_sayfaya_yonlendiriliyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync(GeriTusuAdresi);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/", yanit.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Giris_yapmis_kullanici_gecerli_state_ile_correlation_hatasinda_donus_adresine_yonlendiriliyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var state = GecerliState(factory, "/Notlarim");

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync(
            "/signin-oidc?code=x&state=" + Uri.EscapeDataString(state));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/Notlarim", yanit.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Girissiz_kullanici_eslestirme_cerezi_olmadan_donuste_GirisHatasi_sayfasina_yonlendiriliyor()
    {
        using var factory = new ObisFactory();
        var client = factory.HttpsIstemcisi();

        var yanit = await client.GetAsync(GeriTusuAdresi);

        await GirisHatasiSayfasiAsync(client, yanit, "oturum-suresi", "Giriş işlemi tamamlanamadı.");
    }

    [Fact]
    public async Task Onay_reddedilince_GirisHatasi_sayfasina_erisim_reddedildi_nedeniyle_yonlendiriliyor()
    {
        using var factory = new ObisFactory();
        var client = factory.HttpsIstemcisi();

        var yanit = await client.GetAsync("/signin-oidc?error=access_denied&state=y");

        await GirisHatasiSayfasiAsync(client, yanit, "erisim-reddedildi", "Uygulamaya erişim izni verilmedi.");
    }

    [Fact]
    public async Task Onay_reddedilince_gecerli_state_ile_de_erisim_reddedildi_nedeniyle_yonlendiriliyor()
    {
        using var factory = new ObisFactory();
        var client = factory.HttpsIstemcisi();
        var state = GecerliState(factory, "/Notlarim");

        var yanit = await client.GetAsync("/signin-oidc?error=access_denied&state=" + Uri.EscapeDataString(state));

        await GirisHatasiSayfasiAsync(client, yanit, "erisim-reddedildi", "Uygulamaya erişim izni verilmedi.");
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Dis_donus_adresine_yonlendirilmiyor(string disAdres)
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var state = GecerliState(factory, disAdres);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync(
            "/signin-oidc?code=x&state=" + Uri.EscapeDataString(state) + "&returnUrl=" + Uri.EscapeDataString(disAdres));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/", yanit.Headers.Location!.OriginalString);
    }
}
