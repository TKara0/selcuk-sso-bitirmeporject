using System.ComponentModel.DataAnnotations;

namespace Selcuk.Identity.Data;

/// <summary>
/// Güvenlik olaylarının denetim kaydı. Şifre, TOTP veya kurtarma kodu, sıfırlama token'ı, access/id/refresh token,
/// çerez ve logout_token hiçbir alana yazılmaz. Var olmayan kullanıcıyla başarısız girişte yazılan kullanıcı adı değil,
/// normalize edilmiş hâlinin anahtarlı özeti ("h1:" + HMAC-SHA256) tutulur (eski kayıtlarda anahtarsız SHA-256). Kayıtlar DenetimKaydiSaklamaGun (varsayılan 180) gün sonra silinir.
/// </summary>
public class DenetimKaydi
{
    public long Id { get; set; }

    public DateTime ZamanUtc { get; set; }

    public DenetimOlayi OlayTuru { get; set; }

    [MaxLength(450)]
    public string? KullaniciId { get; set; }

    [MaxLength(100)]
    public string? IstemciId { get; set; }

    /// <summary>İstemcinin gerçek IP adresi (güvenilen proxy arkasında X-Forwarded-For'dan; bkz. GuvenilenProxyler).</summary>
    [MaxLength(45)]
    public string? IpAdresi { get; set; }

    [MaxLength(200)]
    public string? KisaUserAgent { get; set; }

    public DenetimSonucu Sonuc { get; set; }

    /// <summary>Kısa, serbest metin (ör. "neden=sifre-yanlis"). Sır içermez.</summary>
    [MaxLength(500)]
    public string? Ayrinti { get; set; }
}

/// <summary>Veritabanında adıyla (metin) saklanır; sıra değişse de eski kayıtlar bozulmaz.</summary>
public enum DenetimOlayi
{
    GirisBasarili,
    GirisBasarisiz,
    HesapKilitlendi,
    IkiAdimBasarili,
    IkiAdimBasarisiz,
    KurtarmaKoduKullanildi,
    IkiAdimKoduTekrarKullanildi,
    IkiAdimKurulumuBaslatildi,
    IkiAdimAcildi,
    IkiAdimKapatildi,
    KurtarmaKodlariYenilendi,
    Cikis,
    SifreSifirlamaIstendi,
    SifreSifirlandi,
    SifreDegisti,
    OnayVerildi,
    OnayReddedildi,
    TokenVerildi,
    IstemciErisimiReddedildi,
    BackChannelLogoutGonderildi,
    BackChannelLogoutBasarisiz,
    HizSiniriAsildi,
}

public enum DenetimSonucu
{
    Basarili,
    Basarisiz,
}
