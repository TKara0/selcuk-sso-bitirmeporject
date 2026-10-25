using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using QRCoder;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Hesap;

/// <summary>İki aşamalı doğrulama ayarları (appsettings kökündeki IkiAdim* anahtarları).</summary>
public sealed class IkiAdimAyarlari
{
    /// <summary>Bu rollerden birine sahip ve 2FA'sı kapalı kullanıcı kurulumu bitirmeden hiçbir istemciye token alamaz.</summary>
    public string[] ZorunluRoller { get; set; } = [];

    /// <summary>"Bu cihazı hatırla" (Identity TwoFactorRememberMe çerezi) süresi.</summary>
    public int CihazHatirlamaGun { get; set; } = 30;

    public static void Oku(IkiAdimAyarlari ayarlar, IConfiguration configuration)
    {
        ayarlar.ZorunluRoller = configuration.GetSection("IkiAdimZorunluRoller").Get<string[]>() ?? [];
        ayarlar.CihazHatirlamaGun = Math.Max(1, configuration.GetValue<int?>("IkiAdimCihazHatirlamaGun") ?? 30);
    }
}

/// <summary>
/// TOTP (RFC 6238) tabanlı iki aşamalı doğrulama. Anahtar üretimi, kod doğrulama ve kurtarma kodları Identity'nin
/// yerleşik authenticator altyapısıyla yapılır (AuthenticatorTokenProvider, UserManager); kurtarma kodları
/// <see cref="KurtarmaKoduOzetDeposu"/> ile özetlenerek saklanır. Açma, kapatma ve kurtarma kodu yenilemede güvenlik
/// damgası yenilenir: bu oturum yeniden imzalanır ve açık kalır, diğer oturumlar kapanır ve istemcilerine Back-Channel
/// Logout gider (şifre değiştirmedeki mekanizma), kullanıcıya bilgilendirme e-postası gönderilir. Kurulumu başlatmak
/// (yeni anahtar) da damgayı yeniler; o zaman da diğer oturumlar kapanır ve istemcilerine Back-Channel Logout gider.
/// Kurulumda, girişte ve onaylarda kabul edilen TOTP kodu 3 dakika içinde tekrar kabul edilmez (<see cref="TekKullanimlikTotpSaglayici"/>).
/// </summary>
public sealed class IkiAdimIslemleri(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TekKullanimlikTotpSaglayici totp,
    IOptions<IkiAdimAyarlari> ayarlar,
    BackChannelLogoutGonderici backChannelLogout,
    EpostaKuyrugu eposta,
    DenetimKaydedici denetim,
    TimeProvider zaman)
{
    /// <summary>Doğrulayıcı uygulamada hesabın yanında görünen ad (otpauth URI'sindeki issuer).</summary>
    public const string Issuer = "Selcuk SSO (Test)";

    public const int KurtarmaKoduSayisi = 10;

    public enum DogrulamaSonucu
    {
        Gecerli,
        Hatali,
        Kilitli,
    }

    /// <summary>Kullanıcının rolü 2FA gerektiriyor ve 2FA'sı kapalıysa true: kurulum bitene kadar token verilmez.</summary>
    public async Task<bool> KurulumGerekliAsync(ApplicationUser user)
    {
        var zorunlu = ayarlar.Value.ZorunluRoller;
        if (zorunlu.Length == 0 || await userManager.GetTwoFactorEnabledAsync(user))
        {
            return false;
        }

        var roller = await userManager.GetRolesAsync(user);
        return roller.Any(rol => zorunlu.Contains(rol, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Kullanıcının rolü 2FA'yı zorunlu kılıyorsa (2FA'nın açık olup olmadığından bağımsız) true.</summary>
    public async Task<bool> RolGerektiriyorAsync(ApplicationUser user)
    {
        var zorunlu = ayarlar.Value.ZorunluRoller;
        return zorunlu.Length != 0
            && (await userManager.GetRolesAsync(user)).Any(rol => zorunlu.Contains(rol, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Doğrulayıcı uygulamadaki 6 haneli kodu Identity'nin AuthenticatorTokenProvider'ıyla doğrular; kodu kullanılmış
    /// saymaz (şifresi yanlış olan onay denemelerinde).
    /// </summary>
    public Task<bool> KodDogruMuAsync(ApplicationUser user, string? kod) =>
        string.IsNullOrWhiteSpace(kod)
            ? Task.FromResult(false)
            : totp.TuketmedenDogrulaAsync(KodNormallestir(kod), userManager, user);

    /// <summary>Kodu doğrular ve kullanılmış sayar: aynı kod 3 dakika içinde tekrar kabul edilmez.</summary>
    public Task<bool> KoduKullanAsync(ApplicationUser user, string? kod) =>
        string.IsNullOrWhiteSpace(kod)
            ? Task.FromResult(false)
            : userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider,
                KodNormallestir(kod));

    /// <summary>
    /// Kurtarma kodlarını yenileme ve 2FA'yı kapatma için mevcut şifre + geçerli TOTP kodu. İkisi de her zaman kontrol
    /// edilir; biri yanlışsa hatalı deneme sayacı artar (5 denemede hesap kilitlenir). Kod yalnızca şifre doğruysa
    /// kullanılmış sayılır; daha önce kullanılmış (tekrar oynatılan) kod hatalı kod gibi sayılır.
    /// </summary>
    public async Task<DogrulamaSonucu> SifreVeKodDogrulaAsync(ApplicationUser user, string? sifre, string? kod)
    {
        if (await userManager.IsLockedOutAsync(user))
        {
            return DogrulamaSonucu.Kilitli;
        }

        var sifreDogru = !string.IsNullOrEmpty(sifre) && await userManager.CheckPasswordAsync(user, sifre);
        var kodDogru = sifreDogru ? await KoduKullanAsync(user, kod) : await KodDogruMuAsync(user, kod);
        if (sifreDogru && kodDogru)
        {
            return DogrulamaSonucu.Gecerli;
        }

        await HataliDenemeAsync(user, "neden=sifre-veya-kod-hatali");
        return await userManager.IsLockedOutAsync(user) ? DogrulamaSonucu.Kilitli : DogrulamaSonucu.Hatali;
    }

    /// <summary>Hatalı deneme sayacını artırır; deneme hesabı kilitlediyse denetim kaydına yazar.</summary>
    public async Task HataliDenemeAsync(ApplicationUser user, string neden)
    {
        await userManager.AccessFailedAsync(user);
        if (await userManager.IsLockedOutAsync(user))
        {
            denetim.Ekle(DenetimOlayi.HesapKilitlendi, DenetimSonucu.Basarisiz, user.Id, ayrinti: neden);
        }
    }

    /// <summary>
    /// Kurulumu başlatır: yeni paylaşılan anahtar (Identity ResetAuthenticatorKeyAsync, damga yenilenir). Bu oturum
    /// açık kalır; diğer oturumlar kapanır ve onlardan giriş yapılmış istemcilere Back-Channel Logout gider; denetim
    /// kaydı ve bilgilendirme e-postası.
    /// </summary>
    public async Task KurulumuBaslatAsync(ApplicationUser user, string? mevcutSid)
    {
        await userManager.ResetAuthenticatorKeyAsync(user);
        await OturumuYenileAsync(user, cokFaktorlu: false);
        await DegisiklikSonrasiAsync(user, mevcutSid, DenetimOlayi.IkiAdimKurulumuBaslatildi,
            EpostaSablonlari.IkiAdimKurulumuBaslatildi);
    }

    /// <summary>2FA'yı açar ve kullanıcıya bir kez gösterilecek 10 kurtarma kodunu döndürür.</summary>
    public async Task<IReadOnlyList<string>> EtkinlestirAsync(ApplicationUser user, string? mevcutSid)
    {
        var kodlar = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, KurtarmaKoduSayisi))!.ToList();
        await userManager.SetTwoFactorEnabledAsync(user, true);

        // Bu oturumda şifre ve geçerli bir TOTP kodu doğrulandı: oturum çok faktörlü sayılır.
        await OturumuYenileAsync(user, cokFaktorlu: true);
        await DegisiklikSonrasiAsync(user, mevcutSid, DenetimOlayi.IkiAdimAcildi, EpostaSablonlari.IkiAdimAcildi);
        return kodlar;
    }

    public async Task<IReadOnlyList<string>> KurtarmaKodlariniYenileAsync(ApplicationUser user, string? mevcutSid)
    {
        var kodlar = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, KurtarmaKoduSayisi))!.ToList();
        await userManager.UpdateSecurityStampAsync(user);

        await OturumuYenileAsync(user, cokFaktorlu: false);
        await DegisiklikSonrasiAsync(user, mevcutSid, DenetimOlayi.KurtarmaKodlariYenilendi,
            EpostaSablonlari.KurtarmaKodlariYenilendi);
        return kodlar;
    }

    /// <summary>2FA'yı kapatır; eski anahtar ve kalan kurtarma kodları da geçersiz olur.</summary>
    public async Task KapatAsync(ApplicationUser user, string? mevcutSid)
    {
        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);

        await OturumuYenileAsync(user, cokFaktorlu: false);
        await DegisiklikSonrasiAsync(user, mevcutSid, DenetimOlayi.IkiAdimKapatildi, EpostaSablonlari.IkiAdimKapatildi);
    }

    /// <summary>
    /// Ek doğrulama (step-up) başarılı: mevcut oturum çok faktörlü ("amr" = "mfa") olarak yeniden imzalanır; sid ve oturum
    /// başlangıcı korunur, diğer oturumlar etkilenmez. Hatalı deneme sayacı sıfırlanır (girişin ikinci adımındaki gibi).
    /// </summary>
    public async Task EkDogrulamaTamamlandiAsync(ApplicationUser user)
    {
        await userManager.ResetAccessFailedCountAsync(user);
        await OturumuYenileAsync(user, cokFaktorlu: true);
    }

    /// <summary>Girişte kurtarma kodu kullanıldıktan sonra: bilgilendirme e-postası (kalan kod sayısıyla).</summary>
    public async Task KurtarmaKoduKullanildiAsync(ApplicationUser user)
    {
        if (!string.IsNullOrEmpty(user.Email))
        {
            eposta.Ekle(EpostaSablonlari.KurtarmaKoduKullanildi(user.Email, user.AdSoyad, zaman.GetUtcNow(),
                await userManager.CountRecoveryCodesAsync(user)));
        }
    }

    /// <summary>Kullanıcının girdiği koddan boşluk ve tireleri atar.</summary>
    public static string KodNormallestir(string kod) =>
        string.Concat(kod.Where(c => !char.IsWhiteSpace(c) && c != '-'));

    /// <summary>Paylaşılan anahtar 4'lü gruplar hâlinde (elle girerken okunması kolay olsun diye).</summary>
    public static string AnahtarBicimle(string anahtar) =>
        string.Join(' ', anahtar.Chunk(4).Select(grup => new string(grup)));

    /// <summary>Google Authenticator "Key URI Format": otpauth://totp/{issuer}:{hesap}?secret=…&amp;issuer=…</summary>
    public static string OtpauthUri(string hesap, string anahtar) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(hesap)}" +
        $"?secret={anahtar}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

    /// <summary>QR kodu sunucu tarafında SVG olarak üretir (QRCoder); sayfada data: URI'li img olarak gösterilir.</summary>
    public static string QrKodDataUri(string metin)
    {
        using var uretici = new QRCodeGenerator();
        using var veri = uretici.CreateQrCode(metin, QRCodeGenerator.ECCLevel.M);
        using var svg = new SvgQRCode(veri);
        return "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg.GetGraphic(4)));
    }

    /// <summary>
    /// Güvenlik damgası değişti: bu oturumun çerezi yeni damgayla yeniden yazılır (sid ve oturum başlangıcı korunur).
    /// Identity'nin RefreshSignInAsync'i gibidir; ayrıca istenirse oturum çok faktörlü ("amr" = "mfa") işaretlenir.
    /// </summary>
    private async Task OturumuYenileAsync(ApplicationUser user, bool cokFaktorlu)
    {
        var mevcut = await signInManager.Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var amr = cokFaktorlu
            ? OidcKimlik.AmrCokFaktorlu
            : mevcut.Principal?.FindFirst(Claims.AuthenticationMethodReference)?.Value ?? OidcKimlik.AmrSifre;
        await signInManager.SignInWithClaimsAsync(user, mevcut.Properties,
            [new Claim(Claims.AuthenticationMethodReference, amr)]);
    }

    private async Task DegisiklikSonrasiAsync(ApplicationUser user, string? mevcutSid, DenetimOlayi olay,
        Func<string, string, DateTimeOffset, EpostaMesaji> sablon)
    {
        // Tarayıcı bağlantıyı kapatsa da bildirimler tamamlansın (süre sınırı HttpClient'ta).
        await backChannelLogout.KullaniciOturumlariniKapatAsync(user.Id, mevcutSid, CancellationToken.None);

        if (!string.IsNullOrEmpty(user.Email))
        {
            eposta.Ekle(sablon(user.Email, user.AdSoyad, zaman.GetUtcNow()));
        }

        denetim.Ekle(olay, DenetimSonucu.Basarili, user.Id);
    }
}
