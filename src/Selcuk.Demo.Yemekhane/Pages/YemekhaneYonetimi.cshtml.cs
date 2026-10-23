using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Yemekhane.Data;

namespace Selcuk.Demo.Yemekhane.Pages;

[Authorize(Roles = Roller.Personel)]
public class YemekhaneYonetimiModel(RezervasyonDeposu depo, TimeProvider zaman) : PageModel
{
    public IReadOnlyList<(DateOnly Gun, int Sayi)> Sayilar { get; private set; } = [];

    public void OnGet()
    {
        var gunler = Takvim.RezervasyonGunleri(DateOnly.FromDateTime(zaman.GetLocalNow().DateTime));
        Sayilar = [.. gunler.Select(g => (g, depo.Sayi(g)))];
    }
}
