using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// Hesabım > İki aşamalı doğrulama. Kapalıyken: kurulumu başlat → paylaşılan anahtar (metin ve QR) → uygulamadaki kodla
/// doğrula → 2FA açılır ve 10 kurtarma kodu yalnızca bu yanıtta bir kez gösterilir. Açıkken: kurtarma kodlarını yenile
/// ve 2FA'yı kapat (ikisi de mevcut şifre + geçerli TOTP kodu ister). Rolü 2FA gerektiren kullanıcı girişten sonra
/// buraya yönlendirilir ve kurulum bitene kadar hiçbir istemciye token alamaz. Anahtar ve kodlar içeren yanıtlar
/// önbelleğe alınmaz.
/// </summary>
[Authorize]
[EnableRateLimiting(HizSiniri.HassasForm)]
public class IkiAdimDogrulamaModel(
    UserManager<ApplicationUser> userManager,
    IkiAdimIslemleri ikiAdim,
    DenetimKaydedici denetim) : PageModel
{
    public const string KodHatasiMesaji = "Kod doğrulanamadı. Uygulamadaki güncel 6 haneli kodu girin.";
    public const string OnayHatasiMesaji = "Şifre veya doğrulama kodu hatalı.";
    public const string KilitliMesaji =
        "Çok fazla hatalı deneme yapıldı; hesabınız geçici olarak kilitlendi. Bir süre sonra tekrar deneyin.";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Durum { get; set; }

    public bool Etkin { get; private set; }

    public bool KurulumGerekli { get; private set; }

    public bool RolGerektiriyor { get; private set; }

    public string? Anahtar { get; private set; }

    public string? OtpauthUri { get; private set; }

    public string? QrKod { get; private set; }

    public int KalanKodSayisi { get; private set; }

    /// <summary>Yalnızca açma veya yenileme POST'unun yanıtında dolu; tekrar gösterilmez.</summary>
    public IReadOnlyList<string>? YeniKodlar { get; private set; }

    /// <summary>Yeni kodlar 2FA açılırken mi (true) yoksa yenilemede mi üretildi.</summary>
    public bool YeniAcildi { get; private set; }

    public string? DevamAdresi => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null;

    public EtkinlestirmeGirdisi Etkinlestirme { get; set; } = new();

    public OnayGirdisi Onay { get; set; } = new();

    public class EtkinlestirmeGirdisi
    {
        [Display(Name = "Uygulamadaki 6 haneli kod")]
        public string? Kod { get; set; }
    }

    public class OnayGirdisi
    {
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut şifre")]
        public string? Sifre { get; set; }

        [Display(Name = "Uygulamadaki 6 haneli kod")]
        public string? Kod { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await YukleAsync(await KullaniciAsync());
        return Page();
    }

    public async Task<IActionResult> OnPostBaslatAsync()
    {
        var user = await KullaniciAsync();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            await ikiAdim.KurulumuBaslatAsync(user);
        }

        return RedirectToPage(new { returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostEtkinlestirAsync(EtkinlestirmeGirdisi etkinlestirme)
    {
        var user = await KullaniciAsync();
        if (await userManager.GetTwoFactorEnabledAsync(user) || await userManager.GetAuthenticatorKeyAsync(user) is null)
        {
            return RedirectToPage(new { returnUrl = ReturnUrl });
        }

        // Kod doğrulanmadan 2FA açılmaz: kullanıcı anahtarı uygulamasına doğru eklediğini kanıtlar.
        if (!await ikiAdim.KodDogruMuAsync(user, etkinlestirme.Kod))
        {
            denetim.Ekle(DenetimOlayi.IkiAdimAcildi, DenetimSonucu.Basarisiz, user.Id, ayrinti: "neden=kod-hatali");
            ModelState.AddModelError(string.Empty, KodHatasiMesaji);
            await YukleAsync(user);
            return Page();
        }

        YeniKodlar = await ikiAdim.EtkinlestirAsync(user, MevcutSid);
        YeniAcildi = true;
        await YukleAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostKodlariYenileAsync(OnayGirdisi onay)
    {
        var user = await KullaniciAsync();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage(new { returnUrl = ReturnUrl });
        }

        if (!await OnaylaAsync(user, onay, DenetimOlayi.KurtarmaKodlariYenilendi))
        {
            await YukleAsync(user);
            return Page();
        }

        YeniKodlar = await ikiAdim.KurtarmaKodlariniYenileAsync(user, MevcutSid);
        await YukleAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostKapatAsync(OnayGirdisi onay)
    {
        var user = await KullaniciAsync();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage(new { returnUrl = ReturnUrl });
        }

        if (await ikiAdim.RolGerektiriyorAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Rolünüz için iki aşamalı doğrulama zorunludur; kapatılamaz.");
            await YukleAsync(user);
            return Page();
        }

        if (!await OnaylaAsync(user, onay, DenetimOlayi.IkiAdimKapatildi))
        {
            await YukleAsync(user);
            return Page();
        }

        await ikiAdim.KapatAsync(user, MevcutSid);
        return RedirectToPage(new { durum = "kapatildi" });
    }

    private async Task<bool> OnaylaAsync(ApplicationUser user, OnayGirdisi onay, DenetimOlayi olay)
    {
        var sonuc = await ikiAdim.SifreVeKodDogrulaAsync(user, onay.Sifre, onay.Kod);
        if (sonuc == IkiAdimIslemleri.DogrulamaSonucu.Gecerli)
        {
            return true;
        }

        var kilitli = sonuc == IkiAdimIslemleri.DogrulamaSonucu.Kilitli;
        denetim.Ekle(olay, DenetimSonucu.Basarisiz, user.Id,
            ayrinti: kilitli ? "neden=hesap-kilitli" : "neden=sifre-veya-kod-hatali");
        ModelState.AddModelError(string.Empty, kilitli ? KilitliMesaji : OnayHatasiMesaji);
        return false;
    }

    private async Task YukleAsync(ApplicationUser user)
    {
        Response.Headers.CacheControl = "no-store";
        Etkin = await userManager.GetTwoFactorEnabledAsync(user);
        KurulumGerekli = await ikiAdim.KurulumGerekliAsync(user);
        RolGerektiriyor = await ikiAdim.RolGerektiriyorAsync(user);

        if (Etkin)
        {
            KalanKodSayisi = await userManager.CountRecoveryCodesAsync(user);
            return;
        }

        if (await userManager.GetAuthenticatorKeyAsync(user) is { } anahtar)
        {
            Anahtar = IkiAdimIslemleri.AnahtarBicimle(anahtar);
            OtpauthUri = IkiAdimIslemleri.OtpauthUri(user.UserName!, anahtar);
            QrKod = IkiAdimIslemleri.QrKodDataUri(OtpauthUri);
        }
    }

    private string? MevcutSid => User.FindFirst(OidcKimlik.SidClaim)?.Value;

    private async Task<ApplicationUser> KullaniciAsync() =>
        await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
}
