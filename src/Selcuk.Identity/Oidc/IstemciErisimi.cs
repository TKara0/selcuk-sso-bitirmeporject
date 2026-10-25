using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;

namespace Selcuk.Identity.Oidc;

/// <summary>
/// İstemci bazlı erişim izni. İstemcinin özel ayarlarındaki (OpenIddict "Properties") izinliRoller ve
/// izinliKullaniciAdlari listelerinin ikisi de boşsa herkes girebilir; biri doluysa kullanıcı en az birine uymalıdır
/// (rollerinden biri listede veya kullanıcı adı listede; büyük/küçük harf duyarsız). Authorize (kod verilmeden önce),
/// token uç noktası (yetkilendirme kodu ve refresh token) ve Uygulamalarım sayfası aynı kuralı kullanır.
/// </summary>
public sealed class IstemciErisimi(
    IOpenIddictApplicationManager applicationManager,
    UserManager<ApplicationUser> userManager,
    DenetimKaydedici denetim)
{
    public async Task<bool> IzinliMiAsync(object uygulama, ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var ozellikler = await applicationManager.GetPropertiesAsync(uygulama, cancellationToken);
        return Izinli(ozellikler, user.UserName, await userManager.GetRolesAsync(user));
    }

    /// <summary>İzin yoksa denetim kaydına "İstemciye erişim reddedildi" yazar (client_id ile).</summary>
    public async Task<bool> IzinliMiAsync(object uygulama, ApplicationUser user, string uc,
        CancellationToken cancellationToken = default)
    {
        if (await IzinliMiAsync(uygulama, user, cancellationToken))
        {
            return true;
        }

        denetim.Ekle(DenetimOlayi.IstemciErisimiReddedildi, DenetimSonucu.Basarisiz, user.Id,
            await applicationManager.GetClientIdAsync(uygulama, cancellationToken), $"uc={uc}");
        return false;
    }

    public static bool Izinli(ImmutableDictionary<string, JsonElement> ozellikler, string? kullaniciAdi,
        IEnumerable<string> roller)
    {
        var izinliRoller = Liste(ozellikler, Istemciler.IzinliRollerOzelligi);
        var izinliAdlar = Liste(ozellikler, Istemciler.IzinliKullaniciAdlariOzelligi);
        if (izinliRoller is { Count: 0 } && izinliAdlar is { Count: 0 })
        {
            return true;
        }

        // Dizi olmayan (hatalı biçimli) bir ayar null döner: istemci kısıtlı sayılır ve o listeye kimse uymaz.
        return (kullaniciAdi is not null && izinliAdlar?.Contains(kullaniciAdi) == true)
            || (izinliRoller is not null && roller.Any(izinliRoller.Contains));
    }

    /// <summary>Ayar yoksa boş küme, dizi ise içindeki adlar, başka bir türdeyse null.</summary>
    private static HashSet<string>? Liste(ImmutableDictionary<string, JsonElement> ozellikler, string ad)
    {
        if (!ozellikler.TryGetValue(ad, out var deger) || deger.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return deger.ValueKind == JsonValueKind.Array
            ? deger.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                .Select(e => e.GetString()!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
    }
}
