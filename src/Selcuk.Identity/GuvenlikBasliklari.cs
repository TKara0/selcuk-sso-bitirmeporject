namespace Selcuk.Identity;

/// <summary>
/// Tüm yanıtlara tarayıcı güvenlik başlıklarını ekler. Sayfalar yalnızca kendi CSS dosyasını kullanır;
/// satır içi betik veya stil yoktur.
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
                return Task.CompletedTask;
            });

            return next(context);
        });
}
