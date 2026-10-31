using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>
/// Açılış denetimi: uygulamanın her sayfası ya menüdedir (yetkisi menüdeki rollerden türer) ya kendi
/// [Authorize(Roles = …)]'ını taşır ya da aşağıdaki ortak sayfalardandır. Menüye eklenmeyi unutulmuş ve yalnızca "giriş
/// yapmış olmak"la (AuthorizeFolder("/")) korunan bir rol ekranı her kullanıcıya açık kalırdı; böyle bir sayfa varsa
/// uygulama açılışta Türkçe bir hatayla durur.
/// </summary>
public static class SayfaYetkiDenetimi
{
    /// <summary>Denetim dışı ortak sayfalar: herkese açık sayfalar (ana sayfa, giriş/çıkış, hata sayfaları) ve Profilim.</summary>
    public static readonly IReadOnlyList<string> Istisnalar = [.. OtomasyonUzantilari.AcikSayfalar, "/Profilim"];

    /// <summary>Kural dışı sayfaların yolları (boşsa uygulama açılabilir).</summary>
    public static async Task<IReadOnlyList<string>> KuralDisiSayfalarAsync(IServiceProvider servisler)
    {
        var menu = servisler.GetRequiredService<OtomasyonMenusu>();
        var yukleyici = servisler.GetRequiredService<PageLoader>();
        var ilkeler = servisler.GetRequiredService<IAuthorizationPolicyProvider>();
        var sayfalar = servisler.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<PageActionDescriptor>()
            .Where(s => s.AreaName is null)
            .DistinctBy(s => s.ViewEnginePath, StringComparer.OrdinalIgnoreCase);

        var kuralDisi = new List<string>();
        foreach (var sayfa in sayfalar)
        {
            var yol = sayfa.ViewEnginePath;
            if (Istisnalar.Contains(yol, StringComparer.OrdinalIgnoreCase)
                || menu.Ogeler.Any(o => string.Equals(o.Sayfa, yol, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var meta = sayfa is CompiledPageActionDescriptor derlenmis
                ? derlenmis.EndpointMetadata
                : (await yukleyici.LoadAsync(sayfa, EndpointMetadataCollection.Empty)).EndpointMetadata;
            var ilke = await AuthorizationPolicy.CombineAsync(ilkeler, meta.OfType<IAuthorizeData>());
            var rolIstiyor = !meta.OfType<IAllowAnonymous>().Any()
                && ilke?.Requirements.OfType<RolesAuthorizationRequirement>().Any() == true;
            if (!rolIstiyor)
            {
                kuralDisi.Add(yol);
            }
        }

        return kuralDisi;
    }

    internal static void Denetle(WebApplication app)
    {
        var kuralDisi = KuralDisiSayfalarAsync(app.Services).GetAwaiter().GetResult();
        if (kuralDisi.Count > 0)
        {
            throw new InvalidOperationException(
                $"Menüde olmayan ve rol istemeyen sayfa var: {string.Join(", ", kuralDisi)}. Sayfayı menüye ekle veya " +
                $"[Authorize(Roles = …)] ile koru. Denetim dışı ortak sayfalar: {string.Join(", ", Istisnalar)}.");
        }
    }
}
