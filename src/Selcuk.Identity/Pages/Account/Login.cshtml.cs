using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// Şifreyle giriş (ilk adım). 2FA açıksa ikinci adım sayfasına (<see cref="IkiAdimModel"/>) yönlendirir.
/// </summary>
[EnableRateLimiting(HizSiniri.HassasForm)]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    SahteSifreDogrulayici sahteSifre,
    IkiAdimIslemleri ikiAdim,
    DenetimKaydedici denetim) : PageModel
{
    /// <summary>
    /// Yanlış şifre, var olmayan kullanıcı ve kilitli hesap için aynı mesaj gösterilir;
    /// böylece mesajdan bir hesabın var olup olmadığı anlaşılamaz.
    /// </summary>
    public const string GenelHataMesaji =
        "Giriş yapılamadı. Bilgilerinizi kontrol edip tekrar deneyin. " +
        "Art arda hatalı denemelerde hesap geçici olarak kilitlenir.";

    [BindProperty]
    public GirisGirdisi Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class GirisGirdisi
    {
        [Required(ErrorMessage = "Kullanıcı adını veya öğrenci numaranı yaz.")]
        [Display(Name = "Kullanıcı adı veya öğrenci numarası")]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifreni yaz.")]
        [DataType(DataType.Password)]
        [Display(Name = "Şifre")]
        public string Sifre { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Kullanıcı yoksa veya hesap kilitliyse Identity şifre özetini hesaplamaz; bu yollarda aynı maliyette sahte bir
        // doğrulama yapılır. Böylece yanıt süresi "kullanıcı yok", "kilitli" ve "şifre yanlış" arasında ayırt edilemez.
        var girilen = Input.KullaniciAdi.Trim();
        var kullanici = await userManager.FindByNameAsync(girilen);
        var kilitliydi = kullanici is not null && await userManager.IsLockedOutAsync(kullanici);
        if (kullanici is null || kilitliydi)
        {
            sahteSifre.Dogrula(Input.Sifre);
        }

        var sonuc = kullanici is null
            ? SignInResult.Failed
            : await signInManager.PasswordSignInAsync(kullanici, Input.Sifre, isPersistent: false, lockoutOnFailure: true);
        var donus = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (sonuc.Succeeded)
        {
            // 2FA açık ama bu cihaz hatırlanıyorsa Identity ikinci adımı atlar (amr yine "pwd").
            denetim.Ekle(DenetimOlayi.GirisBasarili, DenetimSonucu.Basarili, kullanici!.Id,
                ayrinti: await userManager.GetTwoFactorEnabledAsync(kullanici) ? "amr=pwd; cihaz-hatirlandi" : "amr=pwd");

            // Rolü 2FA gerektiren ve 2FA'sı kapalı kullanıcı önce kuruluma gider (authorize ve token da engeller).
            return await ikiAdim.KurulumGerekliAsync(kullanici)
                ? RedirectToPage("/Account/IkiAdimDogrulama", new { returnUrl = donus })
                : LocalRedirect(donus);
        }

        if (sonuc.RequiresTwoFactor)
        {
            return RedirectToPage("/Account/IkiAdim", new { returnUrl = donus });
        }

        BasarisizGirisiKaydet(kullanici, girilen, kilitliydi, sonuc);
        ModelState.AddModelError(string.Empty, GenelHataMesaji);
        return Page();
    }

    private void BasarisizGirisiKaydet(ApplicationUser? kullanici, string girilen, bool kilitliydi, SignInResult sonuc)
    {
        if (kullanici is null)
        {
            // Yazılan kullanıcı adı saklanmaz (bazen şifre bu alana yazılır); normalize hâlinin özeti saklanır.
            denetim.Ekle(DenetimOlayi.GirisBasarisiz, DenetimSonucu.Basarisiz, ayrinti:
                "neden=kullanici-yok; " + denetim.KullaniciAdiOzeti(userManager.NormalizeName(girilen)));
            return;
        }

        var neden = kilitliydi ? "hesap-kilitli" : sonuc.IsNotAllowed ? "giris-izni-yok" : "sifre-yanlis";
        denetim.Ekle(DenetimOlayi.GirisBasarisiz, DenetimSonucu.Basarisiz, kullanici.Id, ayrinti: $"neden={neden}");

        if (!kilitliydi && sonuc.IsLockedOut)
        {
            denetim.Ekle(DenetimOlayi.HesapKilitlendi, DenetimSonucu.Basarisiz, kullanici.Id,
                ayrinti: "neden=hatali-sifre");
        }
    }
}
