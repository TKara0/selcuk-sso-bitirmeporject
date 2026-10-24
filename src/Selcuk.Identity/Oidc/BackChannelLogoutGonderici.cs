using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// OpenID Connect Back-Channel Logout 1.0 göndericisi. Bir oturum (sid) kapandığında, o oturumda token
/// verilmiş her istemcinin backchannel_logout_uri adresine imzalı bir logout_token POST eder.
/// İmzalama, OpenIddict'in tokenları imzaladığı kimlik bilgisiyle yapılır.
/// </summary>
public sealed class BackChannelLogoutGonderici(
    ApplicationDbContext db,
    IOpenIddictApplicationManager applicationManager,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenIddictServerOptions> serverAyarlari,
    TimeProvider zaman,
    ILogger<BackChannelLogoutGonderici> logger)
{
    public const string HttpIstemciAdi = "backchannel-logout";
    public const string OlayAdi = "http://schemas.openid.net/event/backchannel-logout";
    private static readonly TimeSpan TokenOmru = TimeSpan.FromMinutes(2);

    public async Task OturumuKapatAsync(string sid, CancellationToken cancellationToken = default)
    {
        var kayitlar = await db.IstemciOturumlari
            .Where(o => o.Sid == sid && o.KapanmaZamani == null)
            .ToListAsync(cancellationToken);
        if (kayitlar.Count == 0)
        {
            return;
        }

        // Adresler sırayla okunur (DbContext eşzamanlı sorguya izin vermez); bildirimler paralel gönderilir.
        var bildirimler = new List<(IstemciOturumu Kayit, Uri Adres)>();
        foreach (var kayit in kayitlar)
        {
            if (await BackChannelAdresiAsync(kayit.ClientId, cancellationToken) is { } adres)
            {
                bildirimler.Add((kayit, adres));
            }
        }

        // Bir istemcinin hatası diğerlerini engellemez: her gönderim kendi hatasını yakalar.
        await Task.WhenAll(bildirimler.Select(b => BildirAsync(b.Kayit, b.Adres, cancellationToken)));

        var simdi = zaman.GetUtcNow();
        foreach (var kayit in kayitlar)
        {
            kayit.KapanmaZamani = simdi;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Kullanıcının açık tüm oturumlarını (haricSid verilirse o oturum dışında) kapatır ve her birindeki istemcilere
    /// bildirim gönderir. Şifre sıfırlama ve şifre değiştirmede kullanılır.
    /// </summary>
    public async Task KullaniciOturumlariniKapatAsync(string kullaniciId, string? haricSid,
        CancellationToken cancellationToken = default)
    {
        var sidler = await db.IstemciOturumlari
            .Where(o => o.KullaniciId == kullaniciId && o.KapanmaZamani == null && o.Sid != haricSid)
            .Select(o => o.Sid)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var sid in sidler)
        {
            await OturumuKapatAsync(sid, cancellationToken);
        }
    }

    private async Task<Uri?> BackChannelAdresiAsync(string clientId, CancellationToken cancellationToken)
    {
        var uygulama = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (uygulama is null)
        {
            return null;
        }

        var ozellikler = await applicationManager.GetPropertiesAsync(uygulama, cancellationToken);
        return ozellikler.TryGetValue(Istemciler.BackChannelLogoutUriOzelligi, out var deger)
            && Uri.TryCreate(deger.GetString(), UriKind.Absolute, out var adres)
            && adres.Scheme == Uri.UriSchemeHttps
                ? adres
                : null;
    }

    private async Task BildirAsync(IstemciOturumu kayit, Uri adres, CancellationToken cancellationToken)
    {
        try
        {
            using var icerik = new FormUrlEncodedContent([KeyValuePair.Create("logout_token", LogoutTokenOlustur(kayit))]);
            using var yanit = await httpClientFactory.CreateClient(HttpIstemciAdi)
                .PostAsync(adres, icerik, cancellationToken);

            if (yanit.IsSuccessStatusCode)
            {
                logger.LogInformation("Back-channel logout gönderildi: {ClientId}", kayit.ClientId);
            }
            else
            {
                logger.LogWarning("Back-channel logout {ClientId} istemcisinde başarısız: HTTP {Durum}",
                    kayit.ClientId, (int)yanit.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Token içeriği loglanmaz; yalnızca istemci ve hata türü.
            logger.LogWarning("Back-channel logout {ClientId} istemcisine ulaşamadı: {Hata}",
                kayit.ClientId, ex.GetType().Name);
        }
    }

    private string LogoutTokenOlustur(IstemciOturumu kayit)
    {
        var ayarlar = serverAyarlari.CurrentValue;
        var simdi = zaman.GetUtcNow().UtcDateTime;

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            TokenType = "logout+jwt",
            Issuer = ayarlar.Issuer!.AbsoluteUri,
            Audience = kayit.ClientId,
            IssuedAt = simdi,
            NotBefore = simdi,
            Expires = simdi + TokenOmru,
            SigningCredentials = ayarlar.SigningCredentials.First(),
            // nonce bilerek yok: spesifikasyon logout_token'da nonce'u yasaklar.
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [JwtRegisteredClaimNames.Sub] = kayit.KullaniciId,
                [OidcKimlik.SidClaim] = kayit.Sid,
                ["events"] = new Dictionary<string, object> { [OlayAdi] = new Dictionary<string, object>() },
            },
        });
    }
}
