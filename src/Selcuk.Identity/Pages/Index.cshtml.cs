using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Pages;

/// <summary>
/// "Uygulamalarım": kayıtlı istemcilerin satırları. Satırdaki "Aç" bağlantısı istemcinin giriş başlatma adresine
/// (initiate_login_uri) gider; kimlik sunucusunda oturum açık olduğu için şifre sorulmadan içeri girilir. OIDC Core 1.0
/// bölüm 4'e göre initiate_login_uri'ye iss parametresi eklenir; istemci bunu kendi kimlik sunucusuyla karşılaştırır.
/// Kullanıcının istemci bazlı erişim izni olmayan uygulamaların satırı gösterilmez (<see cref="IstemciErisimi"/>).
/// </summary>
[Authorize]
public class IndexModel(
    IOpenIddictApplicationManager applicationManager,
    UserManager<ApplicationUser> userManager,
    IOptionsMonitor<OpenIddictServerOptions> serverAyarlari) : PageModel
{
    /// <param name="Vurgu">Simgenin vurgu rengi adı (site.css'te .uygulama-simge-&lt;ad&gt;); yoksa firuze.</param>
    /// <param name="Simge">İstemci kaydındaki simge harfleri; yoksa addan üretilir (<see cref="BasHarfler"/>).</param>
    public sealed record UygulamaKarti(string Ad, string Aciklama, string AnaSayfa, string GirisAdresi, bool MfaGerekli,
        string? Vurgu = null, string? Simge = null)
    {
        public string SimgeMetni => Simge ?? BasHarfler(Ad);
    }

    /// <summary>Uygulamalarım'da tanımlı vurgu renkleri (site.css); başka bir değer yok sayılır.</summary>
    public static readonly IReadOnlySet<string> VurguRenkleri = new HashSet<string>(StringComparer.Ordinal) { "zeytin", "eflatun", "bakir", "hardal", "murdum", "arduvaz", "cam", "indigo", "petrol", "gulkurusu", "grafit" };

    public List<UygulamaKarti> Uygulamalar { get; } = [];

    /// <summary>Karşılama başlığındaki ad: ad soyad, yoksa kullanıcı adı.</summary>
    public string Ad { get; private set; } = string.Empty;

    /// <summary>Uygulama simgesindeki baş harfler: addaki ilk iki kelimenin ilk harfleri ("Demo OBİS" → "DO").</summary>
    public static string BasHarfler(string ad) => string.Concat(ad
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(kelime => char.IsLetter(kelime[0]))
        .Take(2)
        .Select(kelime => char.ToUpper(kelime[0], CultureInfo.GetCultureInfo("tr-TR"))));

    /// <summary>İstemci kaydındaki simge: bir veya iki harf (ör. "ST", "ÇY"); başka bir değer yok sayılır.</summary>
    public static string? GecerliSimge(string? simge) =>
        simge is { Length: 1 or 2 } && simge.All(char.IsLetter) ? simge : null;

    public async Task OnGetAsync()
    {
        var uygulamalar = await applicationManager.ListAsync(count: null, offset: null, HttpContext.RequestAborted)
            .ToListAsync(HttpContext.RequestAborted);
        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
        var roller = await userManager.GetRolesAsync(user);
        Ad = string.IsNullOrWhiteSpace(user.AdSoyad) ? user.UserName ?? string.Empty : user.AdSoyad;

        foreach (var uygulama in uygulamalar)
        {
            var ozellikler = await applicationManager.GetPropertiesAsync(uygulama, HttpContext.RequestAborted);
            if (!ozellikler.TryGetValue(Istemciler.ClientUriOzelligi, out var anaSayfa)
                || !IstemciErisimi.Izinli(ozellikler, user.UserName, roller))
            {
                continue;
            }

            var girisAdresi = ozellikler.TryGetValue(Istemciler.InitiateLoginUriOzelligi, out var giris)
                ? QueryHelpers.AddQueryString(giris.GetString()!, "iss", serverAyarlari.CurrentValue.Issuer!.AbsoluteUri)
                : anaSayfa.GetString()!;
            var aciklama = ozellikler.TryGetValue(Istemciler.AciklamaOzelligi, out var a) ? a.GetString()! : string.Empty;
            var ad = await applicationManager.GetLocalizedDisplayNameAsync(uygulama, HttpContext.RequestAborted)
                ?? await applicationManager.GetClientIdAsync(uygulama, HttpContext.RequestAborted)
                ?? string.Empty;

            var vurgu = ozellikler.TryGetValue(Istemciler.VurguOzelligi, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
                && VurguRenkleri.Contains(v.GetString()!) ? v.GetString() : null;

            var simge = ozellikler.TryGetValue(Istemciler.SimgeOzelligi, out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String
                ? GecerliSimge(s.GetString()) : null;

            Uygulamalar.Add(new UygulamaKarti(ad, aciklama, anaSayfa.GetString()!, girisAdresi,
                IstemciErisimi.MfaZorunlu(ozellikler), vurgu, simge));
        }

        Uygulamalar.Sort((x, y) => string.Compare(x.Ad, y.Ad, StringComparison.CurrentCulture));
    }
}
