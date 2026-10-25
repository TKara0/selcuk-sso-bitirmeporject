using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Kimlik sunucusu oturumuna (sid) bağlı yetkilendirmelerin ve token'ların (refresh token'lar dahil) iptali.
/// Authorize her kod verişinde, kalıcı onay kaydından ayrı, o oturuma ait bir ad-hoc yetkilendirme açar ve sid'i
/// özelliğine yazar (<see cref="OturumYetkisiOlusturAsync"/>); kod ve ondan türeyen tüm token'lar ona bağlanır. Böylece
/// çıkışta yalnızca o oturumun, şifre/2FA değişikliğinde mevcut oturum dışındakilerin token'ları iptal edilebilir;
/// kullanıcının uygulamaya verdiği kalıcı onay (onay ekranının tekrar sorulmaması) etkilenmez.
/// </summary>
public sealed class TokenIptali(
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictTokenManager tokenManager,
    TimeProvider zaman,
    ILogger<TokenIptali> logger)
{
    public const string SidOzelligi = "sid";

    /// <summary>Bu oturum (sid) ve istemci için ad-hoc yetkilendirme oluşturur.</summary>
    public async Task<object> OturumYetkisiOlusturAsync(string kullaniciId, string uygulamaId, string? sid,
        IEnumerable<string> kapsamlar, CancellationToken cancellationToken = default)
    {
        var tanim = new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = uygulamaId,
            CreationDate = zaman.GetUtcNow(),
            Status = Statuses.Valid,
            Subject = kullaniciId,
            Type = AuthorizationTypes.AdHoc,
        };
        tanim.Scopes.UnionWith(kapsamlar);
        if (sid is not null)
        {
            tanim.Properties[SidOzelligi] = JsonSerializer.SerializeToElement(sid);
        }

        return await authorizationManager.CreateAsync(tanim, cancellationToken);
    }

    /// <summary>Kimlik sunucusundan çıkış: yalnızca o oturumun yetkilendirmeleri ve token'ları.</summary>
    public Task<int> OturumuIptalEtAsync(string kullaniciId, string sid, CancellationToken cancellationToken = default) =>
        IptalEtAsync(kullaniciId, s => s == sid, cancellationToken);

    /// <summary>Şifre ve 2FA değişiklikleri: kullanıcının tüm oturumları, haricSid verilirse o oturum dışındakiler.</summary>
    public Task<int> KullaniciyiIptalEtAsync(string kullaniciId, string? haricSid, CancellationToken cancellationToken = default) =>
        IptalEtAsync(kullaniciId, s => haricSid is null || s != haricSid, cancellationToken);

    private async Task<int> IptalEtAsync(string kullaniciId, Func<string?, bool> secilsin, CancellationToken cancellationToken)
    {
        // Önce listelenir, sonra iptal edilir (aynı DbContext'te açık okuyucu varken yazılmasın).
        var yetkiler = await authorizationManager.FindBySubjectAsync(kullaniciId, cancellationToken).ToListAsync(cancellationToken);
        var sayi = 0;
        foreach (var yetki in yetkiler)
        {
            if (!await authorizationManager.HasTypeAsync(yetki, AuthorizationTypes.AdHoc, cancellationToken)
                || !await authorizationManager.HasStatusAsync(yetki, Statuses.Valid, cancellationToken))
            {
                continue;
            }

            var ozellikler = await authorizationManager.GetPropertiesAsync(yetki, cancellationToken);
            var sid = ozellikler.TryGetValue(SidOzelligi, out var deger) && deger.ValueKind == JsonValueKind.String
                ? deger.GetString()
                : null;
            if (!secilsin(sid))
            {
                continue;
            }

            // Token'lar (kod, access, refresh) ve yetkilendirme iptal edilir; OpenIddict iptal edilmiş yetkilendirmeye
            // bağlı token'ları da reddeder.
            await tokenManager.RevokeByAuthorizationIdAsync((await authorizationManager.GetIdAsync(yetki, cancellationToken))!,
                cancellationToken);
            await authorizationManager.TryRevokeAsync(yetki, cancellationToken);
            sayi++;
        }

        if (sayi > 0)
        {
            logger.LogInformation("{Sayi} oturum yetkilendirmesi ve token'ları iptal edildi.", sayi);
        }

        return sayi;
    }
}
