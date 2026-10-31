using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Selcuk.Istemci.Ortak;

namespace Selcuk.Otomasyon.Ortak;

/// <summary>Otomasyonun adı, simgesi, menüsü ve veri kaynağı (AddSelcukOtomasyon'a verilir).</summary>
public sealed class OtomasyonSecenekleri
{
    /// <summary>Üst çubukta ve sayfa başlığında görünen ad (ör. "Staj Otomasyonu").</summary>
    public string Ad { get; set; } = string.Empty;

    /// <summary>Üst çubuktaki uygulama simgesi: iki harf (ör. "ST").</summary>
    public string Simge { get; set; } = string.Empty;

    /// <summary>Ana sayfadaki kısa tanıtım cümlesi.</summary>
    public string Aciklama { get; set; } = string.Empty;

    public List<MenuOgesi> Menu { get; } = [];

    internal Action<IServiceCollection>? VeriKaynagiKaydi { get; private set; }

    internal List<Action<IServiceCollection, IConfiguration>> AyarKayitlari { get; } = [];

    /// <summary>
    /// Otomasyona özgü ayarlar (ör. yönetmelik şartları: GNO alt sınırı, AKTS toplamı, kontenjan): appsettings'teki
    /// bölüm T'ye bağlanır, sayfalar IOptions&lt;T&gt; ile okur. Değerler koda gömülmez; T.Hata() null değilse açılış
    /// Türkçe bir hatayla durur (ValidateOnStart).
    /// </summary>
    public void Ayarlar<T>(string bolum) where T : class, IDogrulanabilirAyar =>
        AyarKayitlari.Add((services, configuration) =>
        {
            services.AddOptions<T>().Bind(configuration.GetSection(bolum)).ValidateOnStart();
            services.AddSingleton<IValidateOptions<T>>(new AyarDogrulayici<T>(bolum));
        });

    private sealed class AyarDogrulayici<T>(string bolum) : IValidateOptions<T> where T : class, IDogrulanabilirAyar
    {
        public ValidateOptionsResult Validate(string? name, T options) =>
            options.Hata() is { } hata ? ValidateOptionsResult.Fail($"'{bolum}' ayarı geçersiz: {hata}") : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Otomasyonun veri kaynağı arayüzü ve iki uygulaması: VeriKaynagi:Mod "Mock" ise TMock (örnek veri), "Api" ise
    /// TApi (<see cref="OtomasyonApiIstemcisi"/> ile kaynak sunucusu). Sayfalar yalnızca TArayuz'u kullanır.
    /// </summary>
    public void VeriKaynagi<TArayuz, TMock, TApi>()
        where TArayuz : class
        where TMock : class, TArayuz
        where TApi : class, TArayuz
    {
        VeriKaynagiKaydi = services =>
        {
            services.AddScoped<TMock>();
            services.AddScoped<TApi>();
            services.AddScoped<TArayuz>(sp => sp.GetRequiredService<IOptions<VeriKaynagiAyarlari>>().Value.ApiMi
                ? sp.GetRequiredService<TApi>()
                : sp.GetRequiredService<TMock>());
        };
    }
}

/// <summary>Açılışta doğrulanan otomasyon ayarı (<see cref="OtomasyonSecenekleri.Ayarlar{T}"/>).</summary>
public interface IDogrulanabilirAyar
{
    /// <summary>Ayar geçerliyse null, değilse Türkçe açıklama.</summary>
    string? Hata();
}

/// <summary>Düzenin okuduğu otomasyon bilgileri.</summary>
public sealed record OtomasyonBilgisi(string Ad, string Simge, string Aciklama);

/// <summary>
/// Bölüm otomasyonlarının ortak kurulumu. AddSelcukOtomasyon servisleri, UseSelcukOtomasyon istek hattını ve uç
/// noktaları kurar (ASP.NET Core'da ara katman ve uç noktalar servis kaydında eklenemez; Program.cs'de iki satır).
/// </summary>
public static class OtomasyonUzantilari
{
    /// <summary>Herkese açık ortak sayfalar; diğer bütün sayfalar giriş ister.</summary>
    public static readonly IReadOnlyList<string> AcikSayfalar = ["/Index", "/Giris", "/GirisHatasi", "/Hata", "/ErisimReddedildi", "/Cikis"];

    private const string BackChannelLogoutYolu = "/signout-backchannel";

    /// <summary>
    /// Tek çağrıyla: SSO (AddSelcukSSO: Cookie + OIDC, code + PKCE, token yenileme, Back-Channel Logout alıcısının
    /// servisleri; oturum bileti sunucu tarafında, <see cref="OturumBiletDeposu"/>), rol tabanlı menü ve menüden türetilen sunucu tarafı yetki (her menü sayfası yalnızca kendi
    /// rollerine; diğer sayfalar en az giriş ister), Razor Pages, veri kaynağı seçimi (VeriKaynagi:Mod, açılışta
    /// doğrulanır) ve Api modunun HTTP istemcisi. Hata sayfaları ve güvenlik başlıkları <see cref="UseSelcukOtomasyon"/>
    /// ile istek hattına eklenir.
    /// </summary>
    public static IServiceCollection AddSelcukOtomasyon(this IServiceCollection services, IConfiguration configuration,
        Action<OtomasyonSecenekleri> ayarla)
    {
        var secenekler = new OtomasyonSecenekleri();
        ayarla(secenekler);
        Dogrula(secenekler);

        services.AddSelcukSSO(configuration);
        // Oturum bileti sunucu tarafında, çerezde yalnızca anahtar (localhost'ta çerez birikimi, HTTP 431).
        OturumBiletDeposu.Kaydet(services);
        services.AddHttpContextAccessor();
        services.AddSingleton(new OtomasyonBilgisi(secenekler.Ad, secenekler.Simge, secenekler.Aciklama));
        var menu = new OtomasyonMenusu([.. secenekler.Menu]);
        services.AddSingleton(menu);

        services.AddAuthorization(options =>
        {
            foreach (var oge in menu.Ogeler)
            {
                options.AddPolicy(OtomasyonMenusu.IlkeAdi(oge.Sayfa), ilke => ilke.RequireRole(oge.Roller));
            }
        });
        services.AddRazorPages(options =>
        {
            options.Conventions.AuthorizeFolder("/");
            foreach (var sayfa in AcikSayfalar)
            {
                options.Conventions.AllowAnonymousToPage(sayfa);
            }

            foreach (var oge in menu.Ogeler)
            {
                options.Conventions.AuthorizePage(oge.Sayfa, OtomasyonMenusu.IlkeAdi(oge.Sayfa));
            }
        });

        services.AddOptions<VeriKaynagiAyarlari>().Bind(configuration.GetSection(VeriKaynagiAyarlari.Bolum)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<VeriKaynagiAyarlari>, VeriKaynagiDogrulayici>();
        services.AddScoped<OtomasyonApiIstemcisi>();
        services.AddHttpClient(OtomasyonApiIstemcisi.HttpIstemciAdi).ConfigureHttpClient(OtomasyonApiIstemcisi.Yapilandir);
        secenekler.VeriKaynagiKaydi!(services);
        foreach (var kayit in secenekler.AyarKayitlari)
        {
            kayit(services, configuration);
        }

        return services;
    }

    /// <summary>
    /// İstek hattı: güvenlik başlıkları (giriş isteyen sayfalarda no-store), her ortamda Türkçe hata sayfası (/Hata;
    /// istisna ayrıntısı ve stack trace gösterilmez, veri kaynağı hatası 503/403), durum kodu sayfaları, HTTPS, statik
    /// dosyalar, kimlik doğrulama, yetkilendirme, Razor Pages ve Back-Channel Logout alıcısı. Önce sayfaların yetkisi
    /// denetlenir: menüde olmayan ve rol istemeyen bir sayfa varsa açılış durur (<see cref="SayfaYetkiDenetimi"/>).
    /// </summary>
    public static WebApplication UseSelcukOtomasyon(this WebApplication app)
    {
        SayfaYetkiDenetimi.Denetle(app);
        app.UseSelcukGuvenlikBasliklari();
        app.UseExceptionHandler("/Hata");
        // Back-Channel Logout yanıtları (sunucudan sunucuya) HTML hata sayfasına çevrilmez.
        app.UseWhen(context => !context.Request.Path.StartsWithSegments(BackChannelLogoutYolu),
            dal => dal.UseStatusCodePagesWithReExecute("/Hata", "?kod={0}"));
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapSelcukBackChannelLogout();
        return app;
    }

    private static void Dogrula(OtomasyonSecenekleri secenekler)
    {
        if (string.IsNullOrWhiteSpace(secenekler.Ad) || string.IsNullOrWhiteSpace(secenekler.Simge))
        {
            throw new InvalidOperationException("Otomasyonun adı (Ad) ve simgesi (Simge) verilmeli.");
        }

        if (secenekler.VeriKaynagiKaydi is null)
        {
            throw new InvalidOperationException("Otomasyonun veri kaynağı (VeriKaynagi<TArayuz, TMock, TApi>) verilmeli.");
        }

        foreach (var oge in secenekler.Menu)
        {
            if (!oge.Sayfa.StartsWith('/') || AcikSayfalar.Contains(oge.Sayfa, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Menü öğesi '{oge.Baslik}': sayfa yolu '/' ile başlamalı ve ortak açık sayfa olmamalı.");
            }

            if (oge.Roller.Length == 0 || oge.Roller.Any(rol => Selcuk.Roller.Bul(rol) is null))
            {
                throw new InvalidOperationException($"Menü öğesi '{oge.Baslik}': en az bir rol verilmeli ve roller katalogda olmalı.");
            }
        }

        var tekrar = secenekler.Menu.GroupBy(o => o.Sayfa, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (tekrar is not null)
        {
            throw new InvalidOperationException($"'{tekrar.Key}' sayfası menüde birden çok kez var.");
        }
    }

    private sealed class VeriKaynagiDogrulayici : IValidateOptions<VeriKaynagiAyarlari>
    {
        public ValidateOptionsResult Validate(string? name, VeriKaynagiAyarlari options) =>
            options.Hata() is { } hata ? ValidateOptionsResult.Fail(hata) : ValidateOptionsResult.Success;
    }
}

/// <summary>
/// Temsili işlemler: formlar (Onayla, Reddet, Kaydet, Başvur…) antiforgery korumalı gerçek bir POST yapar; sunucu veriyi
/// değiştirmez ve aynı sayfaya döner, mesaj TempData ile taşınır ve düzende gösterilir (JavaScript gerekmez).
/// </summary>
public static class TemsiliIslem
{
    public const string TempDataAnahtari = "Otomasyon.TemsiliIslem";
    public const string Mesaj = "Bu ekran temsilidir; işlem kaydedilmedi.";

    public static IActionResult TemsiliIslemSonucu(this PageModel sayfa)
    {
        sayfa.TempData[TempDataAnahtari] = Mesaj;
        return sayfa.RedirectToPage();
    }
}
