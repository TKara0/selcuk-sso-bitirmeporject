using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>Çerez süresi, iptal kayıtlarının dağıtık önbellekte tutulması, JWKS hatası ve üçüncü taraf girişte iss.</summary>
public sealed class OturumGuvenligiTests
{
    private static CookieAuthenticationOptions CerezAyarlari(TestUygulamasi uygulama) =>
        uygulama.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

    [Fact]
    public async Task Cerez_suresi_yapilandirmadan_geliyor_ve_kayan()
    {
        await using var varsayilan = await TestUygulamasi.BaslatAsync();
        await using var ayarli = await TestUygulamasi.BaslatAsync(new Dictionary<string, string?>
        {
            ["Kimlik:OturumSuresi"] = "02:30:00",
        });

        Assert.Equal(TimeSpan.FromHours(8), CerezAyarlari(varsayilan).ExpireTimeSpan);
        Assert.True(CerezAyarlari(varsayilan).SlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(150), CerezAyarlari(ayarli).ExpireTimeSpan);
        Assert.True(CerezAyarlari(ayarli).SlidingExpiration);
    }

    [Fact]
    public async Task Iptal_kaydi_ortak_dagitik_onbellekten_okunuyor()
    {
        var onbellek = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        await using var birinci = await TestUygulamasi.BaslatAsync(ortakOnbellek: onbellek);
        await using var ikinci = await TestUygulamasi.BaslatAsync(ortakOnbellek: onbellek);
        Assert.Same(onbellek, ikinci.Services.GetRequiredService<IDistributedCache>());

        // Aynı kullanıcı oturumu (sid) iki örnekte de açık; logout_token yalnızca birinciye gelir.
        var birinciTarayici = await birinci.OturumAcmisTarayiciAsync("sid-ortak");
        var ikinciTarayici = await ikinci.OturumAcmisTarayiciAsync("sid-ortak");
        var digerOturum = await ikinci.OturumAcmisTarayiciAsync("sid-diger");

        Assert.Equal(HttpStatusCode.OK,
            (await birinci.LogoutGonderAsync(birinci.LogoutToken("sid-ortak"))).StatusCode);

        Assert.NotNull(await onbellek.GetAsync("bcl:sid:sid-ortak"));
        Assert.Equal(HttpStatusCode.Redirect, (await birinciTarayici.GetAsync("/korumali")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await ikinciTarayici.GetAsync("/korumali")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await digerOturum.GetAsync("/korumali")).StatusCode);
    }

    [Fact]
    public async Task Jwks_ulasilamazsa_503()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync(jwksUlasilamaz: true);
        var tarayici = await uygulama.OturumAcmisTarayiciAsync("sid-1");

        var yanit = await uygulama.LogoutGonderAsync(uygulama.LogoutToken("sid-1"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, yanit.StatusCode);
        // Doğrulanamayan token oturumu etkilemez.
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync("/korumali")).StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("https://kimlik.test.local")]
    [InlineData("")]
    public async Task Yanlis_iss_ile_baslatilan_giris_reddediliyor(string iss)
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();

        var yanit = await uygulama.YeniTarayici().GetAsync(
            QueryHelpers.AddQueryString("/giris", "iss", iss));

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        Assert.Equal("Geçersiz giriş isteği.", await yanit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dogru_iss_ile_veya_iss_olmadan_giris_kimlik_sunucusuna_gidiyor()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();

        foreach (var adres in new[] { QueryHelpers.AddQueryString("/giris", "iss", TestUygulamasi.Issuer), "/giris" })
        {
            var yanit = await uygulama.YeniTarayici().GetAsync(adres);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith(TestUygulamasi.Issuer + "connect/authorize", yanit.Headers.Location!.ToString());
        }
    }
}
