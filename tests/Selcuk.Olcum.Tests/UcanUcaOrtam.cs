extern alias api;
extern alias kimlik;
extern alias obis;
extern alias yemekhane;

using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using kimlik::Selcuk.Identity.Data;
using kimlik::Selcuk.Identity.Denetim;
using kimlik::Selcuk.Identity.Oidc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Selcuk.Olcum.Tests;

/// <summary>
/// Kimlik sunucusu, Demo OBİS, Demo Yemekhane ve Demo API'yi bellek içinde (TestServer) birlikte çalıştırır. Gerçek
/// ağa çıkılmaz: https://localhost:5001/5101/5102/5201 adreslerine giden her istek (ölçüm aracının, OIDC
/// backchannel'ının, JWKS'nin, Notlarım → API'nin, Back-Channel Logout'un) porta göre ilgili TestServer'a yönlendirilir.
/// Kimlik sunucusu Testing ortamında, kendi SQLite in-memory veritabanıyla çalışır.
/// </summary>
public sealed class UcanUcaOrtam : IDisposable
{
    public const string Sifre = "Olcum-Test-Sifresi-2026";
    public static readonly Uri Kimlik = new("https://localhost:5001/");
    public static readonly Uri Obis = new("https://localhost:5101/");
    public static readonly Uri Yemekhane = new("https://localhost:5102/");
    public static readonly Uri Api = new("https://localhost:5201/");

    private readonly Yonlendirici _yonlendirici = new();
    private readonly KimlikFabrikasi _kimlik;
    private readonly IstemciFabrikasi<obis::Program> _obis;
    private readonly IstemciFabrikasi<yemekhane::Program> _yemekhane;
    private readonly ApiFabrikasi _api;

    public UcanUcaOrtam()
    {
        ObisSecret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        YemekhaneSecret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        _kimlik = new KimlikFabrikasi(_yonlendirici);
        _obis = new IstemciFabrikasi<obis::Program>(_yonlendirici, ObisSecret, services => services
            .AddHttpClient(obis::Selcuk.Demo.Obis.Data.NotlarApiIstemcisi.Ad)
            .ConfigurePrimaryHttpMessageHandler(_yonlendirici.Isleyici));
        _yemekhane = new IstemciFabrikasi<yemekhane::Program>(_yonlendirici, YemekhaneSecret, null);
        _api = new ApiFabrikasi(_yonlendirici);

        _yonlendirici.Ekle(Kimlik.Port, () => _kimlik.Server);
        _yonlendirici.Ekle(Obis.Port, () => _obis.Server);
        _yonlendirici.Ekle(Yemekhane.Port, () => _yemekhane.Server);
        _yonlendirici.Ekle(Api.Port, () => _api.Server);
    }

    public string ObisSecret { get; }

    public string YemekhaneSecret { get; }

    /// <summary>Ölçüm aracının çerez kabı için işleyici: çerezler kapta, istekler TestServer'lara.</summary>
    public HttpMessageHandler TarayiciIsleyicisi(CookieContainer cerezler) =>
        new CerezHandler(cerezler, _yonlendirici.Isleyici(), Gunluk);

    /// <summary>Aracın istekleri: "GET https://localhost:5101/Notlarim → 302" (sorgu dizesi olmadan; teşhis için).</summary>
    public ConcurrentQueue<string> Gunluk { get; } = new();

    /// <summary>Demo istemcileri uygulamaların kendi tanımlarıyla ve ölçüm kullanıcılarını (ogrenci) oluşturur.</summary>
    public async Task HazirlaAsync(int kullaniciSayisi)
    {
        await using var scope = _kimlik.Services.CreateAsyncScope();
        var uygulamalar = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await uygulamalar.CreateAsync(Istemciler.DemoObis(ObisSecret));
        await uygulamalar.CreateAsync(Istemciler.DemoYemekhane(YemekhaneSecret));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        for (var no = 1; no <= kullaniciSayisi; no++)
        {
            var ad = OlcumCalistirici.KullaniciAdi(no);
            var kullanici = new ApplicationUser
            {
                UserName = ad,
                Email = ad + "@test.local",
                EmailConfirmed = true,
                AdSoyad = "Ölçüm Kullanıcısı",
            };
            var sonuc = await userManager.CreateAsync(kullanici, Sifre);
            Assert.True(sonuc.Succeeded, string.Join("; ", sonuc.Errors.Select(e => e.Description)));
            Assert.True((await userManager.AddToRoleAsync(kullanici, Roller.Ogrenci)).Succeeded);
        }
    }

    public void Dispose()
    {
        _obis.Dispose();
        _yemekhane.Dispose();
        _api.Dispose();
        _kimlik.Dispose();
    }

    /// <summary>İsteği hedef portun TestServer'ına iletir; bilinmeyen adres hata verir (ağa çıkılmaz).</summary>
    private sealed class Yonlendirici
    {
        private readonly ConcurrentDictionary<int, Lazy<HttpMessageInvoker>> _sunucular = new();

        public void Ekle(int port, Func<TestServer> sunucu) =>
            _sunucular[port] = new Lazy<HttpMessageInvoker>(() => new HttpMessageInvoker(sunucu().CreateHandler()));

        public HttpMessageHandler Isleyici() => new Handler(this);

        private sealed class Handler(Yonlendirici sahip) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var adres = request.RequestUri!;
                return adres.Host == "localhost" && sahip._sunucular.TryGetValue(adres.Port, out var sunucu)
                    ? sunucu.Value.SendAsync(request, cancellationToken)
                    : throw new HttpRequestException($"Testte bilinmeyen adres: {adres.GetLeftPart(UriPartial.Authority)}");
            }
        }
    }

    /// <summary>TestServer çerez tutmaz; tarayıcı gibi çerezleri CookieContainer'da saklar ve gönderir.</summary>
    private sealed class CerezHandler(CookieContainer cerezler, HttpMessageHandler ic, ConcurrentQueue<string> gunluk)
        : DelegatingHandler(ic)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var adres = request.RequestUri!;
            if (cerezler.GetCookieHeader(adres) is { Length: > 0 } baslik)
            {
                request.Headers.Add("Cookie", baslik);
            }

            var yanit = await base.SendAsync(request, cancellationToken);
            gunluk.Enqueue($"{request.Method} {adres.GetLeftPart(UriPartial.Path)} → {(int)yanit.StatusCode}");
            if (yanit.Headers.TryGetValues("Set-Cookie", out var degerler))
            {
                foreach (var deger in degerler)
                {
                    cerezler.SetCookies(adres, deger);
                }
            }

            return yanit;
        }
    }

    private sealed class KimlikFabrikasi : WebApplicationFactory<kimlik::Program>
    {
        private readonly string _baglantiDizesi = $"Data Source=olcum-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        private readonly SqliteConnection _baglanti;
        private readonly Yonlendirici _yonlendirici;

        public KimlikFabrikasi(Yonlendirici yonlendirici)
        {
            _yonlendirici = yonlendirici;
            _baglanti = new SqliteConnection(_baglantiDizesi);
            _baglanti.Open();
            using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_baglanti).UseOpenIddict().Options);
            db.Database.EnsureCreated();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(AnahtarliOzet.AyarAdi, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_baglantiDizesi).UseOpenIddict());
                services.AddHttpClient(BackChannelLogoutGonderici.HttpIstemciAdi)
                    .ConfigurePrimaryHttpMessageHandler(_yonlendirici.Isleyici);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _baglanti.Dispose();
            }
        }
    }

    private sealed class IstemciFabrikasi<TProgram>(Yonlendirici yonlendirici, string secret, Action<IServiceCollection>? ek)
        : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Kimlik:ClientSecret", secret);
            builder.ConfigureTestServices(services =>
            {
                services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
                    options => options.BackchannelHttpHandler = yonlendirici.Isleyici());
                ek?.Invoke(services);
            });
        }
    }

    private sealed class ApiFabrikasi(Yonlendirici yonlendirici) : WebApplicationFactory<api::Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services => services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.BackchannelHttpHandler = yonlendirici.Isleyici()));
        }
    }
}
