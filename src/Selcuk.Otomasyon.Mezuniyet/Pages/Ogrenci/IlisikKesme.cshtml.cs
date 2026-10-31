using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Mezuniyet.Sozlesme;

namespace Selcuk.Otomasyon.Mezuniyet.Pages.Ogrenci;

/// <summary>Öğrencinin ilişik kesme durumu: birimlerin listesi (yetki: ogrenci).</summary>
public class IlisikKesmeModel(IMezuniyetVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IlisikKesmeBirimi> Birimler { get; private set; } = [];

    public async Task OnGetAsync() => Birimler = await veri.IlisikKesmeAsync(HttpContext.RequestAborted);
}
