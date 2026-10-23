using System.Globalization;

namespace Selcuk.Demo.Yemekhane.Data;

public static class Roller
{
    public const string Personel = "personel";
}

public sealed record GunlukMenu(DateOnly Gun, string Corba, string AnaYemek, string Yardimci, string Tatli);

/// <summary>Demo için uydurma menü ve tarih yardımcıları. Gerçek bir yemekhane sistemine bağlanılmaz.</summary>
public static class Takvim
{
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly (string Corba, string AnaYemek, string Yardimci, string Tatli)[] Yemekler =
    [
        ("Mercimek çorbası", "Orman kebabı", "Bulgur pilavı", "Sütlaç"),
        ("Ezogelin çorbası", "Tavuk sote", "Pirinç pilavı", "Mevsim meyvesi"),
        ("Domates çorbası", "Kuru fasulye", "Pirinç pilavı", "Turşu ve ayran"),
        ("Yayla çorbası", "Fırın köfte", "Patates püresi", "Revani"),
        ("Tarhana çorbası", "Sebzeli güveç", "Makarna", "Kemalpaşa tatlısı"),
    ];

    /// <summary>İçinde bulunulan haftanın pazartesi-cuma menüsü.</summary>
    public static IReadOnlyList<GunlukMenu> HaftalikMenu(DateOnly bugun)
    {
        var pazartesi = bugun.AddDays(-(((int)bugun.DayOfWeek + 6) % 7));
        return [.. Yemekler.Select((y, i) => new GunlukMenu(pazartesi.AddDays(i), y.Corba, y.AnaYemek, y.Yardimci, y.Tatli))];
    }

    /// <summary>Rezervasyon yapılabilecek günler: yarından itibaren sonraki 5 iş günü.</summary>
    public static IReadOnlyList<DateOnly> RezervasyonGunleri(DateOnly bugun)
    {
        var gunler = new List<DateOnly>();
        for (var gun = bugun.AddDays(1); gunler.Count < 5; gun = gun.AddDays(1))
        {
            if (gun.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                gunler.Add(gun);
            }
        }

        return gunler;
    }

    public static string Yaz(DateOnly gun) => gun.ToString("dd.MM.yyyy dddd", Tr);
}
