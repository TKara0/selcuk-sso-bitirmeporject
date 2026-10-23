using System.Net;

namespace Selcuk.Istemci.Ortak.Tests;

public sealed class BackChannelLogoutTests : IAsyncLifetime
{
    private TestUygulamasi _uygulama = null!;

    public async Task InitializeAsync() => _uygulama = await TestUygulamasi.BaslatAsync();

    public async Task DisposeAsync() => await _uygulama.DisposeAsync();

    private static async Task OturumGecersizAsync(HttpClient tarayici)
    {
        // İptal edilen oturum reddedilir; korumalı sayfa yeniden kimlik doğrulamaya (authorize) yönlendirir.
        var yanit = await tarayici.GetAsync("/korumali");
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(TestUygulamasi.Issuer + "connect/authorize", yanit.Headers.Location!.ToString());
    }

    private static async Task OturumGecerliAsync(HttpClient tarayici) =>
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync("/korumali")).StatusCode);

    /// <summary>Geçersiz token 400 alır ve oturum etkilenmez.</summary>
    private async Task ReddedilirAsync(string token, HttpClient tarayici)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _uygulama.LogoutGonderAsync(token)).StatusCode);
        await OturumGecerliAsync(tarayici);
    }

    [Fact]
    public async Task Gecerli_logout_token_oturumu_sonlandiriyor()
    {
        var tarayici = await _uygulama.OturumAcmisTarayiciAsync("sid-1");

        var yanit = await _uygulama.LogoutGonderAsync(_uygulama.LogoutToken("sid-1"));

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal("no-store", yanit.Headers.CacheControl?.ToString());
        await OturumGecersizAsync(tarayici);
    }

    [Fact]
    public async Task Imzasi_gecersiz_token_reddediliyor()
    {
        var tarayici = await _uygulama.OturumAcmisTarayiciAsync("sid-1");
        await ReddedilirAsync(_uygulama.LogoutToken("sid-1", anahtar: TestUygulamasi.YabanciAnahtar()), tarayici);
    }

    [Fact]
    public async Task Yanlis_aud_reddediliyor()
    {
        var tarayici = await _uygulama.OturumAcmisTarayiciAsync("sid-1");
        await ReddedilirAsync(_uygulama.LogoutToken("sid-1", audience: "baska-istemci"), tarayici);
    }

    [Fact]
    public async Task Events_eksik_token_reddediliyor()
    {
        var tarayici = await _uygulama.OturumAcmisTarayiciAsync("sid-1");
        await ReddedilirAsync(_uygulama.LogoutToken("sid-1", degistir: c => c.Remove("events")), tarayici);
    }

    [Fact]
    public async Task Nonce_iceren_token_reddediliyor()
    {
        var tarayici = await _uygulama.OturumAcmisTarayiciAsync("sid-1");
        await ReddedilirAsync(_uygulama.LogoutToken("sid-1", degistir: c => c["nonce"] = "n-123"), tarayici);
    }

    [Fact]
    public async Task Ayni_jti_ikinci_kez_reddediliyor()
    {
        var token = _uygulama.LogoutToken("sid-1");
        Assert.Equal(HttpStatusCode.OK, (await _uygulama.LogoutGonderAsync(token)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await _uygulama.LogoutGonderAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Farkli_sidli_oturum_etkilenmiyor()
    {
        var kapanacak = await _uygulama.OturumAcmisTarayiciAsync("sid-1");
        var digeri = await _uygulama.OturumAcmisTarayiciAsync("sid-2");

        Assert.Equal(HttpStatusCode.OK, (await _uygulama.LogoutGonderAsync(_uygulama.LogoutToken("sid-1"))).StatusCode);

        await OturumGecersizAsync(kapanacak);
        await OturumGecerliAsync(digeri);
    }

    [Fact]
    public async Task Guvenlik_basliklari_istemci_yanitlarinda_mevcut()
    {
        var yanit = await _uygulama.YeniTarayici().GetAsync("/korumali");

        Assert.Equal("nosniff", yanit.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", yanit.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", yanit.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", yanit.Headers.GetValues("Content-Security-Policy").Single());
    }
}
