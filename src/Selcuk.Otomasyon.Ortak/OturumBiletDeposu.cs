using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Selcuk.Istemci.Ortak;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>
/// Oturum biletinin (claim'ler ve SaveTokens ile access/refresh/id token) sunucu tarafında saklanması
/// (CookieAuthenticationOptions.SessionStore). Çerezde yalnızca küçük bir anahtar kalır. Gerekçe: geliştirmede bütün
/// uygulamalar localhost'ta çalışır ve tarayıcı çerezleri porta göre ayırmaz; her uygulamanın token'lı çerezi
/// (3-4 parça) diğerlerine de gönderilir ve birkaç uygulamadan sonra istek başlıkları Kestrel'in sınırını (32 KB)
/// aşar (HTTP 431). Sınır yükseltilmez; bilet bu uygulamanın Data Protection anahtarıyla şifrelenip IDistributedCache'e
/// yazılır (varsayılan bellek içi; birden çok örnekte Redis/SQL Server, OturumIptalDeposu ile aynı).
/// Kaydın süresi biletin (çerezin) bitişiyle aynıdır, kayan sürede her yenilemede uzar; bitişi geçmiş bir bilet
/// okunursa kayıt silinir. Çıkışta çerez şeması kaydı siler (RemoveAsync); Back-Channel Logout'ta iptal edilen
/// oturumun (sid) veya kullanıcının (sub) kayıtları hemen silinir (<see cref="IOturumIptalDinleyicisi"/>). Uygulama
/// yeniden başlarsa kullanıcı tek girişle sessizce yeniden girer.
/// </summary>
public sealed class OturumBiletDeposu(
    IDistributedCache onbellek,
    IDataProtectionProvider dataProtection,
    TimeProvider zaman,
    IServiceProvider servisler) : ITicketStore, IOturumIptalDinleyicisi
{
    private const string Onek = "otomasyon-oturum:";
    private const string SidOneki = "otomasyon-oturum-sid:";
    private const string SubOneki = "otomasyon-oturum-sub:";

    private readonly IDataProtector _koruyucu = dataProtection.CreateProtector("Selcuk.Otomasyon.Ortak.OturumBiletDeposu");

    // Aynı örnekteki eşzamanlı girişlerde sid/sub dizinine ekleme tek adımdır.
    private readonly SemaphoreSlim _dizinKilidi = new(1, 1);

    /// <summary>Çerezin ömrü (Kimlik:OturumSuresi). Seçenekler bu depoyu kurarken okunamaz; ilk kullanımda okunur.</summary>
    private TimeSpan CerezOmru => servisler.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
        .Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan;

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var anahtar = Guid.NewGuid().ToString("N");
        await RenewAsync(anahtar, ticket);
        return anahtar;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var kalan = (ticket.Properties.ExpiresUtc ?? zaman.GetUtcNow() + CerezOmru) - zaman.GetUtcNow();
        if (kalan <= TimeSpan.Zero)
        {
            await onbellek.RemoveAsync(Onek + key);
            return;
        }

        // Göreli süre: önbelleğin kendi saatiyle uygulamanın TimeProvider'ı farklı olsa da kayıt doğru süre yaşar.
        await onbellek.SetAsync(Onek + key, _koruyucu.Protect(TicketSerializer.Default.Serialize(ticket)),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = kalan });
        await DizineEkleAsync(ticket.Principal.FindFirst("sid")?.Value, SidOneki, key, kalan);
        await DizineEkleAsync(ticket.Principal.FindFirst("sub")?.Value, SubOneki, key, kalan);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var veri = await onbellek.GetAsync(Onek + key);
        if (veri is null)
        {
            return null;
        }

        AuthenticationTicket? bilet;
        try
        {
            bilet = TicketSerializer.Default.Deserialize(_koruyucu.Unprotect(veri));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Anahtar değişmiş (ör. yeniden kurulum): bilet yok sayılır, kullanıcı yeniden giriş yapar.
            return null;
        }

        if (bilet?.Properties.ExpiresUtc is { } bitis && bitis <= zaman.GetUtcNow())
        {
            await onbellek.RemoveAsync(Onek + key);
            return null;
        }

        return bilet;
    }

    public Task RemoveAsync(string key) => onbellek.RemoveAsync(Onek + key);

    public Task SidIptalEdildiAsync(string sid, CancellationToken cancellationToken) =>
        DizindekileriSilAsync(SidOneki + sid, cancellationToken);

    public Task SubIptalEdildiAsync(string sub, CancellationToken cancellationToken) =>
        DizindekileriSilAsync(SubOneki + sub, cancellationToken);

    private async Task DizineEkleAsync(string? deger, string onek, string key, TimeSpan kalan)
    {
        if (string.IsNullOrEmpty(deger))
        {
            return;
        }

        await _dizinKilidi.WaitAsync();
        try
        {
            var anahtarlar = await DizinAsync(onek + deger, CancellationToken.None);
            anahtarlar.Add(key);
            // Dizin, içindeki en geç biten bilet kadar yaşar (bilet yenilendikçe dizin de yenilenir).
            await onbellek.SetAsync(onek + deger, Encoding.UTF8.GetBytes(string.Join('\n', anahtarlar)),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = kalan > CerezOmru ? kalan : CerezOmru });
        }
        finally
        {
            _dizinKilidi.Release();
        }
    }

    private async Task DizindekileriSilAsync(string dizin, CancellationToken cancellationToken)
    {
        await _dizinKilidi.WaitAsync(cancellationToken);
        try
        {
            foreach (var anahtar in await DizinAsync(dizin, cancellationToken))
            {
                await onbellek.RemoveAsync(Onek + anahtar, cancellationToken);
            }

            await onbellek.RemoveAsync(dizin, cancellationToken);
        }
        finally
        {
            _dizinKilidi.Release();
        }
    }

    private async Task<HashSet<string>> DizinAsync(string dizin, CancellationToken cancellationToken) =>
        await onbellek.GetAsync(dizin, cancellationToken) is { } veri
            ? [.. Encoding.UTF8.GetString(veri).Split('\n', StringSplitOptions.RemoveEmptyEntries)]
            : [];

    /// <summary>Çerez şemasının oturum deposu bu sınıf olur (AddSelcukOtomasyon).</summary>
    internal static void Kaydet(IServiceCollection services)
    {
        services.AddSingleton<OturumBiletDeposu>();
        services.AddSingleton<IOturumIptalDinleyicisi>(sp => sp.GetRequiredService<OturumBiletDeposu>());
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<OturumBiletDeposu>((ayarlar, depo) => ayarlar.SessionStore = depo);
    }
}
