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
/// Ek doğrulama (step-up): iki aşamalı doğrulama isteyen bir uygulamaya (istemci kaydında mfaZorunlu) yalnızca şifreyle
/// (veya "Bu cihazı hatırla" ile) açılmış bir oturumdan gidilince mevcut oturumda ikinci adım tekrar istenir: TOTP veya
/// kurtarma kodu. Başarılıysa oturum çok faktörlü olur (amr ["pwd","mfa"]), sid korunur ve authorize akışı dönüş
/// adresinden devam eder. Hatalı kodlar hatalı deneme sayacına eklenir, kilitliyken kabul edilmez; TOTP tekrar oynatma
/// koruması ve istek sınırı girişin ikinci adımıyla aynıdır.
/// </summary>
[Authorize]
[EnableRateLimiting(HizSiniri.HassasForm)]
public class EkDogrulamaModel(
    UserManager<ApplicationUser> userManager,
    IkiAdimIslemleri ikiAdim,
    DenetimKaydedici denetim) : PageModel
{
    public const string HataMesaji =
        "Kod doğrulanamadı. Art arda hatalı denemelerde hesap geçici olarak kilitlenir.";

    [BindProperty]
    public KodGirdisi Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class KodGirdisi
    {
        [Required(ErrorMessage = "Kodu yaz.")]
        [StringLength(32)]
        [Display(Name = "Doğrulama kodu veya kurtarma kodu")]
        public string Kod { get; set; } = string.Empty;
    }

    private string Donus => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await KullaniciAsync();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("/Account/IkiAdimDogrulama", new { returnUrl = ReturnUrl });
        }

        // Oturum zaten çok faktörlüyse sorulmaz.
        return OidcKimlik.AmrDegerleri(User).Contains(OidcKimlik.AmrCokFaktorlu) ? LocalRedirect(Donus) : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await KullaniciAsync();
        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            return RedirectToPage("/Account/IkiAdimDogrulama", new { returnUrl = ReturnUrl });
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            denetim.Ekle(DenetimOlayi.EkDogrulama, DenetimSonucu.Basarisiz, user.Id, ayrinti: "neden=hesap-kilitli");
            ModelState.AddModelError(string.Empty, HataMesaji);
            return Page();
        }

        var kod = IkiAdimIslemleri.KodNormallestir(Input.Kod);
        var totp = kod.Length == 6 && kod.All(char.IsAsciiDigit);
        var gecerli = totp
            ? await ikiAdim.KoduKullanAsync(user, kod)
            : (await userManager.RedeemTwoFactorRecoveryCodeAsync(user, kod)).Succeeded;

        if (!gecerli)
        {
            denetim.Ekle(DenetimOlayi.EkDogrulama, DenetimSonucu.Basarisiz, user.Id,
                ayrinti: totp ? "yontem=totp" : "yontem=kurtarma-kodu");
            await ikiAdim.HataliDenemeAsync(user, "neden=hatali-ek-dogrulama-kodu");
            ModelState.AddModelError(string.Empty, HataMesaji);
            return Page();
        }

        await ikiAdim.EkDogrulamaTamamlandiAsync(user);
        denetim.Ekle(DenetimOlayi.EkDogrulama, DenetimSonucu.Basarili, user.Id,
            ayrinti: totp ? "yontem=totp" : "yontem=kurtarma-kodu");
        if (!totp)
        {
            denetim.Ekle(DenetimOlayi.KurtarmaKoduKullanildi, DenetimSonucu.Basarili, user.Id,
                ayrinti: $"kalan={await userManager.CountRecoveryCodesAsync(user)}");
            await ikiAdim.KurtarmaKoduKullanildiAsync(user);
        }

        return LocalRedirect(Donus);
    }

    private async Task<ApplicationUser> KullaniciAsync() =>
        await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
}
