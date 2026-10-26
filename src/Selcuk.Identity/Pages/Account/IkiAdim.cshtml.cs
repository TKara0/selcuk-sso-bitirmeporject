using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// Girişin ikinci adımı: doğrulayıcı uygulamadaki 6 haneli kod (TOTP) veya bir kurtarma kodu. Şifre adımını geçen
/// kullanıcı Identity'nin kısa ömürlü TwoFactorUserId çereziyle tanınır. Hatalı kodlar hatalı deneme sayacına eklenir
/// (5 denemede hesap kilitlenir), POST istek sınırına tabidir. "Bu cihazı hatırla" Identity'nin TwoFactorRememberMe
/// çerezidir (süre IkiAdimCihazHatirlamaGun); güvenlik damgası değişince geçersizleşir.
/// </summary>
[EnableRateLimiting(HizSiniri.HassasForm)]
public class IkiAdimModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IkiAdimIslemleri ikiAdim,
    DenetimKaydedici denetim,
    IOptions<IkiAdimAyarlari> ayarlar) : PageModel
{
    public const string HataMesaji =
        "Kod doğrulanamadı. Art arda hatalı denemelerde hesap geçici olarak kilitlenir.";

    [BindProperty]
    public KodGirdisi Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public int HatirlamaGun => ayarlar.Value.CihazHatirlamaGun;

    public class KodGirdisi
    {
        [Required(ErrorMessage = "Kodu yaz.")]
        [StringLength(32)]
        [Display(Name = "Doğrulama kodu veya kurtarma kodu")]
        public string Kod { get; set; } = string.Empty;

        public bool CihaziHatirla { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await signInManager.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = ReturnUrl });
        }

        Response.Headers.CacheControl = "no-store";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Şifre adımı yapılmamış veya TwoFactorUserId çerezinin süresi dolmuş: girişe baştan başlanır.
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = ReturnUrl });
        }

        Response.Headers.CacheControl = "no-store";
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Identity kurtarma koduyla girişte kilidi kontrol etmez; iki yol için de burada kontrol edilir.
        if (await userManager.IsLockedOutAsync(user))
        {
            denetim.Ekle(DenetimOlayi.IkiAdimBasarisiz, DenetimSonucu.Basarisiz, user.Id, ayrinti: "neden=hesap-kilitli");
            ModelState.AddModelError(string.Empty, HataMesaji);
            return Page();
        }

        var kod = IkiAdimIslemleri.KodNormallestir(Input.Kod);
        var totp = kod.Length == 6 && kod.All(char.IsAsciiDigit);
        var sonuc = totp
            ? await signInManager.TwoFactorAuthenticatorSignInAsync(kod, isPersistent: false, Input.CihaziHatirla)
            : await signInManager.TwoFactorRecoveryCodeSignInAsync(kod);

        if (sonuc.Succeeded)
        {
            var donus = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
            if (totp)
            {
                denetim.Ekle(DenetimOlayi.IkiAdimBasarili, DenetimSonucu.Basarili, user.Id,
                    ayrinti: Input.CihaziHatirla ? "yontem=totp; cihaz-hatirlandi" : "yontem=totp");
            }
            else
            {
                denetim.Ekle(DenetimOlayi.IkiAdimBasarili, DenetimSonucu.Basarili, user.Id, ayrinti: "yontem=kurtarma-kodu");
                denetim.Ekle(DenetimOlayi.KurtarmaKoduKullanildi, DenetimSonucu.Basarili, user.Id,
                    ayrinti: $"kalan={await userManager.CountRecoveryCodesAsync(user)}");
                await ikiAdim.KurtarmaKoduKullanildiAsync(user);
            }

            denetim.Ekle(DenetimOlayi.GirisBasarili, DenetimSonucu.Basarili, user.Id, ayrinti: "amr=pwd,mfa");
            return LocalRedirect(donus);
        }

        denetim.Ekle(DenetimOlayi.IkiAdimBasarisiz, DenetimSonucu.Basarisiz, user.Id,
            ayrinti: totp ? "yontem=totp" : "yontem=kurtarma-kodu");
        if (totp)
        {
            // Identity hatalı TOTP'yi (tekrar kullanılan kod dahil, TekKullanimlikTotpSaglayici) sayaca kendisi ekler ve
            // gerekirse hesabı kilitler.
            if (sonuc.IsLockedOut || await userManager.IsLockedOutAsync(user))
            {
                denetim.Ekle(DenetimOlayi.HesapKilitlendi, DenetimSonucu.Basarisiz, user.Id, ayrinti: "neden=hatali-kod");
            }
        }
        else
        {
            // Identity hatalı kurtarma kodunu saymaz; tahmin denemesine karşı burada sayılır.
            await ikiAdim.HataliDenemeAsync(user, "neden=hatali-kurtarma-kodu");
        }

        ModelState.AddModelError(string.Empty, HataMesaji);
        return Page();
    }
}
