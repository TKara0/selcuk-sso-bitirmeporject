using System.Globalization;
using Selcuk.Otomasyon.LisansProjeleri.Sozlesme;
using Selcuk.Otomasyon.LisansProjeleri.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.LisansProjeleri.Sozlesme.ProjeAsamasi;

namespace Selcuk.Otomasyon.LisansProjeleri;

/// <summary>
/// Proje şartları (appsettings "ProjeSartlari"; değerler örnektir, gerçek yönetmelik değildir): hoca başına bir dönemde
/// en fazla proje ve öğrenci başına konu tercihi sayısı.
/// </summary>
public sealed class ProjeSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "ProjeSartlari";

    public int HocaBasinaEnFazlaProje { get; set; } = 3;

    public int OgrenciBasinaTercihSayisi { get; set; } = 3;

    public string? Hata() =>
        HocaBasinaEnFazlaProje <= 0 ? "HocaBasinaEnFazlaProje sıfırdan büyük olmalı."
        : OgrenciBasinaTercihSayisi is <= 0 or > 10 ? "OgrenciBasinaTercihSayisi 1 ile 10 arasında olmalı."
        : null;
}

/// <summary>Lisans Projeleri Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class LisansProjeleriOtomasyonu
{
    public const string Ad = "Lisans Projeleri Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Konu seçimi", "/Ogrenci/KonuSecimi", "Açık proje konuları ve tercih sıran.", Roller.Ogrenci),
        new("Proje aşamalarım", "/Ogrenci/Asamalarim", "Projenin hangi aşamada olduğu.", Roller.Ogrenci),
        new("Konu önerisi", "/Akademisyen/KonuOnerisi", "Yeni proje konusu ve önerdiğin konular.", Roller.Akademisyen),
        new("Değerlendirme", "/Akademisyen/Degerlendirme", "Hocası olduğun projeler, rubrik ve not.", Roller.Akademisyen),
        new("Eşleştirme", "/Baskan/Eslestirme", "Öğrenci-hoca eşleştirmesi ve hoca başına kontenjan.", Roller.BolumBaskani),
    ];

    public static readonly IReadOnlyList<string> Asamalar = ["Konu seçimi", "Önerge", "Ara rapor", "Teslim", "Sunum ve değerlendirme"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "LP";
        secenekler.Aciklama = "Lisans bitirme projelerinde konu önerisi ve seçimi, eşleştirme, aşamalar ve değerlendirme.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<ILisansProjeleriVeriKaynagi, MockLisansProjeleriVeriKaynagi, ApiLisansProjeleriVeriKaynagi>();
        secenekler.Ayarlar<ProjeSartlari>(ProjeSartlari.Bolum);
    }

    public static string AsamaAdi(ProjeAsamasi asama) =>
        asama == Tamamlandi ? "Tamamlandı" : Asamalar[(int)asama];

    public static DurumRozeti Rozet(ProjeAsamasi asama) => asama switch
    {
        KonuSecimi => new("Konu seçiminde", DurumTuru.Bekliyor),
        Tamamlandi => new("Tamamlandı", DurumTuru.Olumlu),
        _ => new(AsamaAdi(asama), DurumTuru.Bilgi),
    };

    public static BasvuruAdimlari Adimlar(ProjeAsamasi asama) =>
        BasvuruAdimlari.Olustur(Asamalar, (int)asama, "Lisans projesinin aşamaları");

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
