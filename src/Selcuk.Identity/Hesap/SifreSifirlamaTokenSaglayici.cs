using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Şifre sıfırlama için Identity'nin yerleşik <see cref="DataProtectorTokenProvider{TUser}"/>'ı; yalnızca adı (veri
/// koruma amacı) ve ömrü ayrıdır. Token kullanıcının güvenlik damgasını içerir: şifre sıfırlanınca veya değiştirilince
/// damga yenilenir ve token geçersizleşir, bu yüzden tek kullanımlıktır.
/// </summary>
public sealed class SifreSifirlamaTokenSaglayici(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SifreSifirlamaTokenAyarlari> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider, options, logger)
{
    public const string Ad = "SifreSifirlama";
}

public sealed class SifreSifirlamaTokenAyarlari : DataProtectionTokenProviderOptions
{
    public SifreSifirlamaTokenAyarlari()
    {
        Name = SifreSifirlamaTokenSaglayici.Ad;
        TokenLifespan = TimeSpan.FromMinutes(30);
    }
}
