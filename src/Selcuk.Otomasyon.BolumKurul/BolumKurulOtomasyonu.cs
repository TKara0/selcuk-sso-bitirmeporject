using System.Globalization;
using Selcuk.Otomasyon.BolumKurul.Sozlesme;
using Selcuk.Otomasyon.BolumKurul.Veri;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.BolumKurul;

/// <summary>
/// Toplantı şartları (appsettings "ToplantiSartlari"; değerler örnektir, gerçek yönetmelik değildir): toplantı yeter
/// sayısı (katılan, üye tamsayısının bu oranından fazla olmalı) ve karar çoğunluğu (kabul oyu, katılanların bu
/// oranından fazla olmalı).
/// </summary>
public sealed class ToplantiSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "ToplantiSartlari";

    public decimal YeterSayiOrani { get; set; } = 0.5m;

    public decimal KararCogunluguOrani { get; set; } = 0.5m;

    public bool YeterSayiVar(Toplanti t) => t.KatilanSayisi > t.UyeSayisi * YeterSayiOrani;

    /// <summary>Yeter sayı için en az kaç üye katılmalı.</summary>
    public int GerekenKatilim(Toplanti t) => (int)Math.Floor(t.UyeSayisi * YeterSayiOrani) + 1;

    public bool KabulCogunluguVar(GundemMaddesi m, int katilan) => m.Kabul > katilan * KararCogunluguOrani;

    public string? Hata() =>
        YeterSayiOrani is <= 0 or >= 1 ? "YeterSayiOrani 0 ile 1 arasında olmalı."
        : KararCogunluguOrani is <= 0 or >= 1 ? "KararCogunluguOrani 0 ile 1 arasında olmalı."
        : null;
}

/// <summary>Bölüm Kurul Karar Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class BolumKurulOtomasyonu
{
    public const string Ad = "Bölüm Kurul Karar Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Toplantı ve gündem", "/Baskan/Toplantilar", "Toplantı listesi, yeni toplantı ve gündem maddesi.", Roller.BolumBaskani),
        new("Gündem", "/Uye/Gundem", "Planlanan toplantıların gündem maddeleri.", Roller.BolumKuruluUyesi),
        new("Oylama", "/Uye/Oylama", "Oylamaya açık maddeler, oy sayıları ve yeter sayı.", Roller.BolumKuruluUyesi),
        new("Tutanak ve arşiv", "/Sekreter/Arsiv", "Kararlar ve toplantı tutanakları.", Roller.BolumSekreteri),
        new("Gelen kararlar", "/Fakulte/GelenKararlar", "Fakültedeki bölümlerin kurul kararları, bölümlere göre.", Roller.FakulteSekreteri),
    ];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "BK";
        secenekler.Aciklama = "Bölüm kurulu toplantıları, gündem, oylama, karar ve tutanak arşivi.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IBolumKurulVeriKaynagi, MockBolumKurulVeriKaynagi, ApiBolumKurulVeriKaynagi>();
        secenekler.Ayarlar<ToplantiSartlari>(ToplantiSartlari.Bolum);
    }

    public static DurumRozeti Rozet(ToplantiDurumu durum) => durum switch
    {
        ToplantiDurumu.Planlandi => new("Planlandı", DurumTuru.Bekliyor),
        ToplantiDurumu.Yapildi => new("Yapıldı", DurumTuru.Olumlu),
        ToplantiDurumu.Ertelendi => new("Ertelendi", DurumTuru.Bilgi),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti Rozet(MaddeDurumu durum) => durum switch
    {
        MaddeDurumu.Gundemde => new("Gündemde", DurumTuru.Bekliyor),
        MaddeDurumu.OylamaAcik => new("Oylamada", DurumTuru.Bilgi),
        MaddeDurumu.KabulEdildi => new("Kabul edildi", DurumTuru.Olumlu),
        MaddeDurumu.Reddedildi => new("Reddedildi", DurumTuru.Olumsuz),
        MaddeDurumu.Ertelendi => new("Ertelendi", DurumTuru.Bilgi),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static string Tarih(DateOnly tarih) => tarih.ToString("d MMMM yyyy", Tr);

    public static string Oylar(GundemMaddesi m) => $"{m.Kabul} kabul, {m.Ret} ret, {m.Cekimser} çekimser";

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
