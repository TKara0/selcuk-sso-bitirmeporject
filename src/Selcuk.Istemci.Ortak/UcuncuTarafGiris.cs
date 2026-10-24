using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// OpenID Connect Core 1.0, bölüm 4 "Initiating Login from a Third Party": kimlik sunucusu girişi istemcinin
/// initiate_login_uri adresinden başlatırken iss parametresini gönderir. Parametre varsa yapılandırılmış kimlik
/// sunucusunun issuer değeriyle birebir aynı olmalıdır; değilse istek kimlik sunucusuna yönlendirilmeden reddedilir.
/// Uygulamanın kendi "Giriş yap" düğmesi iss göndermez; bu durumda normal giriş yapılır.
/// </summary>
internal static class UcuncuTarafGiris
{
    public const string IssParametresi = "iss";

    public static async Task IssDogrulaAsync(RedirectContext context)
    {
        if (!context.Request.Query.TryGetValue(IssParametresi, out var iss))
        {
            return;
        }

        var yapilandirma = context.Options.Configuration
            ?? await context.Options.ConfigurationManager!.GetConfigurationAsync(context.HttpContext.RequestAborted);
        if (iss.Count == 1 && string.Equals(iss[0], yapilandirma.Issuer, StringComparison.Ordinal))
        {
            return;
        }

        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(UcuncuTarafGiris))
            .LogWarning("Üçüncü taraf giriş isteği reddedildi: iss parametresi yapılandırılmış kimlik sunucusuyla eşleşmiyor.");

        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Geçersiz giriş isteği.", context.HttpContext.RequestAborted);
    }
}
