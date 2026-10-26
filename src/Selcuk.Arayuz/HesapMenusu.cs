namespace Selcuk.Arayuz;

/// <summary>Alt uygulamanın hesap menüsündeki bir bağlantı (<c>_IstemciHesapMenusu</c>).</summary>
/// <param name="Metin">Görünen metin.</param>
/// <param name="Adres">Bağlantı adresi.</param>
/// <param name="Gecerli">Geçerli sayfa mı (aria-current="page").</param>
public sealed record HesapMenusuBaglantisi(string Metin, string Adres, bool Gecerli);
