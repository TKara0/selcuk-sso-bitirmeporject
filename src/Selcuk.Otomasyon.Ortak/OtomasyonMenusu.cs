using System.Security.Claims;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>Menüdeki bir ekran.</summary>
/// <param name="Baslik">Menüde ve kısayol kartında görünen ad.</param>
/// <param name="Sayfa">Razor Page yolu (ör. "/Ogrenci/Stajlarim").</param>
/// <param name="Aciklama">Ana sayfadaki kısayol kartının kısa açıklaması.</param>
/// <param name="Roller">Ekranı görebilecek roller (Selcuk.Roller sabitleri); en az biri olan kullanıcı görür.</param>
public sealed record MenuOgesi(string Baslik, string Sayfa, string Aciklama, params string[] Roller);

/// <summary>Menünün bir rol başlığı altındaki öğeleri (ör. "Öğrenci", "Staj komisyonu üyesi").</summary>
public sealed record MenuGrubu(string Baslik, IReadOnlyList<MenuOgesi> Ogeler);

/// <summary>
/// Rol tabanlı menü. Kullanıcı yalnızca rollerinden en az birine açık öğeleri görür; birden çok rolü olan kullanıcı
/// bütün uygun öğelerin birleşimini, rol başlıkları altında (katalog sırasıyla) görür. Bir öğe kullanıcının birden çok
/// rolüne açıksa yalnızca ilk uyan rolün başlığında bir kez görünür. Aynı tanımlar sunucu tarafı yetkiyi de belirler:
/// her öğenin sayfası yalnızca o rollere açılır (<see cref="OtomasyonUzantilari"/>); menüde görünmeyen ekrana adresle
/// girilemez.
/// </summary>
public sealed class OtomasyonMenusu(IReadOnlyList<MenuOgesi> ogeler)
{
    public IReadOnlyList<MenuOgesi> Ogeler { get; } = ogeler;

    /// <summary>Sayfanın yetki ilkesinin adı (rollerden en az biri).</summary>
    public static string IlkeAdi(string sayfa) => "otomasyon-menu:" + sayfa;

    public IReadOnlyList<MenuGrubu> Gruplar(ClaimsPrincipal kullanici)
    {
        var gruplar = new List<MenuGrubu>();
        var eklenen = new HashSet<MenuOgesi>();
        foreach (var rol in Selcuk.Roller.Katalog.Where(r => kullanici.IsInRole(r.Kimlik)))
        {
            var ogeler = Ogeler.Where(o => o.Roller.Contains(rol.Kimlik, StringComparer.Ordinal) && eklenen.Add(o)).ToList();
            if (ogeler.Count > 0)
            {
                gruplar.Add(new MenuGrubu(rol.Ad, ogeler));
            }
        }

        return gruplar;
    }

    /// <summary>Kullanıcının görebildiği bütün öğeler (gruplardaki sırayla).</summary>
    public IReadOnlyList<MenuOgesi> Gorunenler(ClaimsPrincipal kullanici) =>
        [.. Gruplar(kullanici).SelectMany(g => g.Ogeler)];
}
