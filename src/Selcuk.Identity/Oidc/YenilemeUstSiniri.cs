using System.Security.Claims;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Refresh token'ın mutlak üst sınırı. Refresh token kayan süreli kalır (her yenilemede Tokenlar:RefreshTokenSuresi
/// kadar yeni süre), ama bir yetkilendirme zinciri ilk girişten (authorize'ın açtığı ad-hoc yetkilendirmenin oluşturulma
/// zamanı, <see cref="TokenIptali.OturumYetkisiOlusturAsync"/>) itibaren Tokenlar:MutlakOturumSuresi'ni (varsayılan 12
/// saat) aşamaz. OpenIddict'te ikisini birlikte sağlayan bir ayar yok (DisableSlidingRefreshTokenExpiration kayan
/// süreyi tamamen kapatır); bu yüzden token uç noktası her istekte kalan süreyi hesaplar: süre dolduysa invalid_grant,
/// dolmadıysa verilen refresh ve access token'ın ömrü kalan süreyle kısaltılır (principal'a özel token ömrü).
/// </summary>
public sealed class YenilemeUstSiniri(
    IOpenIddictAuthorizationManager authorizationManager,
    IOptionsMonitor<OpenIddictServerOptions> sunucuAyarlari,
    IConfiguration configuration,
    TimeProvider zaman)
{
    public const string AyarAdi = "Tokenlar:MutlakOturumSuresi";
    public static readonly TimeSpan VarsayilanMutlakSure = TimeSpan.FromHours(12);

    public static TimeSpan MutlakSureOku(IConfiguration configuration) =>
        configuration.GetValue<TimeSpan?>(AyarAdi) ?? VarsayilanMutlakSure;

    public TimeSpan MutlakSure => MutlakSureOku(configuration);

    /// <summary>
    /// Kod veya refresh token'daki yetkilendirmenin kalan ömrü. Yetkilendirme bulunamazsa veya mutlak süre dolduysa
    /// null döner (token verilmez).
    /// </summary>
    public async Task<TimeSpan?> KalanSureAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.GetAuthorizationId() is not { Length: > 0 } yetkiId
            || await authorizationManager.FindByIdAsync(yetkiId, cancellationToken) is not { } yetki
            || await authorizationManager.GetCreationDateAsync(yetki, cancellationToken) is not { } olusturma)
        {
            return null;
        }

        var kalan = olusturma + MutlakSure - zaman.GetUtcNow();
        return kalan > TimeSpan.Zero ? kalan : null;
    }

    /// <summary>Verilecek refresh ve access token'ın ömrünü kalan süreyle sınırlar.</summary>
    public void OmurleriSinirla(ClaimsIdentity identity, TimeSpan kalan)
    {
        var ayarlar = sunucuAyarlari.CurrentValue;
        identity.SetRefreshTokenLifetime(Kisa(ayarlar.RefreshTokenLifetime, kalan));
        identity.SetAccessTokenLifetime(Kisa(ayarlar.AccessTokenLifetime, kalan));

        static TimeSpan Kisa(TimeSpan? sure, TimeSpan kalan) => sure is { } s && s < kalan ? s : kalan;
    }
}
