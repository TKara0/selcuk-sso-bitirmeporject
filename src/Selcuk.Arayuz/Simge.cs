namespace Selcuk.Arayuz;

/// <summary>
/// Elle çizilmiş satır içi SVG simgeler (20 × 20, yalnızca çizgi, currentColor). Hepsi dekoratiftir (aria-hidden);
/// anlam her zaman yanındaki metinle verilir. İkon kütüphanesi yoktur.
/// </summary>
public static class Simge
{
    private const string Bas =
        """<svg viewBox="0 0 20 20" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">""";

    private const string Son = "</svg>";

    public const string Bilgi = Bas + """<circle cx="10" cy="10" r="8"/><path d="M10 9v5M10 6.2v.1"/>""" + Son;

    public const string Uyari = Bas + """<path d="M10 2.5 18 17H2Z"/><path d="M10 8v4M10 14.4v.1"/>""" + Son;

    public const string Hata = Bas + """<circle cx="10" cy="10" r="8"/><path d="m7 7 6 6M13 7l-6 6"/>""" + Son;

    public const string Onay = Bas + """<path d="m4 10.5 4 4 8-9"/>""" + Son;

    public const string Daire = Bas + """<circle cx="10" cy="10" r="6"/>""" + Son;

    public const string Goz = Bas + """<path d="M1.5 10S4.5 4 10 4s8.5 6 8.5 6-3 6-8.5 6-8.5-6-8.5-6Z"/><circle cx="10" cy="10" r="2.5"/>""" + Son;

    public const string GozKapali = Bas + """<path d="M3 3l14 14M8.2 4.3A8 8 0 0 1 10 4c5.5 0 8.5 6 8.5 6a14 14 0 0 1-2.3 3M12.6 15.6A7.6 7.6 0 0 1 10 16c-5.5 0-8.5-6-8.5-6a14.2 14.2 0 0 1 3.7-4.3M8.2 8.2a2.5 2.5 0 0 0 3.6 3.6"/>""" + Son;

    public const string Kopyala = Bas + """<rect x="7" y="7" width="10" height="10" rx="1.5"/><path d="M13 7V4.5A1.5 1.5 0 0 0 11.5 3h-7A1.5 1.5 0 0 0 3 4.5v7A1.5 1.5 0 0 0 4.5 13H7"/>""" + Son;

    public const string Indir = Bas + """<path d="M10 3v10M6 9l4 4 4-4M3.5 16.5h13"/>""" + Son;

    public const string AsagiOk = Bas + """<path d="m5 8 5 5 5-5"/>""" + Son;

    public const string Kisi = Bas + """<circle cx="10" cy="7" r="3.5"/><path d="M3.5 17.5a6.5 6.5 0 0 1 13 0"/>""" + Son;
}
