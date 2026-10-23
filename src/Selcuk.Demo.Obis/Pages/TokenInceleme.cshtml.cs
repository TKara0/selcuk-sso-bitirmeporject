using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.Tokens;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Tez için token içeriklerini gösterir. Yalnızca Development'ta açılır; imza doğrulaması yapmaz,
/// sadece okunur. (Doğrulamayı oturum açılırken OpenID Connect ara katmanı yapmıştır.)
/// </summary>
[Authorize]
public class TokenIncelemeModel(IWebHostEnvironment environment) : PageModel
{
    private static readonly JsonSerializerOptions Bicim = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public sealed record TokenGorunumu(string Ad, string Header, string Payload, DateTimeOffset? Bitis);

    public List<TokenGorunumu> Tokenlar { get; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        foreach (var (ad, tur) in new[] { ("id_token", "id_token"), ("access_token", "access_token") })
        {
            var token = await HttpContext.GetTokenAsync(tur);
            if (!string.IsNullOrEmpty(token))
            {
                Tokenlar.Add(Coz(ad, token));
            }
        }

        return Page();
    }

    private static TokenGorunumu Coz(string ad, string token)
    {
        var parcalar = token.Split('.');
        if (parcalar.Length != 3)
        {
            return new(ad, "(JWT biçiminde değil)", string.Empty, null);
        }

        using var header = JsonDocument.Parse(Base64UrlEncoder.Decode(parcalar[0]));
        using var payload = JsonDocument.Parse(Base64UrlEncoder.Decode(parcalar[1]));

        DateTimeOffset? bitis = payload.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var saniye)
            ? DateTimeOffset.FromUnixTimeSeconds(saniye)
            : null;

        return new(ad,
            JsonSerializer.Serialize(header.RootElement, Bicim),
            JsonSerializer.Serialize(payload.RootElement, Bicim),
            bitis);
    }
}
