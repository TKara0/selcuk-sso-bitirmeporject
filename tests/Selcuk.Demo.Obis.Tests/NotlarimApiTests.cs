using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Selcuk.Demo.Obis.Pages;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>Notlarım sayfası notları Demo API'den, oturumdaki access token ile çeker; API kapalıyken çökmez.</summary>
public sealed class NotlarimApiTests
{
    /// <summary>Demo API'yi taklit eder: gelen istekleri kaydeder, sabit bir not listesi döndürür.</summary>
    private sealed class SahteDemoApi : HttpMessageHandler
    {
        public ConcurrentQueue<HttpRequestMessage> Istekler { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Enqueue(request);
            const string Json = """
                {"ogrenci":"test-kullanici","donem":"2026-2027 Güz","notlar":[
                  {"kod":"API101","ad":"API'den Gelen Ders","vize":91,"final":97,"harf":"AA"}]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Notlarim_Demo_APIden_gelen_veriyi_access_token_ile_gosteriyor()
    {
        var api = new SahteDemoApi();
        using var factory = new ObisFactory(sahteKimlik: true, demoApi: api);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync("/Notlarim");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("<td>API101</td><td>API'den Gelen Ders</td><td>91</td><td>97</td><td><strong>AA</strong></td>", html);
        Assert.DoesNotContain(NotlarimModel.HataMesaji, html);

        var istek = Assert.Single(api.Istekler);
        Assert.Equal(HttpMethod.Get, istek.Method);
        Assert.Equal("https://localhost:5201/api/notlar", istek.RequestUri!.ToString());
        Assert.Equal("Bearer", istek.Headers.Authorization!.Scheme);
        Assert.Equal(ObisFactory.SahteAccessToken, istek.Headers.Authorization.Parameter);
        // Sayfa token'ı veya sub'ı parametre olarak göndermez.
        Assert.Empty(istek.RequestUri.Query);
    }

    [Fact]
    public async Task Demo_API_kapaliyken_Turkce_hata_mesaji_gosteriliyor_500_donmuyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync("/Notlarim");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("Not bilgileri şu an alınamıyor", html);
        Assert.DoesNotContain("<table>", html);
    }
}
