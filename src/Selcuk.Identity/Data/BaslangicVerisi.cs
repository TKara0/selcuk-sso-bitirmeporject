using Microsoft.AspNetCore.Identity;

namespace Selcuk.Identity.Data;

/// <summary>
/// Uygulama açılışında rolleri oluşturur. Yalnızca Development ortamında, şifreleri
/// user-secrets'tan okunan test kullanıcılarını ekler.
/// </summary>
public sealed class BaslangicVerisi(
    IServiceProvider services,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<BaslangicVerisi> logger) : IHostedService
{
    private sealed record TestKullanicisi(
        string AyarAnahtari, string KullaniciAdi, string Email, string AdSoyad,
        string? Fakulte, string? Bolum, string Rol);

    private static readonly TestKullanicisi[] TestKullanicilari =
    [
        new("Ogrenci", "203011001", "203011001@test.local", "Test Öğrenci",
            "Teknoloji Fakültesi", "Bilgisayar Mühendisliği", Roller.Ogrenci),
        new("Akademisyen", "hoca.test", "hoca.test@test.local", "Test Akademisyen",
            "Teknoloji Fakültesi", "Bilgisayar Mühendisliği", Roller.Akademisyen),
        new("Personel", "personel.test", "personel.test@test.local", "Test Personel",
            "Teknoloji Fakültesi", null, Roller.Personel),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in Roller.Tumu)
        {
            if (await roleManager.RoleExistsAsync(rol))
            {
                continue;
            }

            var sonuc = await roleManager.CreateAsync(new IdentityRole(rol));
            if (!sonuc.Succeeded)
            {
                throw new InvalidOperationException(
                    $"'{rol}' rolü oluşturulamadı: {string.Join("; ", sonuc.Errors.Select(e => e.Description))}");
            }
        }

        if (environment.IsDevelopment())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var kullanici in TestKullanicilari)
            {
                await TestKullanicisiOlusturAsync(userManager, kullanici);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TestKullanicisiOlusturAsync(UserManager<ApplicationUser> userManager, TestKullanicisi bilgi)
    {
        if (await userManager.FindByNameAsync(bilgi.KullaniciAdi) is not null)
        {
            return;
        }

        var sifre = configuration[$"TestKullanicilari:{bilgi.AyarAnahtari}:Sifre"];
        if (string.IsNullOrEmpty(sifre))
        {
            logger.LogWarning(
                "Test kullanıcısı {KullaniciAdi} atlandı: 'TestKullanicilari:{Anahtar}:Sifre' user-secrets'ta tanımlı değil.",
                bilgi.KullaniciAdi, bilgi.AyarAnahtari);
            return;
        }

        var kullanici = new ApplicationUser
        {
            UserName = bilgi.KullaniciAdi,
            Email = bilgi.Email,
            EmailConfirmed = true,
            AdSoyad = bilgi.AdSoyad,
            Fakulte = bilgi.Fakulte,
            Bolum = bilgi.Bolum,
        };

        var sonuc = await userManager.CreateAsync(kullanici, sifre);
        if (sonuc.Succeeded)
        {
            sonuc = await userManager.AddToRoleAsync(kullanici, bilgi.Rol);
        }

        if (sonuc.Succeeded)
        {
            logger.LogInformation("Test kullanıcısı oluşturuldu: {KullaniciAdi} ({Rol})", bilgi.KullaniciAdi, bilgi.Rol);
        }
        else
        {
            logger.LogError("Test kullanıcısı {KullaniciAdi} oluşturulamadı: {Hatalar}",
                bilgi.KullaniciAdi, string.Join("; ", sonuc.Errors.Select(e => e.Description)));
        }
    }
}
