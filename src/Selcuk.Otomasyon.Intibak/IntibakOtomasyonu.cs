using System.Globalization;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Intibak.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.Intibak.Sozlesme.IntibakDurumu;

namespace Selcuk.Otomasyon.Intibak;

/// <summary>
/// İntibak şartları (appsettings "IntibakSartlari"; değerler örnektir, koda gömülmez): muafiyet için en düşük harf
/// notu ve bir başvuruda muaf sayılabilecek en fazla AKTS.
/// </summary>
public sealed class IntibakSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "IntibakSartlari";

    /// <summary>Harf notları, yüksekten düşüğe.</summary>
    public static readonly string[] HarfNotlari = ["AA", "BA", "BB", "CB", "CC", "DC", "DD", "FD", "FF"];

    public string MuafiyetIcinEnDusukNot { get; set; } = "CC";

    public int EnFazlaMuafAkts { get; set; } = 120;

    /// <summary>Not, muafiyet için en düşük nota eşit veya ondan yüksek mi.</summary>
    public bool MuafiyeteYeter(string not)
    {
        var sira = Array.IndexOf(HarfNotlari, not);
        return sira >= 0 && sira <= Array.IndexOf(HarfNotlari, MuafiyetIcinEnDusukNot);
    }

    public string? Hata() =>
        !HarfNotlari.Contains(MuafiyetIcinEnDusukNot) ? $"MuafiyetIcinEnDusukNot bir harf notu olmalı ({string.Join(", ", HarfNotlari)})."
        : EnFazlaMuafAkts <= 0 ? "EnFazlaMuafAkts sıfırdan büyük olmalı."
        : null;
}

/// <summary>İntibak Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class IntibakOtomasyonu
{
    public const string Ad = "İntibak Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("İntibak başvurusu", "/Ogrenci/Basvuru", "Geliş türü, önceki kurum ve transkriptle başvuru.", Roller.Ogrenci),
        new("Başvurum", "/Ogrenci/Basvurum", "Başvurunun durumu ve aşamaları.", Roller.Ogrenci),
        new("İntibak sonucu", "/Ogrenci/Sonuc", "Önceki derslerin eşdeğerleri ve muafiyet kararları.", Roller.Ogrenci),
        new("Bekleyen başvurular", "/Komisyon/BekleyenBasvurular", "Komisyon incelemesindeki başvurular.", Roller.IntibakKomisyonu),
        new("Ders eşleştirme", "/Komisyon/DersEslestirme", "Önceki dersleri müfredat dersleriyle eşleştir, muafiyete karar ver.", Roller.IntibakKomisyonu),
        new("Komisyon kararları", "/Komisyon/Kararlar", "Komisyonun verdiği kararlar ve sonraki aşamaları.", Roller.IntibakKomisyonu),
        new("Onay listesi", "/Baskan/OnayListesi", "Komisyondan gelen kararları onayla veya geri gönder.", Roller.BolumBaskani),
        new("İntibak raporları", "/Sekreter/Raporlar", "Bölümdeki intibak kararlarının listesi.", Roller.BolumSekreteri),
        new("Yönetim kurulu evrakı", "/Fakulte/YonetimKuruluEvraki", "Fakülte yönetim kuruluna gidecek kararlar, bölümlere göre.", Roller.FakulteSekreteri),
    ];

    public static readonly IReadOnlyList<string> Asamalar =
        ["Başvuru", "Komisyon incelemesi", "Bölüm başkanı onayı", "Yönetim kurulu", "Sonuçlandı"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "İN";
        secenekler.Aciklama = "Yatay ve dikey geçiş, ek yerleştirme ve af ile gelen öğrencilerin ders intibakı ve muafiyetleri.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IIntibakVeriKaynagi, MockIntibakVeriKaynagi, ApiIntibakVeriKaynagi>();
        secenekler.Ayarlar<IntibakSartlari>(IntibakSartlari.Bolum);
    }

    public static string GelisTuruAdi(GelisTuru tur) => tur switch
    {
        GelisTuru.YatayGecis => "Yatay geçiş",
        GelisTuru.DikeyGecis => "Dikey geçiş",
        GelisTuru.EkYerlestirme => "Ek yerleştirme",
        GelisTuru.Af => "Af",
        _ => tur.ToString(),
    };

    public static DurumRozeti Rozet(IntibakDurumu durum) => durum switch
    {
        KomisyonIncelemesinde => new("Komisyon incelemesinde", DurumTuru.Bekliyor),
        GeriGonderildi => new("Komisyona geri gönderildi", DurumTuru.Bilgi),
        BaskanOnayinda => new("Bölüm başkanı onayında", DurumTuru.Bekliyor),
        YonetimKurulunda => new("Yönetim kurulunda", DurumTuru.Bekliyor),
        Sonuclandi => new("Sonuçlandı", DurumTuru.Olumlu),
        Reddedildi => new("Reddedildi", DurumTuru.Olumsuz),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti MuafiyetRozeti(bool? muaf) => muaf switch
    {
        true => new("Muaf", DurumTuru.Olumlu),
        false => new("Muaf değil", DurumTuru.Olumsuz),
        null => new("Karar bekliyor", DurumTuru.Bekliyor),
    };

    /// <summary>Başvurunun aşamaları; sonuçlanan ve reddedilen başvuruda hepsi tamamlanmıştır.</summary>
    public static BasvuruAdimlari Adimlar(IntibakDurumu durum) => BasvuruAdimlari.Olustur(Asamalar, durum switch
    {
        KomisyonIncelemesinde or GeriGonderildi => 1,
        BaskanOnayinda => 2,
        YonetimKurulunda => 3,
        _ => Asamalar.Count,
    }, "İntibak başvurusunun aşamaları");

    public static int MuafAkts(IntibakBasvurusu basvuru) =>
        basvuru.Dersler.Where(d => d.Muaf == true).Sum(d => d.EsdegerAkts ?? 0);

    public static string Tarih(DateOnly tarih) => tarih.ToString("d MMMM yyyy", Tr);

    public static string Tarih(DateOnly? tarih) => tarih is { } t ? Tarih(t) : "—";

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
