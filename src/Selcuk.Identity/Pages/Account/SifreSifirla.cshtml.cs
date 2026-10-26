using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// E-postadaki bağlantıyla yeni şifre belirleme. Başarıda güvenlik damgası yenilenir (token geçersizleşir), kilit ve
/// hatalı deneme sayacı sıfırlanır, kullanıcının tüm oturumları kapatılır ve bilgilendirme e-postası gider.
/// </summary>
[EnableRateLimiting(HizSiniri.HassasForm)]
public class SifreSifirlaModel(
    UserManager<ApplicationUser> userManager,
    SifreDegisikligi sifreDegisikligi,
    DenetimKaydedici denetim) : PageModel
{
    public const string GecersizBaglantiMesaji =
        "Bağlantı geçersiz, süresi dolmuş veya daha önce kullanılmış. Şifremi unuttum sayfasından yeni bir bağlantı iste.";

    public const string TamamlandiMesaji =
        "Şifren değiştirildi. Açık oturumların kapatıldı, yeni şifrenle giriş yapabilirsin.";

    [BindProperty]
    public SifirlamaGirdisi Input { get; set; } = new();

    public bool BaglantiGecersiz { get; private set; }

    public bool Tamamlandi { get; private set; }

    public class SifirlamaGirdisi
    {
        [Required]
        public string Kullanici { get; set; } = string.Empty;

        [Required]
        public string Kod { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifreni yaz.")]
        [DataType(DataType.Password)]
        [Display(Name = "Yeni şifre")]
        public string YeniSifre { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifreni tekrar yaz.")]
        [DataType(DataType.Password)]
        [Compare(nameof(YeniSifre), ErrorMessage = "Şifreler aynı değil. İki alana da aynı şifreyi yaz.")]
        [Display(Name = "Yeni şifre (tekrar)")]
        public string YeniSifreTekrar { get; set; } = string.Empty;
    }

    public void OnGet(string? kullanici, string? kod)
    {
        if (string.IsNullOrEmpty(kullanici) || string.IsNullOrEmpty(kod))
        {
            BaglantiGecersiz = true;
            return;
        }

        Input = new SifirlamaGirdisi { Kullanici = kullanici, Kod = kod };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Input.Kullanici) || string.IsNullOrEmpty(Input.Kod))
        {
            BaglantiGecersiz = true;
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await userManager.FindByIdAsync(Input.Kullanici);
        var token = KodCoz(Input.Kod);
        if (user is null || token is null)
        {
            BaglantiGecersiz = true;
            return Page();
        }

        var sonuc = await userManager.ResetPasswordAsync(user, token, Input.YeniSifre);
        if (!sonuc.Succeeded)
        {
            // Token önce doğrulanır: geçersizse şifre kurallarına bakılmaz. Şifre kurallara uymuyorsa token
            // kullanılmamış sayılır (damga değişmedi) ve kullanıcı aynı bağlantıyla yeniden deneyebilir.
            if (sonuc.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
            {
                denetim.Ekle(DenetimOlayi.SifreSifirlandi, DenetimSonucu.Basarisiz, user.Id,
                    ayrinti: "neden=gecersiz-baglanti");
                BaglantiGecersiz = true;
                return Page();
            }

            foreach (var hata in sonuc.Errors)
            {
                ModelState.AddModelError(string.Empty, hata.Description);
            }

            return Page();
        }

        await sifreDegisikligi.SifirlandiAsync(user);
        Tamamlandi = true;
        return Page();
    }

    private static string? KodCoz(string kod)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(kod));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
