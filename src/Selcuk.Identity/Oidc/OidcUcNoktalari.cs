using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// Arayüzü olmayan OIDC uç noktaları: token ve userinfo. İstek doğrulamasını (PKCE, istemci,
/// redirect_uri, token imzası vb.) OpenIddict yapar; buraya yalnızca geçerli istekler ulaşır.
/// </summary>
public static class OidcUcNoktalari
{
    public static IEndpointRouteBuilder MapOidcUcNoktalari(this IEndpointRouteBuilder app)
    {
        app.MapPost("connect/token", TokenAsync);
        app.MapMethods("connect/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync);
        return app;
    }

    private static async Task<IResult> TokenAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IkiAdimIslemleri ikiAdim,
        IstemciErisimi istemciErisimi,
        YenilemeUstSiniri ustSinir,
        IOpenIddictApplicationManager applicationManager,
        DenetimKaydedici denetim,
        ApplicationDbContext db,
        TimeProvider zaman)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenID Connect isteği alınamadı.");

        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
        {
            throw new InvalidOperationException("Desteklenmeyen grant türü.");
        }

        // Yetkilendirme kodu veya refresh token içindeki kimlik.
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var subject = result.Principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await userManager.FindByIdAsync(subject);

        // Hatalı denemelerden kaynaklanan geçici kilit (Lockout) burada bilerek kontrol edilmez: kilit yeni girişleri
        // engeller, kilitten önce açılmış oturumu ve o oturumdan token alınmasını engellemez (authorize ile tutarlı).
        // Aksi hâlde yanlış şifre deneyen biri gerçek kullanıcının açık oturumlarını düşürebilirdi (README > Güvenlik).
        if (user is null || !await signInManager.CanSignInAsync(user))
        {
            return Reddet(Errors.InvalidGrant, "Token artık geçerli değil.");
        }

        // İstemci bazlı erişim izni token anında da kontrol edilir: kod veya refresh token verildikten sonra izin
        // kaldırıldıysa token verilmez.
        var clientId = request.ClientId ?? result.Principal!.GetPresenters().FirstOrDefault();
        if (clientId is null || await applicationManager.FindByClientIdAsync(clientId) is not { } uygulama
            || !await istemciErisimi.IzinliMiAsync(uygulama, user, "token"))
        {
            return Reddet(Errors.InvalidGrant, "Bu uygulamaya erişim izni yok.");
        }

        // mfaZorunlu istemciye yalnızca çok faktörlü (amr "mfa") yetkilendirmeden token verilir.
        if (await istemciErisimi.MfaZorunluMuAsync(uygulama)
            && !OidcKimlik.AmrOku((ClaimsIdentity)result.Principal!.Identity!).Contains(OidcKimlik.AmrCokFaktorlu))
        {
            denetim.Ekle(DenetimOlayi.IstemciMfaGerekli, DenetimSonucu.Basarisiz, user.Id, clientId, "uc=token");
            return Reddet(Errors.InvalidGrant, "Bu uygulama iki aşamalı doğrulama gerektiriyor.");
        }

        // Rolü iki aşamalı doğrulama gerektiren kullanıcı kurulumu bitirmeden hiçbir istemciye token alamaz
        // (kod veya refresh token kurulum zorunluluğundan önce verilmiş olsa bile).
        if (await ikiAdim.KurulumGerekliAsync(user))
        {
            return Reddet(Errors.InvalidGrant, "İki aşamalı doğrulama kurulumu gerekli.");
        }

        // Kayan refresh token'a mutlak üst sınır: zincir ilk girişten itibaren Tokenlar:MutlakOturumSuresi'ni aşamaz.
        if (await ustSinir.KalanSureAsync(result.Principal!) is not { } kalan)
        {
            denetim.Ekle(DenetimOlayi.MutlakOturumSuresiDoldu, DenetimSonucu.Basarisiz, user.Id, clientId,
                $"grant_type={request.GrantType}");
            return Reddet(Errors.InvalidGrant, "Oturumun en uzun süresi doldu; yeniden giriş yapın.");
        }

        // Kapsamlar, yetkilendirme kimliği gibi OpenIddict claim'leri korunur; kullanıcı bilgileri tazelenir.
        var identity = new ClaimsIdentity(result.Principal!.Claims,
            authenticationType: result.Principal.Identity?.AuthenticationType,
            nameType: Claims.Name, roleType: Claims.Role);
        await OidcKimlik.KullaniciClaimleriniEkleAsync(identity, user, userManager);
        // amr koddan okunurken dizi ayrı claim'lere bölünebilir; id_token'da yine JSON dizisi olsun.
        if (OidcKimlik.AmrOku(identity).ToList() is { Count: > 0 } amr)
        {
            OidcKimlik.AmrAyarla(identity, amr);
        }

        identity.SetDestinations(OidcKimlik.Hedefler);
        ustSinir.OmurleriSinirla(identity, kalan);

        // Bu oturumda bu istemciye token verildiği kaydedilir; çıkışta Back-Channel Logout bildirimi gider.
        if (request.IsAuthorizationCodeGrantType() && identity.GetClaim(OidcKimlik.SidClaim) is { } sid)
        {
            await IstemciOturumuKaydetAsync(db, zaman, sid, user.Id, clientId);
        }

        denetim.Ekle(DenetimOlayi.TokenVerildi, DenetimSonucu.Basarili, user.Id, clientId,
            $"grant_type={request.GrantType}");

        return Results.SignIn(new ClaimsPrincipal(identity),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task IstemciOturumuKaydetAsync(
        ApplicationDbContext db, TimeProvider zaman, string sid, string kullaniciId, string clientId)
    {
        if (await db.IstemciOturumlari.AnyAsync(o => o.Sid == sid && o.ClientId == clientId))
        {
            return;
        }

        db.IstemciOturumlari.Add(new IstemciOturumu
        {
            Sid = sid,
            KullaniciId = kullaniciId,
            ClientId = clientId,
            OlusturmaZamani = zaman.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private static async Task<IResult> UserInfoAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal;
        var subject = principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await userManager.FindByIdAsync(subject);

        if (principal is null || user is null)
        {
            return Results.Challenge(HataOzellikleri(Errors.InvalidToken, "Access token geçerli bir kullanıcıya ait değil."),
                [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = await userManager.GetUserIdAsync(user),
        };

        if (principal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.AdSoyad;
            claims[Claims.PreferredUsername] = user.UserName!;

            if (user.Fakulte is not null)
            {
                claims[OidcKimlik.FakulteClaim] = user.Fakulte;
            }

            if (user.Bolum is not null)
            {
                claims[OidcKimlik.BolumClaim] = user.Bolum;
            }
        }

        if (principal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email!;
            claims[Claims.EmailVerified] = user.EmailConfirmed;
        }

        if (principal.HasScope(Scopes.Roles))
        {
            claims[Claims.Role] = await userManager.GetRolesAsync(user);
        }

        return Results.Ok(claims);
    }

    private static IResult Reddet(string error, string description) =>
        Results.Forbid(HataOzellikleri(error, description), [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static AuthenticationProperties HataOzellikleri(string error, string description) =>
        new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
        });
}
