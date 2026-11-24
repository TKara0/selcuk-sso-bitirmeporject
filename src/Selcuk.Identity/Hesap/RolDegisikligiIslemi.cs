using Microsoft.AspNetCore.Identity;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Bir kullanıcının rol kümesini değiştiren tek yol. Roller yalnızca başlangıç verisiyle (<see cref="BaslangicVerisi"/>)
/// atanıyor; ileride bir panel eklense de rol değişikliği buradan geçmelidir. Rol kümesi gerçekten değişirse (rol
/// eklendi veya çıkarıldı):
/// <list type="number">
/// <item>sonuç küme <see cref="RolDogrulayici"/> kurallarına uymuyorsa hiçbir şey yapılmaz (hata sonuçta döner);</item>
/// <item>roller uygulanır ve güvenlik damgası yenilenir: damga her istekte doğrulandığı için
/// (<see cref="OturumDamgasi"/>) kimlik sunucusundaki eski oturumlar bir sonraki istekte kapanır;</item>
/// <item>o oturumlardan giriş yapılmış istemcilere Back-Channel Logout gönderilir ve kullanıcının yetkilendirmeleri ve
/// token'ları (refresh token'lar dahil) iptal edilir; şifre sıfırlamadaki mekanizmanın aynısı
/// (<see cref="BackChannelLogoutGonderici"/>, <see cref="TokenIptali"/>);</item>
/// <item>"RolDegisti" denetim kaydı yazılır (eski ve yeni rol listesi; sır, şifre veya token yok).</item>
/// </list>
/// Rol kümesi değişmiyorsa (istenen roller zaten var, çıkarılacak roller zaten yok) hiçbir şey yapılmaz: her açılışta
/// herkesin oturumu kapanmaz. Verilmiş bir access token'ın (JWT, en çok 15 dk) içindeki rol claim'i süresi dolana kadar
/// eski kalır; yenileme ve yeni giriş yeni rolleri alır.
/// </summary>
public sealed class RolDegisikligiIslemi(
    UserManager<ApplicationUser> userManager,
    BackChannelLogoutGonderici backChannelLogout,
    TokenIptali tokenIptali,
    DenetimKaydedici denetim,
    ILogger<RolDegisikligiIslemi> logger)
{
    /// <summary>Değişiklik uygulandıysa <see cref="Degisti"/> true; kurallara uymadığı için reddedildiyse <see cref="Hatalar"/> dolu.</summary>
    public sealed record Sonuc(bool Degisti, IReadOnlyList<string> EskiRoller, IReadOnlyList<string> YeniRoller, IReadOnlyList<string> Hatalar);

    /// <summary>Kullanıcıya <paramref name="eklenecek"/> rolleri ekler ve <paramref name="cikarilacak"/> rolleri çıkarır.</summary>
    public async Task<Sonuc> UygulaAsync(ApplicationUser user, IEnumerable<string> eklenecek, IEnumerable<string> cikarilacak,
        CancellationToken cancellationToken = default)
    {
        var eski = (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        var ekle = eklenecek.Distinct(StringComparer.Ordinal).Except(eski, StringComparer.Ordinal).ToList();
        var cikar = cikarilacak.Distinct(StringComparer.Ordinal).Intersect(eski, StringComparer.Ordinal).ToList();
        if (ekle.Count == 0 && cikar.Count == 0)
        {
            return new Sonuc(false, eski, eski, []);
        }

        var yeni = eski.Except(cikar, StringComparer.Ordinal).Concat(ekle).Order(StringComparer.Ordinal).ToList();
        var hatalar = RolDogrulayici.Hatalar(yeni, user.Fakulte, user.Bolum);
        if (hatalar.Count > 0)
        {
            logger.LogError("Kullanıcı {KullaniciAdi} için rol değişikliği uygulanmadı: {Hatalar}",
                user.UserName, string.Join(" ", hatalar));
            return new Sonuc(false, eski, eski, hatalar);
        }

        // Önce ekleme, sonra çıkarma: bir adım başarısız olsa bile kullanıcı rolsüz kalmaz.
        var uygulama = new List<string>();
        var uygulandi = false;
        if (ekle.Count > 0)
        {
            var sonuc = await userManager.AddToRolesAsync(user, ekle);
            uygulandi |= sonuc.Succeeded;
            uygulama.AddRange(sonuc.Errors.Select(e => e.Description));
        }

        if (cikar.Count > 0 && uygulama.Count == 0)
        {
            var sonuc = await userManager.RemoveFromRolesAsync(user, cikar);
            uygulandi |= sonuc.Succeeded;
            uygulama.AddRange(sonuc.Errors.Select(e => e.Description));
        }

        if (uygulandi)
        {
            // Hangi adım uygulandıysa gerçek kümeyi kaydet ve oturumları kapat (kısmi başarıda da eski roller geçersiz).
            var gercek = (await userManager.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
            await userManager.UpdateSecurityStampAsync(user);
            denetim.Ekle(DenetimOlayi.RolDegisti, DenetimSonucu.Basarili, user.Id,
                ayrinti: $"eski={string.Join(',', eski)}; yeni={string.Join(',', gercek)}");

            // Tarayıcı bağlantıyı kapatsa da bildirimler tamamlansın (süre sınırı HttpClient'ta).
            await backChannelLogout.KullaniciOturumlariniKapatAsync(user.Id, haricSid: null, CancellationToken.None);
            // Kapanan oturumlardan alınmış token'lar (refresh token'lar dahil) iptal edilir.
            await tokenIptali.KullaniciyiIptalEtAsync(user.Id, haricSid: null, CancellationToken.None);
            yeni = gercek;
        }

        if (uygulama.Count > 0)
        {
            logger.LogError("Kullanıcı {KullaniciAdi} için rol değişikliği tamamlanamadı: {Hatalar}",
                user.UserName, string.Join("; ", uygulama));
        }

        return new Sonuc(uygulandi, eski, yeni, uygulama);
    }
}
