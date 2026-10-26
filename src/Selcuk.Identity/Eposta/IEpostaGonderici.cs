namespace Selcuk.Identity.Eposta;

/// <summary>
/// E-posta: düz metin gövde ve isteğe bağlı aynı içerikli HTML sürümü (multipart/alternative). Gövdeler şifre sıfırlama
/// bağlantısı içerebilir; alıcı ve gövdeler loglanmaz.
/// </summary>
public sealed record EpostaMesaji(string Alici, string Konu, string Govde, string? HtmlGovde = null);

/// <summary>E-postayı teslim eder. Development'ta dosyaya yazar, diğer ortamlarda SMTP ile gönderir.</summary>
public interface IEpostaGonderici
{
    Task GonderAsync(EpostaMesaji mesaj, CancellationToken cancellationToken);
}
