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
/// Satırlar istemci kaydındaki "grup" özelliğine göre başlıklar altında toplanır (<see cref="Istemciler.Gruplar"/>
/// sırasıyla); kartı olmayan grup gösterilmez, grubu olmayan veya bilinmeyen gruplu istemciler sonda "Diğer" altında.
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
        string? Vurgu = null, string? Simge = null, string? IstemciKimligi = null, string? Grup = null)
    {
        public string SimgeMetni => Simge ?? BasHarfler(Ad);

        /// <summary>Kart bir bölüm otomasyonuna ait (temsili ekran notu için).</summary>
        public bool Otomasyon => Istemciler.OtomasyonMu(IstemciKimligi);
    }

    /// <summary>Bir grup başlığı ve kullanıcının o gruptaki kartları (en az bir kart).</summary>
    public sealed record UygulamaGrubu(string Baslik, IReadOnlyList<UygulamaKarti> Kartlar);

    /// <summary>Uygulamalarım'da tanımlı vurgu renkleri (site.css); başka bir değer yok sayılır.</summary>
    public static readonly IReadOnlySet<string> VurguRenkleri = new HashSet<string>(StringComparer.Ordinal) { "zeytin", "eflatun", "bakir", "hardal", "murdum", "arduvaz", "cam", "indigo", "petrol", "gulkurusu", "grafit", "obis", "nar" };

    public List<UygulamaKarti> Uygulamalar { get; } = [];

    /// <summary>Kartların grupları: <see cref="Istemciler.Gruplar"/> sırasıyla, en sonda "Diğer"; boş grup yok.</summary>
    public List<UygulamaGrubu> Gruplar { get; } = [];

    /// <summary>
    /// Bölüm otomasyonu kartı içeren ilk grubun <see cref="Gruplar"/>'daki sırası; temsili ekran notu o grubun başlığının
    /// üstünde bir kez gösterilir. Kullanıcı hiçbir otomasyon kartı görmüyorsa -1 (not yok).
    /// </summary>
    public int OtomasyonNotuGrupSirasi => Gruplar.FindIndex(g => g.Kartlar.Any(k => k.Otomasyon));

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

            var istemciKimligi = await applicationManager.GetClientIdAsync(uygulama, HttpContext.RequestAborted);
            var grup = ozellikler.TryGetValue(Istemciler.GrupOzelligi, out var g) && g.ValueKind == System.Text.Json.JsonValueKind.String
                ? GecerliGrup(g.GetString()) : null;

            Uygulamalar.Add(new UygulamaKarti(ad, aciklama, anaSayfa.GetString()!, girisAdresi,
                IstemciErisimi.MfaZorunlu(ozellikler), vurgu, simge, istemciKimligi, grup));
        }

        Uygulamalar.Sort((x, y) =>
        {
            var sira = KartSirasi(x).CompareTo(KartSirasi(y));
            return sira != 0 ? sira : string.Compare(x.Ad, y.Ad, StringComparison.CurrentCulture);
        });

        // Gruplar tanım sırasıyla, kartı olmayanlar atlanır; grubu olmayanlar en sonda "Diğer" altında.
        foreach (var baslik in Istemciler.Gruplar.Append(Istemciler.GrupDiger))
        {
            var kartlar = Uygulamalar.Where(k => (k.Grup ?? Istemciler.GrupDiger) == baslik).ToList();
            if (kartlar.Count > 0)
            {
                Gruplar.Add(new UygulamaGrubu(baslik, kartlar));
            }
        }
    }

    /// <summary>İstemci kaydındaki grup: yalnızca tanımlı gruplardan biri; başka değer yok sayılır ("Diğer").</summary>
    public static string? GecerliGrup(string? grup) =>
        grup is not null && Istemciler.Gruplar.Contains(grup, StringComparer.Ordinal) ? grup : null;

    // Grup içi sıra: Istemciler.KartSirasi; listede olmayanlar sonda (ada göre).
    private static int KartSirasi(UygulamaKarti kart)
    {
        var sira = kart.IstemciKimligi is null ? -1 : Array.IndexOf(Istemciler.KartSirasi, kart.IstemciKimligi);
        return sira < 0 ? int.MaxValue : sira;
    }
}
