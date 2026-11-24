namespace Selcuk.Otomasyon.Ortak;

/// <summary>
/// Sol menünün simge kataloğu: 24x24 çizgi simgeler (elle çizilmiş, kütüphane ve dış kaynak yok). Sayfaya
/// <c>_MenuSimgeleri</c> ile tek bir gizli SVG sprite olarak yazılır (<c>&lt;symbol id="ms-ad"&gt;</c>), menü
/// öğeleri <c>&lt;use href="#ms-ad"&gt;</c> ile kullanır. Çizgi rengi ve kalınlığı otomasyon.css'tedir; yollar
/// yalnızca bu koddan gelir (kullanıcı girdisi değil).
/// </summary>
public static class MenuSimgeleri
{
    /// <summary>Simgesi olmayan veya adı katalogda bulunmayan öğenin simgesi.</summary>
    public const string Varsayilan = "nokta";

    private static readonly (string Ad, string Yol)[] Katalog =
    [
        ("ana-sayfa", """<path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10v9.5h13V10"/><path d="M10 19.5v-5h4v5"/>"""),
        ("form", """<rect x="5" y="4.5" width="14" height="16" rx="2"/><path d="M9 4.5h6V7H9z"/><path d="M8.5 11.5h7M8.5 15h5"/>"""),
        ("liste", """<path d="M9 7h11M9 12h11M9 17h11"/><path d="M4.75 7h.01M4.75 12h.01M4.75 17h.01"/>"""),
        ("onay", """<circle cx="12" cy="12" r="8.5"/><path d="m8 12.3 2.8 2.8 5.4-5.6"/>"""),
        ("takvim", """<rect x="4" y="5.5" width="16" height="14.5" rx="2"/><path d="M4 10h16M8.5 3.5v4M15.5 3.5v4"/>"""),
        ("belge", """<path d="M6.5 3.5H14l4 4v13H6.5z"/><path d="M14 3.5v4h4"/><path d="M9.5 12.5h5M9.5 16h5"/>"""),
        ("arsiv", """<rect x="3.5" y="4.5" width="17" height="4.5" rx="1"/><path d="M5 9v10.5h14V9"/><path d="M10 13h4"/>"""),
        ("kullanicilar", """<circle cx="9" cy="8.5" r="3.25"/><path d="M3 19.5c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5"/><path d="M15.5 5.5a3 3 0 0 1 0 6"/><path d="M17.5 14.5c2 .6 3.5 2.2 3.5 5"/>"""),
        ("grafik", """<path d="M4 4v16h16"/><path d="M8.5 16v-4M12.5 16V8.5M16.5 16v-6.5"/>"""),
        ("rapor", """<path d="M6.5 3.5h11v17h-11z"/><path d="m9 15 2.2-2.6 2 1.6L16 10.5"/>"""),
        ("bilgi", """<circle cx="12" cy="12" r="8.5"/><path d="M12 11v5.5"/><path d="M12 7.9h.01"/>"""),
        ("kurul", """<path d="M3.5 9 12 4l8.5 5z"/><path d="M6 11.5v6M10 11.5v6M14 11.5v6M18 11.5v6"/><path d="M3.5 20h17"/>"""),
        ("defter", """<path d="M12 7c-1.6-1.4-4-2-7.5-1.9v12.4c3.5-.1 5.9.5 7.5 1.9 1.6-1.4 4-2 7.5-1.9V5.1C16 5 13.6 5.6 12 7z"/><path d="M12 7v12.4"/>"""),
        ("arama", """<circle cx="10.5" cy="10.5" r="6"/><path d="m15 15 5 5"/>"""),
        ("eslestirme", """<path d="M4 8h12"/><path d="m13 5 3 3-3 3"/><path d="M20 16H8"/><path d="m11 13-3 3 3 3"/>"""),
        ("kontrol", """<path d="m4 7 1.5 1.5L8.5 5.5"/><path d="m4 15.5 1.5 1.5 3-3"/><path d="M12 7h8M12 16h8"/>"""),
        (Varsayilan, """<circle cx="12" cy="12" r="3"/>"""),
    ];

    private static readonly IReadOnlyDictionary<string, string> Yollar =
        Katalog.ToDictionary(k => k.Ad, k => k.Yol, StringComparer.Ordinal);

    /// <summary>Kataloğun simge adları (varsayılan nokta dahil).</summary>
    public static IReadOnlyList<string> Adlar { get; } = [.. Katalog.Select(k => k.Ad)];

    /// <summary>Sprite'a yazılacak simgeler: ad ve çizim yolu.</summary>
    public static IReadOnlyList<(string Ad, string Yol)> Semboller => Katalog;

    /// <summary>Simge adı katalogda var mı.</summary>
    public static bool Var(string? ad) => ad is not null && Yollar.ContainsKey(ad);

    /// <summary>Verilen ad katalogdaysa kendisi, değilse varsayılan.</summary>
    public static string Gecerli(string? ad) => Var(ad) ? ad! : Varsayilan;

    /// <summary>Sprite içindeki sembolün kimliği (ör. "ms-form").</summary>
    public static string SembolKimligi(string? ad) => "ms-" + Gecerli(ad);
}
