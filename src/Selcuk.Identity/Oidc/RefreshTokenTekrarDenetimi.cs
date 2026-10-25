using OpenIddict.Abstractions;
using OpenIddict.Server;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Kullanılmış (redeemed) bir refresh token tekrar gelirse denetim kaydına "Refresh token tekrar kullanıldı" yazar.
/// Reddetme ve iptal OpenIddict'tedir: kayan refresh token'larda kullanılan token "redeemed" işaretlenir; tekrar
/// gelirse OpenIddict'in ValidateTokenEntry işleyicisi isteği invalid_grant ile reddeder ve aynı yetkilendirmeye bağlı
/// tüm token'ları iptal eder (yeniden kullanım sızıntı süresi kapalı: SetRefreshTokenReuseLeeway(null)). Bu işleyici
/// ondan hemen önce çalışır, yalnızca kayıt yazar; token değeri yazılmaz.
/// </summary>
public sealed class RefreshTokenTekrarDenetimi(
    IOpenIddictTokenManager tokenManager,
    DenetimKaydedici denetim) : IOpenIddictServerHandler<ValidateTokenContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenContext>()
            .UseScopedHandler<RefreshTokenTekrarDenetimi>()
            .SetOrder(OpenIddictServerHandlers.Protection.ValidateTokenEntry.Descriptor.Order - 1)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        if (context.Principal is not { } principal
            || !principal.HasTokenType(TokenTypeIdentifiers.RefreshToken)
            || principal.GetTokenId() is not { Length: > 0 } tokenId
            || await tokenManager.FindByIdAsync(tokenId) is not { } token
            || !await tokenManager.HasStatusAsync(token, Statuses.Redeemed))
        {
            return;
        }

        denetim.Ekle(DenetimOlayi.RefreshTokenTekrarKullanildi, DenetimSonucu.Basarisiz,
            principal.GetClaim(Claims.Subject), principal.GetPresenters().FirstOrDefault(),
            "neden=kullanilmis-refresh-token; yetkilendirmenin-tokenlari-iptal");
    }
}
