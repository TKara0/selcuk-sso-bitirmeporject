using System.Collections.Concurrent;
using System.Net;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Kimlik sunucusunun Back-Channel Logout için yaptığı HTTP POST'ları yakalar; testlerde gerçek ağa çıkılmaz.
/// <see cref="UlasilamayanAdresler"/> içindeki adreslere bağlantı hatası verir.
/// </summary>
public sealed class SahteBackChannelAlicisi : HttpMessageHandler
{
    public sealed record Istek(Uri Adres, string? IcerikTuru, IReadOnlyDictionary<string, string> Form);

    public ConcurrentQueue<Istek> Istekler { get; } = new();

    public ConcurrentDictionary<Uri, bool> UlasilamayanAdresler { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var govde = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var form = govde.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => WebUtility.UrlDecode(p[0]), p => p.Length > 1 ? WebUtility.UrlDecode(p[1]) : string.Empty);

        Istekler.Enqueue(new Istek(request.RequestUri!, request.Content?.Headers.ContentType?.MediaType, form));

        if (UlasilamayanAdresler.ContainsKey(request.RequestUri!))
        {
            throw new HttpRequestException("Sahte bağlantı hatası.");
        }

        return new HttpResponseMessage(HttpStatusCode.OK);
    }
}
