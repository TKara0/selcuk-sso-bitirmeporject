namespace Selcuk.Identity.Eposta;

/// <summary>Düz metin e-posta. Gövde şifre sıfırlama bağlantısı içerebilir; alıcı ve gövde loglanmaz.</summary>
public sealed record EpostaMesaji(string Alici, string Konu, string Govde);

/// <summary>E-postayı teslim eder. Development'ta dosyaya yazar, diğer ortamlarda SMTP ile gönderir.</summary>
public interface IEpostaGonderici
{
    Task GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken);
}
