using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Çerezdeki access token'ın süresi dolmak üzereyken (1 dakikadan az) refresh token ile sessizce yenilenmesi ve
/// çıkışta refresh token'ın iptali (RFC 7009).
/// Kimlik sunucusu kayan refresh token kullanır ve kullanılmış bir refresh token tekrar gelirse oturumun bütün
/// token'larını iptal eder. Bu yüzden aynı çerezle eşzamanlı gelen istekler tek bir yenileme yapar (tek uçuş): aynı
/// refresh token için süren veya az önce biten yenilemenin sonucu paylaşılır (yeni çerezi henüz almamış istekler de
/// eski refresh token'ı ikinci kez göndermez). Kayıtlar bu uygulama örneğinin belleğindedir; birden çok örnekte
/// yapışkan oturum (sticky session) veya dağıtık kilit gerekir. Token değerleri loglanmaz.
/// </summary>
public sealed class TokenYenileyici(
    IOptionsMonitor<OpenIdConnectOptions> oidcAyarlari,
    IConfiguration configuration,
    TimeProvider zaman,
    ILogger<TokenYenileyici> logger)
{
    /// <summary>Access token'ın kalan süresi bundan azsa istek sırasında yenilenir.</summary>
    public static readonly TimeSpan YenilemeEsigi = TimeSpan.FromMinutes(1);

    /// <summary>Biten bir yenilemenin sonucu bu süre boyunca aynı (eski) refresh token için paylaşılır.</summary>
    public static readonly TimeSpan SonucPaylasimSuresi = TimeSpan.FromMinutes(1);

    /// <summary>Çerezdeki refresh token'ın son kullanma zamanı (gösterim için; değer istemcide hesaplanır).</summary>
    public const string RefreshTokenBitisAdi = "refresh_token_expires_at";

    public enum Durum
    {
        Basarili,
        Gecersiz,
        GeciciHata,
    }

    public sealed record Sonuc(Durum Durum, string? AccessToken = null, string? RefreshToken = null,
        string? IdToken = null, DateTimeOffset? AccessTokenBitis = null);

    private readonly ConcurrentDictionary<string, Ucus> _ucuslar = new();

    private sealed record Ucus(Lazy<Task<Sonuc>> Gorev, DateTimeOffset Baslangic);

    /// <summary>Kimlik sunucusundaki Tokenlar:RefreshTokenSuresi ile aynı (Kimlik:YenilemeTokeniSuresi, varsayılan 8 saat).</summary>
    public TimeSpan RefreshTokenSuresi =>
        configuration.GetValue<TimeSpan?>("Kimlik:YenilemeTokeniSuresi") ?? SelcukSsoUzantilari.VarsayilanOturumSuresi;

    public DateTimeOffset Simdi => zaman.GetUtcNow();

    /// <summary>Aynı refresh token için tek bir token isteği yapılır; diğer çağıranlar aynı sonucu bekler.</summary>
    public async Task<Sonuc> YenileAsync(string refreshToken)
    {
        var simdi = zaman.GetUtcNow();
        foreach (var (k, u) in _ucuslar)
        {
            if (u.Gorev.IsValueCreated && u.Gorev.Value.IsCompleted && simdi - u.Baslangic > SonucPaylasimSuresi)
            {
                _ucuslar.TryRemove(k, out _);
            }
        }

        var anahtar = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var ucus = _ucuslar.GetOrAdd(anahtar, _ => new Ucus(new Lazy<Task<Sonuc>>(() => IstekAsync(refreshToken)), simdi));
        var sonuc = await ucus.Gorev.Value;
        if (sonuc.Durum == Durum.GeciciHata)
        {
            // Geçici hatada sonuç paylaşılmaz; sonraki istek yeniden dener.
            _ucuslar.TryRemove(new KeyValuePair<string, Ucus>(anahtar, ucus));
        }

        return sonuc;
    }

    private async Task<Sonuc> IstekAsync(string refreshToken)
    {
        try
        {
            var ayarlar = oidcAyarlari.Get(OpenIdConnectDefaults.AuthenticationScheme);
            var yapilandirma = await ayarlar.ConfigurationManager!.GetConfigurationAsync(CancellationToken.None);
            using var istek = new HttpRequestMessage(HttpMethod.Post, yapilandirma.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(IstemciAlanlari(ayarlar,
                    ("grant_type", "refresh_token"), ("refresh_token", refreshToken))),
            };
            using var yanit = await ayarlar.Backchannel.SendAsync(istek, CancellationToken.None);
            using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
            var kok = json.RootElement;

            if (yanit.IsSuccessStatusCode && Metin(kok, "access_token") is { } accessToken)
            {
                var omur = kok.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var saniye) ? saniye : 0;
                return new Sonuc(Durum.Basarili, accessToken, Metin(kok, "refresh_token"), Metin(kok, "id_token"),
                    zaman.GetUtcNow().AddSeconds(omur));
            }

            if ((int)yanit.StatusCode == 400 && Metin(kok, "error") == "invalid_grant")
            {
                logger.LogWarning("Refresh token reddedildi (invalid_grant: iptal edilmiş, süresi dolmuş veya tekrar kullanılmış).");
                return new Sonuc(Durum.Gecersiz);
            }

            logger.LogWarning("Token yenilenemedi: HTTP {Durum}, hata {Hata}.", (int)yanit.StatusCode, Metin(kok, "error"));
            return new Sonuc(Durum.GeciciHata);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            logger.LogWarning("Token yenilenemedi: {Hata}.", ex.GetType().Name);
            return new Sonuc(Durum.GeciciHata);
        }
    }

    /// <summary>RFC 7009: refresh token'ı kimlik sunucusunda iptal eder. Hata çıkışı engellemez, yalnızca loglanır.</summary>
    public async Task IptalEtAsync(string refreshToken)
    {
        try
        {
            var ayarlar = oidcAyarlari.Get(OpenIdConnectDefaults.AuthenticationScheme);
            var yapilandirma = await ayarlar.ConfigurationManager!.GetConfigurationAsync(CancellationToken.None);
            if (string.IsNullOrEmpty(yapilandirma.RevocationEndpoint))
            {
                return;
            }

            using var istek = new HttpRequestMessage(HttpMethod.Post, yapilandirma.RevocationEndpoint)
            {
                Content = new FormUrlEncodedContent(IstemciAlanlari(ayarlar,
                    ("token", refreshToken), ("token_type_hint", "refresh_token"))),
            };
            using var yanit = await ayarlar.Backchannel.SendAsync(istek, CancellationToken.None);
            if (!yanit.IsSuccessStatusCode)
            {
                logger.LogWarning("Refresh token iptal edilemedi: HTTP {Durum}.", (int)yanit.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning("Refresh token iptal edilemedi: {Hata}.", ex.GetType().Name);
        }
    }

    /// <summary>
    /// Çerez doğrulamasında: access token'ın süresi dolmak üzereyse yeniler ve çerezi günceller; refresh token
    /// reddedilirse (invalid_grant) yerel oturumu kapatır, istek oturumsuz devam eder ve korumalı sayfa kullanıcıyı
    /// yeniden kimlik doğrulamaya gönderir. Geçici hatada oturum korunur.
    /// </summary>
    internal static async Task GerekirseYenileAsync(CookieValidatePrincipalContext context)
    {
        var ozellikler = context.Properties;
        var refreshToken = ozellikler.GetTokenValue("refresh_token");
        if (refreshToken is null
            || !DateTimeOffset.TryParse(ozellikler.GetTokenValue("expires_at"), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var bitis))
        {
            return;
        }

        var yenileyici = context.HttpContext.RequestServices.GetRequiredService<TokenYenileyici>();
        if (bitis - yenileyici.Simdi > YenilemeEsigi)
        {
            return;
        }

        var sonuc = await yenileyici.YenileAsync(refreshToken);
        switch (sonuc.Durum)
        {
            case Durum.Basarili:
                TokenYaz(ozellikler, "access_token", sonuc.AccessToken!);
                TokenYaz(ozellikler, "expires_at", sonuc.AccessTokenBitis!.Value.ToString("o", CultureInfo.InvariantCulture));
                if (sonuc.RefreshToken is not null)
                {
                    TokenYaz(ozellikler, "refresh_token", sonuc.RefreshToken);
                    TokenYaz(ozellikler, RefreshTokenBitisAdi,
                        (yenileyici.Simdi + yenileyici.RefreshTokenSuresi).ToString("o", CultureInfo.InvariantCulture));
                }

                if (sonuc.IdToken is not null)
                {
                    TokenYaz(ozellikler, "id_token", sonuc.IdToken);
                }

                context.ShouldRenew = true;
                break;

            case Durum.Gecersiz:
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                break;
        }
    }

    /// <summary>Girişte refresh token'ın son kullanma zamanını çereze yazar (OpenIdConnectEvents.OnTicketReceived).</summary>
    internal static Task GiristeKaydetAsync(TicketReceivedContext context)
    {
        if (context.Properties?.GetTokenValue("refresh_token") is not null)
        {
            var yenileyici = context.HttpContext.RequestServices.GetRequiredService<TokenYenileyici>();
            TokenYaz(context.Properties, RefreshTokenBitisAdi,
                (yenileyici.Simdi + yenileyici.RefreshTokenSuresi).ToString("o", CultureInfo.InvariantCulture));
        }

        return Task.CompletedTask;
    }

    /// <summary>RP-initiated logout'ta önce refresh token iptal edilir, sonra end-session'a gidilir.</summary>
    internal static async Task CikistaIptalEtAsync(RedirectContext context)
    {
        var refreshToken = await context.HttpContext.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            "refresh_token");
        if (refreshToken is not null)
        {
            await context.HttpContext.RequestServices.GetRequiredService<TokenYenileyici>().IptalEtAsync(refreshToken);
        }
    }

    private static void TokenYaz(AuthenticationProperties ozellikler, string ad, string deger)
    {
        if (!ozellikler.UpdateTokenValue(ad, deger))
        {
            ozellikler.StoreTokens([.. ozellikler.GetTokens(), new AuthenticationToken { Name = ad, Value = deger }]);
        }
    }

    private static Dictionary<string, string> IstemciAlanlari(OpenIdConnectOptions ayarlar,
        params (string Ad, string Deger)[] alanlar)
    {
        var form = alanlar.ToDictionary(a => a.Ad, a => a.Deger);
        form["client_id"] = ayarlar.ClientId!;
        if (!string.IsNullOrEmpty(ayarlar.ClientSecret))
        {
            form["client_secret"] = ayarlar.ClientSecret;
        }

        return form;
    }

    private static string? Metin(JsonElement kok, string ad) =>
        kok.ValueKind == JsonValueKind.Object && kok.TryGetProperty(ad, out var deger) && deger.ValueKind == JsonValueKind.String
            ? deger.GetString()
            : null;
}
