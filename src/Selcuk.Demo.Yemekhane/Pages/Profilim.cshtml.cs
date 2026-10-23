using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Demo.Yemekhane.Pages;

[Authorize]
public class ProfilimModel : PageModel
{
    public void OnGet()
    {
    }
}
