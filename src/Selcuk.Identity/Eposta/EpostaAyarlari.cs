using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace Selcuk.Identity.Eposta;

/// <summary>
/// "Eposta" ayar bölümü. SMTP şifresi appsettings'e yazılmaz: Development'ta user-secrets
/// (<c>Eposta:Smtp:Sifre</c>), diğer ortamlarda ortam değişkeni (<c>Eposta__Smtp__Sifre</c>) veya sır deposu.
/// </summary>
public sealed class EpostaAyarlari
{
    public const string Bolum = "Eposta";

    public string Gonderen { get; set; } = "tek-sifre@test.local";

    public string GonderenAdi { get; set; } = EpostaSablonlari.Baslik;

    public SmtpAyarlari Smtp { get; set; } = new();

    public sealed class SmtpAyarlari
    {
        public string? Host { get; set; }

        public int Port { get; set; } = 587;

        public string? Kullanici { get; set; }

        public string? Sifre { get; set; }

        public bool SslKullan { get; set; } = true;
    }

    internal MailMessage MailMessageOlustur(EpostaMesaji mesaj)
    {
        var mail = new MailMessage(new MailAddress(Gonderen, GonderenAdi, Encoding.UTF8), new MailAddress(mesaj.Alici))
        {
            Subject = mesaj.Konu,
            SubjectEncoding = Encoding.UTF8,
            Body = mesaj.Govde,
            BodyEncoding = Encoding.UTF8,
            // .eml dosyası metin düzenleyicide açıldığında da okunabilsin ve bağlantı satır kırılmadan kopyalanabilsin.
            BodyTransferEncoding = TransferEncoding.EightBit,
            IsBodyHtml = false,
        };

        // HTML sürümü düz metnin alternatifi (multipart/alternative); istemci destekliyorsa onu gösterir.
        if (mesaj.HtmlGovde is not null)
        {
            var html = AlternateView.CreateAlternateViewFromString(mesaj.HtmlGovde, Encoding.UTF8, MediaTypeNames.Text.Html);
            html.TransferEncoding = TransferEncoding.QuotedPrintable;
            mail.AlternateViews.Add(html);
        }

        return mail;
    }
}
