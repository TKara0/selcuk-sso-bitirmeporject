using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Selcuk.Identity;

/// <summary>
/// X-Forwarded-For ve X-Forwarded-Proto başlıklarına yalnızca "GuvenilenProxyler" ayarındaki adreslerden (IP veya CIDR
/// ağı) gelen isteklerde güvenilir. Liste boşsa (varsayılan) başlıklar hiç işlenmez; ASP.NET Core'un loopback'e güvenen
/// varsayılanı da temizlenir. Böylece istek sınırı ve denetim kaydı, istemcinin kendi yazdığı bir başlıkla kandırılamaz;
/// proxy arkasında ise proxy'nin değil gerçek istemcinin IP'sini kullanır. TLS'i proxy sonlandırıyorsa
/// X-Forwarded-Proto: https sayesinde istek HTTPS sayılır (OpenIddict'in HTTPS zorunluluğu, yönlendirmeler, çerezler).
/// </summary>
public static class GuvenilenProxyler
{
    public const string AyarAdi = "GuvenilenProxyler";

    public static void Yapilandir(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var adresler = configuration.GetSection(AyarAdi).Get<string[]>() ?? [];
        if (adresler.Length == 0)
        {
            options.ForwardedHeaders = ForwardedHeaders.None;
            return;
        }

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var adres in adresler)
        {
            if (adres.Contains('/') && System.Net.IPNetwork.TryParse(adres, out var ag))
            {
                options.KnownIPNetworks.Add(ag);
            }
            else if (IPAddress.TryParse(adres, out var ip))
            {
                options.KnownProxies.Add(ip);
            }
            else
            {
                throw new InvalidOperationException(
                    $"'{AyarAdi}' içindeki '{adres}' geçerli bir IP adresi veya CIDR ağı değil.");
            }
        }
    }
}
