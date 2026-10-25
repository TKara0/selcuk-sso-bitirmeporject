using Microsoft.AspNetCore.Identity;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Hesap;

/// <summary>
/// Şifre sıfırlama ve şifre değiştirme sonrasındaki ortak işler. Güvenlik damgasını Identity yeniler
/// (ResetPasswordAsync / ChangePasswordAsync); damga her istekte doğrulandığı için (<see cref="OturumDamgasi"/>)
/// kimlik sunucusundaki eski oturumlar bir sonraki istekte kapanır. Bu sınıf ayrıca o oturumlardan giriş yapılmış
/// istemcilere Back-Channel Logout gönderir ve bilgilendirme e-postasını kuyruğa bırakır.
/// </summary>
public sealed class SifreDegisikligi(
    UserManager<ApplicationUser> userManager,
    BackChannelLogoutGonderici backChannelLogout,
    TokenIptali tokenIptali,
    EpostaKuyrugu eposta,
    DenetimKaydedici denetim,
    TimeProvider zaman)
{
    /// <summary>Sıfırlama: hesap kilidi ve hatalı deneme sayacı sıfırlanır, kullanıcının tüm oturumları kapatılır.</summary>
    public async Task SifirlandiAsync(ApplicationUser user)
    {
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await TamamlaAsync(user, haricSid: null, DenetimOlayi.SifreSifirlandi);
    }

    /// <summary>Değiştirme: şifreyi değiştiren oturum (mevcutSid) açık kalır, diğerleri kapatılır.</summary>
    public Task DegistirildiAsync(ApplicationUser user, string? mevcutSid) =>
        TamamlaAsync(user, mevcutSid, DenetimOlayi.SifreDegisti);

    private async Task TamamlaAsync(ApplicationUser user, string? haricSid, DenetimOlayi olay)
    {
        denetim.Ekle(olay, DenetimSonucu.Basarili, user.Id);

        // Tarayıcı bağlantıyı kapatsa da bildirimler tamamlansın (süre sınırı HttpClient'ta).
        await backChannelLogout.KullaniciOturumlariniKapatAsync(user.Id, haricSid, CancellationToken.None);
        // Kapanan oturumlardan alınmış token'lar (refresh token'lar dahil) iptal edilir.
        await tokenIptali.KullaniciyiIptalEtAsync(user.Id, haricSid, CancellationToken.None);

        if (!string.IsNullOrEmpty(user.Email))
        {
            eposta.Ekle(EpostaSablonlari.SifreDegistirildi(user.Email, user.AdSoyad, zaman.GetUtcNow()));
        }
    }
}
