using System.ComponentModel.DataAnnotations;

namespace Selcuk.Identity.Data;

/// <summary>
/// Bir kimlik sunucusu oturumunda (sid) hangi istemciye token verildiğinin kaydı. Kullanıcı çıkış yaptığında
/// bu kayıtlardaki her istemciye Back-Channel Logout bildirimi gönderilir ve kayıtlar kapatılır.
/// </summary>
public class IstemciOturumu
{
    public long Id { get; set; }

    [MaxLength(64)]
    public required string Sid { get; set; }

    [MaxLength(450)]
    public required string KullaniciId { get; set; }

    [MaxLength(100)]
    public required string ClientId { get; set; }

    public DateTimeOffset OlusturmaZamani { get; set; }

    /// <summary>Çıkışta doldurulur; null ise oturum açık.</summary>
    public DateTimeOffset? KapanmaZamani { get; set; }
}
