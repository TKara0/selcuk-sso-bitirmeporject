using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// /signin-oidc dönüşü başarısız olduğunda (ör. geri tuşuyla kullanılmış bir dönüş adresine gelinmesi, eşleştirme
/// çerezinin süresinin dolması, kullanıcının onay ekranında reddetmesi) ham hata sayfası yerine kullanıcıyı
/// yönlendirir. Correlation, nonce ve token doğrulamaları değişmez; yalnızca hatanın nasıl gösterildiği ele alınır.
/// </summary>
public static class GirisDonusHatasi
{
    public const string VarsayilanYol = "/GirisHatasi";

    /// <summary>Giriş işlemi tamamlanamadı (eşleştirme/state hatası, süresi dolmuş giriş vb.).</summary>
    public const string OturumSuresi = "oturum-suresi";

    /// <summary>Kullanıcı kimlik sunucusunun onay ekranında erişim izni vermedi (error=access_denied).</summary>
    public const string ErisimReddedildi = "erisim-reddedildi";

    internal static async Task IsleAsync(RemoteFailureContext context, string hataSayfasi)
    {
        var http = context.HttpContext;
        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(GirisDonusHatasi));
        var zaman = http.RequestServices.GetRequiredService<TimeProvider>();

        // Varsayılan şema istemci çerezidir; Back-Channel Logout ile iptal edilmiş oturum burada da reddedilir.
        var giris = await http.AuthenticateAsync();
        var girisYapmis = giris.Succeeded && giris.Principal?.Identity?.IsAuthenticated == true;

        var neden = http.Request.Query["error"] == "access_denied" ? ErisimReddedildi : OturumSuresi;

        // code, state, token ve çerez değerleri loglanmaz; yalnızca hata türü ve zamanı. AuthenticationFailureException
        // mesajları framework'ün sabit metinleridir ("Correlation failed." gibi); diğer istisnaların mesajı yazılmaz.
        var hataTuru = context.Failure is AuthenticationFailureException { InnerException: null } f
            ? f.Message
            : context.Failure?.GetType().Name;
        logger.LogWarning("Giriş dönüşü başarısız: {Neden} ({HataTuru}), giriş yapmış: {GirisYapmis}, zaman: {Zaman:O}",
            neden, hataTuru, girisYapmis, zaman.GetUtcNow());

        if (girisYapmis)
        {
            var donus = context.Properties?.RedirectUri;
            http.Response.Redirect(YerelAdresMi(donus) ? donus! : "/");
        }
        else
        {
            http.Response.Redirect(hataSayfasi + QueryString.Create("neden", neden));
        }

        context.HandleResponse();
    }

    /// <summary>Yalnızca bu uygulamaya ait göreli yollar: "/x" kabul, "//x", "/\x" ve kontrol karakterleri red.</summary>
    internal static bool YerelAdresMi(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Any(char.IsControl);
}
