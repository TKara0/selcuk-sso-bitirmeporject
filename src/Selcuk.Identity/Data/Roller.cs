namespace Selcuk.Identity.Data;

public static class Roller
{
    public const string Ogrenci = "ogrenci";
    public const string Akademisyen = "akademisyen";
    public const string Personel = "personel";

    public static readonly IReadOnlyList<string> Tumu = [Ogrenci, Akademisyen, Personel];
}
