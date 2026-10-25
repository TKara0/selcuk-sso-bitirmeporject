using System.Net;
using System.Threading.Channels;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Denetim;

/// <summary>
/// Güvenlik olaylarını denetim kaydına bırakır. Kayıt bellek içi kuyruğa eklenir, veritabanına
/// <see cref="DenetimYazmaHizmeti"/> arka planda toplu olarak yazar: istek yavaşlamaz, yazım hatası ana işlemi bozmaz
/// (yalnızca loglanır). IP adresi ve user agent o anki istekten alınır; IP, güvenilen proxy arkasında
/// X-Forwarded-For'dan gelen gerçek istemci adresidir (<see cref="GuvenilenProxyler"/>).
/// </summary>
public sealed class DenetimKaydedici(
    IHttpContextAccessor httpContextAccessor,
    AnahtarliOzet ozet,
    TimeProvider zaman,
    ILogger<DenetimKaydedici> logger)
{
    public const int UserAgentUzunlugu = 200;
    public const int AyrintiUzunlugu = 500;

    private readonly Channel<DenetimKaydi> _kanal = Channel.CreateBounded<DenetimKaydi>(
        new BoundedChannelOptions(10_000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public void Ekle(DenetimOlayi olay, DenetimSonucu sonuc,
        string? kullaniciId = null, string? istemciId = null, string? ayrinti = null)
    {
        var istek = httpContextAccessor.HttpContext;
        var kayit = new DenetimKaydi
        {
            ZamanUtc = zaman.GetUtcNow().UtcDateTime,
            OlayTuru = olay,
            Sonuc = sonuc,
            KullaniciId = kullaniciId,
            IstemciId = istemciId,
            IpAdresi = IpAdresi(istek?.Connection.RemoteIpAddress),
            KisaUserAgent = Kisalt(istek?.Request.Headers.UserAgent.ToString(), UserAgentUzunlugu),
            Ayrinti = Kisalt(ayrinti, AyrintiUzunlugu),
        };

        if (!_kanal.Writer.TryWrite(kayit))
        {
            logger.LogWarning("Denetim kuyruğu dolu; kayıt yazılmadı ({Olay}).", olay);
        }
    }

    /// <summary>
    /// Var olmayan bir kullanıcı adıyla yapılan denemede yazılan metin saklanmaz (kullanıcılar bazen şifrelerini bu alana
    /// yazar). Normalize edilmiş hâlinin anahtarlı özeti ("h1:" + HMAC-SHA256, <see cref="AnahtarliOzet"/>) saklanır;
    /// aynı hedefe yapılan denemeler bununla gruplanabilir. Bu sürümden önceki kayıtlarda alan
    /// "kullanici_adi_sha256=" (anahtarsız SHA-256) olarak kalır.
    /// </summary>
    public string KullaniciAdiOzeti(string normalizeAd) =>
        "kullanici_adi_ozeti=" + ozet.Hesapla(AnahtarliOzet.KullaniciAdiAmaci, normalizeAd);

    internal ChannelReader<DenetimKaydi> Okuyucu => _kanal.Reader;

    private static string? IpAdresi(IPAddress? adres) =>
        adres is { IsIPv4MappedToIPv6: true } ? adres.MapToIPv4().ToString() : adres?.ToString();

    private static string? Kisalt(string? metin, int uzunluk) =>
        string.IsNullOrEmpty(metin) ? null : metin.Length <= uzunluk ? metin : metin[..uzunluk];
}

/// <summary>
/// Kuyruktaki denetim kayıtlarını toplu olarak veritabanına yazar. Hata loglanır, istek etkilenmez. Kapanışta kuyrukta
/// kalan kayıtlar da yazılır.
/// </summary>
public sealed class DenetimYazmaHizmeti(
    DenetimKaydedici kaydedici,
    IServiceScopeFactory scopeFactory,
    ILogger<DenetimYazmaHizmeti> logger) : BackgroundService
{
    private const int TopluYazim = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await kaydedici.Okuyucu.WaitToReadAsync(stoppingToken))
            {
                await YazAsync();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            while (await YazAsync())
            {
            }
        }
    }

    /// <summary>Kuyruktan en fazla <see cref="TopluYazim"/> kayıt yazar; kuyruk boşsa false döner.</summary>
    private async Task<bool> YazAsync()
    {
        var kayitlar = new List<DenetimKaydi>();
        while (kayitlar.Count < TopluYazim && kaydedici.Okuyucu.TryRead(out var kayit))
        {
            kayitlar.Add(kayit);
        }

        if (kayitlar.Count == 0)
        {
            return false;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DenetimKayitlari.AddRange(kayitlar);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Denetim kaydı yazılamadı ({Adet} kayıt).", kayitlar.Count);
        }

        return true;
    }
}
