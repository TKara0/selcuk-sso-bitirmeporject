using System.Globalization;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;
using Selcuk.Otomasyon.EtikKurul.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.EtikKurul.Sozlesme.EtikDurumu;

namespace Selcuk.Otomasyon.EtikKurul;

/// <summary>
/// Etik kurul şartları (appsettings "EtikKurulSartlari"; değerler örnektir, gerçek yönetmelik değildir): inceleme
/// süresi hedefi (gün) ve karar için gereken üye görüşü sayısı.
/// </summary>
public sealed class EtikKurulSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "EtikKurulSartlari";

    public int IncelemeSuresiHedefiGun { get; set; } = 30;

    public int KararIcinGerekenGorusSayisi { get; set; } = 3;

    public string? Hata() =>
        IncelemeSuresiHedefiGun <= 0 ? "IncelemeSuresiHedefiGun sıfırdan büyük olmalı."
        : KararIcinGerekenGorusSayisi <= 0 ? "KararIcinGerekenGorusSayisi sıfırdan büyük olmalı."
        : null;
}

/// <summary>Etik Kurul Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class EtikKurulOtomasyonu
{
    public const string Ad = "Etik Kurul Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Etik başvurusu", "/Akademisyen/Basvuru", "Proje, araştırma türü, katılımcılar ve veri toplama yöntemiyle başvuru.", Roller.Akademisyen),
        new("Başvurularım", "/Akademisyen/Basvurularim", "Etik başvurularının durumu ve aşamaları.", Roller.Akademisyen),
        new("Atanan dosyalar", "/Kurul/AtananDosyalar", "Görüşün istenen başvurular.", Roller.EtikKurulUyesi),
        new("Görüş", "/Kurul/Gorus", "Değerlendirmedeki dosyalar için görüş ve gerekçe.", Roller.EtikKurulUyesi),
        new("Karar listesi", "/Baskan/KararListesi", "Bölümündeki başvurular için kurul kararları.", Roller.BolumBaskani),
    ];

    public static readonly IReadOnlyList<string> Asamalar = ["Başvuru", "Ön inceleme", "Kurul değerlendirmesi", "Karar"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "EK";
        secenekler.Aciklama = "Araştırmalar için etik kurul başvurusu, kurul üyelerinin görüşleri ve kurul kararları.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IEtikKurulVeriKaynagi, MockEtikKurulVeriKaynagi, ApiEtikKurulVeriKaynagi>();
        secenekler.Ayarlar<EtikKurulSartlari>(EtikKurulSartlari.Bolum);
    }

    public static string TurAdi(ArastirmaTuru tur) => tur switch
    {
        ArastirmaTuru.Anket => "Anket",
        ArastirmaTuru.Gorusme => "Görüşme",
        ArastirmaTuru.Deney => "Deney",
        ArastirmaTuru.IkincilVeri => "İkincil veri",
        _ => tur.ToString(),
    };

    public static DurumRozeti Rozet(EtikDurumu durum) => durum switch
    {
        OnIncelemede => new("Ön incelemede", DurumTuru.Bekliyor),
        KurulDegerlendirmesinde => new("Kurul değerlendirmesinde", DurumTuru.Bekliyor),
        DuzeltmeIstendi => new("Düzeltme istendi", DurumTuru.Bilgi),
        Onaylandi => new("Onaylandı", DurumTuru.Olumlu),
        Reddedildi => new("Reddedildi", DurumTuru.Olumsuz),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti GorusRozeti(GorusTuru? gorus) => gorus switch
    {
        GorusTuru.Uygun => new("Uygun", DurumTuru.Olumlu),
        GorusTuru.DuzeltmeGerekli => new("Düzeltme gerekli", DurumTuru.Bilgi),
        GorusTuru.UygunDegil => new("Uygun değil", DurumTuru.Olumsuz),
        _ => new("Görüş bekleniyor", DurumTuru.Bekliyor),
    };

    public static string GorusAdi(GorusTuru gorus) => GorusRozeti(gorus).Ad;

    /// <summary>Aşamalar; düzeltme istenen başvuru yeniden kurul değerlendirmesine döner, karar verilende hepsi tamam.</summary>
    public static BasvuruAdimlari Adimlar(EtikDurumu durum) => BasvuruAdimlari.Olustur(Asamalar, durum switch
    {
        OnIncelemede => 1,
        KurulDegerlendirmesinde or DuzeltmeIstendi => 2,
        _ => Asamalar.Count,
    }, "Etik başvurusunun aşamaları");

    /// <summary>Başvurudan karara geçen gün (karar yoksa null).</summary>
    public static int? KararSuresi(EtikBasvurusu b) => b.KararTarihi is { } k ? k.DayNumber - b.BasvuruTarihi.DayNumber : null;

    public static string Tarih(DateOnly? tarih) => tarih?.ToString("d MMMM yyyy", Tr) ?? "—";

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
