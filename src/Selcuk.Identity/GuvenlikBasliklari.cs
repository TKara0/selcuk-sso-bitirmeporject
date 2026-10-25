using Microsoft.AspNetCore.Authorization;

namespace Selcuk.Identity;

/// <summary>
/// Tüm yanıtlara tarayıcı güvenlik başlıklarını ekler. Sayfalar yalnızca kendi CSS dosyasını kullanır;
/// satır içi betik veya stil yoktur. Giriş gerektiren uç noktaların ([Authorize]) yanıtları önbelleğe alınmaz
/// (Cache-Control: no-store): çıkıştan sonra geri tuşu kişisel bir sayfayı tarayıcı önbelleğinden göstermesin. Herkese
/// açık sayfalar ve statik dosyalar normal önbelleklenir.
/// </summary>
public static class GuvenlikBasliklari
{
    // form-action bilerek kısıtlanmadı: giriş, onay ve çıkış formlarının POST'u istemcinin
    // redirect_uri / post_logout_redirect_uri adresine yönlendirmeyle sonuçlanır.
    public const string ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";

    public static IApplicationBuilder UseGuvenlikBasliklari(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            // OnStarting: hata sayfası gibi yanıtı sıfırlayan ara katmanlardan sonra da başlıklar korunur.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "no-referrer";
                headers.XFrameOptions = "DENY";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                if (GirisGerektirir(context.GetEndpoint()))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            return next(context);
        });

    /// <summary>Uç nokta [Authorize] (veya eşdeğer yetkilendirme meta verisi) taşıyor ve [AllowAnonymous] değilse true.</summary>
    public static bool GirisGerektirir(Endpoint? endpoint) =>
        endpoint is not null
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
        && endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null;
}
