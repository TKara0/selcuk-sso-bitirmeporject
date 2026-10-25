using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Selcuk.Istemci.Ortak.Tests;

/// <summary>
/// Ortak kütüphanede access token'ın istek sırasında yenilenmesi (refresh token, tek uçuş), invalid_grant'te yerel
/// oturumun kapanması ve çıkışta refresh token'ın iptali. Kimlik sunucusu yerine sahte token uç noktası kullanılır.
/// </summary>
public sealed class TokenYenilemeTests
{
    private static async Task<HttpClient> TokenliOturumAsync(TestUygulamasi uygulama, int kalanSaniye)
    {
        var tarayici = uygulama.YeniTarayici();
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync($"/test/tokenli-oturum-ac?kalanSaniye={kalanSaniye}")).StatusCode);
        return tarayici;
    }

    private static async Task<JsonElement> TokenlarAsync(HttpClient tarayici) =>
        JsonDocument.Parse(await tarayici.GetStringAsync("/test/tokenlar")).RootElement.Clone();

    [Fact]
    public async Task Suresi_dolmak_uzere_olan_access_token_istek_sirasinda_yenileniyor_uzun_sureli_yenilenmiyor()
    {
        var loglar = new LogYakalayici();
        await using var uygulama = await TestUygulamasi.BaslatAsync(logYakalayici: loglar);

        // 10 dakika kalan token yenilenmez.
        var uzun = await TokenliOturumAsync(uygulama, kalanSaniye: 600);
        Assert.Equal(HttpStatusCode.OK, (await uzun.GetAsync("/korumali")).StatusCode);
        Assert.Equal(0, uygulama.TokenUcu.YenilemeSayisi);

        // 30 saniye kalan token istek sırasında sessizce yenilenir, çerez güncellenir.
        var tarayici = await TokenliOturumAsync(uygulama, kalanSaniye: 30);
        var yanit = await tarayici.GetAsync("/korumali");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Equal(1, uygulama.TokenUcu.YenilemeSayisi);
        var istek = Assert.Single(uygulama.TokenUcu.YenilemeIstekleri);
        Assert.Equal("refresh_token", istek["grant_type"]);
        Assert.Equal("eski-rt", istek["refresh_token"]);
        Assert.Equal(TestUygulamasi.ClientId, istek["client_id"]);
        Assert.True(istek.ContainsKey("client_secret"));

        var tokenlar = await TokenlarAsync(tarayici);
        Assert.Equal("yeni-at-1", tokenlar.GetProperty("access_token").GetString());
        Assert.Equal("yeni-rt-1", tokenlar.GetProperty("refresh_token").GetString());
        var bitis = DateTimeOffset.Parse(tokenlar.GetProperty("expires_at").GetString()!, CultureInfo.InvariantCulture);
        Assert.InRange(bitis - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));

        // Yenilenen token'la sonraki istekler yeniden yenilemez.
        Assert.Equal(HttpStatusCode.OK, (await tarayici.GetAsync("/korumali")).StatusCode);
        Assert.Equal(1, uygulama.TokenUcu.YenilemeSayisi);

        // Token değerleri loglanmaz.
        foreach (var deger in new[] { "eski-at", "eski-rt", "eski-idt", "yeni-at-1", "yeni-rt-1" })
        {
            Assert.DoesNotContain(loglar.Mesajlar, m => m.Contains(deger, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Invalid_grant_gelince_yerel_oturum_kapaniyor_ve_kimlik_dogrulamaya_gidiliyor()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();
        uygulama.TokenUcu.InvalidGrant = true;
        var tarayici = await TokenliOturumAsync(uygulama, kalanSaniye: 30);

        var yanit = await tarayici.GetAsync("/korumali");

        // Oturum kapandı: korumalı sayfa kimlik sunucusuna (OIDC challenge) yönlendirir.
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(TestUygulamasi.Issuer + "connect/authorize", yanit.Headers.Location!.ToString());
        Assert.Equal(1, uygulama.TokenUcu.YenilemeSayisi);

        // Çerez silindi: sonraki istek de oturumsuz ve refresh token bir daha gönderilmez.
        var sonraki = await tarayici.GetAsync("/test/tokenlar");
        Assert.Equal(HttpStatusCode.Redirect, sonraki.StatusCode);
        Assert.StartsWith(TestUygulamasi.Issuer + "connect/authorize", sonraki.Headers.Location!.ToString());
        Assert.Equal(1, uygulama.TokenUcu.YenilemeSayisi);
    }

    [Fact]
    public async Task Eszamanli_istekler_ayni_refresh_tokeni_bir_kez_kullaniyor()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();
        uygulama.TokenUcu.Gecikme = TimeSpan.FromMilliseconds(400);
        var tarayici = await TokenliOturumAsync(uygulama, kalanSaniye: 30);

        var yanitlar = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => tarayici.GetAsync("/korumali")));

        Assert.All(yanitlar, y => Assert.Equal(HttpStatusCode.OK, y.StatusCode));
        Assert.Equal(1, uygulama.TokenUcu.YenilemeSayisi);
        Assert.Equal("yeni-at-1", (await TokenlarAsync(tarayici)).GetProperty("access_token").GetString());
    }

    [Fact]
    public async Task Cikista_once_refresh_token_iptal_ediliyor_sonra_end_sessiona_gidiliyor()
    {
        await using var uygulama = await TestUygulamasi.BaslatAsync();
        var tarayici = await TokenliOturumAsync(uygulama, kalanSaniye: 600);

        var yanit = await tarayici.GetAsync("/test/cikis");

        var iptal = Assert.Single(uygulama.TokenUcu.IptalIstekleri);
        Assert.Equal("eski-rt", iptal["token"]);
        Assert.Equal("refresh_token", iptal["token_type_hint"]);
        Assert.Equal(TestUygulamasi.ClientId, iptal["client_id"]);
        Assert.True(iptal.ContainsKey("client_secret"));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith(TestUygulamasi.Issuer + "connect/logout", yanit.Headers.Location!.ToString());
    }

    private sealed class LogYakalayici : ILoggerProvider
    {
        public ConcurrentQueue<string> Mesajlar { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Kaydedici(this);

        public void Dispose()
        {
        }

        private sealed class Kaydedici(LogYakalayici sahip) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => sahip.Mesajlar.Enqueue(formatter(state, exception) + exception);
        }
    }
}
