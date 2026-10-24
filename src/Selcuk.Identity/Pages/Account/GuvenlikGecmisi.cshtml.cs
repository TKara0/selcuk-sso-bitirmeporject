using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;

namespace Selcuk.Identity.Pages.Account;

/// <summary>Hesabım > Güvenlik geçmişi: kullanıcının yalnızca kendi son 20 denetim kaydı.</summary>
[Authorize]
public class GuvenlikGecmisiModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IOpenIddictApplicationManager applicationManager) : PageModel
{
    public const int KayitSayisi = 20;

    public sealed record Satir(string Zaman, string Olay, string? Uygulama, string? IpAdresi);

    public List<Satir> Satirlar { get; } = [];

    public async Task OnGetAsync()
    {
        var iptal = HttpContext.RequestAborted;
        var kullaniciId = userManager.GetUserId(User)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");

        var kayitlar = await db.DenetimKayitlari.AsNoTracking()
            .Where(k => k.KullaniciId == kullaniciId)
            .OrderByDescending(k => k.Id)
            .Take(KayitSayisi)
            .ToListAsync(iptal);

        // Uygulama sütununda client_id yerine istemcinin görünen adı (kayıtlı değilse client_id).
        var uygulamaAdlari = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var clientId in kayitlar.Select(k => k.IstemciId).OfType<string>().Distinct())
        {
            var uygulama = await applicationManager.FindByClientIdAsync(clientId, iptal);
            uygulamaAdlari[clientId] = uygulama is null
                ? clientId
                : await applicationManager.GetLocalizedDisplayNameAsync(uygulama, iptal) ?? clientId;
        }

        Satirlar.AddRange(kayitlar.Select(k => new Satir(
            EpostaSablonlari.Bicimle(new DateTimeOffset(DateTime.SpecifyKind(k.ZamanUtc, DateTimeKind.Utc))),
            DenetimOlaylari.Ad(k.OlayTuru, k.Sonuc),
            k.IstemciId is null ? null : uygulamaAdlari[k.IstemciId],
            k.IpAdresi)));
    }
}
