using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Selcuk.Identity.Hesap;
using Selcuk.Identity.Pages.Account;

namespace Selcuk.Identity.Tests;

/// <summary>
/// İstek sınırı ve kullanıcı adı tahmini koruması. Yanıt süreleri ölçülmez (kırılgan olurdu); iki yolun aynı işi
/// yaptığı kodda gözden geçirilmiştir (README > Güvenlik önlemleri).
/// </summary>
public sealed class KabaKuvvetTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Giris_POST_siniri_asilinca_429()
    {
        var client = _factory.HttpsIstemcisi();
        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek; i++)
        {
            var deneme = await SelcukIdentityFactory.GirisYapAsync(client, "olmayan.kullanici", "YanlisSifre123");
            Assert.Equal(HttpStatusCode.OK, deneme.StatusCode);
        }

        var yanit = await SelcukIdentityFactory.GirisYapAsync(client, "olmayan.kullanici", "YanlisSifre123");

        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);
        Assert.Equal("text/html", yanit.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Çok fazla deneme", await yanit.Content.ReadAsStringAsync());
        Assert.True(yanit.Headers.RetryAfter is not null, "Retry-After başlığı yok.");

        // Yalnızca POST sayılır: giriş sayfası açılmaya devam eder.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
    }

    [Theory]
    [InlineData("/Account/IkiAdim")]
    [InlineData("/Account/IkiAdimDogrulama")]
    [InlineData("/Account/SifreDegistir")]
    public async Task Iki_adim_ve_sifre_degistirme_POST_siniri_asilinca_429_ve_denetim_kaydi(string sayfa)
    {
        // İstek sınırı kimlik doğrulama ve antiforgery'den önce çalışır: geçersiz POST'lar da sayılır.
        var client = _factory.HttpsIstemcisi();
        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek; i++)
        {
            var deneme = await client.PostAsync(sayfa, new FormUrlEncodedContent([]));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, deneme.StatusCode);
        }

        var yanit = await client.PostAsync(sayfa, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.TooManyRequests, yanit.StatusCode);
        Assert.Contains("Çok fazla deneme", await yanit.Content.ReadAsStringAsync());
        await _factory.DenetimKayitlariAsync(k =>
            k.Any(x => x.OlayTuru == Data.DenetimOlayi.HizSiniriAsildi && x.Ayrinti == $"sayfa={sayfa}"));
    }

    [Fact]
    public async Task Olmayan_kullanici_ve_yanlis_sifre_ayni_mesaji_donuyor()
    {
        const string olmayan = "olmayan.kullanici";
        var (kullaniciAdi, _) = await _factory.KullaniciOlusturAsync();

        var yanlisSifre = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), kullaniciAdi, "YanlisSifre123");
        var kullaniciYok = await SelcukIdentityFactory.GirisYapAsync(_factory.HttpsIstemcisi(), olmayan, "YanlisSifre123");

        Assert.Equal(HttpStatusCode.OK, yanlisSifre.StatusCode);
        Assert.Equal(yanlisSifre.StatusCode, kullaniciYok.StatusCode);

        // Formda yazılan kullanıcı adı ve antiforgery token'ı dışında sayfalar birebir aynı.
        var a = Normallestir(await yanlisSifre.Content.ReadAsStringAsync(), kullaniciAdi);
        var b = Normallestir(await kullaniciYok.Content.ReadAsStringAsync(), olmayan);
        Assert.Contains(LoginModel.GenelHataMesaji, WebUtility.HtmlDecode(a));
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", "false", false)]
    [InlineData("Development", null, false)]
    [InlineData("Testing", "true", false)]
    [InlineData("Production", "true", false)]
    public void Olcum_gevsetmesi_yalnizca_Developmentta_ve_acik_anahtarla_etkin(string ortam, string? anahtar, bool beklenen)
    {
        var ayarlar = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OlcumKullanicilari.GevsetmeAyari] = anahtar })
            .Build();

        Assert.Equal(beklenen, OlcumKullanicilari.GevsetmeEtkinMi(new Ortam(ortam), ayarlar));
    }

    [Theory]
    [InlineData("olcum.kullanici.01", true)]
    [InlineData("OLCUM.KULLANICI.50", true)]
    [InlineData(" olcum.kullanici.07 ", true)]
    [InlineData("olcum.kullanici.00", false)]
    [InlineData("olcum.kullanici.51", false)]
    [InlineData("olcum.kullanici.1", false)]
    [InlineData("olcum.kullanici.01x", false)]
    [InlineData("203011001", false)]
    [InlineData(null, false)]
    public void Olcum_kullanicisi_yalnizca_01_50_arasi_adlar(string? ad, bool beklenen) =>
        Assert.Equal(beklenen, OlcumKullanicilari.OlcumKullanicisiMi(ad));

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public async Task Gevsetme_anahtar_kapaliyken_veya_Development_disinda_olcum_kullanicisi_icin_de_devrede_degil(string? anahtar)
    {
        // Testing ortamı: anahtar açık olsa da gevşetme yok (yalnızca Development).
        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            [OlcumKullanicilari.GevsetmeAyari] = anahtar,
        });
        Assert.False(factory.Services.GetRequiredService<OlcumGevsetmesi>().Etkin);

        await SinirAsilincaReddediliyorAsync(factory.HttpsIstemcisi(), OlcumKullanicilari.KullaniciAdi(1));
    }

    [Fact]
    public async Task Gevsetme_etkinken_olcum_kullanicisi_olmayan_icin_sinir_devrede_olcum_kullanicisi_sayilmiyor()
    {
        // Development + Olcum:HizSiniriGevset=true kararının sonucu (karar yukarıdaki testte doğrulanıyor).
        using var factory = new SelcukIdentityFactory(services => services.AddSingleton(new OlcumGevsetmesi(true)));
        var client = factory.HttpsIstemcisi();

        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek + 5; i++)
        {
            var olcum = await SelcukIdentityFactory.GirisYapAsync(client, OlcumKullanicilari.KullaniciAdi(2), "YanlisSifre123");
            Assert.Equal(HttpStatusCode.OK, olcum.StatusCode);
        }

        // Ölçüm hesabının denemeleri sayılmadı; başka bir kullanıcı adı için sınır aynen geçerli.
        await SinirAsilincaReddediliyorAsync(client, "olmayan.kullanici");
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SelcukIdentityFactory.GirisYapAsync(client, "olcum.kullanici.99", "YanlisSifre123")).StatusCode);
    }

    private static async Task SinirAsilincaReddediliyorAsync(HttpClient client, string kullaniciAdi)
    {
        for (var i = 0; i < HizSiniri.VarsayilanDakikadaIstek; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, "YanlisSifre123")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, "YanlisSifre123")).StatusCode);
    }

    private sealed class Ortam(string ad) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = ad;
        public string ApplicationName { get; set; } = "Selcuk.Identity";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string Normallestir(string html, string kullaniciAdi) =>
        html.Replace(SelcukIdentityFactory.AntiforgeryTokenAl(html), "TOKEN").Replace(kullaniciAdi, "KULLANICI");
}
