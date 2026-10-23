using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Selcuk.Identity.Data;

/// <summary>
/// Merkezi kimlik sunucusundaki kullanıcı. Öğrencilerde UserName öğrenci numarasıdır,
/// akademik ve idari personelde kurumsal kullanıcı adıdır.
/// </summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(150)]
    public string AdSoyad { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? Fakulte { get; set; }

    [MaxLength(150)]
    public string? Bolum { get; set; }
}
