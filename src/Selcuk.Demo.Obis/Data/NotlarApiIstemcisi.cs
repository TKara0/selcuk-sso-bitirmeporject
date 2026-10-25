using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Selcuk.Demo.Obis.Data;

/// <summary>
/// Demo API'den (GET /api/notlar) oturumdaki kullanıcının notlarını çeker. Access token çerezde saklıdır
/// (SaveTokens); süresi dolmak üzereyse ortak kütüphane istekten önce yenilemiştir. Token loglanmaz.
/// API'ye ulaşılamaz veya hata dönerse null döner; sayfa Türkçe bir mesaj gösterir.
/// </summary>
public sealed class NotlarApiIstemcisi(
    IHttpClientFactory httpClientFactory,
    ILogger<NotlarApiIstemcisi> logger)
{
    public const string Ad = "demo-api";

    private sealed record NotlarYaniti(string Ogrenci, string Donem, IReadOnlyList<DersNotu> Notlar);

    public async Task<IReadOnlyList<DersNotu>?> NotlarimAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var accessToken = await context.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            logger.LogWarning("Oturumda access token yok; notlar alınamadı.");
            return null;
        }

        try
        {
            using var istek = new HttpRequestMessage(HttpMethod.Get, "api/notlar");
            istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var yanit = await httpClientFactory.CreateClient(Ad).SendAsync(istek, cancellationToken);
            if (!yanit.IsSuccessStatusCode)
            {
                logger.LogWarning("Demo API notları döndürmedi: HTTP {Durum}.", (int)yanit.StatusCode);
                return null;
            }

            var govde = await yanit.Content.ReadFromJsonAsync<NotlarYaniti>(JsonSerializerOptions.Web, cancellationToken);
            return govde?.Notlar;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            logger.LogWarning("Demo API'ye ulaşılamadı: {Hata}.", ex.GetType().Name);
            return null;
        }
    }
}
