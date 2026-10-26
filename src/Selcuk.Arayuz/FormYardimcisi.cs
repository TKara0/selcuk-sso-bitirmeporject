using Microsoft.AspNetCore.Mvc.Rendering;

namespace Selcuk.Arayuz;

/// <summary>Form alanlarının erişilebilirlik öznitelikleri için küçük yardımcılar.</summary>
public static class FormYardimcisi
{
    /// <summary>Alanın doğrulama hatası varsa "true" (aria-invalid), yoksa null (öznitelik yazılmaz).</summary>
    public static string? AlanHatali(this IHtmlHelper html, string alan) =>
        html.ViewData.ModelState.TryGetValue(alan, out var durum) && durum.Errors.Count > 0 ? "true" : null;

    /// <summary>Forma ait (alana bağlı olmayan) hata var mı; varsa form hata bandı gösterilir.</summary>
    public static bool FormHatasiVar(this IHtmlHelper html) =>
        html.ViewData.ModelState.TryGetValue(string.Empty, out var durum) && durum.Errors.Count > 0;
}
