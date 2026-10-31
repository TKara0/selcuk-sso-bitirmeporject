using Microsoft.AspNetCore.Mvc.RazorPages;
using Selcuk.Otomasyon.Danismanlik.Sozlesme;

namespace Selcuk.Otomasyon.Danismanlik.Pages.Ogrenci;

/// <summary>Öğrencinin danışmanı ve son görüşmesi (yalnızca kendi kaydı; yetki: ogrenci).</summary>
public class DanismanimModel(IDanismanlikVeriKaynagi veri) : PageModel
{
    public DanismanlikOgrencisi? Kayit { get; private set; }

    public async Task OnGetAsync() => Kayit = (await veri.KaydimAsync(HttpContext.RequestAborted)).FirstOrDefault();
}
