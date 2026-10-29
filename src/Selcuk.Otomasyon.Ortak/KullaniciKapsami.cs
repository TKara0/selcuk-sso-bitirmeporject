using System.Security.Claims;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>Bir kaydın kullanıcıya hangi bağla görüneceği.</summary>
public enum KayitKapsami
{
    /// <summary>Yalnızca kendi kaydı (öğrenci: kayıttaki öğrenci numarası = kullanıcı adı).</summary>
    Kendi,

    /// <summary>Kullanıcının bölümündeki kayıtlar (bölüm başkanı, bölüm sekreteri, bölüm komisyonları).</summary>
    Bolum,

    /// <summary>Kullanıcının fakültesindeki kayıtlar (fakülte sekreteri).</summary>
    Fakulte,
}

/// <summary>
/// Kullanıcının kapsam bilgileri: id_token'daki preferred_username, bolum ve fakulte claim'leri. Mock veri kaynakları
/// kayıtları bununla süzer. Api modunda süzmeyi API, access token'daki claim'lere göre kendisi yapar; istemcinin
/// gönderdiği bir değere güvenmez (docs/otomasyon-sablonu.md).
/// </summary>
public sealed record KullaniciKapsami(string? KullaniciAdi, string? Bolum, string? Fakulte)
{
    public static KullaniciKapsami Oku(ClaimsPrincipal kullanici) => new(
        Deger(kullanici, "preferred_username"), Deger(kullanici, "bolum"), Deger(kullanici, "fakulte"));

    /// <summary>
    /// Kayıt bu kapsamda kullanıcıya görünür mü. Boş kullanıcı bilgisi hiçbir kayda uymaz (ör. bölümü boş kullanıcı
    /// bölüm kapsamında hiçbir şey görmez). Bölüm kapsamında, iki tarafta da fakülte varsa fakülte de aynı olmalıdır.
    /// </summary>
    public bool Kapsar(KayitKapsami kapsam, string? ogrenciNo, string? bolum, string? fakulte) => kapsam switch
    {
        KayitKapsami.Kendi => Esit(KullaniciAdi, ogrenciNo),
        KayitKapsami.Bolum => Esit(Bolum, bolum)
            && (string.IsNullOrEmpty(Fakulte) || string.IsNullOrEmpty(fakulte) || Esit(Fakulte, fakulte)),
        KayitKapsami.Fakulte => Esit(Fakulte, fakulte),
        _ => false,
    };

    private static bool Esit(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.Ordinal);

    private static string? Deger(ClaimsPrincipal kullanici, string tur) =>
        kullanici.FindFirst(tur)?.Value is { Length: > 0 } deger ? deger : null;
}
