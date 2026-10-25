using OpenIddict.Abstractions;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// OpenIddict tablolarının budanması (prune): açılışta ve ardından TokenBudama:Aralik (varsayılan 1 gün) aralıkla,
/// süresi dolmuş, iptal edilmiş veya kullanılmış token'ları; geçersiz yetkilendirmeleri ve token'ı kalmamış ad-hoc
/// (yetim) yetkilendirmeleri siler. Silme OpenIddict'in kendi PruneAsync işlevidir (Quartz entegrasyonunun yaptığı iş);
/// yalnızca TokenBudama:EnAzYas (varsayılan 14 gün) süresinden önce oluşturulmuş kayıtlar silinir. Bu süre mutlak oturum
/// süresinden (<see cref="YenilemeUstSiniri"/>) kısa verilse de mutlak süre kullanılır: yaşayan bir zincirin kullanılmış
/// refresh token'ı silinirse tekrar kullanım tespiti (zincirin iptali) çalışmaz. Hata loglanır, sonraki turda denenir.
/// Kalıcı onaylar (permanent, geçerli) silinmez.
/// </summary>
public sealed class TokenBudamaHizmeti(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider zaman,
    ILogger<TokenBudamaHizmeti> logger) : BackgroundService
{
    public const string AralikAyari = "TokenBudama:Aralik";
    public const string EnAzYasAyari = "TokenBudama:EnAzYas";
    public static readonly TimeSpan VarsayilanAralik = TimeSpan.FromDays(1);
    public static readonly TimeSpan VarsayilanEnAzYas = TimeSpan.FromDays(14);

    public TimeSpan Aralik
    {
        get
        {
            var aralik = configuration.GetValue<TimeSpan?>(AralikAyari) ?? VarsayilanAralik;
            return aralik >= TimeSpan.FromMinutes(1) ? aralik : TimeSpan.FromMinutes(1);
        }
    }

    public TimeSpan EnAzYas
    {
        get
        {
            var yas = configuration.GetValue<TimeSpan?>(EnAzYasAyari) ?? VarsayilanEnAzYas;
            var mutlak = YenilemeUstSiniri.MutlakSureOku(configuration);
            return yas > mutlak ? yas : mutlak;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var zamanlayici = new PeriodicTimer(Aralik, zaman);
        do
        {
            try
            {
                await BudaAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Token ve yetkilendirme budaması başarısız.");
            }
        }
        while (await zamanlayici.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Budamayı bir kez çalıştırır ve silinen token ve yetkilendirme sayılarını döndürür.</summary>
    public async Task<(long Token, long Yetkilendirme)> BudaAsync(CancellationToken cancellationToken = default)
    {
        var sinir = zaman.GetUtcNow() - EnAzYas;

        await using var scope = scopeFactory.CreateAsyncScope();
        // Önce token'lar: token'ı kalmayan ad-hoc yetkilendirmeler ancak ondan sonra yetim kalır (OpenIddict notu).
        var token = await scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>()
            .PruneAsync(sinir, cancellationToken);
        var yetkilendirme = await scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>()
            .PruneAsync(sinir, cancellationToken);

        if (token > 0 || yetkilendirme > 0)
        {
            logger.LogInformation("Budama: {Token} token ve {Yetkilendirme} yetkilendirme silindi.", token, yetkilendirme);
        }

        return (token, yetkilendirme);
    }
}
