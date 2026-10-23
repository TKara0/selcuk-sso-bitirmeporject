using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

[Authorize(Roles = Roller.Ogrenci)]
public class NotlarimModel : PageModel
{
    public void OnGet()
    {
    }
}
