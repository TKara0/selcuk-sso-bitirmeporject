using System.Globalization;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;
using Selcuk.Otomasyon.Danismanlik.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.Danismanlik.Sozlesme.TalepDurumu;

namespace Selcuk.Otomasyon.Danismanlik;

/// <summary>
/// Danışmanlık şartları (appsettings "DanismanlikSartlari"; değerler örnektir, gerçek yönetmelik değildir): danışman
/// başına en fazla öğrenci ve iki görüşme arasında hedeflenen en fazla gün.
/// </summary>
public sealed class DanismanlikSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "DanismanlikSartlari";

    public int DanismanBasinaEnFazlaOgrenci { get; set; } = 5;

    public int GorusmeAraligiHedefiGun { get; set; } = 120;

    /// <summary>Son görüşmeden bu yana hedef aşıldı mı (hiç görüşme yoksa aşılmış sayılır).</summary>
    public bool GorusmeGecikti(DanismanlikOgrencisi o, DateOnly bugun) =>
        o.SonGorusme is not { } son || bugun.DayNumber - son.DayNumber > GorusmeAraligiHedefiGun;

    public string? Hata() =>
        DanismanBasinaEnFazlaOgrenci <= 0 ? "DanismanBasinaEnFazlaOgrenci sıfırdan büyük olmalı."
        : GorusmeAraligiHedefiGun <= 0 ? "GorusmeAraligiHedefiGun sıfırdan büyük olmalı."
        : null;
}

/// <summary>Öğrenci Danışmanlık Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class DanismanlikOtomasyonu
{
    public const string Ad = "Öğrenci Danışmanlık Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Danışmanım", "/Ogrenci/Danismanim", "Danışmanın ve son görüşme.", Roller.Ogrenci),
        new("Görüşme talebi", "/Ogrenci/GorusmeTalebi", "Yeni görüşme talebi ve taleplerinin durumu.", Roller.Ogrenci),
        new("Öğrenci listesi", "/Danisman/Ogrencilerim", "Danışmanı olduğun öğrenciler, AKTS, GNO ve görüşme uyarısı.", Roller.Danisman),
        new("Görüşme notları", "/Danisman/GorusmeNotlari", "Kendi görüşme kayıtların ve yeni not.", Roller.Danisman),
        new("Danışman atama", "/Baskan/DanismanAtama", "Öğrenci-danışman eşleşmeleri ve danışman başına öğrenci.", Roller.BolumBaskani),
    ];

    public static readonly IReadOnlyList<string> Asamalar = ["Talep", "Danışman onayı", "Görüşme yapıldı"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "DA";
        secenekler.Aciklama = "Danışman-öğrenci eşleşmeleri, görüşme talepleri ve görüşme notları.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IDanismanlikVeriKaynagi, MockDanismanlikVeriKaynagi, ApiDanismanlikVeriKaynagi>();
        secenekler.Ayarlar<DanismanlikSartlari>(DanismanlikSartlari.Bolum);
    }

    public static string KonuAdi(GorusmeKonusu konu) => konu switch
    {
        GorusmeKonusu.DersSecimi => "Ders seçimi",
        GorusmeKonusu.AkademikIlerleme => "Akademik ilerleme",
        GorusmeKonusu.StajPlani => "Staj planı",
        GorusmeKonusu.KariyerPlani => "Kariyer planı",
        _ => konu.ToString(),
    };

    public static DurumRozeti Rozet(TalepDurumu durum) => durum switch
    {
        Bekliyor => new("Danışman onayı bekliyor", DurumTuru.Bekliyor),
        Onaylandi => new("Onaylandı", DurumTuru.Bilgi),
        Yapildi => new("Görüşme yapıldı", DurumTuru.Olumlu),
        Reddedildi => new("Reddedildi", DurumTuru.Olumsuz),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti GorusmeRozeti(bool gecikti) =>
        gecikti ? new("Görüşme gecikti", DurumTuru.Olumsuz) : new("Güncel", DurumTuru.Olumlu);

    public static BasvuruAdimlari Adimlar(TalepDurumu durum) => BasvuruAdimlari.Olustur(Asamalar, durum switch
    {
        Bekliyor => 1,
        Onaylandi => 2,
        _ => Asamalar.Count,
    }, "Görüşme talebinin aşamaları");

    public static DateOnly Bugun(TimeProvider zaman) => DateOnly.FromDateTime(zaman.GetLocalNow().DateTime);

    public static string Tarih(DateOnly? tarih) => tarih?.ToString("d MMMM yyyy", Tr) ?? "—";

    public static string Gno(decimal gno) => gno.ToString("0.00", Tr);

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
