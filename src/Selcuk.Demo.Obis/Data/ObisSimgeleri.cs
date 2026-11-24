namespace Selcuk.Demo.Obis.Data;

/// <summary>
/// Demo OBİS'in elle çizilmiş yerel SVG simgeleri (20 × 20, yalnızca çizgi, currentColor, dekoratif: aria-hidden).
/// Harici simge kütüphanesi, yazı tipi veya görsel yoktur; anlam her zaman yanındaki metinle verilir.
/// </summary>
public static class ObisSimgeleri
{
    private const string Bas =
        """<svg class="obis-simge" viewBox="0 0 20 20" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">""";

    private const string Son = "</svg>";

    public const string Anasayfa = Bas + """<path d="M3 9.5 10 3.5l7 6M5 8.5V16h10V8.5M8.5 16v-4.5h3V16"/>""" + Son;

    public const string OnayTakip = Bas + """<rect x="4.5" y="3.5" width="11" height="13" rx="1.5"/><path d="M8 3.5h4v2H8zM7.5 10.5l2 2 3.5-4"/>""" + Son;

    public const string SonYilNotlari = Bas + """<path d="M5.5 2.5h6L15 6v11.5H5.5z"/><path d="M11.5 2.5V6H15M8 10h4M8 13h4"/>""" + Son;

    public const string NotDurumu = Bas + """<path d="M3.5 16.5h13M6 16.5v-5M10 16.5v-9M14 16.5v-7"/>""" + Son;

    public const string Transkript = Bas + """<path d="M10 3 2.5 7 10 11l7.5-4zM5.5 9v4c0 1 2 2.5 4.5 2.5s4.5-1.5 4.5-2.5V9"/>""" + Son;

    public const string HarcDurumu = Bas + """<rect x="2.5" y="5" width="15" height="10" rx="1.5"/><path d="M2.5 8.5h15M5.5 12h3"/>""" + Son;

    public const string SinavTarihleri = Bas + """<rect x="3" y="4.5" width="14" height="12" rx="1.5"/><path d="M3 8h14M7 3v3M13 3v3"/>""" + Son;

    public const string UzaktanEgitim = Bas + """<rect x="2.5" y="4" width="15" height="10" rx="1.5"/><path d="M7 17h6M10 14v3M8.5 7.5l3 1.5-3 1.5z"/>""" + Son;

    public const string UzaktanSinav = Bas + """<rect x="2.5" y="4" width="15" height="10" rx="1.5"/><path d="M7 17h6M10 14v3M7.5 9l2 2 3-3.5"/>""" + Son;

    public const string YkkKarari = Bas + """<path d="M10 3v13M6 16.5h8M4 6.5h12M4 6.5l-2 5a2.5 2 0 0 0 4 0zM16 6.5l-2 5a2.5 2 0 0 0 4 0z"/>""" + Son;

    public const string DersYonetimi = Bas + """<path d="M4 3.5h9.5A1.5 1.5 0 0 1 15 5v11.5H5.5A1.5 1.5 0 0 1 4 15zM4 15a1.5 1.5 0 0 1 1.5-1.5H15"/>""" + Son;

    /// <summary>Bilgi tablosunda Fakülte ve Bölüm değerlerinin önündeki küçük konum simgesi.</summary>
    public const string Konum =
        """<svg class="obis-konum" viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><path d="M10 17s5-4.5 5-8.5a5 5 0 0 0-10 0C5 12.5 10 17 10 17z"/><circle cx="10" cy="8.5" r="1.8"/></svg>""";

    public const string Eposta =
        """<svg class="obis-konum" viewBox="0 0 20 20" width="16" height="16" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><rect x="2.5" y="4.5" width="15" height="11" rx="1.5"/><path d="m3 5.5 7 5.5 7-5.5"/></svg>""";
}
