using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Anasayfa. Herkese açık karşılama (girişsiz); girişli öğrenci ve akademisyen için kimlik bilgileri id_token'daki
/// claim'lerden (name, preferred_username, fakulte, bolum, email) gelir; danışman ve sınıf örnek veridir.
/// </summary>
public class IndexModel : PageModel
{
    public const string Yok = "—";

    public string AdSoyad => User.Identity?.Name ?? Yok;

    public string KullaniciAdi => Claim("preferred_username");

    public string Fakulte => Claim("fakulte");

    public string Bolum => Claim("bolum");

    public string Eposta => Claim("email");

    public bool Ogrenci => User.IsInRole(Roller.Ogrenci);

    public bool Akademisyen => User.IsInRole(Roller.Akademisyen);

    public OgrenciKaydi Kayit => OrnekOgrenciVerisi.Bul(User);

    private string Claim(string ad) => User.FindFirst(ad)?.Value is { Length: > 0 } deger ? deger : Yok;

    public void OnGet()
    {
    }
}
