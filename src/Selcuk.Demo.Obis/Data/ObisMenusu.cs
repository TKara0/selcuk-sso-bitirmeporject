using System.Security.Claims;

namespace Selcuk.Demo.Obis.Data;

/// <param name="Sayfa">Razor sayfa yolu (ör. "/Transkript").</param>
/// <param name="Metin">Sekmedeki metin.</param>
/// <param name="Simge">Sekmedeki yerel SVG simge (<see cref="ObisSimgeleri"/>).</param>
public sealed record ObisSekmesi(string Sayfa, string Metin, string Simge);

/// <summary>
/// Demo OBİS'in sekme çubuğu. Öğrenci sekmeleri yalnızca ogrenci, Ders Yönetimi yalnızca akademisyen rolüne açıktır
/// (sayfaların kendi <c>[Authorize(Roles = …)]</c> kuralı asıl denetimdir; sekme yalnızca görünürlüktür). Anasayfa
/// her girişli kullanıcıdadır. Token İnceleme sekmede değil, hesap menüsündedir.
/// </summary>
public static class ObisMenusu
{
    public static readonly ObisSekmesi Anasayfa = new("/Index", "Anasayfa", ObisSimgeleri.Anasayfa);

    /// <summary>Anasayfa dahil, öğrencinin gördüğü sekmeler (gerçek sıra).</summary>
    public static readonly IReadOnlyList<ObisSekmesi> Ogrenci =
    [
        Anasayfa,
        new("/OnayTakip", "Onay Takip", ObisSimgeleri.OnayTakip),
        new("/SonYilNotlari", "Son Yıl Notları", ObisSimgeleri.SonYilNotlari),
        new("/Notlarim", "Not Durumu", ObisSimgeleri.NotDurumu),
        new("/Transkript", "Transkript", ObisSimgeleri.Transkript),
        new("/HarcDurumu", "Harç Durumu", ObisSimgeleri.HarcDurumu),
        new("/SinavTarihleri", "Sınav Tarihleri", ObisSimgeleri.SinavTarihleri),
        new("/UzaktanEgitim", "Uzaktan Eğitim", ObisSimgeleri.UzaktanEgitim),
        new("/UzaktanSinav", "Uzaktan Sınav", ObisSimgeleri.UzaktanSinav),
        new("/YkkKarari", "YKK Kararı", ObisSimgeleri.YkkKarari),
    ];

    /// <summary>Anasayfa dahil, akademisyenin gördüğü sekmeler.</summary>
    public static readonly IReadOnlyList<ObisSekmesi> Akademisyen =
    [
        Anasayfa,
        new("/DersYonetimi", "Ders Yönetimi", ObisSimgeleri.DersYonetimi),
    ];

    /// <summary>Kullanıcının rollerine göre sekmeler; Anasayfa bir kez, başta.</summary>
    public static IReadOnlyList<ObisSekmesi> Sekmeler(ClaimsPrincipal kullanici)
    {
        var sekmeler = new List<ObisSekmesi> { Anasayfa };
        if (kullanici.IsInRole(Roller.Ogrenci))
        {
            sekmeler.AddRange(Ogrenci.Skip(1));
        }

        if (kullanici.IsInRole(Roller.Akademisyen))
        {
            sekmeler.AddRange(Akademisyen.Skip(1));
        }

        return sekmeler;
    }

    /// <summary>Profil görselinde ve hesap düğmesinde gösterilen baş harfler (ilk ve son sözcük); ad yoksa "?".</summary>
    public static string BasHarfler(string? ad)
    {
        var sozcukler = (ad ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sozcukler.Length == 0)
        {
            return "?";
        }

        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        static string Ilk(string sozcuk) => char.IsSurrogate(sozcuk[0]) ? sozcuk[..2] : sozcuk[..1];
        return sozcukler.Length == 1
            ? Ilk(sozcukler[0]).ToUpper(tr)
            : (Ilk(sozcukler[0]) + Ilk(sozcukler[^1])).ToUpper(tr);
    }
}
