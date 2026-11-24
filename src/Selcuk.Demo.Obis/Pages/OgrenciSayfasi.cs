using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Öğrenci ekranlarının ortak tabanı: yalnızca ogrenci rolü (adresle gidilince Erişim reddedildi), kayıt oturumdaki
/// id_token'ın preferred_username claim'inden seçilir; öğrenci yalnızca kendi örnek kaydını görür.
/// </summary>
[Authorize(Roles = Roller.Ogrenci)]
public abstract class OgrenciSayfasi : PageModel
{
    public OgrenciKaydi Kayit => OrnekOgrenciVerisi.Bul(User);
}
