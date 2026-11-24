using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Selcuk.Demo.Yemekhane.Data;

namespace Selcuk.Demo.Yemekhane.Tests;

/// <summary>
/// Siteler arası istek sahteciliği (CSRF) koruması: Demo Yemekhane'teki her Razor sayfası POST işleyicisi antiforgery token'ı
/// olmadan gönderildiğinde 400 alır ve yan etki oluşturmaz; aynı istek token'la 400 almaz. İşleyici listesi çalışan
/// uygulamadan toplanır, yeni bir POST işleyicisi eklenirse <see cref="Her_POST_isleyicisinin_senaryosu_var"/> başarısız
/// olur. OIDC protokol uçları (/signout-backchannel: imzalı logout_token ile doğrulanır) tarayıcı formu değildir ve
/// antiforgery beklemez (DisableAntiforgery, ortak kütüphane testleri).
/// </summary>
public sealed class CsrfTests : IDisposable
{
    private readonly YemekhaneFactory _factory = new(sahteKimlik: true);

    public void Dispose() => _factory.Dispose();

    private const string Kullanici = "test-kullanici";

    /// <summary>Senaryo anahtarları: "Sayfa/Yolu" (varsayılan işleyici) veya "Sayfa/Yolu:IsimliIsleyici".</summary>
    public static readonly string[] Senaryolar = ["Cikis", "Rezervasyonlarim:Ekle", "Rezervasyonlarim:Iptal"];

    public static IEnumerable<object[]> SenaryoVerisi => Senaryolar.Select(s => new object[] { s });

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

    [Theory]
    [MemberData(nameof(SenaryoVerisi))]
    public async Task Antiforgery_token_olmadan_POST_400_alir_yan_etki_olusturmaz_tokenla_ayni_istek_400_almaz(string anahtar)
    {
        var depo = _factory.Services.GetRequiredService<RezervasyonDeposu>();
        var gun = Takvim.RezervasyonGunleri(DateOnly.FromDateTime(DateTime.Now))[0];
        var client = _factory.HttpsIstemcisi("ogrenci");
        var gunAlani = new Dictionary<string, string> { ["gun"] = gun.ToString("yyyy-MM-dd") };
        var (sayfaAdresi, postAdresi, alanlar) = anahtar switch
        {
            "Cikis" => ("/Cikis", "/Cikis", new Dictionary<string, string>()),
            "Rezervasyonlarim:Ekle" => ("/Rezervasyonlarim", "/Rezervasyonlarim?handler=Ekle", gunAlani),
            "Rezervasyonlarim:Iptal" => ("/Rezervasyonlarim", "/Rezervasyonlarim?handler=Iptal", gunAlani),
            _ => throw new InvalidOperationException($"'{anahtar}' için senaryo yok."),
        };
        if (anahtar == "Rezervasyonlarim:Iptal")
        {
            Assert.True(depo.Ekle(Kullanici, gun));
        }

        var sayfa = await client.GetAsync(sayfaAdresi);
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);
        var html = await sayfa.Content.ReadAsStringAsync();
        DateOnly[] oncesi = anahtar == "Rezervasyonlarim:Iptal" ? [gun] : [];

        // Antiforgery çerezi var (sayfa açıldı) ama formda token yok.
        var tokensiz = await client.PostAsync(postAdresi, new FormUrlEncodedContent(alanlar));
        Assert.Equal(HttpStatusCode.BadRequest, tokensiz.StatusCode);
        Assert.Null(tokensiz.Headers.Location);
        // Yan etki yok: rezervasyon eklenmedi / iptal edilmedi.
        Assert.Equal(oncesi, depo.Listele(Kullanici));

        // Kontrol: aynı istek token'la gönderilince işleyici çalışır (400 değil) ve etkisi görünür.
        var tokenli = new Dictionary<string, string>(alanlar) { ["__RequestVerificationToken"] = Token(html) };
        var yanit = await client.PostAsync(postAdresi, new FormUrlEncodedContent(tokenli));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        if (anahtar != "Cikis")
        {
            DateOnly[] sonrasi = anahtar == "Rezervasyonlarim:Ekle" ? [gun] : [];
            Assert.Equal(sonrasi, depo.Listele(Kullanici));
        }
    }

}
