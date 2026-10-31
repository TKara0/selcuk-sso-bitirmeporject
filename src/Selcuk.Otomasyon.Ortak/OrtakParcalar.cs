using System.Text;
using System.Text.RegularExpressions;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>Durum rozetinin türü: rengi belirler; anlamı her zaman rozetin metnidir.</summary>
public enum DurumTuru
{
    /// <summary>Bekleyen, süren işlem (nötr).</summary>
    Bekliyor,

    /// <summary>Onaylanan, tamamlanan (uygulama rengi).</summary>
    Olumlu,

    /// <summary>Reddedilen, eksik (kiremit).</summary>
    Olumsuz,

    /// <summary>Yalnızca bilgi (çerçeveli, uygulama rengi).</summary>
    Bilgi,
}

/// <summary>
/// Durum rozeti (_DurumRozeti): durumun adı metin olarak yazılır, renk yalnızca destekler (WCAG 1.4.1). Tür
/// data-durum özniteliğinde de bulunur.
/// </summary>
public sealed record DurumRozeti(string Ad, DurumTuru Tur)
{
    public string Sinif => Tur switch
    {
        DurumTuru.Olumlu => "etiket etiket-uygulama durum-rozeti",
        DurumTuru.Olumsuz => "etiket etiket-tehlike durum-rozeti",
        DurumTuru.Bilgi => "etiket durum-rozeti durum-bilgi",
        _ => "etiket etiket-notr durum-rozeti",
    };

    public string Veri => Tur switch
    {
        DurumTuru.Olumlu => "olumlu",
        DurumTuru.Olumsuz => "olumsuz",
        DurumTuru.Bilgi => "bilgi",
        _ => "bekliyor",
    };
}

public enum AdimDurumu
{
    Tamamlandi,
    SuAn,
    Bekliyor,
}

public sealed record BasvuruAdimi(string Ad, AdimDurumu Durum)
{
    public string DurumAdi => Durum switch
    {
        AdimDurumu.Tamamlandi => "Tamamlandı",
        AdimDurumu.SuAn => "Şu an",
        _ => "Bekliyor",
    };
}

/// <summary>
/// Bir başvurunun aşamaları (_BasvuruAdimlari): sıralı liste (ol/li); şu anki aşama aria-current="step" taşır, her
/// aşamanın durumu metin olarak da yazılır.
/// </summary>
public sealed record BasvuruAdimlari(string Baslik, IReadOnlyList<BasvuruAdimi> Adimlar)
{
    /// <summary>
    /// simdiki: şu anki aşamanın sırası (0'dan). Öncekiler tamamlanmış, sonrakiler bekliyor sayılır; aşama sayısına eşit
    /// veya büyükse bütün aşamalar tamamlanmıştır (aria-current yok).
    /// </summary>
    public static BasvuruAdimlari Olustur(IReadOnlyList<string> adimlar, int simdiki, string baslik = "Başvuru aşamaları") =>
        new(baslik, [.. adimlar.Select((ad, i) => new BasvuruAdimi(ad,
            i < simdiki ? AdimDurumu.Tamamlandi : i == simdiki ? AdimDurumu.SuAn : AdimDurumu.Bekliyor))]);
}

/// <summary>Sayı kartı (_SayiKartlari): başlık ve sayı; Anahtar verilirse data-ozet özniteliği olur.</summary>
public sealed record SayiKarti(string Baslik, string Sayi, string? Anahtar = null)
{
    public SayiKarti(string baslik, int sayi, string? anahtar = null)
        : this(baslik, sayi.ToString(System.Globalization.CultureInfo.InvariantCulture), anahtar)
    {
    }
}

/// <summary>
/// Kayıt tablosunun bir sütunu. Deger metin döndürürse kodlanarak yazılır; <see cref="DurumRozeti"/> rozet olarak,
/// IHtmlContent (ör. Razor şablonu @&lt;text&gt;…&lt;/text&gt;) olduğu gibi yazılır.
/// </summary>
public sealed record KayitSutunu<T>(string Baslik, Func<T, object?> Deger, bool Sayi = false);

public sealed record TabloSutunu(string Baslik, bool Sayi);

public sealed record KayitSatiri(string? Kimlik, IReadOnlyList<object?> Hucreler);

/// <summary>
/// Kayıt tablosu (_KayitTablosu): başlık (h2 veya h3), kayıt yoksa "Kayıt yok." metni, varsa dar ekranda yatay
/// kaydırılan tablo (kaydırma alanı klavyeyle de odaklanır). İlk sütun satır başlığıdır. VeriAdi verilirse her satır
/// data-&lt;VeriAdi&gt;="&lt;kimlik&gt;" taşır (testler kapsamı bununla denetler).
/// </summary>
public sealed partial record KayitTablosu
{
    public const string VarsayilanBosMetin = "Kayıt yok.";

    public required string Baslik { get; init; }

    /// <summary>Başlığın id'si (tablonun aria-labelledby'ı); verilmezse başlıktan türetilir.</summary>
    public string? Kimlik { get; init; }

    /// <summary>2 veya 3.</summary>
    public int BaslikDuzeyi { get; init; } = 2;

    public string? VeriAdi { get; init; }

    public string BosMetin { get; init; } = VarsayilanBosMetin;

    public required IReadOnlyList<TabloSutunu> Sutunlar { get; init; }

    public required IReadOnlyList<KayitSatiri> Satirlar { get; init; }

    public string BaslikKimligi => Kimlik ?? "tablo-" + Kisalt(Baslik);

    public static KayitTablosu Olustur<T>(string baslik, IEnumerable<T> kayitlar, Func<T, string>? kimlik, string? veriAdi,
        params KayitSutunu<T>[] sutunlar)
    {
        if (veriAdi is not null && !VeriAdiKurali().IsMatch(veriAdi))
        {
            throw new ArgumentException("Veri adı küçük harf ve tire olmalı.", nameof(veriAdi));
        }

        return new KayitTablosu
        {
            Baslik = baslik,
            VeriAdi = veriAdi,
            Sutunlar = [.. sutunlar.Select(s => new TabloSutunu(s.Baslik, s.Sayi))],
            Satirlar = [.. kayitlar.Select(k => new KayitSatiri(kimlik?.Invoke(k), [.. sutunlar.Select(s => s.Deger(k))]))],
        };
    }

    private static string Kisalt(string metin)
    {
        var sb = new StringBuilder();
        foreach (var c in metin.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
            }
            else if (c == 'ı')
            {
                sb.Append('i');
            }
            else if (char.IsWhiteSpace(c) || c == '-')
            {
                sb.Append('-');
            }
        }

        return sb.ToString().Trim('-');
    }

    [GeneratedRegex("^[a-z][a-z-]*$")]
    private static partial Regex VeriAdiKurali();
}
