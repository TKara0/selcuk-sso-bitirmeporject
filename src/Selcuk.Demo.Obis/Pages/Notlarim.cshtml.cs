using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Pages;

/// <summary>
/// Notlar sunucu tarafında Demo API'den, oturumdaki access token ile çekilir. Toplam kredi ve dönem ağırlıklı
/// ortalaması bu notlardan ve OBİS'in ders kataloğundaki kredilerden hesaplanır (<see cref="NotOzeti"/>).
/// </summary>
[Authorize(Roles = Roller.Ogrenci)]
public class NotlarimModel(NotlarApiIstemcisi notlarApi) : PageModel
{
    public const string HataMesaji = "Not bilgileri şu an alınamıyor. Biraz sonra tekrar dene.";

    public IReadOnlyList<DersNotu> Notlar { get; private set; } = [];

    public int ToplamKredi { get; private set; }

    public decimal? Ortalama { get; private set; }

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
        (ToplamKredi, Ortalama) = NotOzeti.Hesapla(notlar);
    }
}
