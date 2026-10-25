namespace Selcuk.Demo.Api.Data;

public sealed record DersNotu(string Kod, string Ad, int Vize, int Final, string Harf);

/// <summary>Öğrencinin notları; yalnızca kimliği (sub) doğrulanmış token'dan gelen öğrenci için okunur.</summary>
public interface INotDeposu
{
    IReadOnlyList<DersNotu> Notlar(string ogrenciKimligi);
}

/// <summary>
/// Demo için uydurma, bellek içinde sabit veriler (Demo OBİS'in önceki sabit verisiyle aynı). Gerçek bir OBİS'e
/// bağlanılmaz. Her öğrenci için aynı örnek liste döner; gerçek bir sistemde sorgu öğrenci kimliğine göre yapılır.
/// </summary>
public sealed class SahteNotDeposu : INotDeposu
{
    private static readonly IReadOnlyList<DersNotu> OrnekNotlar =
    [
        new("BIL101", "Programlamaya Giriş", 78, 85, "BA"),
        new("BIL203", "Veri Yapıları", 65, 72, "CB"),
        new("BIL305", "Veritabanı Sistemleri", 90, 88, "AA"),
        new("BIL307", "Bilgisayar Ağları", 55, 61, "CC"),
        new("MAT201", "Lineer Cebir", 70, 80, "BB"),
    ];

    public IReadOnlyList<DersNotu> Notlar(string ogrenciKimligi) => OrnekNotlar;
}
