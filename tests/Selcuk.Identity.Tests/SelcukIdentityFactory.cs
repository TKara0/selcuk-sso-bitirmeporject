using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Her örnek kendi SQLite in-memory veritabanını kullanır; testler LocalDB'ye ve
/// birbirlerine bağımlı değildir. "Testing" ortamında geçici (ephemeral) imzalama anahtarları
/// kullanılır ve Development'a özgü test kullanıcıları seed edilmez.
/// </summary>
public sealed partial class SelcukIdentityFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SelcukIdentityFactory()
    {
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
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(_connection);
                options.UseOpenIddict();
            });
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
