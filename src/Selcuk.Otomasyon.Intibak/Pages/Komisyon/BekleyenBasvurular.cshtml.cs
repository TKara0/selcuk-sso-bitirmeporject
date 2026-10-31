using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Intibak.Sozlesme;

namespace Selcuk.Otomasyon.Intibak.Pages.Komisyon;

/// <summary>Bölümün komisyon incelemesindeki ve geri gönderilen başvuruları (yetki: intibak_komisyonu).</summary>
public class BekleyenBasvurularModel(IIntibakVeriKaynagi veri) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Bekleyenler { get; private set; } = [];

    public async Task OnGetAsync() => Bekleyenler = await Bekleyenlerim(veri, HttpContext.RequestAborted);

    /// <summary>Eşleştirme bekleyen başvurular: komisyon incelemesinde veya komisyona geri gönderilmiş.</summary>
    internal static async Task<IReadOnlyList<IntibakBasvurusu>> Bekleyenlerim(IIntibakVeriKaynagi veri, CancellationToken iptal) =>
        [.. (await veri.BasvurularAsync(null, iptal))
            .Where(b => b.Durum is IntibakDurumu.KomisyonIncelemesinde or IntibakDurumu.GeriGonderildi)];
}
