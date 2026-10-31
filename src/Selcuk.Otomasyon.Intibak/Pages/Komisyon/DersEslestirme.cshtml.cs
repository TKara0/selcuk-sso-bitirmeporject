using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Intibak.Sozlesme;
using Selcuk.Otomasyon.Ortak;

namespace Selcuk.Otomasyon.Intibak.Pages.Komisyon;

/// <summary>
/// Ders eşleştirme: seçilen başvurunun önceki dersleri ile bölüm müfredatı yan yana; satır başına eşdeğer ders ve
/// muafiyet kararı (gönderim temsilidir). Önerilen karar ayardaki şartlara göre (IntibakSartlari).
/// </summary>
public class DersEslestirmeModel(IIntibakVeriKaynagi veri, IOptions<IntibakSartlari> sartlar) : PageModel
{
    public IReadOnlyList<IntibakBasvurusu> Bekleyenler { get; private set; } = [];

    public IReadOnlyList<MufredatDersi> Mufredat { get; private set; } = [];

    public IntibakBasvurusu? Secili { get; private set; }

    /// <summary>Adreste id verildi ama bölümün bekleyen başvurularında yok.</summary>
    public bool Bulunamadi { get; private set; }

    public IntibakSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync(string? id)
    {
        Bekleyenler = await BekleyenBasvurularModel.Bekleyenlerim(veri, HttpContext.RequestAborted);
        Mufredat = await veri.MufredatAsync(HttpContext.RequestAborted);
        Secili = id is null ? Bekleyenler.FirstOrDefault() : Bekleyenler.FirstOrDefault(b => b.Id == id);
        Bulunamadi = id is not null && Secili is null;
    }

    public IActionResult OnPost() => this.TemsiliIslemSonucu();

    /// <summary>Satırdaki eşdeğer ders: komisyonun önceki kararı, yoksa adı aynı müfredat dersi.</summary>
    public string? EsdegerOnerisi(DersEslesmesi ders) => ders.EsdegerKod
        ?? Mufredat.FirstOrDefault(m => string.Equals(m.Ad, ders.OncekiDersAdi, StringComparison.OrdinalIgnoreCase))?.Kod;

    /// <summary>Önerilen karar: komisyonun önceki kararı, yoksa not şartı (muafiyet için en düşük not).</summary>
    public bool MuafiyetOnerisi(DersEslesmesi ders) => ders.Muaf ?? (EsdegerOnerisi(ders) is not null && Sartlar.MuafiyeteYeter(ders.OncekiNot));
}
