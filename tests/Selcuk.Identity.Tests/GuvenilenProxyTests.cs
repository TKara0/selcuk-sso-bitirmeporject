using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Tests;

/// <summary>
/// X-Forwarded-For yalnızca GuvenilenProxyler listesindeki adreslerden gelirse dikkate alınır. TestServer bağlantının
/// uzak adresini doldurmadığı için her istek, uygulamanın ara katmanlarından önce çalışan bir başlangıç filtresiyle
/// <see cref="ProxyAdresi"/>'nden geliyormuş gibi işaretlenir.
/// </summary>
public sealed class GuvenilenProxyTests
{
    private static readonly IPAddress ProxyAdresi = IPAddress.Parse("10.0.0.5");
    private const string IstemciAdresi = "203.0.113.7";

    private sealed class UzakAdresFiltresi : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, sonraki) =>
            {
                context.Connection.RemoteIpAddress = ProxyAdresi;
                return sonraki(context);
            });
            next(app);
        };
    }

    private static SelcukIdentityFactory Factory(params string[] guvenilenler) => new(
        services => services.AddSingleton<IStartupFilter, UzakAdresFiltresi>(),
        guvenilenler.Select((adres, i) => KeyValuePair.Create($"GuvenilenProxyler:{i}", (string?)adres))
            .ToDictionary());

    private static HttpClient Istemci(SelcukIdentityFactory factory, string xForwardedFor)
    {
        var client = factory.HttpsIstemcisi();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", xForwardedFor);
        return client;
    }

    /// <summary>X-Forwarded-For'lu başarısız bir giriş yapar ve denetim kaydına yazılan IP'yi döndürür.</summary>
    private static async Task<string?> KaydedilenIpAsync(SelcukIdentityFactory factory)
    {
        await SelcukIdentityFactory.GirisYapAsync(Istemci(factory, IstemciAdresi), "olmayan.kullanici", "YanlisSifre123");
        var kayitlar = await factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.GirisBasarisiz));
        return kayitlar.Single(x => x.OlayTuru == DenetimOlayi.GirisBasarisiz).IpAdresi;
    }

    /// <summary>
    /// Aynı proxy'den, X-Forwarded-For'u farklı iki istemci: ilki sınırı doldurur, ikincisinin isteği 429 alır mı?
    /// </summary>
    private static async Task<HttpStatusCode> IkinciIstemcininDurumuAsync(SelcukIdentityFactory factory)
    {
        var ilk = Istemci(factory, IstemciAdresi);
        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek; i++)
        {
            await SelcukIdentityFactory.GirisYapAsync(ilk, "olmayan.kullanici", "YanlisSifre123");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SelcukIdentityFactory.GirisYapAsync(ilk, "olmayan.kullanici", "YanlisSifre123")).StatusCode);
        return (await SelcukIdentityFactory.GirisYapAsync(Istemci(factory, "203.0.113.8"), "olmayan.kullanici",
            "YanlisSifre123")).StatusCode;
    }

    [Fact]
    public async Task GuvenilenProxyler_bosken_X_Forwarded_For_dikkate_alinmiyor()
    {
        using var factory = Factory();

        Assert.Equal(ProxyAdresi.ToString(), await KaydedilenIpAsync(factory));
        // Başlık yok sayıldığı için iki "istemci" aynı IP sayılır: ikincisi de sınıra takılır.
        Assert.Equal(HttpStatusCode.TooManyRequests, await IkinciIstemcininDurumuAsync(factory));
    }

    [Fact]
    public async Task GuvenilenProxyler_doluyken_listedeki_proxyden_gelen_baslik_dikkate_aliniyor()
    {
        using var factory = Factory(ProxyAdresi.ToString());

        Assert.Equal(IstemciAdresi, await KaydedilenIpAsync(factory));
        // Gerçek istemci IP'leri ayrı: ikinci istemci sınıra takılmaz.
        Assert.Equal(HttpStatusCode.OK, await IkinciIstemcininDurumuAsync(factory));
    }

    /// <summary>
    /// TLS'i proxy sonlandırmış gibi: istek uygulamaya HTTP ile ve X-Forwarded-Proto: https başlığıyla gelir. Discovery
    /// (OpenIddict HTTPS dışı isteği reddeder) ve girişe yönlendirmenin şeması (istek şemasından üretilir) döndürülür.
    /// </summary>
    private static async Task<(HttpStatusCode Discovery, string GirisYonlendirmesi)> HttpUzerindenAsync(
        SelcukIdentityFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var discovery = await client.GetAsync("/.well-known/openid-configuration");
        var hesabim = await client.GetAsync("/Account/Hesabim");
        Assert.Equal(HttpStatusCode.Redirect, hesabim.StatusCode);
        return (discovery.StatusCode, hesabim.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GuvenilenProxyler_doluyken_X_Forwarded_Proto_https_dikkate_aliniyor()
    {
        using var factory = Factory(ProxyAdresi.ToString());

        var (discovery, yonlendirme) = await HttpUzerindenAsync(factory);

        Assert.Equal(HttpStatusCode.OK, discovery);
        Assert.StartsWith("https://localhost/Account/Login", yonlendirme);
    }

    [Fact]
    public async Task GuvenilenProxyler_bosken_X_Forwarded_Proto_dikkate_alinmiyor()
    {
        using var factory = Factory();

        var (discovery, yonlendirme) = await HttpUzerindenAsync(factory);

        Assert.NotEqual(HttpStatusCode.OK, discovery);
        Assert.StartsWith("http://localhost/Account/Login", yonlendirme);
    }

    [Fact]
    public async Task Listede_olmayan_adresten_gelen_baslik_dikkate_alinmiyor()
    {
        using var factory = Factory("10.9.9.0/24", "192.0.2.1");

        Assert.Equal(ProxyAdresi.ToString(), await KaydedilenIpAsync(factory));
    }
}
