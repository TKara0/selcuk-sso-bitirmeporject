using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Pages.Account;

[EnableRateLimiting(HizSiniri.HassasForm)]
public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    SahteSifreDogrulayici sahteSifre) : PageModel
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
        [Required(ErrorMessage = "Kullanıcı adı zorunludur.")]
        [Display(Name = "Kullanıcı adı veya öğrenci numarası")]
        public string KullaniciAdi { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre zorunludur.")]
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
        var kullanici = await userManager.FindByNameAsync(Input.KullaniciAdi.Trim());
        if (kullanici is null || await userManager.IsLockedOutAsync(kullanici))
        {
            sahteSifre.Dogrula(Input.Sifre);
        }

        if (kullanici is not null && (await signInManager.PasswordSignInAsync(
                kullanici, Input.Sifre, isPersistent: false, lockoutOnFailure: true)).Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
        }

        ModelState.AddModelError(string.Empty, GenelHataMesaji);
        return Page();
    }
}
