using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>
/// "VeriKaynagi" ayarları: Mod "Mock" (varsayılan; uygulamanın içindeki örnek veri) veya "Api" (ApiAdresi'ndeki
/// kaynak sunucusu, yalnızca https). Geçersiz mod veya Api modunda geçersiz adres açılışta hatayla durdurur
/// (ValidateOnStart).
/// </summary>
public sealed class VeriKaynagiAyarlari
{
    public const string Bolum = "VeriKaynagi";
    public const string MockModu = "Mock";
    public const string ApiModu = "Api";

    public string Mod { get; set; } = MockModu;

    public string? ApiAdresi { get; set; }

    /// <summary>API isteğinin zaman aşımı (varsayılan 5 sn, Demo OBİS'in Demo API isteğiyle aynı).</summary>
    public TimeSpan ZamanAsimi { get; set; } = TimeSpan.FromSeconds(5);

    public bool ApiMi => string.Equals(Mod, ApiModu, StringComparison.Ordinal);

    /// <summary>Ayar geçerliyse null, değilse Türkçe açıklama.</summary>
    public string? Hata()
    {
        if (Mod is not (MockModu or ApiModu))
        {
            return $"'{Bolum}:Mod' ayarı geçersiz ('{Mod}'): \"{MockModu}\" veya \"{ApiModu}\" olmalı.";
        }

        // Access token yalnızca TLS üzerinden gönderilir.
        if (ApiMi && !(Uri.TryCreate(ApiAdresi, UriKind.Absolute, out var adres) && adres.Scheme == Uri.UriSchemeHttps))
        {
            return $"'{Bolum}:ApiAdresi' Api modunda https ile başlayan mutlak bir adres olmalı.";
        }

        return ZamanAsimi > TimeSpan.Zero ? null : $"'{Bolum}:ZamanAsimi' sıfırdan büyük olmalı.";
    }
}

/// <summary>Veri kaynağına ulaşılamadı (bağlantı hatası, zaman aşımı, 5xx, okunamayan yanıt) veya erişim reddedildi.</summary>
public sealed class VeriKaynagiHatasi(VeriKaynagiHatasi.Turu tur, string mesaj) : Exception(mesaj)
{
    public enum Turu
    {
        Ulasilamiyor,
        Yetki,
    }

    public Turu Tur { get; } = tur;
}

/// <summary>
/// Otomasyonların Api veri kaynaklarının ortak HTTP istemcisi. Her istek oturumdaki access token'ı Authorization:
/// Bearer başlığıyla taşır; token çerezdedir (SaveTokens) ve süresi dolmak üzereyse Selcuk.Istemci.Ortak'ın
/// TokenYenileyici'si istekten önce çerez doğrulamasında yenilemiştir. Token tarayıcıya, loga veya adrese yazılmaz.
/// Bağlantı hatası, zaman aşımı, 5xx veya okunamayan yanıt <see cref="VeriKaynagiHatasi.Turu.Ulasilamiyor"/>; 401/403
/// <see cref="VeriKaynagiHatasi.Turu.Yetki"/> olarak fırlatılır ve "/Hata" sayfası Türkçe mesaj gösterir.
/// </summary>
public sealed class OtomasyonApiIstemcisi(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OtomasyonApiIstemcisi> logger)
{
    public const string HttpIstemciAdi = "otomasyon-api";

    /// <summary>Sözleşmelerin JSON biçimi: camelCase alanlar, enum'lar metin (ör. "basvuruBekliyor").</summary>
    public static readonly JsonSerializerOptions JsonAyarlari = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<T> GetAsync<T>(string yol, CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("API isteği yalnızca bir HTTP isteği içinde yapılabilir.");
        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogWarning("Oturumda access token yok; veri kaynağına istek yapılmadı.");
            throw new VeriKaynagiHatasi(VeriKaynagiHatasi.Turu.Yetki, "Oturumda access token yok.");
        }

        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Get, yol);
            istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var yanit = await httpClientFactory.CreateClient(HttpIstemciAdi).SendAsync(istek, cancellationToken);
            if (yanit.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Veri kaynağı isteği reddetti: HTTP {Durum}.", (int)yanit.StatusCode);
                throw new VeriKaynagiHatasi(VeriKaynagiHatasi.Turu.Yetki, $"HTTP {(int)yanit.StatusCode}");
            }

            if (!yanit.IsSuccessStatusCode)
            {
                logger.LogWarning("Veri kaynağı yanıt vermedi: HTTP {Durum}.", (int)yanit.StatusCode);
                throw new VeriKaynagiHatasi(VeriKaynagiHatasi.Turu.Ulasilamiyor, $"HTTP {(int)yanit.StatusCode}");
            }

            return await yanit.Content.ReadFromJsonAsync<T>(JsonAyarlari, cancellationToken)
                ?? throw new JsonException("Boş yanıt.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException
            && !cancellationToken.IsCancellationRequested)
        {
            // Yalnızca hata türü loglanır (adres, token ve yanıt gövdesi yazılmaz).
            logger.LogWarning("Veri kaynağına ulaşılamadı: {Hata}.", ex.GetType().Name);
            throw new VeriKaynagiHatasi(VeriKaynagiHatasi.Turu.Ulasilamiyor, ex.GetType().Name);
        }
    }

    /// <summary>API adresi ve zaman aşımı (AddSelcukOtomasyon, Api modunda).</summary>
    internal static void Yapilandir(IServiceProvider services, HttpClient client)
    {
        var ayarlar = services.GetRequiredService<IOptions<VeriKaynagiAyarlari>>().Value;
        if (ayarlar.ApiAdresi is { } adres)
        {
            client.BaseAddress = new Uri(adres.EndsWith('/') ? adres : adres + "/");
        }

        client.Timeout = ayarlar.ZamanAsimi;
    }
}
