using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Pages.Account;

public class LoginModel(SignInManager<ApplicationUser> signInManager) : PageModel
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

        var sonuc = await signInManager.PasswordSignInAsync(
            Input.KullaniciAdi.Trim(), Input.Sifre, isPersistent: false, lockoutOnFailure: true);

        if (sonuc.Succeeded)
        {
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/");
        }

        ModelState.AddModelError(string.Empty, GenelHataMesaji);
        return Page();
    }
}
