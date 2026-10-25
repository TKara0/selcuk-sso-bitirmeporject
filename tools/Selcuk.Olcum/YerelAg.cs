using System.Net;

namespace Selcuk.Olcum;

/// <summary>
/// Ölçüm aracı yalnızca bu bilgisayardaki kendi uygulamalarımıza istek atar. Hedef adresler açılışta denetlenir
/// (<see cref="Dogrula"/>); ayrıca her giden istek <see cref="YalnizcaYerelHandler"/>'dan geçer, böylece bir yönlendirme
/// de araçtan dışarı (gerçek Selçuk sistemleri, internet) istek attıramaz.
/// </summary>
public static class YerelAg
{
    /// <summary>localhost, 127.0.0.0/8 veya ::1 mi.</summary>
    public static bool YerelMi(Uri adres) =>
        adres.IsAbsoluteUri
        && (string.Equals(adres.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(adres.Host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip)));

    /// <summary>Hedef adres https ve yerel değilse ölçüm başlamaz.</summary>
    public static Uri Dogrula(string ad, string? deger)
    {
        if (!Uri.TryCreate(deger, UriKind.Absolute, out var adres))
        {
            throw new OlcumAyarHatasi($"Hedef '{ad}' geçerli bir mutlak adres değil.");
        }

        if (!YerelMi(adres))
        {
            throw new OlcumAyarHatasi(
                $"Hedef '{ad}' ({adres.Host}) yerel değil. Ölçüm aracı yalnızca localhost / 127.0.0.1 / ::1 adreslerine istek atar.");
        }

        if (adres.Scheme != Uri.UriSchemeHttps)
        {
            throw new OlcumAyarHatasi($"Hedef '{ad}' https olmalıdır.");
        }

        return adres.AbsoluteUri.EndsWith('/') ? adres : new Uri(adres.AbsoluteUri + "/");
    }
}

/// <summary>Yerel olmayan bir adrese giden her isteği gönderilmeden reddeder.</summary>
public sealed class YalnizcaYerelHandler(HttpMessageHandler ic) : DelegatingHandler(ic)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || !YerelAg.YerelMi(request.RequestUri))
        {
            throw new OlcumHatasi("yerel-olmayan-adres");
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Yapılandırma hatası: ölçüm başlamaz.</summary>
public sealed class OlcumAyarHatasi(string message) : Exception(message);

/// <summary>Bir ölçüm turunun başarısız olması. Kategori kısa bir koddur; adres, kullanıcı adı veya token içermez.</summary>
public sealed class OlcumHatasi(string kategori) : Exception(kategori)
{
    public string Kategori { get; } = kategori;
}
