using System.Threading.Channels;

namespace Selcuk.Identity.Eposta;

/// <summary>
/// Sayfalar e-postayı kuyruğa bırakır, gönderim <see cref="EpostaGonderimHizmeti"/> ile arka planda yapılır. Böylece
/// "Şifremi unuttum" yanıtının süresi e-posta gönderiminden (SMTP gecikmesi) etkilenmez ve var olan hesapla olmayan
/// hesap yanıt süresinden ayırt edilemez.
/// </summary>
public sealed class EpostaKuyrugu(ILogger<EpostaKuyrugu> logger)
{
    private readonly Channel<EpostaMesaji> _kanal = Channel.CreateBounded<EpostaMesaji>(
        new BoundedChannelOptions(1000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public void Ekle(EpostaMesaji mesaj)
    {
        if (!_kanal.Writer.TryWrite(mesaj))
        {
            logger.LogWarning("E-posta kuyruğu dolu; e-posta gönderilmedi ({Konu}).", mesaj.Konu);
        }
    }

    internal ChannelReader<EpostaMesaji> Okuyucu => _kanal.Reader;
}

/// <summary>Kuyruktaki e-postaları sırayla gönderir. Bir gönderimin hatası diğerlerini durdurmaz.</summary>
public sealed class EpostaGonderimHizmeti(
    EpostaKuyrugu kuyruk,
    IEpostaGonderici gonderici,
    ILogger<EpostaGonderimHizmeti> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mesaj in kuyruk.Okuyucu.ReadAllAsync(stoppingToken))
        {
            try
            {
                await gonderici.GonderAsync(mesaj, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Alıcı ve gövde (sıfırlama bağlantısı) loglanmaz; yalnızca konu ve hata türü.
                logger.LogWarning("E-posta gönderilemedi ({Konu}): {Hata}", mesaj.Konu, ex.GetType().Name);
            }
        }
    }
}
