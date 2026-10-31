using System.Buffers.Binary;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Back-Channel Logout ile iptal edilen oturumları (sid) ve kullanılmış logout_token kimliklerini (jti)
/// <see cref="IDistributedCache"/> üzerinde, süreli olarak tutar. Varsayılan önbellek bellek içidir; aynı uygulamanın
/// birden çok örneği çalışıyorsa Redis veya SQL Server dağıtık önbelleği kaydedilir (README).
/// Kayıtlar en az çerez süresi kadar saklanır: iptal edilen bir oturumun çerezi bu süreden önce geçerliliğini yitirmez.
/// İptalden sonra kayıtlı <see cref="IOturumIptalDinleyicisi"/>'ler çağrılır (ör. sunucu tarafındaki oturum biletini
/// hemen silmek için).
/// </summary>
public sealed class OturumIptalDeposu(
    IDistributedCache cache,
    TimeProvider zaman,
    IOptionsMonitor<CookieAuthenticationOptions> cerezAyarlari,
    IEnumerable<IOturumIptalDinleyicisi> dinleyiciler)
{
    private static readonly byte[] Isaret = [1];

    // Aynı örnekteki eşzamanlı isteklerde jti kontrolü ve kaydı tek adımdır. IDistributedCache'te atomik "yoksa ekle"
    // olmadığından birden çok örnekte aynı token'ın iki kez kabul edilmesi mümkündür; etkisi aynı oturumun ikinci kez
    // iptal edilmesidir.
    private readonly SemaphoreSlim _jtiKilidi = new(1, 1);

    /// <summary>jti daha önce görülmediyse kaydeder ve true döner; tekrar oynatmada false.</summary>
    public async Task<bool> JtiIlkKezMiAsync(string jti, DateTimeOffset tokenBitisi, CancellationToken cancellationToken = default)
    {
        var anahtar = "bcl:jti:" + jti;
        await _jtiKilidi.WaitAsync(cancellationToken);
        try
        {
            if (await cache.GetAsync(anahtar, cancellationToken) is not null)
            {
                return false;
            }

            var kalan = tokenBitisi - zaman.GetUtcNow();
            await cache.SetAsync(anahtar, Isaret, SureliKayit(kalan > Saklama ? kalan : Saklama), cancellationToken);
            return true;
        }
        finally
        {
            _jtiKilidi.Release();
        }
    }

    public async Task SidIptalEtAsync(string sid, CancellationToken cancellationToken = default)
    {
        await cache.SetAsync("bcl:sid:" + sid, Isaret, SureliKayit(Saklama), cancellationToken);
        foreach (var dinleyici in dinleyiciler)
        {
            await dinleyici.SidIptalEdildiAsync(sid, cancellationToken);
        }
    }

    /// <summary>sid'siz logout_token: kullanıcının bu ana kadar açılmış tüm oturumları geçersiz.</summary>
    public async Task SubIptalEtAsync(string sub, CancellationToken cancellationToken = default)
    {
        var deger = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(deger, zaman.GetUtcNow().UtcTicks);
        await cache.SetAsync("bcl:sub:" + sub, deger, SureliKayit(Saklama), cancellationToken);
        foreach (var dinleyici in dinleyiciler)
        {
            await dinleyici.SubIptalEdildiAsync(sub, cancellationToken);
        }
    }

    public async Task<bool> IptalEdildiMiAsync(ClaimsPrincipal principal, DateTimeOffset? oturumBaslangici,
        CancellationToken cancellationToken = default)
    {
        var sid = principal.FindFirst("sid")?.Value;
        if (sid is not null && await cache.GetAsync("bcl:sid:" + sid, cancellationToken) is not null)
        {
            return true;
        }

        var sub = principal.FindFirst("sub")?.Value;
        if (sub is null || await cache.GetAsync("bcl:sub:" + sub, cancellationToken) is not { Length: sizeof(long) } deger)
        {
            return false;
        }

        var iptalZamani = new DateTimeOffset(BinaryPrimitives.ReadInt64BigEndian(deger), TimeSpan.Zero);
        return oturumBaslangici is null || oturumBaslangici <= iptalZamani;
    }

    // Kayıt, iptal edilen oturumun çerezinin geçerli kalabileceği süre boyunca tutulur.
    private TimeSpan Saklama => cerezAyarlari.Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan;

    // Göreli süre: önbelleğin kendi saatiyle uygulamanın TimeProvider'ı farklı olsa da kayıt doğru süre yaşar.
    private static DistributedCacheEntryOptions SureliKayit(TimeSpan sure) =>
        new() { AbsoluteExpirationRelativeToNow = sure };

    internal static async Task CerezDogrulaAsync(CookieValidatePrincipalContext context)
    {
        var depo = context.HttpContext.RequestServices.GetRequiredService<OturumIptalDeposu>();
        if (context.Principal is not null
            && await depo.IptalEdildiMiAsync(context.Principal, context.Properties.IssuedUtc, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
