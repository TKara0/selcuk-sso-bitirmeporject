using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Selcuk.Identity.Eposta;

/// <summary>
/// Development dışındaki ortamlar: "Eposta:Smtp" ayarlarıyla (host, port, kullanıcı, şifre, SSL) SMTP üzerinden
/// gönderir. Ayar eksikse gönderim anında hata verir; hata arka plan gönderim hizmetinde loglanır.
/// </summary>
public sealed class SmtpEpostaGonderici(IOptions<EpostaAyarlari> ayarlar) : IEpostaGonderici
{
    public async Task GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken)
    {
        var a = ayarlar.Value;
        if (string.IsNullOrWhiteSpace(a.Smtp.Host))
        {
            throw new InvalidOperationException("'Eposta:Smtp:Host' ayarı tanımlı değil.");
        }

        using var mail = a.MailMessageOlustur(mesaj);
        using var smtp = new SmtpClient(a.Smtp.Host, a.Smtp.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = a.Smtp.SslKullan,
        };
        if (!string.IsNullOrEmpty(a.Smtp.Kullanici))
        {
            smtp.Credentials = new NetworkCredential(a.Smtp.Kullanici, a.Smtp.Sifre);
        }

        await smtp.SendMailAsync(mail, cancellationToken);
    }
}
