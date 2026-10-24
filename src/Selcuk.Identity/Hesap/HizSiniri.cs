using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Kaba kuvvet ve tahmin denemelerine karşı IP başına istek sınırı (ASP.NET Core rate limiter). Giriş, iki aşamalı
/// doğrulama (giriş ikinci adımı ve kurulum/yönetim), şifremi unuttum, şifre sıfırlama ve şifre değiştirme sayfaları
/// <c>[EnableRateLimiting(HassasForm)]</c> ile işaretlidir; yalnızca POST sayılır, her sayfanın ayrı sayacı vardır.
/// IP, güvenilen proxy arkasında gerçek istemci adresidir (<see cref="GuvenilenProxyler"/>). Sınır aşılınca 429,
/// Türkçe hata sayfası ve denetim kaydı.
/// </summary>
public static class HizSiniri
{
    public const string HassasForm = "hassas-form";
    public const int VarsayilanDakikadaIstek = 10;

    public static IServiceCollection AddHizSiniri(this IServiceCollection services, IConfiguration configuration)
    {
        var dakikadaIstek = configuration.GetValue<int?>("HizSiniri:DakikadaIstek") ?? VarsayilanDakikadaIstek;

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = ReddedildiAsync;
            options.AddPolicy(HassasForm, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(
                    // Sayfa, yolun yazılışından (büyük/küçük harf, sondaki /) bağımsız olarak uç noktayla belirlenir.
                    $"{context.GetEndpoint()?.DisplayName}|{context.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = dakikadaIstek,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                : RateLimitPartition.GetNoLimiter(string.Empty));
        });
    }

    private static async ValueTask ReddedildiAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var sayfa = http.GetEndpoint()?.DisplayName;
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HizSiniri))
            .LogWarning("İstek sınırı aşıldı: {Sayfa}", sayfa);
        // İstek sınırı kimlik doğrulamadan önce çalışır; kayıtta kullanıcı yok, IP ve sayfa var.
        http.RequestServices.GetRequiredService<DenetimKaydedici>().Ekle(DenetimOlayi.HizSiniriAsildi,
            DenetimSonucu.Basarisiz, ayrinti: $"sayfa={sayfa}");

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var bekleme))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(bekleme.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        http.Response.ContentType = "text/html; charset=utf-8";
        await http.Response.WriteAsync(CokFazlaIstekSayfasi, cancellationToken);
    }

    // Razor düzeniyle aynı görünüm; dinamik içerik yok.
    private const string CokFazlaIstekSayfasi = """
        <!DOCTYPE html>
        <html lang="tr">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <title>Çok fazla deneme · Tek Şifre Giriş</title>
            <link rel="stylesheet" href="/css/site.css" />
        </head>
        <body>
            <header class="ust"><a class="marka" href="/">Tek Şifre Giriş</a></header>
            <main class="kap">
                <section class="kart">
                    <h1>Çok fazla deneme</h1>
                    <p class="aciklama">
                        Kısa sürede çok fazla istek gönderildi. Lütfen bir dakika bekleyip tekrar deneyin.
                    </p>
                    <a class="dugme ikincil" href="/Account/Login">Giriş sayfasına dön</a>
                </section>
            </main>
            <footer class="alt">Merkezi kimlik doğrulama sunucusu</footer>
        </body>
        </html>
        """;
}
