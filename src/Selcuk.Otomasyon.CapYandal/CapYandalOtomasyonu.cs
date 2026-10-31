using System.Globalization;
using Selcuk.Otomasyon.CapYandal.Sozlesme;
using Selcuk.Otomasyon.CapYandal.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.CapYandal.Sozlesme.CapDurumu;

namespace Selcuk.Otomasyon.CapYandal;

/// <summary>Bir program türünün şartları: başvuru GNO alt sınırı, başarı sırası (ilk yüzde), devam GNO'su, kontenjan.</summary>
public sealed class ProgramSarti
{
    public decimal BasvuruGnoAltSiniri { get; set; }

    /// <summary>Başarı sırası sınıfın ilk yüzde kaçında olmalı; null ise aranmaz.</summary>
    public int? BasariYuzdesiUstSiniri { get; set; }

    /// <summary>Programa devam için GNO alt sınırı (öğrenci takibindeki uyarı).</summary>
    public decimal DevamGnoAltSiniri { get; set; }

    /// <summary>Her programın kontenjanı.</summary>
    public int Kontenjan { get; set; }

    internal string? Hata(string ad) =>
        BasvuruGnoAltSiniri is < 0 or > 4 || DevamGnoAltSiniri is < 0 or > 4 ? $"{ad}: GNO sınırları 0 ile 4 arasında olmalı."
        : BasariYuzdesiUstSiniri is < 1 or > 100 ? $"{ad}: BasariYuzdesiUstSiniri 1 ile 100 arasında olmalı."
        : Kontenjan <= 0 ? $"{ad}: Kontenjan sıfırdan büyük olmalı."
        : null;
}

/// <summary>ÇAP ve yandal şartları (appsettings "CapYandalSartlari"; değerler örnektir, koda gömülmez).</summary>
public sealed class CapYandalSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "CapYandalSartlari";

    public ProgramSarti Cap { get; set; } = new() { BasvuruGnoAltSiniri = 3.00m, BasariYuzdesiUstSiniri = 20, DevamGnoAltSiniri = 2.50m, Kontenjan = 4 };

    public ProgramSarti Yandal { get; set; } = new() { BasvuruGnoAltSiniri = 2.50m, DevamGnoAltSiniri = 2.00m, Kontenjan = 6 };

    public ProgramSarti Sart(ProgramTuru tur) => tur == ProgramTuru.Cap ? Cap : Yandal;

    public string? Hata() => Cap.Hata("Cap") ?? Yandal.Hata("Yandal");
}

/// <summary>Öğrencinin bir program türünün başvuru şartlarını karşılayıp karşılamadığı.</summary>
public sealed record SartDurumu(bool GnoTamam, bool? SiraTamam, decimal BasariYuzdesi)
{
    public bool Uygun => GnoTamam && SiraTamam != false;

    public static SartDurumu Hesapla(decimal gno, int sira, int mevcut, ProgramSarti sart)
    {
        var yuzde = mevcut > 0 ? Math.Round(100m * sira / mevcut, 1) : 100m;
        return new SartDurumu(gno >= sart.BasvuruGnoAltSiniri,
            sart.BasariYuzdesiUstSiniri is { } ust ? yuzde <= ust : null, yuzde);
    }
}

/// <summary>ÇAP-Yandal Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class CapYandalOtomasyonu
{
    public const string Ad = "ÇAP-Yandal Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Başvuru", "/Ogrenci/Basvuru", "Program türü ve programla başvuru; şartları karşılıyor musun.", Roller.Ogrenci),
        new("Başvurularım", "/Ogrenci/Basvurularim", "Başvurularının durumu ve aşamaları.", Roller.Ogrenci),
        new("Programdaki ilerlemem", "/Ogrenci/Ilerleme", "İkinci programda alınan ve kalan dersler, dönemlik GNO.", Roller.Ogrenci),
        new("Gelen başvurular", "/Koordinator/GelenBasvurular", "GNO'ya göre sıralı başvurular ve kontenjan.", Roller.CapYandalKoordinatoru),
        new("Öğrenci takibi", "/Koordinator/OgrenciTakibi", "Programdaki öğrenciler; GNO'su şartın altına düşenler.", Roller.CapYandalKoordinatoru),
        new("Başvuru onayı", "/Baskan/BasvuruOnayi", "Koordinatörün sıraladığı başvuruları onayla.", Roller.BolumBaskani),
        new("Evrak", "/Sekreter/Evrak", "Kesin kayıt evraklarının durumu.", Roller.BolumSekreteri),
    ];

    public static readonly IReadOnlyList<string> Asamalar =
        ["Başvuru", "Koordinatör değerlendirmesi", "Bölüm başkanı onayı", "Kesin kayıt"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "ÇY";
        secenekler.Aciklama = "Çift anadal (ÇAP) ve yandal başvuruları, koordinatör sıralaması, bölüm onayı ve programdaki ilerleme.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<ICapYandalVeriKaynagi, MockCapYandalVeriKaynagi, ApiCapYandalVeriKaynagi>();
        secenekler.Ayarlar<CapYandalSartlari>(CapYandalSartlari.Bolum);
    }

    public static string TurAdi(ProgramTuru tur) => tur == ProgramTuru.Cap ? "ÇAP" : "Yandal";

    public static DurumRozeti Rozet(CapDurumu durum) => durum switch
    {
        KoordinatorDegerlendirmesinde => new("Koordinatör değerlendirmesinde", DurumTuru.Bekliyor),
        BaskanOnayinda => new("Bölüm başkanı onayında", DurumTuru.Bekliyor),
        KesinKayitBekliyor => new("Kesin kayıt bekliyor", DurumTuru.Bilgi),
        KesinKayitYapildi => new("Kesin kayıt yapıldı", DurumTuru.Olumlu),
        Reddedildi => new("Reddedildi", DurumTuru.Olumsuz),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti SartRozeti(bool? tamam) => tamam switch
    {
        true => new("Karşılıyor", DurumTuru.Olumlu),
        false => new("Karşılamıyor", DurumTuru.Olumsuz),
        null => new("Aranmıyor", DurumTuru.Bilgi),
    };

    public static DurumRozeti EvrakRozeti(bool teslim) =>
        teslim ? new("Teslim edildi", DurumTuru.Olumlu) : new("Eksik", DurumTuru.Olumsuz);

    /// <summary>Aşamalar; kesin kaydı yapılan ve reddedilen başvuruda hepsi tamamlanmıştır.</summary>
    public static BasvuruAdimlari Adimlar(CapDurumu durum) => BasvuruAdimlari.Olustur(Asamalar, durum switch
    {
        KoordinatorDegerlendirmesinde => 1,
        BaskanOnayinda => 2,
        KesinKayitBekliyor => 3,
        _ => Asamalar.Count,
    }, "ÇAP veya yandal başvurusunun aşamaları");

    public static string Gno(decimal gno) => gno.ToString("0.00", Tr);

    public static string Tarih(DateOnly tarih) => tarih.ToString("d MMMM yyyy", Tr);

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
