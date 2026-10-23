using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Selcuk.Demo.Obis.Pages;

[Authorize]
public class ProfilimModel : PageModel
{
    public void OnGet()
    {
    }
}
