using System.Globalization;
using Selcuk.Otomasyon.Mudek.Sozlesme;
using Selcuk.Otomasyon.Mudek.Veri;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Mudek;

/// <summary>
/// MÜDEK şartları (appsettings "MudekSartlari"; değerler örnektir, gerçek akreditasyon ölçütü değildir): ders çıktısı
/// başarı eşiği ve program çıktısı hedef oranı (bağlı ders çıktılarının ortalaması).
/// </summary>
public sealed class MudekSartlari : IDogrulanabilirAyar
{
    public const string Bolum = "MudekSartlari";

    public decimal DersCiktisiBasariEsigi { get; set; } = 0.6m;

    public decimal ProgramCiktisiHedefOrani { get; set; } = 0.7m;

    /// <summary>Program çıktısının gerçekleşme oranı: bağlı ve ölçülmüş ders çıktılarının ortalaması (veri yoksa null).</summary>
    public static decimal? Gerceklesme(ProgramCiktisi pc, IEnumerable<DersCiktisi> dersCiktilari)
    {
        var olcumler = dersCiktilari.Where(d => d.ProgramCiktilari.Contains(pc.Kod) && d.OlculenOran is not null)
            .Select(d => d.OlculenOran!.Value).ToList();
        return olcumler.Count == 0 ? null : olcumler.Average();
    }

    public DurumRozeti ProgramRozeti(decimal? gerceklesme) =>
        gerceklesme is not { } g ? new("Veri yok", DurumTuru.Bekliyor)
        : g >= ProgramCiktisiHedefOrani ? new("Hedefte", DurumTuru.Olumlu)
        : new("Hedef altında", DurumTuru.Olumsuz);

    public bool DersCiktisiEksik(DersCiktisi d) => d.OlculenOran is not { } o || o < DersCiktisiBasariEsigi;

    public DurumRozeti DersRozeti(DersCiktisi d) =>
        d.OlculenOran is not { } o ? new("Veri girilmedi", DurumTuru.Bekliyor)
        : o < DersCiktisiBasariEsigi ? new("Eşik altında", DurumTuru.Olumsuz)
        : new("Eşikte", DurumTuru.Olumlu);

    public string? Hata() =>
        DersCiktisiBasariEsigi is <= 0 or > 1 ? "DersCiktisiBasariEsigi 0 ile 1 arasında olmalı."
        : ProgramCiktisiHedefOrani is <= 0 or > 1 ? "ProgramCiktisiHedefOrani 0 ile 1 arasında olmalı."
        : null;
}

/// <summary>MÜDEK Otomasyonu'nun adı, menüsü (rol → ekran), veri kaynağı ve görünüm yardımcıları.</summary>
public static class MudekOtomasyonu
{
    public const string Ad = "MÜDEK Otomasyonu";

    public static readonly IReadOnlyList<MenuOgesi> Menu =
    [
        new("Ders çıktısı veri girişi", "/Akademisyen/VeriGirisi", "Kendi derslerinin ders çıktıları için ölçülen oran.", Roller.Akademisyen),
        new("Raporlar", "/Rapor/Donem", "Dönem raporu özeti.", Roller.BolumBaskani, Roller.MudekKomisyonu),
        new("Rapor ve evrak arşivi", "/Sekreter/Arsiv", "MÜDEK raporları ve belgelerinin durumu.", Roller.BolumSekreteri),
        new("Program çıktıları", "/Komisyon/ProgramCiktilari", "Program çıktıları, bağlı ders çıktıları ve gerçekleşme oranı.", Roller.MudekKomisyonu),
        new("Eksikler", "/Komisyon/Eksikler", "Eşik altında veya verisi girilmemiş çıktılar.", Roller.MudekKomisyonu),
    ];

    public static void Ayarla(OtomasyonSecenekleri secenekler)
    {
        secenekler.Ad = Ad;
        secenekler.Simge = "MÜ";
        secenekler.Aciklama = "Program ve ders çıktılarının izlenmesi, eksikler ve dönem raporları (MÜDEK hazırlığı).";
        secenekler.Menu.AddRange(Menu);
        secenekler.VeriKaynagi<IMudekVeriKaynagi, MockMudekVeriKaynagi, ApiMudekVeriKaynagi>();
        secenekler.Ayarlar<MudekSartlari>(MudekSartlari.Bolum);
    }

    public static string TurAdi(BelgeTuru tur) => tur switch
    {
        BelgeTuru.DonemRaporu => "Dönem raporu",
        BelgeTuru.OzDegerlendirme => "Öz değerlendirme",
        BelgeTuru.DersDosyasi => "Ders dosyası",
        BelgeTuru.Tutanak => "Tutanak",
        _ => tur.ToString(),
    };

    public static DurumRozeti BelgeRozeti(BelgeDurumu durum) => durum switch
    {
        BelgeDurumu.Hazirlaniyor => new("Hazırlanıyor", DurumTuru.Bekliyor),
        BelgeDurumu.Tamamlandi => new("Tamamlandı", DurumTuru.Olumlu),
        BelgeDurumu.Arsivlendi => new("Arşivlendi", DurumTuru.Bilgi),
        _ => new(durum.ToString(), DurumTuru.Bilgi),
    };

    public static string Yuzde(decimal? oran) => oran is { } o ? "%" + Math.Round(o * 100).ToString("0", Tr) : "—";

    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
}
