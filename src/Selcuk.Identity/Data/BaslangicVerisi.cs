using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using Selcuk.Identity.Hesap;

namespace Selcuk.Identity.Data;

/// <summary>
/// Uygulama açılışında rol kataloğundaki (Selcuk.Roller) bütün rolleri oluşturur. Yalnızca Development ortamında,
/// şifreleri user-secrets'tan okunan test kullanıcılarını (rolleri <see cref="RolDogrulayici"/>'dan geçer; var olan
/// kullanıcıya yalnızca eksik rolleri eklenir), ölçüm kullanıcılarını (<see cref="OlcumKullanicilari"/>), demo
/// istemcileri (demo-obis, demo-yemekhane), Staj Otomasyonu'nu (staj-otomasyonu) ve Tek Kapı'yı (public istemci,
/// izinli kullanıcılar Istemciler:TekKapi:IzinliKullaniciAdlari ayarından) ekler veya günceller.
/// </summary>
public sealed class BaslangicVerisi(
    IServiceProvider services,
    IHostEnvironment environment,
    IConfiguration configuration,
    ILogger<BaslangicVerisi> logger) : IHostedService
{
    /// <summary>Development test kullanıcısı; şifresi TestKullanicilari:{AyarAnahtari}:Sifre (user-secrets).</summary>
    public sealed record TestKullanicisi(
        string AyarAnahtari, string KullaniciAdi, string AdSoyad, string? Fakulte, string? Bolum,
        params string[] Roller)
    {
        public string Email => $"{KullaniciAdi}@test.local";
    }

    private const string Teknoloji = "Teknoloji Fakültesi";
    private const string Bilgisayar = "Bilgisayar Mühendisliği";

    /// <summary>Uydurma test kullanıcıları (yalnızca Development). Roller açılışta <see cref="RolDogrulayici"/>'dan geçer.</summary>
    public static readonly IReadOnlyList<TestKullanicisi> TestKullanicilari =
    [
        new("Ogrenci", "203011001", "Test Öğrenci", Teknoloji, Bilgisayar, Roller.Ogrenci),
        new("Ogrenci2", "203011002", "Test Öğrenci İki", Teknoloji, Bilgisayar, Roller.Ogrenci),
        new("Ogrenci3", "203011003", "Test Öğrenci Üç", Teknoloji, Bilgisayar, Roller.Ogrenci),
        new("Akademisyen", "hoca.test", "Test Akademisyen", Teknoloji, Bilgisayar, Roller.Akademisyen, Roller.Danisman),
        new("Personel", "personel.test", "Test Personel", Teknoloji, null, Roller.Personel),
        new("BolumBaskani", "baskan.test", "Test Bölüm Başkanı", Teknoloji, Bilgisayar,
            Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi),
        new("BolumSekreteri", "sekreter.test", "Test Bölüm Sekreteri", Teknoloji, Bilgisayar,
            Roller.Personel, Roller.BolumSekreteri),
        new("FakulteSekreteri", "fakultesekreter.test", "Test Fakülte Sekreteri", Teknoloji, null,
            Roller.Personel, Roller.FakulteSekreteri),
        new("Komisyon", "komisyon.test", "Test Komisyon Üyesi", Teknoloji, Bilgisayar,
            Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.StajKomisyonu, Roller.MezuniyetKomisyonu,
            Roller.BolumKuruluUyesi),
        new("Mudek", "mudek.test", "Test MÜDEK Üyesi", Teknoloji, Bilgisayar,
            Roller.Akademisyen, Roller.MudekKomisyonu, Roller.BolumKuruluUyesi),
        new("Etik", "etik.test", "Test Etik Kurul Üyesi", Teknoloji, Bilgisayar, Roller.Akademisyen, Roller.EtikKurulUyesi),
        new("Koordinator", "koordinator.test", "Test ÇAP-Yandal Koordinatörü", Teknoloji, Bilgisayar,
            Roller.Akademisyen, Roller.CapYandalKoordinatoru, Roller.Danisman),
        new("Iskur", "iskur.test", "Test İŞKUR Sorumlusu", Teknoloji, Bilgisayar, Roller.Personel, Roller.IskurSorumlusu),
    ];

    /// <summary>Seed tanımlarının rolleri kurallara uymuyorsa açılış bu hatayla durur.</summary>
    public static void TanimlariDogrula(IEnumerable<TestKullanicisi> tanimlar)
    {
        foreach (var tanim in tanimlar)
        {
            RolDogrulayici.Dogrula(tanim.KullaniciAdi, tanim.Roller, tanim.Fakulte, tanim.Bolum);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        TanimlariDogrula(TestKullanicilari);

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

        // Demo API kapsamı (notlar.oku -> aud demo-api) her ortamda tanımlı olmalı: authorize kaynakları buradan okur.
        // Staj API'sinin kapsamları (staj.oku, staj.yaz -> aud staj-api) da aynı şekilde.
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        foreach (var kapsam in (IEnumerable<OpenIddictScopeDescriptor>)[Istemciler.NotlarOku(), .. Istemciler.StajKapsamlari()])
        {
            if (await scopeManager.FindByNameAsync(kapsam.Name!) is { } mevcutKapsam)
            {
                await scopeManager.UpdateAsync(mevcutKapsam, kapsam);
            }
            else
            {
                await scopeManager.CreateAsync(kapsam);
            }
        }

        if (environment.IsDevelopment())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var kullanici in TestKullanicilari)
            {
                await TestKullanicisiOlusturAsync(userManager, kullanici,
                    configuration[$"TestKullanicilari:{kullanici.AyarAnahtari}:Sifre"], logger);
            }

            await OlcumKullanicilariniOlusturAsync(userManager);

            var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            await IstemciKaydetAsync(applicationManager, "Istemciler:DemoObis:ClientSecret", Istemciler.DemoObis);
            await IstemciKaydetAsync(applicationManager, "Istemciler:DemoYemekhane:ClientSecret", Istemciler.DemoYemekhane);
            await IstemciKaydetAsync(applicationManager, "Istemciler:StajOtomasyonu:ClientSecret", Istemciler.StajOtomasyonu);

            // Public istemci: secret yok. Liste yoksa veya boşsa varsayılan izinli kullanıcı (herkese açılmaz).
            var izinliler = configuration.GetSection(Istemciler.TekKapiAyarlari + ":IzinliKullaniciAdlari").Get<string[]>() ?? [];
            await KaydetVeyaGuncelleAsync(applicationManager, Istemciler.TekKapi(izinliler));
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task IstemciKaydetAsync(
        IOpenIddictApplicationManager applicationManager,
        string secretAnahtari,
        Func<string, OpenIddictApplicationDescriptor> tanim)
    {
        var secret = configuration[secretAnahtari];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogWarning("İstemci kaydedilmedi: '{Anahtar}' user-secrets'ta tanımlı değil.", secretAnahtari);
            return;
        }

        await KaydetVeyaGuncelleAsync(applicationManager, tanim(secret));
    }

    private async Task KaydetVeyaGuncelleAsync(
        IOpenIddictApplicationManager applicationManager, OpenIddictApplicationDescriptor descriptor)
    {
        // http yalnızca 127.0.0.1 loopback adreslerinde kabul edilir; aksi hâlde açılış durur.
        Istemciler.AdresleriDogrula(descriptor);
        var mevcut = await applicationManager.FindByClientIdAsync(descriptor.ClientId!);
        if (mevcut is null)
        {
            await applicationManager.CreateAsync(descriptor);
            logger.LogInformation("İstemci kaydedildi: {ClientId}", descriptor.ClientId);
        }
        else
        {
            // Tanım (izinler, adresler, özel ayarlar, secret) her açılışta user-secrets, ayarlar ve koddaki hâliyle
            // eşitlenir.
            await applicationManager.UpdateAsync(mevcut, descriptor);
            logger.LogInformation("İstemci güncellendi: {ClientId}", descriptor.ClientId);
        }
    }

    /// <summary>
    /// olcum.kullanici.01 … 50 (ogrenci, 2FA kapalı); hepsinin şifresi OlcumKullanicilari:Sifre (user-secrets). Şifre
    /// tanımlı değilse atlanır. Var olan hesaba dokunulmaz.
    /// </summary>
    private async Task OlcumKullanicilariniOlusturAsync(UserManager<ApplicationUser> userManager)
    {
        var sifre = configuration[OlcumKullanicilari.SifreAyari];
        if (string.IsNullOrEmpty(sifre))
        {
            logger.LogInformation("Ölçüm kullanıcıları atlandı: '{Anahtar}' user-secrets'ta tanımlı değil.",
                OlcumKullanicilari.SifreAyari);
            return;
        }

        var olusturulan = 0;
        for (var no = 1; no <= OlcumKullanicilari.Sayi; no++)
        {
            var kullaniciAdi = OlcumKullanicilari.KullaniciAdi(no);
            if (await userManager.FindByNameAsync(kullaniciAdi) is not null)
            {
                continue;
            }

            var kullanici = new ApplicationUser
            {
                UserName = kullaniciAdi,
                Email = $"{kullaniciAdi}@test.local",
                EmailConfirmed = true,
                AdSoyad = $"Ölçüm Kullanıcısı {no:00}",
                Fakulte = "Teknoloji Fakültesi",
                Bolum = "Bilgisayar Mühendisliği",
            };
            var sonuc = await userManager.CreateAsync(kullanici, sifre);
            if (sonuc.Succeeded)
            {
                sonuc = await userManager.AddToRoleAsync(kullanici, Roller.Ogrenci);
            }

            if (!sonuc.Succeeded)
            {
                logger.LogError("Ölçüm kullanıcısı {KullaniciAdi} oluşturulamadı: {Hatalar}",
                    kullaniciAdi, string.Join("; ", sonuc.Errors.Select(e => e.Description)));
                return;
            }

            olusturulan++;
        }

        if (olusturulan > 0)
        {
            logger.LogInformation("{Adet} ölçüm kullanıcısı oluşturuldu.", olusturulan);
        }
    }

    /// <summary>
    /// Kullanıcı yoksa verilen şifreyle oluşturur (şifre yoksa atlar). Varsa şifresine, 2FA ayarlarına ve diğer
    /// bilgilerine dokunmaz; yalnızca tanımdaki eksik rolleri ekler (sonuç kümesi kurallara uymuyorsa eklemez).
    /// </summary>
    public static async Task TestKullanicisiOlusturAsync(
        UserManager<ApplicationUser> userManager, TestKullanicisi bilgi, string? sifre, ILogger logger)
    {
        if (await userManager.FindByNameAsync(bilgi.KullaniciAdi) is { } mevcut)
        {
            var roller = await userManager.GetRolesAsync(mevcut);
            var eksik = bilgi.Roller.Except(roller, StringComparer.Ordinal).ToArray();
            if (eksik.Length == 0)
            {
                return;
            }

            var hatalar = RolDogrulayici.Hatalar([.. roller, .. eksik], mevcut.Fakulte, mevcut.Bolum);
            if (hatalar.Count > 0)
            {
                logger.LogError("Test kullanıcısı {KullaniciAdi} için roller eklenmedi: {Hatalar}",
                    bilgi.KullaniciAdi, string.Join(" ", hatalar));
                return;
            }

            var ekleme = await userManager.AddToRolesAsync(mevcut, eksik);
            if (ekleme.Succeeded)
            {
                logger.LogInformation("Test kullanıcısına roller eklendi: {KullaniciAdi} ({Roller})",
                    bilgi.KullaniciAdi, string.Join(", ", eksik));
            }
            else
            {
                logger.LogError("Test kullanıcısı {KullaniciAdi} için roller eklenemedi: {Hatalar}",
                    bilgi.KullaniciAdi, string.Join("; ", ekleme.Errors.Select(e => e.Description)));
            }

            return;
        }

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
            sonuc = await userManager.AddToRolesAsync(kullanici, bilgi.Roller);
        }

        if (sonuc.Succeeded)
        {
            logger.LogInformation("Test kullanıcısı oluşturuldu: {KullaniciAdi} ({Roller})",
                bilgi.KullaniciAdi, string.Join(", ", bilgi.Roller));
        }
        else
        {
            logger.LogError("Test kullanıcısı {KullaniciAdi} oluşturulamadı: {Hatalar}",
                bilgi.KullaniciAdi, string.Join("; ", sonuc.Errors.Select(e => e.Description)));
        }
    }
}
