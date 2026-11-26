using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Başlangıç verisinin gösterim hesaplarında şifre eşitlemesi (<see cref="BaslangicVerisi.GosterimHesaplari"/>):
/// yalnızca Development'ta, veritabanındaki şifre ayardaki değerden farklıysa Identity ile sıfırlanır, oturumlar ve
/// token'lar kapanır, şifresiz denetim kaydı yazılır. Diğer test kullanıcıları, ölçüm kullanıcıları ve listede olmayan
/// hesaplar etkilenmez. Bütün şifreler çalışma anında rastgele üretilir.
/// </summary>
public sealed class TestSifresiEsitlemeTests : IDisposable
{
    private const string Hesap = "baskan.test";
    private const string HesapAnahtari = "BolumBaskani";
    private const string GosterimDisi = "203011001";
    private const string TumKapsamlar = "openid profile email roles offline_access notlar.oku";

    private readonly SelcukIdentityFactory _factory = new();
    private readonly List<string> _loglar = [];

    public void Dispose() => _factory.Dispose();

    private static string YeniSifre() => $"Aa1{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}";

    /// <summary>Bütün test kullanıcıları için rastgele şifreli ayarlar.</summary>
    private static Dictionary<string, string?> Ayarlar() =>
        BaslangicVerisi.TestKullanicilari.ToDictionary(
            t => $"TestKullanicilari:{t.AyarAnahtari}:Sifre", _ => (string?)YeniSifre());

    private static string Sifre(Dictionary<string, string?> ayarlar, string kullaniciAdi) =>
        ayarlar[$"TestKullanicilari:{BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == kullaniciAdi).AyarAnahtari}:Sifre"]!;

    /// <summary>Uygulamanın servisleriyle, verilen ortam adı ve ayarlarla başlangıç verisini bir kez çalıştırır.</summary>
    private Task AcilisAsync(Dictionary<string, string?> ayarlar, string ortam = "Development") =>
        new BaslangicVerisi(_factory.Services, new Ortam(ortam),
            new ConfigurationBuilder().AddInMemoryCollection(ayarlar).Build(), new Kaydedici(_loglar))
            .StartAsync(CancellationToken.None);

    private sealed record Oturum(HttpClient Client, string Refresh, string Sid);

    private async Task<Oturum> OturumAcAsync(string kullaniciAdi, string sifre, string secret)
    {
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.DemoObisClientId, Istemciler.DemoObisRedirectUri, TumKapsamlar);
        var yanit = await OidcYardimcisi.TokenIsteAsync(client, code, pkce.Verifier, secret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var sid = OidcYardimcisi.JwtGovdesi(json.RootElement.GetProperty("id_token").GetString()!).GetProperty("sid").GetString()!;
        return new Oturum(client, json.RootElement.GetProperty("refresh_token").GetString()!, sid);
    }

    private static Task<HttpResponseMessage> YenileAsync(Oturum oturum, string secret) =>
        oturum.Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.RefreshToken,
            [Parameters.RefreshToken] = oturum.Refresh,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
        }));

    private static async Task<bool> KimlikSunucusundaAcikAsync(Oturum oturum) =>
        (await oturum.Client.GetAsync("/Account/Hesabim")).StatusCode == HttpStatusCode.OK;

    private async Task<bool> GirisOlurAsync(string kullaniciAdi, string sifre) =>
        (await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, sifre)).StatusCode
        == HttpStatusCode.Redirect;

    private async Task<bool> SifreDogruAsync(string kullaniciAdi, string sifre)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.CheckPasswordAsync((await userManager.FindByNameAsync(kullaniciAdi))!, sifre);
    }

    private IEnumerable<string> BackChannelSidleri() =>
        _factory.BackChannel.Istekler.Where(i => i.Adres.AbsoluteUri == Istemciler.DemoObisBackChannelLogoutUri)
            .Select(i => OidcYardimcisi.JwtGovdesi(i.Form["logout_token"]).GetProperty("sid").GetString()!);

    /// <summary>Denetim kuyruğu sıralıdır: sonradan yapılan bir girişin kaydı yazıldıysa önceki olaylar da yazılmıştır.</summary>
    private async Task<List<DenetimKaydi>> SifirlamaKayitlariAsync(string girisYapan, string sifre)
    {
        var id = (await _factory.KullaniciAsync(girisYapan)).Id;
        var once = await _factory.DenetimKayitlariAsync(_ => true);
        var girisSayisi = once.Count(k => k.OlayTuru == DenetimOlayi.GirisBasarili && k.KullaniciId == id);
        Assert.True(await GirisOlurAsync(girisYapan, sifre));
        var kayitlar = await _factory.DenetimKayitlariAsync(k =>
            k.Count(x => x.OlayTuru == DenetimOlayi.GirisBasarili && x.KullaniciId == id) > girisSayisi);
        return [.. kayitlar.Where(k => k.OlayTuru == DenetimOlayi.SifreSifirlandi)];
    }

    [Fact]
    public async Task Development_acilisinda_degisen_sifre_esitleniyor_oturumlar_kapaniyor_denetimde_sifre_yok()
    {
        var ayarlar = Ayarlar();
        await AcilisAsync(ayarlar);
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var eski = Sifre(ayarlar, Hesap);
        var oturum = await OturumAcAsync(Hesap, eski, secret);
        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        var id = (await _factory.KullaniciAsync(Hesap)).Id;

        var yeni = YeniSifre();
        ayarlar[$"TestKullanicilari:{HesapAnahtari}:Sifre"] = yeni;
        await AcilisAsync(ayarlar);

        // Oturum, refresh token ve istemci oturumu kapandı.
        Assert.False(await KimlikSunucusundaAcikAsync(oturum));
        Assert.Equal(HttpStatusCode.BadRequest, (await YenileAsync(oturum, secret)).StatusCode);
        Assert.Equal([oturum.Sid], BackChannelSidleri());
        // Eski şifre reddediliyor, yeni şifreyle giriş oluyor.
        Assert.False(await GirisOlurAsync(Hesap, eski));
        var kayit = Assert.Single(await SifirlamaKayitlariAsync(Hesap, yeni));
        Assert.Equal(id, kayit.KullaniciId);
        Assert.Equal(DenetimSonucu.Basarili, kayit.Sonuc);
        Assert.Equal("neden=test-sifresi-esitleme", kayit.Ayrinti);
        // Şifreler denetim tablosunda, logda ve e-postada yok.
        var tablo = string.Join('\n', (await _factory.TabloAsync("DenetimKayitlari")).Select(s => string.Join('|', s)));
        foreach (var sifre in new[] { eski, yeni })
        {
            Assert.DoesNotContain(sifre, tablo);
            Assert.All(_loglar, satir => Assert.DoesNotContain(sifre, satir));
        }

        // Aynı ayarlarla tekrar açılış: değişiklik yok, açık oturum kalır, ikinci kayıt yazılmaz.
        var sonraki = await OturumAcAsync(Hesap, yeni, secret);
        await AcilisAsync(ayarlar);
        Assert.True(await KimlikSunucusundaAcikAsync(sonraki));
        Assert.Equal(HttpStatusCode.OK, (await YenileAsync(sonraki, secret)).StatusCode);
        Assert.Single(await SifirlamaKayitlariAsync(Hesap, yeni));
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task Development_disindaki_ortamlarda_esitleme_calismiyor(string ortam)
    {
        var ayarlar = Ayarlar();
        await AcilisAsync(ayarlar);
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var eski = Sifre(ayarlar, Hesap);
        var oturum = await OturumAcAsync(Hesap, eski, secret);

        var yeni = YeniSifre();
        ayarlar[$"TestKullanicilari:{HesapAnahtari}:Sifre"] = yeni;
        await AcilisAsync(ayarlar, ortam);

        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        Assert.Equal(HttpStatusCode.OK, (await YenileAsync(oturum, secret)).StatusCode);
        Assert.False(await SifreDogruAsync(Hesap, yeni));
        Assert.Empty(BackChannelSidleri());
        Assert.Empty(await SifirlamaKayitlariAsync(Hesap, eski));
    }

    [Fact]
    public async Task Test_listesinde_olmayan_olcum_ve_gosterim_disi_hesaplar_etkilenmiyor()
    {
        var ayarlar = Ayarlar();
        var olcumEski = YeniSifre();
        ayarlar[OlcumKullanicilari.SifreAyari] = olcumEski;
        await AcilisAsync(ayarlar);
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (baska, baskaSifre) = await _factory.KullaniciOlusturAsync();
        var gosterimDisiEski = Sifre(ayarlar, GosterimDisi);
        var baskaOturum = await OturumAcAsync(baska, baskaSifre, secret);
        var gosterimDisiOturum = await OturumAcAsync(GosterimDisi, gosterimDisiEski, secret);

        // Bütün ayar değerleri değişiyor (listede olmayan kullanıcı adıyla bir anahtar da veriliyor).
        var yeniAyarlar = Ayarlar();
        var olcumYeni = YeniSifre();
        yeniAyarlar[OlcumKullanicilari.SifreAyari] = olcumYeni;
        yeniAyarlar[$"TestKullanicilari:{baska}:Sifre"] = YeniSifre();
        await AcilisAsync(yeniAyarlar);

        foreach (var oturum in new[] { baskaOturum, gosterimDisiOturum })
        {
            Assert.True(await KimlikSunucusundaAcikAsync(oturum));
            Assert.Equal(HttpStatusCode.OK, (await YenileAsync(oturum, secret)).StatusCode);
        }

        Assert.True(await SifreDogruAsync(baska, baskaSifre));
        Assert.True(await SifreDogruAsync(GosterimDisi, gosterimDisiEski));
        Assert.True(await SifreDogruAsync(OlcumKullanicilari.KullaniciAdi(1), olcumEski));
        Assert.False(await SifreDogruAsync(OlcumKullanicilari.KullaniciAdi(1), olcumYeni));
        Assert.Empty(BackChannelSidleri());
        // Gösterim dışı test kullanıcılarının hiçbirinin şifresi değişmedi; gösterim hesaplarının hepsininki değişti.
        foreach (var tanim in BaslangicVerisi.TestKullanicilari)
        {
            var beklenen = BaslangicVerisi.GosterimHesaplari.Contains(tanim.KullaniciAdi) ? yeniAyarlar : ayarlar;
            Assert.True(await SifreDogruAsync(tanim.KullaniciAdi, Sifre(beklenen, tanim.KullaniciAdi)), tanim.KullaniciAdi);
        }

        var kayitlar = await SifirlamaKayitlariAsync(Hesap, Sifre(yeniAyarlar, Hesap));
        Assert.Equal(BaslangicVerisi.GosterimHesaplari.Count, kayitlar.Count);
    }

    [Fact]
    public async Task Eksik_veya_politikaya_uymayan_deger_sifreyi_ve_oturumu_degistirmiyor_iki_adim_acik_kaliyor()
    {
        var ayarlar = Ayarlar();
        await AcilisAsync(ayarlar);
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var eski = Sifre(ayarlar, Hesap);
        var oturum = await OturumAcAsync(Hesap, eski, secret);

        // Değer yok.
        var eksik = new Dictionary<string, string?>(ayarlar);
        eksik.Remove($"TestKullanicilari:{HesapAnahtari}:Sifre");
        await AcilisAsync(eksik);
        // Politikaya uymayan değer (kısa, büyük harf ve rakam yok): reddedilir, loga yazılmaz.
        const string Zayif = "zayifdeg";
        var zayif = new Dictionary<string, string?>(ayarlar) { [$"TestKullanicilari:{HesapAnahtari}:Sifre"] = Zayif };
        await AcilisAsync(zayif);

        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        Assert.True(await SifreDogruAsync(Hesap, eski));
        Assert.Empty(BackChannelSidleri());
        Assert.All(_loglar, satir => Assert.DoesNotContain(Zayif, satir));
        Assert.Empty(await SifirlamaKayitlariAsync(Hesap, eski));

        // 2FA açık hesapta şifre eşitlenir, 2FA açık kalır.
        await _factory.IkiAdimAcAsync(Hesap);
        var yeni = YeniSifre();
        ayarlar[$"TestKullanicilari:{HesapAnahtari}:Sifre"] = yeni;
        await AcilisAsync(ayarlar);
        Assert.True(await SifreDogruAsync(Hesap, yeni));
        Assert.True((await _factory.KullaniciAsync(Hesap)).TwoFactorEnabled);
    }

    private sealed class Ortam(string ad) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = ad;
        public string ApplicationName { get; set; } = "Selcuk.Identity";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Kaydedici(List<string> satirlar) : ILogger<BaslangicVerisi>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => satirlar.Add(formatter(state, exception) + exception);
    }
}
