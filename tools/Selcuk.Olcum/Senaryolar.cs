using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Selcuk.Olcum;

/// <summary>
/// Ölçülen senaryolar. Her yöntem yalnızca ölçülen kısmın süresini (ms) döndürür; hazırlık adımları (ör. çıkış
/// formunu açmak, kendi token'ını almak) süreye girmez. Başarısızlık <see cref="OlcumHatasi"/> fırlatır.
/// </summary>
public sealed class Senaryolar(OlcumAyarlari ayarlar, Func<CookieContainer, HttpMessageHandler> handlerFabrikasi)
{
    public const string SogukGiris = "a-soguk-giris";
    public const string TekGiris = "b-tek-giris";
    public const string TokenYenileme = "c-token-yenileme";
    public const string TekCikis = "d-tek-cikis";
    public const string ApiCagrisi = "e-api-cagrisi";
    public const string EszamanliGiris = "f-eszamanli-giris";

    // Demo OBİS'in istediği kapsamlar (onay bunlar için verilmiş olur; ek onay ekranı çıkmaz).
    private const string ObisKapsamlari = "openid profile email roles offline_access notlar.oku";
    private const string NotlarHataMetni = "Not bilgileri şu an alınamıyor";
    private static readonly TimeSpan CikisBeklemeSiniri = TimeSpan.FromSeconds(10);

    public OlcumAyarlari Ayarlar => ayarlar;

    public TarayiciOturumu YeniOturum() => new(handlerFabrikasi, ayarlar.Kimlik, ayarlar.IstekZamanAsimi);

    /// <summary>(a) Boş çerez kabıyla Demo OBİS Notlarım → giriş formu → (onay) → kod → token → Notlarım 200.</summary>
    public async Task<double> SogukGirisAsync(TarayiciOturumu oturum, string kullaniciAdi)
    {
        var baslangic = Stopwatch.GetTimestamp();
        var son = await oturum.IzleAsync(() => oturum.GetAsync(new Uri(ayarlar.Obis, "Notlarim")),
            new AkisSecenekleri(kullaniciAdi, ayarlar.Sifre));
        var sure = Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds;

        Beklenen(son, ayarlar.Obis, "/Notlarim");
        if (son.Metin.Contains(NotlarHataMetni, StringComparison.Ordinal))
        {
            throw new OlcumHatasi("notlar-api-hatasi");
        }

        return sure;
    }

    /// <summary>(b) Aynı çerezlerle Demo Yemekhane Rezervasyonlarım: şifre sorulmadan 200 olana kadar.</summary>
    public async Task<double> TekGirisAsync(TarayiciOturumu oturum)
    {
        var baslangic = Stopwatch.GetTimestamp();
        var son = await oturum.IzleAsync(() => oturum.GetAsync(new Uri(ayarlar.Yemekhane, "Rezervasyonlarim")),
            new AkisSecenekleri());
        var sure = Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds;

        Beklenen(son, ayarlar.Yemekhane, "/Rezervasyonlarim");
        return sure;
    }

    /// <summary>
    /// (c) için hazırlık (süreye girmez): açık kimlik sunucusu oturumuyla demo-obis istemcisi olarak authorize (PKCE
    /// S256) ve kod değişimi. Aracın kendi access ve refresh token'ı olur; Demo OBİS'in çerezindeki token'lara erişilmez.
    /// </summary>
    public async Task<(string Access, string Refresh)> TokenAlAsync(TarayiciOturumu oturum)
    {
        var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var state = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(16));
        var redirectUri = new Uri(ayarlar.Obis, "signin-oidc");
        var authorize = QueryHelpers.AddQueryString(new Uri(ayarlar.Kimlik, "connect/authorize").AbsoluteUri,
            new Dictionary<string, string?>
            {
                ["client_id"] = "demo-obis",
                ["redirect_uri"] = redirectUri.AbsoluteUri,
                ["response_type"] = "code",
                ["scope"] = ObisKapsamlari,
                ["state"] = state,
                ["nonce"] = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(16)),
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = "S256",
            });

        var son = await oturum.IzleAsync(() => oturum.GetAsync(new Uri(authorize)), new AkisSecenekleri(
            Durdur: hedef => hedef.GetLeftPart(UriPartial.Path) == redirectUri.AbsoluteUri));
        if (son.Yonlendirme is not { } donus || donus.GetLeftPart(UriPartial.Path) != redirectUri.AbsoluteUri)
        {
            throw new OlcumHatasi("kod-alinamadi");
        }

        var sorgu = QueryHelpers.ParseQuery(donus.Query);
        if (sorgu["state"] != state || sorgu["code"] is not { Count: 1 } kod)
        {
            throw new OlcumHatasi("kod-alinamadi");
        }

        return await TokenIsteAsync(oturum, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = kod.ToString(),
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["code_verifier"] = verifier,
        });
    }

    /// <summary>(c) Refresh token ile yeni access token (ve kayan yeni refresh token) alma süresi.</summary>
    public async Task<(double Sure, string Access, string Refresh)> TokenYenileAsync(TarayiciOturumu oturum, string refreshToken)
    {
        var baslangic = Stopwatch.GetTimestamp();
        var (access, refresh) = await TokenIsteAsync(oturum, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        });
        return (Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds, access, refresh);
    }

    /// <summary>(e) Demo API GET /api/notlar (geçerli access token ile) süresi.</summary>
    public async Task<double> ApiAsync(TarayiciOturumu oturum, string accessToken)
    {
        var istek = new HttpRequestMessage(HttpMethod.Get, new Uri(ayarlar.Api, "api/notlar"));
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var baslangic = Stopwatch.GetTimestamp();
        var yanit = await oturum.GonderAsync(istek);
        var sure = Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds;

        if (yanit.Durum != HttpStatusCode.OK || !yanit.Govde.Contains("\"notlar\"", StringComparison.Ordinal))
        {
            throw new OlcumHatasi($"durum-{(int)yanit.Durum}");
        }

        return sure;
    }

    /// <summary>
    /// (d) Demo OBİS'ten çıkış isteğinden (çıkış formunun gönderilmesi; kimlik sunucusunun çıkış onayı dahil), hem
    /// Demo OBİS'in hem Demo Yemekhane'nin sonraki istekte oturumu reddetmesine (giriş için yönlendirmesine) kadar.
    /// </summary>
    public async Task<double> TekCikisAsync(TarayiciOturumu oturum)
    {
        var cikisSayfasi = new Uri(ayarlar.Obis, "Cikis");
        var form = await oturum.GetAsync(cikisSayfasi);
        if (form.Durum != HttpStatusCode.OK)
        {
            throw new OlcumHatasi($"cikis-sayfasi-{(int)form.Durum}");
        }

        var alanlar = TarayiciOturumu.GizliAlanlar(form.Govde);
        var baslangic = Stopwatch.GetTimestamp();
        var son = await oturum.IzleAsync(() => oturum.PostAsync(cikisSayfasi, alanlar), new AkisSecenekleri());
        Beklenen(son, ayarlar.Obis, "/");

        var obisKapandi = false;
        var yemekhaneKapandi = false;
        while (!(obisKapandi && yemekhaneKapandi))
        {
            obisKapandi = obisKapandi || await OturumReddediliyorMuAsync(oturum, new Uri(ayarlar.Obis, "Notlarim"));
            yemekhaneKapandi = yemekhaneKapandi
                || await OturumReddediliyorMuAsync(oturum, new Uri(ayarlar.Yemekhane, "Rezervasyonlarim"));
            if (Stopwatch.GetElapsedTime(baslangic) > CikisBeklemeSiniri)
            {
                throw new OlcumHatasi("oturum-kapanmadi");
            }

            if (!(obisKapandi && yemekhaneKapandi))
            {
                await Task.Delay(10);
            }
        }

        return Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds;
    }

    // Oturum reddedildi: korumalı sayfa 200 yerine kimlik sunucusuna (authorize) yönlendiriyor.
    private async Task<bool> OturumReddediliyorMuAsync(TarayiciOturumu oturum, Uri sayfa)
    {
        var yanit = await oturum.GetAsync(sayfa);
        if (yanit.Durum == HttpStatusCode.OK)
        {
            return false;
        }

        return yanit.Yonlendirme is { } hedef && oturum.KimlikSunucusundaMi(hedef)
            ? true
            : throw new OlcumHatasi($"durum-{(int)yanit.Durum}");
    }

    private async Task<(string Access, string Refresh)> TokenIsteAsync(TarayiciOturumu oturum, Dictionary<string, string> alanlar)
    {
        alanlar["client_id"] = "demo-obis";
        alanlar["client_secret"] = ayarlar.DemoObisSecret;
        var yanit = await oturum.PostAsync(new Uri(ayarlar.Kimlik, "connect/token"), alanlar);
        if (yanit.Durum != HttpStatusCode.OK)
        {
            throw new OlcumHatasi($"token-durum-{(int)yanit.Durum}");
        }

        using var json = JsonDocument.Parse(yanit.Govde);
        return json.RootElement.TryGetProperty("access_token", out var access)
            && json.RootElement.TryGetProperty("refresh_token", out var refresh)
            ? (access.GetString()!, refresh.GetString()!)
            : throw new OlcumHatasi("token-yok");
    }

    private static void Beklenen(Sayfa son, Uri uygulama, string yol)
    {
        if (son.Durum != HttpStatusCode.OK)
        {
            throw new OlcumHatasi(son.YonlendirmeMi ? "beklenmeyen-yonlendirme" : $"durum-{(int)son.Durum}");
        }

        if (son.Adres.GetLeftPart(UriPartial.Authority) != uygulama.GetLeftPart(UriPartial.Authority)
            || !string.Equals(son.Adres.AbsolutePath.TrimEnd('/'), yol.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            throw new OlcumHatasi("beklenmeyen-sayfa");
        }
    }
}
