using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Selcuk.Identity.Eposta;

/// <summary>
/// Development: e-postaları göndermeden <c>App_Data/eposta/</c> klasörüne .eml dosyası olarak yazar (SmtpClient'ın
/// "pickup directory" teslimi, ağ bağlantısı yok). Klasör .gitignore'dadır; dosyalar sıfırlama bağlantısı içerir.
/// </summary>
public sealed class DosyaEpostaGonderici(
    IOptions<EpostaAyarlari> ayarlar,
    IHostEnvironment ortam,
    ILogger<DosyaEpostaGonderici> logger) : IEpostaGonderici
{
    public string Klasor { get; } = Path.Combine(ortam.ContentRootPath, "App_Data", "eposta");

    public async Task GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Klasor);

        using var mail = ayarlar.Value.MailMessageOlustur(mesaj);
        using var smtp = new SmtpClient
        {
            DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
            PickupDirectoryLocation = Klasor,
            DeliveryFormat = SmtpDeliveryFormat.International,
        };
        await smtp.SendMailAsync(mail, cancellationToken);

        logger.LogInformation("E-posta dosyaya yazıldı ({Konu}): {Klasor}", mesaj.Konu, Klasor);
    }
}
