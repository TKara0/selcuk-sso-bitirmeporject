using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// Giriş yapmış kullanıcının şifre değiştirmesi. Başarıda güvenlik damgası yenilenir; bu oturum yeni damgayla yeniden
/// imzalanır ve açık kalır, kullanıcının diğer oturumları kapatılır ve bilgilendirme e-postası gider.
/// </summary>
[Authorize]
public class SifreDegistirModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    SifreDegisikligi sifreDegisikligi) : PageModel
{
    [BindProperty]
    public DegistirmeGirdisi Input { get; set; } = new();

    public class DegistirmeGirdisi
    {
        [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mevcut şifre")]
        public string MevcutSifre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre zorunludur.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string YeniSifre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
        [DataType(DataType.Password)]
        [Compare(nameof(YeniSifre), ErrorMessage = "Şifreler aynı değil.")]
        [Display(Name = "Yeni şifre (tekrar)")]
        public string YeniSifreTekrar { get; set; } = string.Empty;
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

        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");

        var sonuc = await userManager.ChangePasswordAsync(user, Input.MevcutSifre, Input.YeniSifre);
        if (!sonuc.Succeeded)
        {
            foreach (var hata in sonuc.Errors)
            {
                ModelState.AddModelError(string.Empty, hata.Description);
            }

            return Page();
        }

        // ChangePasswordAsync güvenlik damgasını yeniledi. Bu oturumun çerezi yeni damgayla yeniden yazılır (sid
        // korunur: OturumKimligi); diğer oturumlar bir sonraki istekte reddedilir ve istemcilerine bildirim gider.
        var sid = User.FindFirst(OidcKimlik.SidClaim)?.Value;
        await signInManager.RefreshSignInAsync(user);
        await sifreDegisikligi.DegistirildiAsync(user, sid);

        return RedirectToPage("/Account/Hesabim", new { sifreDegisti = true });
    }
}
