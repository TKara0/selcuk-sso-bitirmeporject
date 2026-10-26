using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Selcuk.Arayuz;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Kaba kuvvet ve tahmin denemelerine karşı IP başına istek sınırı (ASP.NET Core rate limiter). Giriş, iki aşamalı
/// doğrulama (giriş ikinci adımı ve kurulum/yönetim), şifremi unuttum, şifre sıfırlama ve şifre değiştirme sayfaları
/// <c>[EnableRateLimiting(HassasForm)]</c> ile işaretlidir; yalnızca POST sayılır, her sayfanın ayrı sayacı vardır.
/// IP, güvenilen proxy arkasında gerçek istemci adresidir (<see cref="GuvenilenProxyler"/>). Sınır aşılınca 429,
/// Türkçe hata sayfası ve denetim kaydı. Tek istisna performans ölçümüdür: yalnızca Development'ta ve
/// Olcum:HizSiniriGevset açıkça true ise, giriş sayfasına ölçüm hesaplarıyla (<see cref="OlcumKullanicilari"/>) yapılan
/// POST'lar sayılmaz (<see cref="UseOlcumGevsetmesi"/>); diğer kullanıcılar ve sayfalar için sınır aynıdır.
/// </summary>
public static class HizSiniri
{
    public const string HassasForm = "hassas-form";
    public const int VarsayilanDakikadaIstek = 10;
    private const string GirisSayfasi = "/Account/Login";
    private static readonly object OlcumIstegi = new();

    public static IServiceCollection AddHizSiniri(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var dakikadaIstek = configuration.GetValue<int?>("HizSiniri:DakikadaIstek") ?? VarsayilanDakikadaIstek;
        services.AddSingleton(new OlcumGevsetmesi(OlcumKullanicilari.GevsetmeEtkinMi(environment, configuration)));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = ReddedildiAsync;
            options.AddPolicy(HassasForm, context => HttpMethods.IsPost(context.Request.Method)
                && !context.Items.ContainsKey(OlcumIstegi)
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

    /// <summary>
    /// UseRateLimiter'dan önce: gevşetme etkinse giriş sayfasına gelen POST'un formu okunur ve kullanıcı adı ölçüm
    /// hesabıysa istek işaretlenir (istek sınırı politikası işaretli isteği saymaz). Gevşetme kapalıyken hiçbir şey
    /// yapmaz; form okunmaz. Form ASP.NET Core'un form önbelleğinde kalır, model bağlama aynı formu kullanır.
    /// </summary>
    public static IApplicationBuilder UseOlcumGevsetmesi(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.RequestServices.GetRequiredService<OlcumGevsetmesi>().Etkin
                && HttpMethods.IsPost(context.Request.Method)
                && context.Request.HasFormContentType
                && context.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath == GirisSayfasi)
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                if (OlcumKullanicilari.OlcumKullanicisiMi(form["Input.KullaniciAdi"]))
                {
                    context.Items[OlcumIstegi] = true;
                }
            }

            await next(context);
        });

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

    // Ortak düzenle (Selcuk.Arayuz/_SelcukDuzen) aynı görünüm; dinamik içerik yok. İstek sınırı Razor'dan önce
    // çalıştığı için sayfa sabit metindir; kapı işareti, alt bilgi ve stil dosyası ortak kütüphaneden gelir.
    private const string CokFazlaIstekSayfasi = $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1" />
            <meta name="color-scheme" content="light dark" />
            <title>Çok fazla deneme — Tek Şifre Giriş</title>
            <link rel="stylesheet" href="{{Duzen.StilDosyasi}}" />
            <link rel="stylesheet" href="/css/site.css" />
        </head>
        <body>
            <a class="atla" href="#icerik">İçeriğe geç</a>
            <header class="ust-cubuk">
                <a class="marka" href="/">{{KapiIsareti.Kucuk}}<span>Tek Şifre Giriş</span><span class="etiket etiket-notr">Test</span></a>
            </header>
            <main id="icerik" class="icerik icerik-dar">
                <div class="durum-sayfasi">
                    <h1>Çok fazla deneme</h1>
                    <p class="giris-metni">Kısa sürede çok fazla istek geldi. Bir dakika bekleyip tekrar dene.</p>
                    <a class="dugme dugme-ana" href="/Account/Login">Giriş sayfasına dön</a>
                </div>
            </main>
            <footer class="alt-bilgi"><p>{{Duzen.AltBilgi}}</p></footer>
        </body>
        </html>
        """;
}

/// <summary>Ölçüm hesapları için giriş sınırı gevşetmesi açık mı (<see cref="OlcumKullanicilari.GevsetmeEtkinMi"/>).</summary>
public sealed record OlcumGevsetmesi(bool Etkin);
