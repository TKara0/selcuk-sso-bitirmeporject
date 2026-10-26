using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Her örnek kendi SQLite in-memory veritabanını kullanır; testler LocalDB'ye ve
/// birbirlerine bağımlı değildir. "Testing" ortamında geçici (ephemeral) imzalama anahtarları
/// kullanılır ve Development'a özgü test kullanıcıları seed edilmez. ekHizmetler, uygulamanın servislerini test
/// için değiştirmeye yarar (ör. token ömrü); ayarlar, appsettings değerlerini geçersiz kılar. Her örnek kendi rastgele
/// anahtarlı özet anahtarını (Denetim:KullaniciAdiOzetAnahtari) verir; ayarlar ile değiştirilebilir veya boşaltılabilir.
/// Veritabanı adlandırılmış, paylaşımlı önbellekli bir in-memory veritabanıdır: her DbContext kendi bağlantısını açar
/// (arka plandaki denetim kaydı yazıcısı isteklerle eşzamanlı çalışır); açık tutulan _connection veritabanını yaşatır.
/// </summary>
public sealed partial class SelcukIdentityFactory : WebApplicationFactory<Program>
{
    private const string OturumCerezi = "SelcukSSO.Oturum";

    private readonly string _baglantiDizesi = $"Data Source=selcuk-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _connection;
    private readonly Action<IServiceCollection>? _ekHizmetler;
    private readonly IReadOnlyDictionary<string, string?>? _ayarlar;

    /// <summary>Back-Channel Logout POST'larını yakalayan sahte HTTP alıcısı (gerçek ağa çıkılmaz).</summary>
    public SahteBackChannelAlicisi BackChannel { get; } = new();

    /// <summary>Bu örneğin varsayılan HMAC anahtarı (ayarlar ile geçersiz kılınmadıysa uygulamanın kullandığı).</summary>
    public byte[] OzetAnahtari { get; } = RandomNumberGenerator.GetBytes(32);

    /// <summary>Gönderilen e-postaları yakalayan sahte gönderici (dosyaya veya ağa yazılmaz).</summary>
    public SahteEpostaGonderici Eposta { get; } = new();

    public SelcukIdentityFactory(Action<IServiceCollection>? ekHizmetler = null,
        IReadOnlyDictionary<string, string?>? ayarlar = null)
    {
        _ekHizmetler = ekHizmetler;
        _ayarlar = ayarlar;
        _connection = new SqliteConnection(_baglantiDizesi);
        _connection.Open();

        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseOpenIddict()
            .Options);
        db.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [AnahtarliOzet.AyarAdi] = Convert.ToBase64String(OzetAnahtari),
            });
            if (_ayarlar is not null)
            {
                config.AddInMemoryCollection(_ayarlar);
            }
        });

        // Ortak tasarım kütüphanesinin (Selcuk.Arayuz) _content/ dosyaları: ASP.NET Core bunları yalnızca Development'ta
        // derleme çıktısındaki manifestten sunar; yayımlanan uygulamada wwwroot/_content altındadır.
        builder.UseStaticWebAssets();

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_baglantiDizesi);
                options.UseOpenIddict();
            });
            services.AddHttpClient(BackChannelLogoutGonderici.HttpIstemciAdi)
                .ConfigurePrimaryHttpMessageHandler(() => BackChannel);
            services.RemoveAll<IEpostaGonderici>();
            services.AddSingleton<IEpostaGonderici>(Eposta);
            _ekHizmetler?.Invoke(services);
        });
    }

    public HttpClient HttpsIstemcisi() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
    });

    /// <summary>Rastgele adlı ve çalışma anında üretilmiş şifreli bir kullanıcı oluşturur.</summary>
    public async Task<(string KullaniciAdi, string Sifre)> KullaniciOlusturAsync(
        string? rol = null, string? fakulte = null, string? bolum = null)
    {
        var kullaniciAdi = $"test.{Guid.NewGuid():N}"[..13];
        var sifre = $"Aa1{Guid.NewGuid():N}";

        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var kullanici = new ApplicationUser
        {
            UserName = kullaniciAdi,
            Email = $"{kullaniciAdi}@test.local",
            EmailConfirmed = true,
            AdSoyad = "Test Kullanıcısı",
            Fakulte = fakulte,
            Bolum = bolum,
        };
        var sonuc = await userManager.CreateAsync(kullanici, sifre);
        if (sonuc.Succeeded && rol is not null)
        {
            sonuc = await userManager.AddToRoleAsync(kullanici, rol);
        }

        Assert.True(sonuc.Succeeded, string.Join("; ", sonuc.Errors.Select(e => e.Description)));
        return (kullaniciAdi, sifre);
    }

    public async Task<ApplicationUser> KullaniciAsync(string kullaniciAdi)
    {
        await using var scope = Services.CreateAsyncScope();
        var kullanici = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            .FindByNameAsync(kullaniciAdi);
        Assert.NotNull(kullanici);
        return kullanici;
    }

    /// <summary>demo-obis ve demo-yemekhane'yi uygulamanın kendi tanımlarıyla, rastgele secret'larla kaydeder.</summary>
    public async Task IstemcileriKaydetAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(Istemciler.DemoObis(Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))));
        await manager.CreateAsync(Istemciler.DemoYemekhane(Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))));
    }

    /// <summary>Token uç noktasının yazacağı oturum–istemci kaydını doğrudan ekler (tam OIDC akışı yerine).</summary>
    public async Task IstemciOturumuEkleAsync(string sid, string kullaniciId, string clientId, bool kapali = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.IstemciOturumlari.Add(new IstemciOturumu
        {
            Sid = sid,
            KullaniciId = kullaniciId,
            ClientId = clientId,
            OlusturmaZamani = DateTimeOffset.UtcNow,
            KapanmaZamani = kapali ? DateTimeOffset.UtcNow : null,
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<IstemciOturumu>> IstemciOturumlariAsync(string kullaniciId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.IstemciOturumlari.AsNoTracking().Where(o => o.KullaniciId == kullaniciId).ToListAsync();
    }

    /// <summary>Yanıtın yazdığı kimlik sunucusu oturum çerezini çözer ve oturum kimliğini (sid) döndürür.</summary>
    public string OturumSid(HttpResponseMessage yanit)
    {
        Assert.True(yanit.Headers.TryGetValues("Set-Cookie", out var cerezler), "Yanıt oturum çerezi yazmadı.");
        var cerez = cerezler.Single(c => c.StartsWith(OturumCerezi + "=", StringComparison.Ordinal));
        var deger = cerez[(OturumCerezi.Length + 1)..cerez.IndexOf(';')];

        var ayarlar = Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var bilet = ayarlar.TicketDataFormat.Unprotect(deger);
        Assert.NotNull(bilet);
        return bilet.Principal.FindFirst(OidcKimlik.SidClaim)!.Value;
    }

    /// <summary>
    /// Sayfayı açar ve formunu antiforgery token'ıyla birlikte gönderir (tarayıcının yapacağı gibi). Formun gönderildiği
    /// adres sayfadan farklıysa (ör. "?handler=…") postAdresi verilir.
    /// </summary>
    public static async Task<HttpResponseMessage> FormGonderAsync(
        HttpClient client, string sayfa, Dictionary<string, string> alanlar, string? postAdresi = null)
    {
        var form = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);

        alanlar["__RequestVerificationToken"] = AntiforgeryTokenAl(await form.Content.ReadAsStringAsync());
        return await client.PostAsync(postAdresi ?? sayfa, new FormUrlEncodedContent(alanlar));
    }

    /// <summary>
    /// Kullanıcıda 2FA'yı doğrudan Identity API'siyle açar (kurulum sayfasını test etmeyen testler için) ve paylaşılan
    /// anahtarı ile 10 kurtarma kodunu döndürür.
    /// </summary>
    public async Task<(string Anahtar, IReadOnlyList<string> KurtarmaKodlari)> IkiAdimAcAsync(string kullaniciAdi)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var kullanici = (await userManager.FindByNameAsync(kullaniciAdi))!;
        await userManager.ResetAuthenticatorKeyAsync(kullanici);
        var kodlar = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(kullanici, 10))!.ToList();
        await userManager.SetTwoFactorEnabledAsync(kullanici, true);
        return ((await userManager.GetAuthenticatorKeyAsync(kullanici))!, kodlar);
    }

    /// <summary>Kullanıcının doğrulayıcı uygulama anahtarı (kurulum sayfasında gösterilenin biçimlenmemiş hâli).</summary>
    public async Task<string?> AnahtarAsync(string kullaniciAdi)
    {
        await using var scope = Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.GetAuthenticatorKeyAsync((await userManager.FindByNameAsync(kullaniciAdi))!);
    }

    /// <summary>
    /// Denetim kayıtları arka planda yazıldığı için koşul sağlanana kadar (en fazla 10 sn) bekler ve tüm kayıtları
    /// döndürür. Koşul sağlanmazsa test başarısız olur.
    /// </summary>
    public async Task<List<DenetimKaydi>> DenetimKayitlariAsync(Func<List<DenetimKaydi>, bool> kosul)
    {
        var bitis = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            await using (var scope = Services.CreateAsyncScope())
            {
                var kayitlar = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                    .DenetimKayitlari.AsNoTracking().OrderBy(k => k.Id).ToListAsync();
                if (kosul(kayitlar))
                {
                    return kayitlar;
                }

                Assert.True(DateTime.UtcNow < bitis,
                    "Beklenen denetim kayıtları oluşmadı. Mevcut: " + string.Join(", ", kayitlar.Select(k => k.OlayTuru)));
            }

            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Bir tablonun tüm satırlarını, her sütunu metne çevrilmiş olarak EF Core'u atlayarak doğrudan SQL ile okur
    /// (veritabanında bir değerin hiçbir sütunda bulunmadığını doğrulamak için).
    /// </summary>
    public async Task<List<string?[]>> TabloAsync(string tablo)
    {
        await using var baglanti = new SqliteConnection(_baglantiDizesi);
        await baglanti.OpenAsync();
        await using var komut = baglanti.CreateCommand();
        komut.CommandText = $"SELECT * FROM \"{tablo}\"";
        await using var okuyucu = await komut.ExecuteReaderAsync();

        var satirlar = new List<string?[]>();
        while (await okuyucu.ReadAsync())
        {
            satirlar.Add([.. Enumerable.Range(0, okuyucu.FieldCount)
                .Select(i => okuyucu.IsDBNull(i) ? null : Convert.ToString(okuyucu.GetValue(i), CultureInfo.InvariantCulture))]);
        }

        return satirlar;
    }

    /// <summary>Girişin ikinci adımını (TOTP veya kurtarma kodu) gönderir.</summary>
    public static Task<HttpResponseMessage> IkinciAdimAsync(HttpClient client, string kod, bool cihaziHatirla = false) =>
        FormGonderAsync(client, "/Account/IkiAdim", new()
        {
            ["Input.Kod"] = kod,
            ["Input.CihaziHatirla"] = cihaziHatirla ? "true" : "false",
        });

    public static string AntiforgeryTokenAl(string html)
    {
        var token = AntiforgeryToken().Match(html).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token), "Formda antiforgery token bulunamadı.");
        return token;
    }

    /// <summary>Giriş formunu antiforgery token'ıyla birlikte gönderir.</summary>
    public static async Task<HttpResponseMessage> GirisYapAsync(HttpClient client, string kullaniciAdi, string sifre)
    {
        var sayfa = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, sayfa.StatusCode);

        var token = AntiforgeryTokenAl(await sayfa.Content.ReadAsStringAsync());

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.KullaniciAdi"] = kullaniciAdi,
            ["Input.Sifre"] = sifre,
            ["__RequestVerificationToken"] = token,
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
