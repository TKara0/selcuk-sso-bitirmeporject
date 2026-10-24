using Microsoft.EntityFrameworkCore;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Denetim;

/// <summary>
/// KVKK saklama süresi: açılışta ve ardından günde bir kez, DenetimKaydiSaklamaGun (varsayılan 180) günden eski denetim
/// kayıtlarını siler. Hata loglanır, bir sonraki gün tekrar denenir.
/// </summary>
public sealed class DenetimTemizlemeHizmeti(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider zaman,
    ILogger<DenetimTemizlemeHizmeti> logger) : BackgroundService
{
    public const string AyarAdi = "DenetimKaydiSaklamaGun";
    public const int VarsayilanSaklamaGun = 180;

    public int SaklamaGun => Math.Max(1, configuration.GetValue<int?>(AyarAdi) ?? VarsayilanSaklamaGun);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var zamanlayici = new PeriodicTimer(TimeSpan.FromDays(1), zaman);
        do
        {
            try
            {
                await TemizleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Eski denetim kayıtları silinemedi.");
            }
        }
        while (await zamanlayici.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Saklama süresini aşan kayıtları siler ve silinen kayıt sayısını döndürür.</summary>
    public async Task<int> TemizleAsync(CancellationToken cancellationToken = default)
    {
        var gun = SaklamaGun;
        var sinir = zaman.GetUtcNow().UtcDateTime.AddDays(-gun);

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var silinen = await db.DenetimKayitlari.Where(k => k.ZamanUtc < sinir).ExecuteDeleteAsync(cancellationToken);

        if (silinen > 0)
        {
            logger.LogInformation("{Adet} denetim kaydı silindi ({Gun} günden eski).", silinen, gun);
        }

        return silinen;
    }
}
