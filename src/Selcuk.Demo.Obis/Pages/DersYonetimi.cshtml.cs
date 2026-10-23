using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

[Authorize(Roles = Roller.Akademisyen)]
public class DersYonetimiModel : PageModel
{
    public void OnGet()
    {
    }
}
