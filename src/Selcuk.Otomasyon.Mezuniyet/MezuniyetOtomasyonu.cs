using System.Globalization;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;
using Selcuk.Otomasyon.Mezuniyet.Veri;
using Selcuk.Otomasyon.Ortak;
using static Selcuk.Otomasyon.Mezuniyet.Sozlesme.MezuniyetDurumu;

namespace Selcuk.Otomasyon.Mezuniyet;

/// <summary>
/// Mezuniyet şartları (appsettings "MezuniyetSartlari"; değerler örnektir, koda gömülmez): gereken toplam AKTS ve GNO
/// alt sınırı. Zorunlu derslerin ve stajın tamamlanması da aranır.
/// </summary>
public sealed class MezuniyetSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "MezuniyetSartlari";

    public int GerekenAkts { get; set; } = 240;

    public decimal GnoAltSiniri { get; set; } = 2.00m;

    public string? Hata() =>
        GerekenAkts <= 0 ? "GerekenAkts sıfırdan büyük olmalı."
        : GnoAltSiniri is < 0 or > 4 ? "GnoAltSiniri 0 ile 4 arasında olmalı."
        : null;
}

/// <summary>Bir adayın şartlara göre kontrolü: eksikler listesi boşsa mezuniyete uygundur.</summary>
public sealed record MezuniyetKontrolu(
    bool AktsTamam, bool GnoTamam, IReadOnlyList<ZorunluDers> EksikDersler, bool StajTamam, IReadOnlyList<string> Eksikler)
{
    public bool Uygun => Eksikler.Count == 0;

    public static MezuniyetKontrolu Yap(MezuniyetAdayi aday, MezuniyetSartlari sartlar)
    {
        var eksikDersler = aday.ZorunluDersler.Where(d => !d.Tamamlandi).ToList();
        var eksikler = new List<string>();
        if (aday.ToplamAkts < sartlar.GerekenAkts)
        {
            eksikler.Add($"{sartlar.GerekenAkts - aday.ToplamAkts} AKTS eksik ({aday.ToplamAkts} / {sartlar.GerekenAkts}).");
        }

        if (aday.Gno < sartlar.GnoAltSiniri)
        {
            eksikler.Add($"GNO {MezuniyetOtomasyonu.Gno(aday.Gno)}, en az {MezuniyetOtomasyonu.Gno(sartlar.GnoAltSiniri)} olmalı.");
        }

        eksikler.AddRange(eksikDersler.Select(d => $"Zorunlu ders tamamlanmadı: {d.Kod} {d.Ad}."));
        if (aday.Staj != StajSonucu.Tamamlandi)
        {
            eksikler.Add(aday.Staj == StajSonucu.DevamEdiyor ? "Staj devam ediyor." : "Zorunlu staj tamamlanmadı.");
        }

        return new MezuniyetKontrolu(aday.ToplamAkts >= sartlar.GerekenAkts, aday.Gno >= sartlar.GnoAltSiniri, eksikDersler,
            aday.Staj == StajSonucu.Tamamlandi, eksikler);
    }
}

/// <summary>Mezuniyet Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class MezuniyetOtomasyonu
{
    public const string Ad = "Mezuniyet Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Mezuniyet durumum", "/Ogrenci/Durumum", "AKTS, GNO, zorunlu dersler, staj ve eksikler.", Roller.Ogrenci),
        new("Mezuniyet başvurusu", "/Ogrenci/Basvuru", "Başvuru formu ve başvurunun aşamaları.", Roller.Ogrenci),
        new("İlişik kesme", "/Ogrenci/IlisikKesme", "Kütüphane, bölüm ve laboratuvar gibi birimlerin durumu.", Roller.Ogrenci),
        new("Mezuniyet adayları", "/Komisyon/Adaylar", "Adayların kontrol sonuçları; onayla veya eksik bildir.", Roller.MezuniyetKomisyonu),
        new("Dekanlığa gidecek liste", "/Sekreter/DekanlikListesi", "Komisyonca onaylanan, dekanlığa gönderilecek mezunlar.", Roller.BolumSekreteri),
        new("Gelen mezuniyet listeleri", "/Fakulte/GelenListeler", "Bölümlerden gelen listeler ve diploma hazırlık durumu.", Roller.FakulteSekreteri),
    ];

    public static readonly IReadOnlyList<string> Asamalar =
        ["Başvuru", "Komisyon kontrolü", "Bölüm onayı", "Dekanlık", "Diploma hazırlığı"];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "ME";
        secenekler.Aciklama = "Mezuniyet şartlarının kontrolü, mezuniyet başvurusu, ilişik kesme ve diploma hazırlığı.";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IMezuniyetVeriKaynagi, MockMezuniyetVeriKaynagi, ApiMezuniyetVeriKaynagi>();
        secenekler.Ayarlar<MezuniyetSartlari>(MezuniyetSartlari.Bolum);
    }

    public static DurumRozeti Rozet(MezuniyetDurumu durum) => durum switch
    {
        BasvuruYapilmadi => new("Başvuru yapılmadı", DurumTuru.Bilgi),
        KomisyonKontrolunde => new("Komisyon kontrolünde", DurumTuru.Bekliyor),
        EksikBildirildi => new("Eksik bildirildi", DurumTuru.Olumsuz),
        BolumOnayinda => new("Bölüm onayında", DurumTuru.Bekliyor),
        Dekanlikta => new("Dekanlıkta", DurumTuru.Bekliyor),
        DiplomaHazirlaniyor => new("Diploma hazırlanıyor", DurumTuru.Bekliyor),
        DiplomaHazir => new("Diploma hazır", DurumTuru.Olumlu),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static DurumRozeti StajRozeti(StajSonucu staj) => staj switch
    {
        StajSonucu.Tamamlandi => new("Staj tamamlandı", DurumTuru.Olumlu),
        StajSonucu.DevamEdiyor => new("Staj devam ediyor", DurumTuru.Bekliyor),
        _ => new("Staj eksik", DurumTuru.Olumsuz),
    };

    public static DurumRozeti SartRozeti(bool tamam, string tamamMetni = "Karşılıyor", string eksikMetni = "Karşılamıyor") =>
        tamam ? new(tamamMetni, DurumTuru.Olumlu) : new(eksikMetni, DurumTuru.Olumsuz);

    public static DurumRozeti BirimRozeti(BirimDurumu durum) => durum switch
    {
        BirimDurumu.Tamam => new("İlişiği yok", DurumTuru.Olumlu),
        BirimDurumu.Borclu => new("Borcu var", DurumTuru.Olumsuz),
        _ => new("Onay bekliyor", DurumTuru.Bekliyor),
    };

    /// <summary>Aşamalar; diploması hazır adayda hepsi tamamlanmıştır.</summary>
    public static BasvuruAdimlari Adimlar(MezuniyetDurumu durum) => BasvuruAdimlari.Olustur(Asamalar, durum switch
    {
        BasvuruYapilmadi => 0,
        KomisyonKontrolunde or EksikBildirildi => 1,
        BolumOnayinda => 2,
        Dekanlikta => 3,
        DiplomaHazirlaniyor => 4,
        _ => Asamalar.Count,
    }, "Mezuniyet başvurusunun aşamaları");

    public static string Gno(decimal gno) => gno.ToString("0.00", Tr);

    public static string Tarih(DateOnly? tarih) => tarih?.ToString("d MMMM yyyy", Tr) ?? "—";

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
