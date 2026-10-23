using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Yemekhane.Data;

namespace Selcuk.Demo.Yemekhane.Pages;

[Authorize]
public class RezervasyonlarimModel(RezervasyonDeposu depo, TimeProvider zaman) : PageModel
{
    public IReadOnlyList<DateOnly> Rezervasyonlar { get; private set; } = [];

    /// <summary>Henüz rezervasyon yapılmamış, seçilebilir günler.</summary>
    public IReadOnlyList<DateOnly> Bos { get; private set; } = [];

    private string Kullanici => User.FindFirst("sub")?.Value ?? User.Identity!.Name!;

    private IReadOnlyList<DateOnly> Gunler => Takvim.RezervasyonGunleri(DateOnly.FromDateTime(zaman.GetLocalNow().DateTime));

    public void OnGet() => Doldur();

    public IActionResult OnPostEkle(DateOnly gun)
    {
        // Yalnızca listelenen günlere rezervasyon yapılabilir.
        if (Gunler.Contains(gun))
        {
            depo.Ekle(Kullanici, gun);
        }

        return RedirectToPage();
    }

    public IActionResult OnPostIptal(DateOnly gun)
    {
        depo.IptalEt(Kullanici, gun);
        return RedirectToPage();
    }

    private void Doldur()
    {
        Rezervasyonlar = depo.Listele(Kullanici);
        Bos = [.. Gunler.Except(Rezervasyonlar)];
    }
}
