using System.Globalization;
using Selcuk.Otomasyon.Iskur.Sozlesme;
using Selcuk.Otomasyon.Iskur.Veri;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Iskur;

/// <summary>
/// İŞKUR şartları (appsettings "IskurSartlari"; değerler örnektir, gerçek yönetmelik değildir): aylık en fazla devam
/// saati ve devamsızlık uyarı eşiği (devam / planlanan oranı bunun altındaysa uyarı).
/// </summary>
public sealed class IskurSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "IskurSartlari";

    public int AylikEnFazlaDevamSaati { get; set; } = 60;

    public decimal DevamsizlikUyariEsigi { get; set; } = 0.8m;

    public decimal Oran(AylikDevam d) => d.PlanlananSaat > 0 ? (decimal)d.DevamSaati / d.PlanlananSaat : 0m;

    public DurumRozeti Rozet(AylikDevam d) =>
        d.DevamSaati > AylikEnFazlaDevamSaati ? new("Aylık sınır aşıldı", DurumTuru.Olumsuz)
        : Oran(d) < DevamsizlikUyariEsigi ? new("Devamsızlık uyarısı", DurumTuru.Olumsuz)
        : new("Uygun", DurumTuru.Olumlu);

    public string? Hata() =>
        AylikEnFazlaDevamSaati <= 0 ? "AylikEnFazlaDevamSaati sıfırdan büyük olmalı."
        : DevamsizlikUyariEsigi is <= 0 or > 1 ? "DevamsizlikUyariEsigi 0 ile 1 arasında olmalı."
        : null;
}

/// <summary>İŞKUR Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class IskurOtomasyonu
{
    public const string Ad = "İŞKUR Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Devam durumum", "/Ogrenci/DevamDurumum", "Aylık devam kayıtların ve toplamı.", Roller.Ogrenci),
        new("Devam girişi", "/Sorumlu/DevamGirisi", "Öğrenci ve gün seçerek devam saati gir.", Roller.IskurSorumlusu),
        new("Takip", "/Sorumlu/Takip", "Öğrenci başına aylık devam özeti ve uyarılar.", Roller.IskurSorumlusu),
    ];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "İŞ";
        secenekler.Aciklama = "İŞKUR gençlik programındaki öğrencilerin devam girişi ve aylık takibi.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IIskurVeriKaynagi, MockIskurVeriKaynagi, ApiIskurVeriKaynagi>();
        secenekler.Ayarlar<IskurSartlari>(IskurSartlari.Bolum);
    }

    public static DurumRozeti DevamRozeti(DevamDurumu durum) => durum switch
    {
        DevamDurumu.Giriliyor => new("Giriş sürüyor", DurumTuru.Bekliyor),
        DevamDurumu.Onaylandi => new("Onaylandı", DurumTuru.Bilgi),
        DevamDurumu.Odendi => new("Ödendi", DurumTuru.Olumlu),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    /// <summary>"2026-09" → "Eylül 2026".</summary>
    public static string AyAdi(string ay) =>
        DateTime.ParseExact(ay, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMMM yyyy", Tr);

    public static string Yuzde(decimal oran) => "%" + Math.Round(oran * 100).ToString("0", Tr);

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
