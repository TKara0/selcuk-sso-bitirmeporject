using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Pages.Account;

/// <summary>"Hesabım": kullanıcının hesap bilgileri ve şifre değiştirme bağlantısı.</summary>
[Authorize]
public class HesabimModel(UserManager<ApplicationUser> userManager) : PageModel
{
    public ApplicationUser Kullanici { get; private set; } = null!;

    [BindProperty(SupportsGet = true)]
    public bool SifreDegisti { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Kullanici = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
        return Page();
    }
}
