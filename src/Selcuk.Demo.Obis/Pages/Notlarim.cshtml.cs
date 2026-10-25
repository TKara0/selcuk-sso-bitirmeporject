using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>Notlar sunucu tarafında Demo API'den, oturumdaki access token ile çekilir.</summary>
[Authorize(Roles = Roller.Ogrenci)]
public class NotlarimModel(NotlarApiIstemcisi notlarApi) : PageModel
{
    public const string HataMesaji = "Not bilgileri şu an alınamıyor. Lütfen biraz sonra tekrar deneyin.";

    public IReadOnlyList<DersNotu> Notlar { get; private set; } = [];

    public string? Hata { get; private set; }

    public async Task OnGetAsync()
    {
        var notlar = await notlarApi.NotlarimAsync(HttpContext, HttpContext.RequestAborted);
        if (notlar is null)
        {
            Hata = HataMesaji;
            return;
        }

        Notlar = notlar;
    }
}
