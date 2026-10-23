using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Back-Channel Logout ile iptal edilen oturumları (sid) ve kullanılmış logout_token kimliklerini (jti)
/// bellek içinde, süreli olarak tutar. Uygulama yeniden başlarsa kayıtlar silinir (demo için yeterli).
/// </summary>
public sealed class OturumIptalDeposu(
    IMemoryCache cache,
    TimeProvider zaman,
    IOptionsMonitor<CookieAuthenticationOptions> cerezAyarlari)
{
    private readonly Lock _jtiKilidi = new();

    /// <summary>jti daha önce görülmediyse kaydeder ve true döner; tekrar oynatmada false.</summary>
    public bool JtiIlkKezMi(string jti, DateTimeOffset saklamaBitisi)
    {
        var anahtar = "bcl:jti:" + jti;
        lock (_jtiKilidi)
        {
            if (cache.TryGetValue(anahtar, out _))
            {
                return false;
            }

            cache.Set(anahtar, true, saklamaBitisi);
            return true;
        }
    }

    public void SidIptalEt(string sid) => cache.Set("bcl:sid:" + sid, true, Saklama);

    /// <summary>sid'siz logout_token: kullanıcının bu ana kadar açılmış tüm oturumları geçersiz.</summary>
    public void SubIptalEt(string sub) => cache.Set("bcl:sub:" + sub, zaman.GetUtcNow(), Saklama);

    public bool IptalEdildiMi(ClaimsPrincipal principal, DateTimeOffset? oturumBaslangici)
    {
        var sid = principal.FindFirst("sid")?.Value;
        if (sid is not null && cache.TryGetValue("bcl:sid:" + sid, out _))
        {
            return true;
        }

        var sub = principal.FindFirst("sub")?.Value;
        return sub is not null
            && cache.TryGetValue("bcl:sub:" + sub, out DateTimeOffset iptalZamani)
            && (oturumBaslangici is null || oturumBaslangici <= iptalZamani);
    }

    // İptal kaydı, çerezin geçerli kalabileceği süre boyunca tutulur.
    private TimeSpan Saklama => cerezAyarlari.Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan;

    internal static async Task CerezDogrulaAsync(CookieValidatePrincipalContext context)
    {
        var depo = context.HttpContext.RequestServices.GetRequiredService<OturumIptalDeposu>();
        if (context.Principal is not null && depo.IptalEdildiMi(context.Principal, context.Properties.IssuedUtc))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
