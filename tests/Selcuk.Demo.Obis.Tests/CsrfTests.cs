using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Siteler arası istek sahteciliği (CSRF) koruması: Demo OBİS'teki her Razor sayfası POST işleyicisi antiforgery token'ı
/// olmadan gönderildiğinde 400 alır ve yan etki oluşturmaz; aynı istek token'la 400 almaz. İşleyici listesi çalışan
/// uygulamadan toplanır, yeni bir POST işleyicisi eklenirse <see cref="Her_POST_isleyicisinin_senaryosu_var"/> başarısız
/// olur. OIDC protokol uçları (/signout-backchannel: imzalı logout_token ile doğrulanır) tarayıcı formu değildir ve
/// antiforgery beklemez (DisableAntiforgery, ortak kütüphane testleri).
/// </summary>
public sealed class CsrfTests : IDisposable
{
    private readonly ObisFactory _factory = new(sahteKimlik: true);

    public void Dispose() => _factory.Dispose();

    /// <summary>Senaryo anahtarları: "Sayfa/Yolu" (varsayılan işleyici) veya "Sayfa/Yolu:IsimliIsleyici".</summary>
    public static readonly string[] Senaryolar = ["Cikis", "TemsiliIslem"];

    private static async Task<List<string>> KesfetAsync(IServiceProvider servisler)
    {
        var yukleyici = servisler.GetRequiredService<PageLoader>();
        var islayiciler = new List<string>();
        foreach (var sayfa in servisler.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<PageActionDescriptor>())
        {
            var derlenmis = await yukleyici.LoadAsync(sayfa, EndpointMetadataCollection.Empty);
            var yol = sayfa.RelativePath.Replace("/Pages/", "", StringComparison.Ordinal).Replace(".cshtml", "", StringComparison.Ordinal);
            foreach (var h in derlenmis.HandlerMethods.Where(h => string.Equals(h.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)))
            {
                // POST işleyicisi olan hiçbir sayfa antiforgery doğrulamasını kapatamaz.
                Assert.DoesNotContain(derlenmis.FilterDescriptors, f => f.Filter is IgnoreAntiforgeryTokenAttribute);
                islayiciler.Add(string.IsNullOrEmpty(h.Name) ? yol : $"{yol}:{h.Name}");
            }
        }

        return islayiciler;
    }

    private static string Token(string html)
    {
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Formda antiforgery token bulunamadı.");
        return token;
    }

    [Fact]
    public async Task Her_POST_isleyicisinin_senaryosu_var()
    {
        Assert.Equal(Senaryolar.Order(StringComparer.Ordinal), (await KesfetAsync(_factory.Services)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Cikis_antiforgery_token_olmadan_400_alir_tokenla_ayni_istek_400_almaz()
    {
        var client = _factory.HttpsIstemcisi("ogrenci");
        var sayfa = await client.GetAsync("/Cikis");
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = await sayfa.Content.ReadAsStringAsync();

        // Antiforgery çerezi var (sayfa açıldı) ama formda token yok: çıkış yapılmaz, end-session'a yönlendirilmez.
        var tokensiz = await client.PostAsync("/Cikis", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, tokensiz.StatusCode);
        Assert.Null(tokensiz.Headers.Location);

        // Kontrol: token'la çıkış işleyicisi çalışır (kimlik sunucusunun end-session adresine yönlendirir).
        var tokenli = await client.PostAsync("/Cikis", new FormUrlEncodedContent(
            [KeyValuePair.Create("__RequestVerificationToken", Token(html))]));
        Assert.Equal(HttpStatusCode.Redirect, tokenli.StatusCode);
    }

}
