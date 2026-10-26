using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Pages.Account;

/// <summary>
/// Şifre sıfırlama isteği. Hesap var olsun ya da olmasın aynı mesaj gösterilir ve aynı iş yapılır; e-posta yalnızca
/// eşleşen, e-postası doğrulanmış hesaba gönderilir.
/// </summary>
[EnableRateLimiting(HizSiniri.HassasForm)]
public class SifremiUnuttumModel(
    UserManager<ApplicationUser> userManager,
    EpostaKuyrugu eposta,
    DenetimKaydedici denetim,
    IOptions<SifreSifirlamaTokenAyarlari> tokenAyarlari,
    IOptionsMonitor<OpenIddictServerOptions> serverAyarlari) : PageModel
{
    public const string GonderildiMesaji =
        "Bilgiler bir hesapla eşleşiyorsa şifre sıfırlama bağlantısı e-posta adresine gönderildi.";

    [BindProperty]
    public IstekGirdisi Input { get; set; } = new();

    public bool Gonderildi { get; private set; }

    public TimeSpan Gecerlilik => tokenAyarlari.Value.TokenLifespan;

    public class IstekGirdisi
    {
        [Required(ErrorMessage = "Kullanıcı adını, öğrenci numaranı veya e-posta adresini yaz.")]
        [MaxLength(256)]
        [Display(Name = "Kullanıcı adı, öğrenci numarası veya e-posta")]
        public string KullaniciAdiVeyaEposta { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // İki arama da her istekte yapılır; hangisinin eşleştiği yanıt süresini değiştirmez.
        var girdi = Input.KullaniciAdiVeyaEposta.Trim();
        var adla = await userManager.FindByNameAsync(girdi);
        var epostayla = await userManager.FindByEmailAsync(girdi);
        var kullanici = adla ?? epostayla;
        var gonderilecek = kullanici is { EmailConfirmed: true } && !string.IsNullOrEmpty(kullanici.Email);

        // Hesap yoksa da aynı iş yapılır: bellekteki sahte bir kullanıcı için token, bağlantı ve e-posta metni üretilir
        // (veritabanına yazılmaz), yalnızca kuyruğa bırakılmaz. Gönderim arka planda olduğundan gecikmesi yanıta yansımaz.
        var hedef = gonderilecek ? kullanici! : new ApplicationUser { Email = "eslesmeyen@test.local" };
        var token = await userManager.GeneratePasswordResetTokenAsync(hedef);
        var mesaj = EpostaSablonlari.SifreSifirlama(hedef.Email!, hedef.AdSoyad,
            SifirlamaBaglantisi(hedef.Id, token), tokenAyarlari.Value.TokenLifespan);
        if (gonderilecek)
        {
            eposta.Ekle(mesaj);
        }

        // Hesap bulunamadıysa yazılan metin saklanmaz; normalize hâlinin özeti saklanır (girişteki gibi).
        denetim.Ekle(DenetimOlayi.SifreSifirlamaIstendi, DenetimSonucu.Basarili, kullanici?.Id,
            ayrinti: kullanici is null ? "hesap=bulunamadi; " + denetim.KullaniciAdiOzeti(userManager.NormalizeName(girdi))
                : gonderilecek ? "eposta=gonderildi" : "eposta=gonderilmedi; neden=eposta-dogrulanmamis");

        Gonderildi = true;
        return Page();
    }

    // Bağlantı istekteki Host başlığından değil yapılandırılmış issuer adresinden üretilir; Host başlığı değiştirilerek
    // sıfırlama bağlantısı başka bir alan adına yönlendirilemez.
    private Uri SifirlamaBaglantisi(string kullaniciId, string token) =>
        new(serverAyarlari.CurrentValue.Issuer!, QueryHelpers.AddQueryString("Account/SifreSifirla",
            new Dictionary<string, string?>
            {
                ["kullanici"] = kullaniciId,
                ["kod"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)),
            }));
}
