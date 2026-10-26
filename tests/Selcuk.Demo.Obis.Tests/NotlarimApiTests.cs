using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Selcuk.Demo.Obis.Data;
using Selcuk.Demo.Obis.Pages;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Notlarım sayfası notları Demo API'den, oturumdaki access token ile çeker; API kapalıyken çökmez. Toplam kredi ve
/// dönem ağırlıklı ortalaması bu notlardan ve OBİS'in ders kataloğundaki kredilerden hesaplanır.
/// </summary>
public sealed class NotlarimApiTests
{
    private const string TekDersJson = """
        {"ogrenci":"test-kullanici","donem":"2026-2027 Güz","notlar":[
          {"kod":"API101","ad":"API'den Gelen Ders","vize":91,"final":97,"harf":"AA"}]}
        """;

    // Demo API'nin örnek verisiyle aynı dersler (src/Selcuk.Demo.Api/Data/NotDeposu.cs).
    private const string OrnekNotlarJson = """
        {"ogrenci":"test-kullanici","donem":"2026-2027 Güz","notlar":[
          {"kod":"BIL101","ad":"Programlamaya Giriş","vize":78,"final":85,"harf":"BA"},
          {"kod":"BIL203","ad":"Veri Yapıları","vize":65,"final":72,"harf":"CB"},
          {"kod":"BIL305","ad":"Veritabanı Sistemleri","vize":90,"final":88,"harf":"AA"},
          {"kod":"BIL307","ad":"Bilgisayar Ağları","vize":55,"final":61,"harf":"CC"},
          {"kod":"MAT201","ad":"Lineer Cebir","vize":70,"final":80,"harf":"BB"}]}
        """;

    /// <summary>Demo API'yi taklit eder: gelen istekleri kaydeder, verilen not listesini döndürür.</summary>
    private sealed class SahteDemoApi(string json = TekDersJson) : HttpMessageHandler
    {
        public ConcurrentQueue<HttpRequestMessage> Istekler { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Enqueue(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static async Task<string> NotlarimAsync(SahteDemoApi api)
    {
        using var factory = new ObisFactory(sahteKimlik: true, demoApi: api);
        var yanit = await factory.HttpsIstemcisi("ogrenci").GetAsync("/Notlarim");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Notlarim_Demo_APIden_gelen_veriyi_access_token_ile_gosteriyor()
    {
        var api = new SahteDemoApi();

        var html = await NotlarimAsync(api);

        // Ders kodu, ders adı, kredi (katalogda yok: —), vize, final ve harf notu (etiket) aynı satırda.
        Assert.Matches(new Regex(
            @">API101</td>\s*<th scope=""row""[^>]*>API'den Gelen Ders</th>\s*<td[^>]*>—</td>\s*<td[^>]*>91</td>\s*" +
            @"<td[^>]*>97</td>\s*<td[^>]*><span class=""etiket etiket-uygulama"">AA</span></td>"), html);
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
        Assert.Contains("class=\"bant bant-hata\" role=\"alert\"", html);
        Assert.DoesNotContain("<table", html);
        Assert.DoesNotContain("Toplam kredi", html);
    }

    [Fact]
    public async Task Notlarim_toplam_krediyi_ve_donem_agirlikli_ortalamasini_veriden_hesapliyor()
    {
        var html = await NotlarimAsync(new SahteDemoApi(OrnekNotlarJson));

        // Krediler 3 + 4 + 4 + 3 + 3 = 17; (3×3,5 + 4×2,5 + 4×4,0 + 3×2,0 + 3×3,0) / 17 = 51,5 / 17 = 3,029…
        Assert.Matches(new Regex(@"<dt>Toplam kredi</dt>\s*<dd>17</dd>"), html);
        Assert.Matches(new Regex(@"<dt>Dönem ağırlıklı ortalaması</dt>\s*<dd>3,03</dd>"), html);
        Assert.Matches(new Regex(@">BIL203</td>\s*<th[^>]*>Veri Yapıları</th>\s*<td[^>]*>4</td>"), html);
    }

    [Fact]
    public void Katalogda_olmayan_ders_ve_bilinmeyen_harf_ozete_girmiyor()
    {
        var (kredi, ortalama) = NotOzeti.Hesapla(
        [
            new("BIL305", "Veritabanı Sistemleri", 90, 88, "AA"),
            new("API101", "Katalogda olmayan ders", 91, 97, "AA"),
            new("BIL307", "Bilgisayar Ağları", 55, 61, "FF"),
            new("MAT201", "Lineer Cebir", 0, 0, "DZ"),
        ]);

        Assert.Equal(7, kredi);
        Assert.Equal(4m * 4.0m / 7, ortalama);
        Assert.Equal((0, null), NotOzeti.Hesapla([new("API101", "Katalogda olmayan ders", 91, 97, "AA")]));
    }
}
