using System.Net;
using System.Text.RegularExpressions;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Temsili işlemler ("Danışmanıma Mail At" ve benzeri düğmeler): antiforgery'li POST; token yoksa 400; token'la veriyi
/// değiştirmeden, "işlem kaydedilmedi" mesajıyla geldiği sayfaya döner. Dönüş adresi yalnızca uygulama içi olabilir.
/// </summary>
public sealed partial class TemsiliIslemTests
{
    private const string Mesaj = "Bu ekran temsilidir; işlem kaydedilmedi.";

    private static HttpClient Ogrenci(ObisFactory factory, string kullanici = "203011002")
    {
        var client = factory.HttpsIstemcisi(Roller.Ogrenci);
        client.DefaultRequestHeaders.Add(ObisFactory.KullaniciBasligi, kullanici);
        client.DefaultRequestHeaders.Add(ObisFactory.AdBasligi, "Test Öğrenci İki");
        return client;
    }

    private static string Token(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Formda antiforgery token bulunamadı.");
        return token;
    }

    /// <summary>Sayfa içeriği: temsili mesaj bandı ve antiforgery token'ı (her yanıtta farklı) çıkarılır.</summary>
    private static string Icerik(string html) =>
        Regex.Replace(Regex.Replace(Regex.Replace(html,
                "<div class=\"bant bant-uygulama\" role=\"status\">.*?</div>", string.Empty, RegexOptions.Singleline),
            "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"[^\"]+\"", "TOKEN"), @"\s+", " ");

    [Theory]
    [InlineData("/")]
    [InlineData("/HarcDurumu")]
    [InlineData("/UzaktanEgitim")]
    [InlineData("/UzaktanSinav")]
    public async Task Temsili_dugmeler_antiforgeryli_forma_bagli_ve_tek_islem_ucuna_gidiyor(string sayfa)
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var html = WebUtility.HtmlDecode(await (await Ogrenci(factory).GetAsync(sayfa)).Content.ReadAsStringAsync());

        Assert.Contains("<form id=\"temsili-islem\" method=\"post\" action=\"/TemsiliIslem\">", html);
        Token(html);
        Assert.Matches(new Regex("<input type=\"hidden\" name=\"donus\" value=\"" + Regex.Escape(sayfa) + "\""), html);
        Assert.Contains("form=\"temsili-islem\"", html);
        Assert.DoesNotContain("<form id=\"temsili-islem\" method=\"get\"", html);
    }

    [Fact]
    public async Task Antiforgery_token_olmadan_400_alir_veri_degismez_tokenla_ayni_sayfaya_doner()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Ogrenci(factory);
        var once = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        var token = Token(once);

        // Token'sız: 400, yönlendirme yok, mesaj yok.
        var tokensiz = await client.PostAsync("/TemsiliIslem", new FormUrlEncodedContent([KeyValuePair.Create("donus", "/")]));
        Assert.Equal(HttpStatusCode.BadRequest, tokensiz.StatusCode);
        Assert.Null(tokensiz.Headers.Location);
        var araSayfa = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Mesaj, WebUtility.HtmlDecode(araSayfa));

        // Yanlış token da 400.
        var yanlis = await client.PostAsync("/TemsiliIslem", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", "gecersiz"), KeyValuePair.Create("donus", "/")]));
        Assert.Equal(HttpStatusCode.BadRequest, yanlis.StatusCode);

        // Token'la: geldiği sayfaya yönlenir, mesaj bir kez gösterilir, sayfa verisi değişmez.
        var tokenli = await client.PostAsync("/TemsiliIslem", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", token), KeyValuePair.Create("donus", "/")]));
        Assert.Equal(HttpStatusCode.Redirect, tokenli.StatusCode);
        Assert.Equal("/", tokenli.Headers.Location!.OriginalString);

        var sonra = WebUtility.HtmlDecode(await (await client.GetAsync("/")).Content.ReadAsStringAsync());
        Assert.Contains(Mesaj, sonra);
        Assert.Contains("class=\"bant bant-uygulama\" role=\"status\"", sonra);
        Assert.Equal(Icerik(WebUtility.HtmlDecode(once)), Icerik(sonra));

        // Mesaj tek seferliktir (TempData).
        Assert.DoesNotContain(Mesaj, WebUtility.HtmlDecode(await (await client.GetAsync("/")).Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Temsili_islem_diger_ekranlarda_da_veriyi_degistirmiyor_donus_adresi_korunuyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Ogrenci(factory);
        var harc = await (await client.GetAsync("/HarcDurumu")).Content.ReadAsStringAsync();

        var yanit = await client.PostAsync("/TemsiliIslem", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", Token(harc)), KeyValuePair.Create("donus", "/HarcDurumu")]));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/HarcDurumu", yanit.Headers.Location!.OriginalString);
        var sonra = WebUtility.HtmlDecode(await (await client.GetAsync("/HarcDurumu")).Content.ReadAsStringAsync());
        Assert.Contains(Mesaj, sonra);
        // Ödenmemiş harç hâlâ ödenmemiş: "Ödeme yap" temsilidir.
        Assert.Contains("Bekliyor", sonra);
        Assert.Equal(Icerik(WebUtility.HtmlDecode(harc)), Icerik(sonra));
    }

    [Theory]
    [InlineData("https://baska-site.example/")]
    [InlineData("//baska-site.example/yol")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    public async Task Donus_adresi_uygulama_disi_ise_ana_sayfaya_donuyor(string donus)
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Ogrenci(factory);
        var token = Token(await (await client.GetAsync("/")).Content.ReadAsStringAsync());

        var yanit = await client.PostAsync("/TemsiliIslem", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", token), KeyValuePair.Create("donus", donus)]));

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal("/", yanit.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Girissiz_kullanici_temsili_islemi_kullanamiyor_GET_ana_sayfaya_yonlendiriyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var post = await factory.HttpsIstemcisi().PostAsync("/TemsiliIslem", new FormUrlEncodedContent([]));
        // Antiforgery veya yetki: 400, 401 veya kimlik sunucusuna yönlendirme; hiçbir durumda mesaj/işlem yok.
        Assert.True(post.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, post.StatusCode.ToString());
        if (post.StatusCode == HttpStatusCode.Redirect)
        {
            Assert.StartsWith("https://localhost:5001/connect/authorize", post.Headers.Location!.ToString());
        }

        var get = await Ogrenci(factory).GetAsync("/TemsiliIslem");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Equal("/", get.Headers.Location!.OriginalString.TrimEnd('?'));
    }
}
