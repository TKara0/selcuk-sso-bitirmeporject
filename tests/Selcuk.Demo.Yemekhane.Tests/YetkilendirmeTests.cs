using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace Selcuk.Demo.Yemekhane.Tests;

public sealed class YetkilendirmeTests
{
    [Fact]
    public async Task Haftalik_menu_herkese_acik()
    {
        using var factory = new YemekhaneFactory();

        var yanit = await factory.HttpsIstemcisi().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("Demo Yemekhane — test uygulaması", html);
        Assert.Contains("Haftalık menü", html);
    }

    [Fact]
    public async Task Girissiz_kullanici_Rezervasyonlarim_icin_kimlik_sunucusuna_yonlendiriliyor()
    {
        using var factory = new YemekhaneFactory();

        var yanit = await factory.HttpsIstemcisi().GetAsync("/Rezervasyonlarim");

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!;
        Assert.StartsWith("https://localhost:5001/connect/authorize", konum.ToString());

        var sorgu = QueryHelpers.ParseQuery(konum.Query);
        Assert.Equal("demo-yemekhane", sorgu["client_id"].ToString());
        Assert.Equal("code", sorgu["response_type"].ToString());
        Assert.False(string.IsNullOrEmpty(sorgu["code_challenge"].ToString()), "code_challenge parametresi yok.");
        Assert.Equal("S256", sorgu["code_challenge_method"].ToString());
    }

    [Fact]
    public async Task Ogrenci_Yemekhane_Yonetimi_erisim_reddedildi()
    {
        using var factory = new YemekhaneFactory(sahteKimlik: true);
        var client = factory.HttpsIstemcisi("ogrenci");

        var yanit = await client.GetAsync("/YemekhaneYonetimi");

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        var konum = yanit.Headers.Location!.ToString();
        Assert.StartsWith("https://localhost/ErisimReddedildi", konum);
        var reddedildi = await client.GetAsync(konum);
        Assert.Contains("Erişim reddedildi", WebUtility.HtmlDecode(await reddedildi.Content.ReadAsStringAsync()));

        // Rezervasyonlarım giriş yapmış herkese açık.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Rezervasyonlarim")).StatusCode);
    }

    [Fact]
    public async Task Personel_Yemekhane_Yonetimi_aciliyor()
    {
        using var factory = new YemekhaneFactory(sahteKimlik: true);

        var yanit = await factory.HttpsIstemcisi("personel").GetAsync("/YemekhaneYonetimi");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("Yemekhane Yönetimi", WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync()));
    }
}
