using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>
/// Oturum biletinin (claim'ler ve SaveTokens ile access/refresh/id token) sunucu tarafında saklanması
/// (CookieAuthenticationOptions.SessionStore). Çerezde yalnızca küçük bir anahtar kalır. Gerekçe: geliştirmede bütün
/// uygulamalar localhost'ta çalışır ve tarayıcı çerezleri porta göre ayırmaz; her uygulamanın token'lı çerezi
/// (3-4 parça) diğerlerine de gönderilir ve birkaç uygulamadan sonra istek başlıkları Kestrel'in sınırını (32 KB)
/// aşar (HTTP 431). Sınır yükseltilmez; bilet bu uygulamanın Data Protection anahtarıyla şifrelenip IDistributedCache'e
/// yazılır (varsayılan bellek içi; birden çok örnekte Redis/SQL Server, OturumIptalDeposu ile aynı). Bilet, oturum
/// süresi (kayan) dolunca önbellekten düşer; uygulama yeniden başlarsa kullanıcı tek girişle sessizce yeniden girer.
/// </summary>
public sealed class OturumBiletDeposu(IDistributedCache onbellek, IDataProtectionProvider dataProtection, TimeProvider zaman)
    : ITicketStore
{
    private const string Onek = "otomasyon-oturum:";
    private static readonly TimeSpan VarsayilanSure = TimeSpan.FromHours(8);

    private readonly IDataProtector _koruyucu = dataProtection.CreateProtector("Selcuk.Otomasyon.Ortak.OturumBiletDeposu");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var anahtar = Guid.NewGuid().ToString("N");
        await RenewAsync(anahtar, ticket);
        return anahtar;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket) =>
        onbellek.SetAsync(Onek + key, _koruyucu.Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = ticket.Properties.ExpiresUtc ?? zaman.GetUtcNow() + VarsayilanSure,
            });

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var veri = await onbellek.GetAsync(Onek + key);
        if (veri is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_koruyucu.Unprotect(veri));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Anahtar değişmiş (ör. yeniden kurulum): bilet yok sayılır, kullanıcı yeniden giriş yapar.
            return null;
        }
    }

    public Task RemoveAsync(string key) => onbellek.RemoveAsync(Onek + key);

    /// <summary>Çerez şemasının oturum deposu bu sınıf olur (AddSelcukOtomasyon).</summary>
    internal static void Kaydet(IServiceCollection services)
    {
        services.AddSingleton<OturumBiletDeposu>();
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<OturumBiletDeposu>((ayarlar, depo) => ayarlar.SessionStore = depo);
    }
}
