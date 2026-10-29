using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;
using Selcuk.Istemci.Ortak;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Tez için token içeriklerini gösterir. Yalnızca Development'ta açılır; imza doğrulaması yapmaz,
/// sadece okunur. (Doğrulamayı oturum açılırken OpenID Connect ara katmanı yapmıştır.) Her claim bir satır: ad, değer
/// (uzunsa kırpılır, tam hâli title'da) ve Türkçe kısa açıklama. Token'ın kendisi ve imzası gösterilmez. Refresh
/// token'ın değeri gösterilmez; yalnızca var olup olmadığı ve son kullanma zamanı.
/// </summary>
[Authorize]
public class TokenIncelemeModel(IWebHostEnvironment environment) : PageModel
{
    /// <summary>Bu uzunluğu aşan değer kırpılır.</summary>
    public const int EnUzunDeger = 40;

    /// <summary>Açıklaması tanımlı olmayan alan için gösterilen metin.</summary>
    public const string AciklamaYok = "Bu alan için açıklama yok";

    private static readonly JsonSerializerOptions Bicim = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions TekSatir = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Kimlik sunucusunun (OpenIddict) id_token ve access_token'da verdiği alanların Türkçe açıklamaları.</summary>
    public static readonly IReadOnlyDictionary<string, string> Aciklamalar = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Başlık (header)
        ["alg"] = "İmza algoritması",
        ["kid"] = "İmzalayan anahtarın kimliği (JWKS'te aranır)",
        ["typ"] = "Token türü",
        ["x5t"] = "İmzalayan sertifikanın parmak izi",
        ["cty"] = "İçerik türü",

        // Kayıtlı claim'ler (RFC 7519, OpenID Connect Core)
        ["iss"] = "Token'ı veren kimlik sunucusu",
        ["sub"] = "Kullanıcının kimlik sunucusundaki değişmez kimliği",
        ["aud"] = "Token'ın geçerli olduğu alıcı",
        ["exp"] = "Son kullanma zamanı",
        ["nbf"] = "Bu zamandan önce geçersiz",
        ["iat"] = "Verildiği zaman",
        ["jti"] = "Token'ın benzersiz kimliği",
        ["azp"] = "Token'ın verildiği istemci",
        ["auth_time"] = "Kullanıcının kimliğini doğruladığı zaman",
        ["nonce"] = "Giriş isteğini bu token'a bağlayan tek kullanımlık değer",
        ["at_hash"] = "Access token'ın özeti (id_token ile aynı yanıttan geldiğini gösterir)",
        ["c_hash"] = "Yetkilendirme kodunun özeti",
        ["sid"] = "Kimlik sunucusundaki oturum",
        ["amr"] = "Giriş yöntemi",
        ["acr"] = "Kimlik doğrulama düzeyi",
        ["scope"] = "Verilen izinler (kapsamlar)",
        ["client_id"] = "Token'ı isteyen istemci",

        // Profil, e-posta ve rol
        ["name"] = "Ad soyad",
        ["preferred_username"] = "Kullanıcı adı veya öğrenci numarası",
        ["email"] = "E-posta adresi",
        ["email_verified"] = "E-posta adresi doğrulanmış mı",
        ["role"] = "Kullanıcının rolleri (kullanıcı türü, idari görev ve komisyon üyelikleri)",
        ["fakulte"] = "Fakülte",
        ["bolum"] = "Bölüm",

        // OpenIddict'in kendi alanları
        ["oi_au_id"] = "OpenIddict: bağlı yetkilendirmenin kimliği",
        ["oi_tkn_id"] = "OpenIddict: token kaydının kimliği",
        ["oi_prst"] = "OpenIddict: token'ı sunabilecek istemci",
        ["oi_tkn_typ"] = "OpenIddict: token türü",
        ["oi_crt_dt"] = "OpenIddict: oluşturulma zamanı",
        ["oi_exp_dt"] = "OpenIddict: son kullanma zamanı",
    };

    private static readonly HashSet<string> ZamanAlanlari = ["exp", "nbf", "iat", "auth_time"];

    /// <param name="Ad">Claim adı.</param>
    /// <param name="Deger">Gösterilen değer (uzunsa kırpılmış).</param>
    /// <param name="TamDeger">Kırpıldıysa tam değer (title), değilse null.</param>
    /// <param name="Aciklama">Türkçe kısa açıklama.</param>
    /// <param name="Zaman">Unix zamanı alanlarında okunur tarih.</param>
    public sealed record ClaimSatiri(string Ad, string Deger, string? TamDeger, string Aciklama, DateTimeOffset? Zaman);

    public sealed record TokenGorunumu(
        string Ad, IReadOnlyList<ClaimSatiri> Baslik, IReadOnlyList<ClaimSatiri> Claimler, string HamBaslik, string HamYuk,
        DateTimeOffset? Bitis);

    public List<TokenGorunumu> Tokenlar { get; } = [];

    public bool RefreshTokenVar { get; private set; }

    public DateTimeOffset? RefreshTokenBitis { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        foreach (var ad in new[] { "id_token", "access_token" })
        {
            var token = await HttpContext.GetTokenAsync(ad);
            if (!string.IsNullOrEmpty(token))
            {
                Tokenlar.Add(Coz(ad, token));
            }
        }

        RefreshTokenVar = !string.IsNullOrEmpty(await HttpContext.GetTokenAsync("refresh_token"));
        RefreshTokenBitis = DateTimeOffset.TryParse(await HttpContext.GetTokenAsync(TokenYenileyici.RefreshTokenBitisAdi),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var bitis) ? bitis : null;

        return Page();
    }

    public static string AciklamaBul(string ad) => Aciklamalar.TryGetValue(ad, out var aciklama) ? aciklama : AciklamaYok;

    private static TokenGorunumu Coz(string ad, string token)
    {
        var parcalar = token.Split('.');
        if (parcalar.Length != 3)
        {
            return new(ad, [], [], "(JWT biçiminde değil)", string.Empty, null);
        }

        using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(parcalar[0]));
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(parcalar[1]));

        DateTimeOffset? bitis = payload.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var saniye)
            ? DateTimeOffset.FromUnixTimeSeconds(saniye)
            : null;

        return new(ad,
            Satirlar(header.RootElement),
            Satirlar(payload.RootElement),
            JsonSerializer.Serialize(header.RootElement, Bicim),
            JsonSerializer.Serialize(payload.RootElement, Bicim),
            bitis);
    }

    private static List<ClaimSatiri> Satirlar(JsonElement nesne) => [.. nesne.EnumerateObject().Select(alan =>
    {
        var deger = alan.Value.ValueKind == JsonValueKind.String
            ? alan.Value.GetString()!
            : JsonSerializer.Serialize(alan.Value, TekSatir);
        DateTimeOffset? zaman = ZamanAlanlari.Contains(alan.Name) && alan.Value.TryGetInt64(out var saniye)
            ? DateTimeOffset.FromUnixTimeSeconds(saniye)
            : null;
        return deger.Length > EnUzunDeger
            ? new ClaimSatiri(alan.Name, deger[..(EnUzunDeger - 4)] + "…", deger, AciklamaBul(alan.Name), zaman)
            : new ClaimSatiri(alan.Name, deger, null, AciklamaBul(alan.Name), zaman);
    })];
}
