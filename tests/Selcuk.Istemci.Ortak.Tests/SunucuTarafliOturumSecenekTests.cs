using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>
/// AddSelcukSSO'nun isteğe bağlı sunucu tarafı oturum seçeneği (varsayılan kapalı) ve "Uygulamalarım" bağlantısı için
/// kimlik sunucusu kök adresi (KimlikSunucusuBilgisi).
/// </summary>
public sealed class SunucuTarafliOturumSecenekTests
{
    private static ServiceProvider Servisler(Action<SelcukSsoSecenekleri>? ayarla, string? authority = "https://localhost:5001")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:ClientId"] = "test-istemci",
            ["Kimlik:Authority"] = authority,
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSelcukSSO(configuration, ayarla);
        return services.BuildServiceProvider();
    }

    private static CookieAuthenticationOptions Cerez(ServiceProvider servisler) =>
        servisler.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);

    [Fact]
    public void Varsayilan_kapali_token_lar_cerezde_kalir_depo_ve_dinleyici_yok()
    {
        using var servisler = Servisler(null);

        Assert.Null(Cerez(servisler).SessionStore);
        Assert.Null(servisler.GetService<OturumBiletDeposu>());
        Assert.Empty(servisler.GetServices<IOturumIptalDinleyicisi>());
    }

    [Fact]
    public void Secenek_acikken_cerez_semasinin_oturum_deposu_OturumBiletDeposu_ve_iptal_dinleyicisi_ayni_nesne()
    {
        using var servisler = Servisler(o => o.SunucuTarafliOturum = true);

        var depo = Assert.IsType<OturumBiletDeposu>(Cerez(servisler).SessionStore);
        Assert.Same(depo, servisler.GetRequiredService<OturumBiletDeposu>());
        Assert.Same(depo, Assert.Single(servisler.GetServices<IOturumIptalDinleyicisi>()));
    }

    [Theory]
    [InlineData("https://localhost:5001", "https://localhost:5001/")]
    [InlineData("https://kimlik.test.local/yol?x=1", "https://kimlik.test.local/")]
    [InlineData("http://127.0.0.1:5001", "http://127.0.0.1:5001/")]
    [InlineData("http://localhost:5001", null)]
    [InlineData("ftp://localhost:5001", null)]
    [InlineData("https://kullanici:parola@localhost:5001", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Kimlik_sunucusu_adresi_yalnizca_https_veya_loopback_Authority_kokunden_turetiliyor(string? authority, string? beklenen)
    {
        using var servisler = Servisler(null, authority);

        Assert.Equal(beklenen, servisler.GetRequiredService<KimlikSunucusuBilgisi>().Adres);
        Assert.Equal(beklenen, KimlikSunucusuBilgisi.Koku(authority));
    }
}
