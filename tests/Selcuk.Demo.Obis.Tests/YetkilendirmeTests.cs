using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace Selcuk.Demo.Obis.Tests;

public sealed class YetkilendirmeTests
{
    private static async Task ErisimReddedildiAsync(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!.ToString();
        Assert.StartsWith("https://localhost/ErisimReddedildi", konum);

        var reddedildi = await client.GetAsync(konum);
        Assert.Equal(HttpStatusCode.OK, reddedildi.StatusCode);
        Assert.Contains("Erişim reddedildi", WebUtility.HtmlDecode(await reddedildi.Content.ReadAsStringAsync()));
    }

    private static async Task SayfaAciliyorAsync(HttpClient client, string sayfa, string baslik)
    {
        var yanit = await client.GetAsync(sayfa);

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains(baslik, WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Girissiz_kullanici_Notlarim_icin_kimlik_sunucusuna_yonlendiriliyor()
    {
        using var factory = new ObisFactory();

        var yanit = await factory.HttpsIstemcisi().GetAsync("/Notlarim");

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith("https://localhost:5001/connect/authorize", konum.ToString());

        var sorgu = QueryHelpers.ParseQuery(konum.Query);
        Assert.Equal("demo-obis", sorgu["client_id"].ToString());
        Assert.Equal("code", sorgu["response_type"].ToString());
        Assert.False(string.IsNullOrEmpty(sorgu["code_challenge"].ToString()), "code_challenge parametresi yok.");
        Assert.Equal("S256", sorgu["code_challenge_method"].ToString());
        // offline_access (refresh token) ve notlar.oku (Demo API) da istenir.
        Assert.Equal(["openid", "profile", "email", "roles", "offline_access", "notlar.oku"], sorgu["scope"].ToString().Split(' '));
        Assert.Equal("https://localhost/signin-oidc", sorgu["redirect_uri"].ToString());
    }

    [Fact]
    public async Task Ogrenci_Notlarim_acilir_Ders_Yonetimi_reddedilir()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = factory.HttpsIstemcisi("ogrenci");

        await SayfaAciliyorAsync(client, "/Notlarim", "Notlarım");
        await ErisimReddedildiAsync(client, "/DersYonetimi");
    }

    [Fact]
    public async Task Akademisyen_Ders_Yonetimi_acilir_Notlarim_reddedilir()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = factory.HttpsIstemcisi("akademisyen");

        await SayfaAciliyorAsync(client, "/DersYonetimi", "Ders Yönetimi");
        await ErisimReddedildiAsync(client, "/Notlarim");
    }

    [Fact]
    public async Task Token_Inceleme_Development_disinda_kapali()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync("/TokenInceleme");

        Assert.Equal(HttpStatusCode.NotFound, yanit.StatusCode);
    }
}
