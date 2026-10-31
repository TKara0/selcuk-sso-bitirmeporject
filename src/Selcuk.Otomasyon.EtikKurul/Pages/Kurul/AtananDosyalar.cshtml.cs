using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.EtikKurul.Sozlesme;

namespace Selcuk.Otomasyon.EtikKurul.Pages.Kurul;

/// <summary>Kurul üyesine atanan dosyalar, bildirilen görüş sayısı (şart ayardan) ve üyenin kendi görüşü (yetki: etik_kurul_uyesi).</summary>
public class AtananDosyalarModel(IEtikKurulVeriKaynagi veri, IOptions<EtikKurulSartlari> sartlar) : PageModel
{
    public IReadOnlyList<EtikBasvurusu> Dosyalar { get; private set; } = [];

    public EtikKurulSartlari Sartlar => sartlar.Value;

    public async Task OnGetAsync() => Dosyalar = await veri.AtananDosyalarimAsync(HttpContext.RequestAborted);

    public int GorusSayisi(EtikBasvurusu b) => b.Gorusler.Count(g => g.Gorus is not null);

    /// <summary>Oturumdaki üyenin görüşü (kullanıcı adı = atamadaki üye).</summary>
    public GorusTuru? BenimGorusum(EtikBasvurusu b) => KendiGorusu(b, User.FindFirst("preferred_username")?.Value)?.Gorus;

    internal static UyeGorusu? KendiGorusu(EtikBasvurusu b, string? kullaniciAdi) =>
        b.Gorusler.FirstOrDefault(g => string.Equals(g.UyeKullaniciAdi, kullaniciAdi, StringComparison.Ordinal));
}
