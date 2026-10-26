using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Yemekhane.Data;

namespace Selcuk.Demo.Yemekhane.Pages;

[Authorize]
public class RezervasyonlarimModel(RezervasyonDeposu depo, TimeProvider zaman) : PageModel
{
    /// <summary>Listelenen günler: rezervasyon yapılabilecek günler ve (varsa) bunların dışındaki kayıtlı rezervasyonlar.</summary>
    public IReadOnlyList<(DateOnly Gun, bool Rezervasyonlu)> Gunler { get; private set; } = [];

    /// <summary>Son işlemin sonucu (yönlendirmeden sonra bir kez gösterilir ve ekran okuyucuya duyurulur).</summary>
    [TempData]
    public string? Bildirim { get; set; }

    private string Kullanici => User.FindFirst("sub")?.Value ?? User.Identity!.Name!;

    private IReadOnlyList<DateOnly> SecilebilirGunler =>
        Takvim.RezervasyonGunleri(DateOnly.FromDateTime(zaman.GetLocalNow().DateTime));

    public void OnGet()
    {
        var rezervasyonlar = depo.Listele(Kullanici);
        Gunler = [.. SecilebilirGunler.Union(rezervasyonlar).Order().Select(g => (g, rezervasyonlar.Contains(g)))];
    }

    public IActionResult OnPostEkle(DateOnly gun)
    {
        // Yalnızca listelenen günlere rezervasyon yapılabilir.
        if (SecilebilirGunler.Contains(gun))
        {
            depo.Ekle(Kullanici, gun);
            Bildirim = $"{Takvim.Yaz(gun)} için rezervasyon yapıldı.";
        }

        return RedirectToPage();
    }

    public IActionResult OnPostIptal(DateOnly gun)
    {
        if (depo.IptalEt(Kullanici, gun))
        {
            Bildirim = $"{Takvim.Yaz(gun)} rezervasyonu iptal edildi.";
        }

        return RedirectToPage();
    }
}
