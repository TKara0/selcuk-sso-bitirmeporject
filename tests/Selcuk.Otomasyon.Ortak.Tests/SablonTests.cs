using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Selcuk.Otomasyon.Ortak.Tests;

/// <summary>
/// Ortak otomasyon şablonu: rol tabanlı menü (gruplar, birleşim), kapsam yardımcısı, veri kaynağı ayarları ve seçimi,
/// AddSelcukOtomasyon doğrulamaları ve Api istemcisinin Bearer token / hata eşlemesi.
/// </summary>
public sealed class SablonTests
{
    private static readonly OtomasyonMenusu Menu = new(
    [
        new("Başvurum", "/Ogrenci/Basvurum", "", Roller.Ogrenci),
        new("İnceleme", "/Komisyon/Inceleme", "", Roller.StajKomisyonu),
        new("Rapor", "/Rapor", "", Roller.BolumBaskani, Roller.StajKomisyonu),
        new("Evrak", "/Evrak", "", Roller.BolumSekreteri),
    ]);

    private static ClaimsPrincipal Kullanici(params string[] roller) =>
        new(new ClaimsIdentity(roller.Select(r => new Claim("role", r)), "test", "name", "role"));

    // ---------- Menü ----------

    [Fact]
    public void Ogrenci_yalnizca_ogrenci_ogelerini_goruyor()
    {
        var gruplar = Menu.Gruplar(Kullanici(Roller.Ogrenci));

        var grup = Assert.Single(gruplar);
        Assert.Equal("Öğrenci", grup.Baslik);
        Assert.Equal(["/Ogrenci/Basvurum"], grup.Ogeler.Select(o => o.Sayfa));
    }

    [Fact]
    public void Cok_rollu_kullanici_birlesimi_katalog_sirasiyla_rol_basliklari_altinda_goruyor()
    {
        // Katalog sırası: bölüm başkanı (idari görev) staj komisyonundan önce. Rapor iki role de açık: ilk başlıkta, bir kez.
        var gruplar = Menu.Gruplar(Kullanici(Roller.StajKomisyonu, Roller.Akademisyen, Roller.BolumBaskani));

        Assert.Equal(["Bölüm başkanı", "Staj komisyonu üyesi"], gruplar.Select(g => g.Baslik));
        Assert.Equal(["/Rapor"], gruplar[0].Ogeler.Select(o => o.Sayfa));
        Assert.Equal(["/Komisyon/Inceleme"], gruplar[1].Ogeler.Select(o => o.Sayfa));
        Assert.Equal(["/Rapor", "/Komisyon/Inceleme"],
            Menu.Gorunenler(Kullanici(Roller.StajKomisyonu, Roller.BolumBaskani)).Select(o => o.Sayfa));
    }

    [Fact]
    public void Rolune_uyan_oge_yoksa_menu_bos()
    {
        Assert.Empty(Menu.Gruplar(Kullanici(Roller.Akademisyen, Roller.EtikKurulUyesi)));
        Assert.Empty(Menu.Gruplar(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    // ---------- Kapsam ----------

    [Fact]
    public void Kapsam_claimlerden_okunuyor_ve_kayitlari_suzuyor()
    {
        var kapsam = KullaniciKapsami.Oku(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", "203011001"),
            new Claim("bolum", "Bilgisayar Mühendisliği"),
            new Claim("fakulte", "Teknoloji Fakültesi"),
        ])));

        Assert.Equal(new KullaniciKapsami("203011001", "Bilgisayar Mühendisliği", "Teknoloji Fakültesi"), kapsam);
        Assert.True(kapsam.Kapsar(KayitKapsami.Kendi, "203011001", null, null));
        Assert.False(kapsam.Kapsar(KayitKapsami.Kendi, "203011002", "Bilgisayar Mühendisliği", "Teknoloji Fakültesi"));
        Assert.True(kapsam.Kapsar(KayitKapsami.Bolum, "x", "Bilgisayar Mühendisliği", "Teknoloji Fakültesi"));
        Assert.False(kapsam.Kapsar(KayitKapsami.Bolum, "x", "Elektrik-Elektronik Mühendisliği", "Teknoloji Fakültesi"));
        // Aynı adlı bölüm başka fakültede: kapsam dışı.
        Assert.False(kapsam.Kapsar(KayitKapsami.Bolum, "x", "Bilgisayar Mühendisliği", "Mühendislik Fakültesi"));
        Assert.True(kapsam.Kapsar(KayitKapsami.Fakulte, "x", "Elektrik-Elektronik Mühendisliği", "Teknoloji Fakültesi"));
        Assert.False(kapsam.Kapsar(KayitKapsami.Fakulte, "x", "Bilgisayar Mühendisliği", "Mühendislik Fakültesi"));
    }

    [Fact]
    public void Bos_kullanici_bilgisi_hicbir_kayda_uymuyor()
    {
        var bos = KullaniciKapsami.Oku(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(bos.Kapsar(KayitKapsami.Kendi, null, null, null));
        Assert.False(bos.Kapsar(KayitKapsami.Bolum, "x", null, null));
        Assert.False(bos.Kapsar(KayitKapsami.Fakulte, "x", null, null));
    }

    // ---------- Veri kaynağı ayarları ve seçimi ----------

    [Theory]
    [InlineData("Mock", null, true)]
    [InlineData("Api", "https://api.test.local/", true)]
    [InlineData("Api", "https://localhost:5402/", true)]
    [InlineData("Api", "http://localhost:5402/", false)]
    [InlineData("Api", "http://uzak.test.local/", false)]
    [InlineData("Api", null, false)]
    [InlineData("mock", null, false)]
    [InlineData("", null, false)]
    public void Veri_kaynagi_ayarlari_dogrulaniyor(string mod, string? adres, bool gecerli)
    {
        var hata = new VeriKaynagiAyarlari { Mod = mod, ApiAdresi = adres }.Hata();

        Assert.Equal(gecerli, hata is null);
    }

    private interface IOrnekKaynak;

    private sealed class MockKaynak : IOrnekKaynak;

    private sealed class ApiKaynak : IOrnekKaynak;

    private static ServiceProvider Servisler(Dictionary<string, string?> ayarlar, Action<OtomasyonSecenekleri>? ayarla = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kimlik:ClientId"] = "test-otomasyon",
            ["Kimlik:Authority"] = "https://kimlik.test.local/",
        }).AddInMemoryCollection(ayarlar).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSelcukOtomasyon(configuration, ayarla ?? (s =>
        {
            s.Ad = "Örnek Otomasyon";
            s.Simge = "ÖR";
            s.Menu.AddRange(Menu.Ogeler);
            s.VeriKaynagi<IOrnekKaynak, MockKaynak, ApiKaynak>();
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Moda_gore_uygulama_seciliyor_gecersiz_mod_hata_veriyor()
    {
        using (var mock = Servisler([]))
        {
            Assert.IsType<MockKaynak>(mock.CreateScope().ServiceProvider.GetRequiredService<IOrnekKaynak>());
        }

        using (var api = Servisler(new() { ["VeriKaynagi:Mod"] = "Api", ["VeriKaynagi:ApiAdresi"] = "https://api.test.local/" }))
        {
            Assert.IsType<ApiKaynak>(api.CreateScope().ServiceProvider.GetRequiredService<IOrnekKaynak>());
        }

        using var gecersiz = Servisler(new() { ["VeriKaynagi:Mod"] = "Veritabani" });
        var hata = Assert.Throws<OptionsValidationException>(() => gecersiz.GetRequiredService<IOptions<VeriKaynagiAyarlari>>().Value);
        Assert.Contains("'VeriKaynagi:Mod' ayarı geçersiz ('Veritabani')", hata.Message);
    }

    [Theory]
    [InlineData("", "/Sayfa", Roller.Ogrenci)]
    [InlineData("Ad", "Sayfa", Roller.Ogrenci)]
    [InlineData("Ad", "/Index", Roller.Ogrenci)]
    [InlineData("Ad", "/Sayfa", "dekan")]
    [InlineData("Ad", "/Sayfa", null)]
    public void Hatali_otomasyon_tanimi_acilista_duruyor(string ad, string sayfa, string? rol)
    {
        Assert.Throws<InvalidOperationException>(() => Servisler([], s =>
        {
            s.Ad = ad;
            s.Simge = "ÖR";
            s.Menu.Add(new MenuOgesi("Öğe", sayfa, "", rol is null ? [] : [rol]));
            s.VeriKaynagi<IOrnekKaynak, MockKaynak, ApiKaynak>();
        }));
    }

    [Fact]
    public void Veri_kaynagi_ve_ayni_sayfa_iki_kez_hata()
    {
        Assert.Throws<InvalidOperationException>(() => Servisler([], s => { s.Ad = "Ad"; s.Simge = "ÖR"; }));
        Assert.Throws<InvalidOperationException>(() => Servisler([], s =>
        {
            s.Ad = "Ad";
            s.Simge = "ÖR";
            s.Menu.Add(new MenuOgesi("A", "/Sayfa", "", Roller.Ogrenci));
            s.Menu.Add(new MenuOgesi("B", "/sayfa", "", Roller.Akademisyen));
            s.VeriKaynagi<IOrnekKaynak, MockKaynak, ApiKaynak>();
        }));
    }

    // ---------- Oturum bileti deposu ----------

    [Fact]
    public async Task Oturum_bileti_sunucuda_sifreli_saklaniyor_cerezde_yalnizca_anahtar()
    {
        using var servisler = Servisler([]);
        var ayarlar = servisler.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
            .Get(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
        var depo = Assert.IsType<OturumBiletDeposu>(ayarlar.SessionStore);
        var ozellikler = new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) };
        var token = new string('t', 3000);
        ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = token }]);
        var bilet = new AuthenticationTicket(Kullanici(Roller.Ogrenci), ozellikler, "Cookies");

        var anahtar = await depo.StoreAsync(bilet);

        Assert.Equal(32, anahtar.Length);
        Assert.Equal(token, (await depo.RetrieveAsync(anahtar))!.Properties.GetTokenValue("access_token"));
        Assert.Equal(Roller.Ogrenci, Assert.Single((await depo.RetrieveAsync(anahtar))!.Principal.Claims).Value);
        // Önbellekteki değer şifreli: token düz metin olarak geçmez.
        var ham = await servisler.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>()
            .GetAsync("otomasyon-oturum:" + anahtar);
        Assert.DoesNotContain(token[..100], System.Text.Encoding.UTF8.GetString(ham!));

        await depo.RemoveAsync(anahtar);
        Assert.Null(await depo.RetrieveAsync(anahtar));
    }

    // ---------- Api istemcisi ----------

    private sealed record Kayit(string Ad);

    private static (OtomasyonApiIstemcisi Istemci, SahteApi Api) ApiIstemcisi(Func<HttpRequestMessage, HttpResponseMessage> yanit,
        string? accessToken = "erisim-tokeni")
    {
        var api = new SahteApi(yanit);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAuthenticationService>(new SahteKimlik(accessToken));
        services.AddHttpClient(OtomasyonApiIstemcisi.HttpIstemciAdi, c => c.BaseAddress = new Uri("https://api.test.local/"))
            .ConfigurePrimaryHttpMessageHandler(() => api);
        var provider = services.BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = provider } };
        return (new OtomasyonApiIstemcisi(provider.GetRequiredService<IHttpClientFactory>(), accessor,
            NullLogger<OtomasyonApiIstemcisi>.Instance), api);
    }

    [Fact]
    public async Task Api_istemcisi_Bearer_token_gonderiyor_ve_json_okuyor()
    {
        var (istemci, api) = ApiIstemcisi(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"ad\":\"bir\"}]", System.Text.Encoding.UTF8, "application/json"),
        });

        var sonuc = await istemci.GetAsync<List<Kayit>>("api/ornek");

        Assert.Equal([new Kayit("bir")], sonuc);
        var istek = Assert.Single(api.Istekler);
        Assert.Equal("https://api.test.local/api/ornek", istek.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", istek.Headers.Authorization!.Scheme);
        Assert.Equal("erisim-tokeni", istek.Headers.Authorization.Parameter);
        // Token adreste yok.
        Assert.DoesNotContain("erisim-tokeni", istek.RequestUri.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, VeriKaynagiHatasi.Turu.Ulasilamiyor)]
    [InlineData(HttpStatusCode.BadGateway, VeriKaynagiHatasi.Turu.Ulasilamiyor)]
    [InlineData(HttpStatusCode.NotFound, VeriKaynagiHatasi.Turu.Ulasilamiyor)]
    [InlineData(HttpStatusCode.Unauthorized, VeriKaynagiHatasi.Turu.Yetki)]
    [InlineData(HttpStatusCode.Forbidden, VeriKaynagiHatasi.Turu.Yetki)]
    public async Task Api_istemcisi_durum_kodlarini_hata_turune_ceviriyor(HttpStatusCode durum, VeriKaynagiHatasi.Turu tur)
    {
        var (istemci, _) = ApiIstemcisi(_ => new HttpResponseMessage(durum));

        var hata = await Assert.ThrowsAsync<VeriKaynagiHatasi>(() => istemci.GetAsync<List<Kayit>>("api/ornek"));

        Assert.Equal(tur, hata.Tur);
    }

    [Fact]
    public async Task Api_istemcisi_baglanti_hatasi_ve_tokensiz_oturum()
    {
        var (kapali, _) = ApiIstemcisi(_ => throw new HttpRequestException("kapalı"));
        Assert.Equal(VeriKaynagiHatasi.Turu.Ulasilamiyor,
            (await Assert.ThrowsAsync<VeriKaynagiHatasi>(() => kapali.GetAsync<List<Kayit>>("api/ornek"))).Tur);

        var (tokensiz, api) = ApiIstemcisi(_ => new HttpResponseMessage(HttpStatusCode.OK), accessToken: null);
        Assert.Equal(VeriKaynagiHatasi.Turu.Yetki,
            (await Assert.ThrowsAsync<VeriKaynagiHatasi>(() => tokensiz.GetAsync<List<Kayit>>("api/ornek"))).Tur);
        Assert.Empty(api.Istekler);
    }

    private sealed class SahteApi(Func<HttpRequestMessage, HttpResponseMessage> yanit) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Istekler { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Istekler.Add(request);
            return Task.FromResult(yanit(request));
        }
    }

    /// <summary>Oturumdaki token'ı (SaveTokens) taklit eder: GetTokenAsync("access_token").</summary>
    private sealed class SahteKimlik(string? accessToken) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var ozellikler = new AuthenticationProperties();
            if (accessToken is not null)
            {
                ozellikler.StoreTokens([new AuthenticationToken { Name = "access_token", Value = accessToken }]);
            }

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity("test")), ozellikler, "test")));
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
