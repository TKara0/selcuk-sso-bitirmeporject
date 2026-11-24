namespace Selcuk.Istemci.Ortak;

/// <summary>
/// Alt uygulamaların "Uygulamalarım" bağlantısı için kimlik sunucusunun kök adresi. Adres yalnızca "Kimlik:Authority"
/// ayarından türetilir (kullanıcı girdisinden adres türetilmez); ayar https veya http://127.0.0.1 değilse
/// (veya kullanıcı bilgisi içeriyorsa) null olur ve bağlantı gösterilmez. AddSelcukSSO bunu servis olarak kaydeder;
/// Authority kullanım anında okunur.
/// </summary>
public sealed record KimlikSunucusuBilgisi(string? Adres)
{
    public static string? Koku(string? authority) =>
        Uri.TryCreate(authority, UriKind.Absolute, out var adres) && string.IsNullOrEmpty(adres.UserInfo)
        && (adres.Scheme == Uri.UriSchemeHttps || (adres.Scheme == Uri.UriSchemeHttp && adres.Host == "127.0.0.1"))
            ? adres.GetLeftPart(UriPartial.Authority) + "/"
            : null;
}
