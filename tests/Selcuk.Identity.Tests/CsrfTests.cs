using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Siteler arası istek sahteciliği (CSRF) koruması (tehdit modeli T2, KNOWN_ISSUES R13). Kimlik sunucusundaki her Razor
/// sayfası POST işleyicisi antiforgery token'ı olmadan gönderildiğinde 400 alır ve hiçbir yan etki oluşturmaz (oturum
/// açılmaz, şifre/2FA/onay/çıkış değişmez, e-posta gitmez). İşleyici listesi çalışan uygulamadan (Razor sayfa
/// tanımları) toplanır; yeni bir POST işleyicisi <see cref="Senaryolar"/>'a eklenmediyse
/// <see cref="Her_POST_isleyicisinin_senaryosu_var_listede_olmayan_islayici_yok"/> başarısız olur. Her senaryoda
/// kontrol olarak aynı istek token'la gönderilir ve 400 almaz: 400'ün antiforgery'den geldiği (istek başka bir nedenle
/// reddedilmediği) böylece kanıtlanır.
///
/// OIDC protokol uç noktaları (/connect/token, /connect/revoke, /connect/userinfo ve istemci uygulamalardaki /signout-backchannel) tarayıcı
/// formu değildir: çerezle değil client_id/secret, kod veya imzalı token ile doğrulanır, antiforgery BEKLEMEZ ve
/// eklenmemelidir; bu ayrım <see cref="Protokol_uc_noktalari_antiforgery_beklemez_sayfa_formlari_bekler"/> testindedir.
/// </summary>
public sealed class CsrfTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    /// <summary>Senaryo anahtarları: "Sayfa/Yolu" (varsayılan işleyici) veya "Sayfa/Yolu:IsimliIsleyici".</summary>
    public static readonly string[] Senaryolar =
    [
        "Account/Login",
        "Account/IkiAdim",
        "Account/EkDogrulama",
        "Account/IkiAdimDogrulama:Baslat",
        "Account/IkiAdimDogrulama:Etkinlestir",
        "Account/IkiAdimDogrulama:KodlariYenile",
        "Account/IkiAdimDogrulama:Kapat",
        "Account/SifreDegistir",
        "Account/SifremiUnuttum",
        "Account/SifreSifirla",
        "Connect/Authorize",
        "Connect/Logout",
    ];

    public static IEnumerable<object[]> SenaryoVerisi => Senaryolar.Select(s => new object[] { s });

    /// <summary>Bir senaryonun hazırlığı: kullanılacak istemci, form ve yan etkisizlik denetimi.</summary>
    private sealed record Hazir(
        HttpClient Client,
        string FormSayfasi,
        string PostAdresi,
        Dictionary<string, string> Alanlar,
        Func<Task> YanEtkiYok,
        Func<Task<HttpResponseMessage>>? Kontrol = null,
        Func<Task>? KontrolSonrasi = null);

    // ---------- Çalışan uygulamadan toplanan POST işleyicileri ----------

    private async Task<(List<string> Islayiciler, List<string> Hatalar)> KesfetAsync()
    {
        var sayfalar = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<PageActionDescriptor>().ToList();
        var yukleyici = _factory.Services.GetRequiredService<PageLoader>();
        var islayiciler = new List<string>();
        var hatalar = new List<string>();
        foreach (var sayfa in sayfalar)
        {
            var derlenmis = await yukleyici.LoadAsync(sayfa, EndpointMetadataCollection.Empty);
            var yol = sayfa.RelativePath.Replace("/Pages/", "", StringComparison.Ordinal).Replace(".cshtml", "", StringComparison.Ordinal);
            var postlar = derlenmis.HandlerMethods
                .Where(h => string.Equals(h.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var h in postlar)
            {
                islayiciler.Add(string.IsNullOrEmpty(h.Name) ? yol : $"{yol}:{h.Name}");
            }

            // POST işleyicisi olan hiçbir sayfa antiforgery doğrulamasını kapatamaz.
            if (postlar.Count > 0 && derlenmis.FilterDescriptors.Any(f => f.Filter is IgnoreAntiforgeryTokenAttribute))
            {
                hatalar.Add($"{yol}: [IgnoreAntiforgeryToken] ile antiforgery kapatılmış.");
            }
        }

        return (islayiciler, hatalar);
    }

    [Fact]
    public async Task Her_POST_isleyicisinin_senaryosu_var_listede_olmayan_islayici_yok()
    {
        var (islayiciler, hatalar) = await KesfetAsync();

        Assert.Empty(hatalar);
        // Yeni bir POST işleyicisi eklendiyse bu liste güncellenmeli (ve senaryosu yazılmalı); kaldırıldıysa listeden çıkmalı.
        Assert.Equal(Senaryolar.Order(StringComparer.Ordinal), islayiciler.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Sayfa_olmayan_POST_uc_noktalari_yalnizca_protokol_ucu()
    {
        var uclar = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<HttpMethodMetadata>() is { } m
                        && m.HttpMethods.Contains("POST", StringComparer.OrdinalIgnoreCase)
                        && e.Metadata.GetMetadata<PageActionDescriptor>() is null)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        // Tarayıcı formu olmayan, antiforgery beklemeyen POST uçları: /connect/token (kod veya refresh token + PKCE/secret)
        // ve /connect/userinfo (OIDC gereği GET ve POST; Bearer access token). OpenIddict'in /connect/revoke ucu da bu
        // gruptadır (OpenIddict ara katmanı işler, endpoint kaydı yoktur). Yeni bir POST ucu eklenirse bilinçli karar gerekir.
        Assert.Equal(["connect/token", "connect/userinfo"], uclar.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Protokol_uc_noktalari_antiforgery_beklemez_sayfa_formlari_bekler()
    {
        var client = _factory.HttpsIstemcisi();

        // Protokol uçları: antiforgery'siz POST OIDC protokol hatası (JSON "error") döner, antiforgery 400'ü değil.
        foreach (var uc in new[] { "/connect/token", "/connect/revoke" })
        {
            var yanit = await client.PostAsync(uc, new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
            Assert.Equal("application/json", yanit.Content.Headers.ContentType?.MediaType);
            Assert.Contains("\"error\"", await yanit.Content.ReadAsStringAsync());
        }

        // Tarayıcı formu: aynı koşulda antiforgery 400'ü, gövdesiz ve JSON olmayan yanıt.
        var sayfa = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, sayfa.StatusCode);
        Assert.NotEqual("application/json", sayfa.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("\"error\"", await sayfa.Content.ReadAsStringAsync());
    }

    // ---------- Her işleyici için antiforgery'siz POST ----------

    [Theory]
    [MemberData(nameof(SenaryoVerisi))]
    public async Task Antiforgery_token_olmadan_POST_400_alir_yan_etki_olusturmaz_tokenla_ayni_istek_400_almaz(string anahtar)
    {
        var hazir = await HazirlaAsync(anahtar);

        // Antiforgery çerezi var (sayfa açıldı) ama formda token yok.
        var yanit = await hazir.Client.PostAsync(hazir.PostAdresi, new FormUrlEncodedContent(hazir.Alanlar));

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Null(yanit.Headers.Location);
        await hazir.YanEtkiYok();

        // Kontrol: aynı istek token'la gönderilince işleyici çalışır (400 değil).
        var kontrol = hazir.Kontrol is null
            ? await SelcukIdentityFactory.FormGonderAsync(hazir.Client, hazir.FormSayfasi, new(hazir.Alanlar), hazir.PostAdresi)
            : await hazir.Kontrol();
        Assert.NotEqual(HttpStatusCode.BadRequest, kontrol.StatusCode);
        if (hazir.KontrolSonrasi is not null)
        {
            await hazir.KontrolSonrasi();
        }
    }

    // ---------- Senaryo hazırlıkları ----------

    private async Task<Hazir> HazirlaAsync(string anahtar) => anahtar switch
    {
        "Account/Login" => await LoginAsync(),
        "Account/IkiAdim" => await IkiAdimAsync(),
        "Account/EkDogrulama" => await EkDogrulamaAsync(),
        "Account/IkiAdimDogrulama:Baslat" => await BaslatAsync(),
        "Account/IkiAdimDogrulama:Etkinlestir" => await EtkinlestirAsync(),
        "Account/IkiAdimDogrulama:KodlariYenile" => await IkiAdimAcikAsync("KodlariYenile"),
        "Account/IkiAdimDogrulama:Kapat" => await IkiAdimAcikAsync("Kapat"),
        "Account/SifreDegistir" => await SifreDegistirAsync(),
        "Account/SifremiUnuttum" => await SifremiUnuttumAsync(),
        "Account/SifreSifirla" => await SifreSifirlaAsync(),
        "Connect/Authorize" => await AuthorizeAsync(),
        "Connect/Logout" => await LogoutAsync(),
        _ => throw new InvalidOperationException($"'{anahtar}' için senaryo yok."),
    };

    private async Task<(HttpClient Client, string Kullanici, string Sifre)> GirisliAsync()
    {
        var (kullanici, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullanici, sifre)).StatusCode);
        return (client, kullanici, sifre);
    }

    private async Task<ApplicationUser> KullaniciAsync(string kullaniciAdi) => await _factory.KullaniciAsync(kullaniciAdi);

    private async Task<T> UserManagerAsync<T>(string kullaniciAdi, Func<UserManager<ApplicationUser>, ApplicationUser, Task<T>> islem)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await islem(userManager, (await userManager.FindByNameAsync(kullaniciAdi))!);
    }

    private static async Task GirisYapilmadiAsync(HttpClient client)
    {
        // Korumalı sayfa girişe yönlendirir: oturum açılmadı.
        var yanit = await client.GetAsync("/Account/Hesabim");
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith("https://localhost/Account/Login", yanit.Headers.Location!.ToString());
    }

    private async Task<Hazir> LoginAsync()
    {
        var (kullanici, sifre) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await client.GetAsync("/Account/Login");
        return new Hazir(client, "/Account/Login", "/Account/Login",
            new() { ["Input.KullaniciAdi"] = kullanici, ["Input.Sifre"] = sifre },
            () => GirisYapilmadiAsync(client));
    }

    private async Task<Hazir> IkiAdimAsync()
    {
        var (kullanici, sifre) = await _factory.KullaniciOlusturAsync();
        var (anahtar, _) = await _factory.IkiAdimAcAsync(kullanici);
        var client = _factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullanici, sifre);
        // Şifre adımı geçti, ikinci adım bekliyor (TwoFactorUserId çerezi); oturum henüz yok.
        Assert.StartsWith("/Account/IkiAdim", giris.Headers.Location!.OriginalString);
        return new Hazir(client, "/Account/IkiAdim", "/Account/IkiAdim",
            new() { ["Input.Kod"] = Totp.Uret(anahtar) },
            () => GirisYapilmadiAsync(client));
    }

    /// <summary>2FA'yı güvenlik damgasını değiştirmeden açar: oturum yalnızca şifreyle açık, 2FA açık kullanıcı.</summary>
    private async Task<string> IkiAdimiDamgaDegismedenAcAsync(string kullaniciAdi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();
        var user = (await userManager.FindByNameAsync(kullaniciAdi))!;
        var damga = user.SecurityStamp;
        var anahtar = userManager.GenerateNewAuthenticatorKey();
        await ((IUserAuthenticatorKeyStore<ApplicationUser>)store).SetAuthenticatorKeyAsync(user, anahtar, CancellationToken.None);
        await ((IUserTwoFactorStore<ApplicationUser>)store).SetTwoFactorEnabledAsync(user, true, CancellationToken.None);
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        Assert.Equal(damga, (await userManager.FindByNameAsync(kullaniciAdi))!.SecurityStamp);
        return anahtar;
    }

    private async Task<Hazir> EkDogrulamaAsync()
    {
        var (client, kullanici, _) = await GirisliAsync();
        var anahtar = await IkiAdimiDamgaDegismedenAcAsync(kullanici);
        return new Hazir(client, "/Account/EkDogrulama", "/Account/EkDogrulama",
            new() { ["Input.Kod"] = Totp.Uret(anahtar) },
            async () =>
            {
                // Oturum hâlâ yalnızca şifreyle açık: ek doğrulama sayfası kod istemeye devam eder (dönüş yönlendirmesi yok).
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/EkDogrulama")).StatusCode);
            });
    }

    private async Task<Hazir> BaslatAsync()
    {
        var (client, kullanici, _) = await GirisliAsync();
        return new Hazir(client, "/Account/IkiAdimDogrulama", "/Account/IkiAdimDogrulama?handler=Baslat", [],
            async () => Assert.Null(await _factory.AnahtarAsync(kullanici)));
    }

    private async Task<Hazir> EtkinlestirAsync()
    {
        var (client, kullanici, _) = await GirisliAsync();
        // Kurulum başlatıldı (anahtar var) ama 2FA açık değil.
        var baslat = await SelcukIdentityFactory.FormGonderAsync(client, "/Account/IkiAdimDogrulama", [],
            "/Account/IkiAdimDogrulama?handler=Baslat");
        Assert.Equal(HttpStatusCode.Redirect, baslat.StatusCode);
        var anahtar = (await _factory.AnahtarAsync(kullanici))!;
        return new Hazir(client, "/Account/IkiAdimDogrulama", "/Account/IkiAdimDogrulama?handler=Etkinlestir",
            new() { ["Etkinlestirme.Kod"] = Totp.Uret(anahtar) },
            async () => Assert.False(await UserManagerAsync(kullanici, (m, u) => m.GetTwoFactorEnabledAsync(u))));
    }

    /// <summary>Oturum açıkken 2FA damga değişmeden açılır; işleyici (KodlariYenile/Kapat) şifre + kod ister.</summary>
    private async Task<Hazir> IkiAdimAcikAsync(string isleyici)
    {
        var (client, kullanici, sifre) = await GirisliAsync();
        var anahtar = await IkiAdimiDamgaDegismedenAcAsync(kullanici);
        var damga = (await KullaniciAsync(kullanici)).SecurityStamp;
        return new Hazir(client, "/Account/IkiAdimDogrulama", $"/Account/IkiAdimDogrulama?handler={isleyici}",
            new() { ["Onay.Sifre"] = sifre, ["Onay.Kod"] = Totp.Uret(anahtar) },
            async () =>
            {
                // Kapatma ve kod yenileme güvenlik damgasını yeniler ve 2FA'yı değiştirir; hiçbiri olmadı.
                var user = await KullaniciAsync(kullanici);
                Assert.Equal(damga, user.SecurityStamp);
                Assert.True(user.TwoFactorEnabled);
                Assert.Equal(anahtar, await _factory.AnahtarAsync(kullanici));
            });
    }

    private async Task<Hazir> SifreDegistirAsync()
    {
        var (client, kullanici, sifre) = await GirisliAsync();
        var damga = (await KullaniciAsync(kullanici)).SecurityStamp;
        var yeni = $"Bb2{Guid.NewGuid():N}";
        return new Hazir(client, "/Account/SifreDegistir", "/Account/SifreDegistir",
            new() { ["Input.MevcutSifre"] = sifre, ["Input.YeniSifre"] = yeni, ["Input.YeniSifreTekrar"] = yeni },
            async () =>
            {
                Assert.True(await UserManagerAsync(kullanici, (m, u) => m.CheckPasswordAsync(u, sifre)));
                Assert.False(await UserManagerAsync(kullanici, (m, u) => m.CheckPasswordAsync(u, yeni)));
                Assert.Equal(damga, (await KullaniciAsync(kullanici)).SecurityStamp);
            });
    }

    private async Task<Hazir> SifremiUnuttumAsync()
    {
        // Antiforgery'siz istek A için, kontrol isteği B için: e-posta kuyruğu sıralıdır; B'nin e-postası geldiğinde A'ya
        // gönderilmiş olsaydı daha önce gelmiş olurdu.
        var (a, _) = await _factory.KullaniciOlusturAsync();
        var (b, _) = await _factory.KullaniciOlusturAsync();
        var client = _factory.HttpsIstemcisi();
        await client.GetAsync("/Account/SifremiUnuttum");
        return new Hazir(client, "/Account/SifremiUnuttum", "/Account/SifremiUnuttum",
            new() { ["Input.KullaniciAdiVeyaEposta"] = a },
            () => Task.CompletedTask,
            Kontrol: () => SelcukIdentityFactory.FormGonderAsync(client, "/Account/SifremiUnuttum",
                new() { ["Input.KullaniciAdiVeyaEposta"] = b }),
            KontrolSonrasi: async () =>
            {
                var gelen = await _factory.Eposta.SiradakiAsync();
                Assert.Equal($"{b}@test.local", gelen.Alici);
                Assert.DoesNotContain(_factory.Eposta.Gonderilenler, m => m.Alici == $"{a}@test.local");
            });
    }

    private async Task<Hazir> SifreSifirlaAsync()
    {
        var (kullanici, sifre) = await _factory.KullaniciOlusturAsync();
        var user = await KullaniciAsync(kullanici);
        var kod = await UserManagerAsync(kullanici, async (m, u) =>
            WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(await m.GeneratePasswordResetTokenAsync(u))));
        var yeni = $"Cc3{Guid.NewGuid():N}";
        var sayfa = $"/Account/SifreSifirla?kullanici={user.Id}&kod={kod}";
        var client = _factory.HttpsIstemcisi();
        await client.GetAsync(sayfa);
        return new Hazir(client, sayfa, "/Account/SifreSifirla",
            new() { ["Input.Kullanici"] = user.Id, ["Input.Kod"] = kod, ["Input.YeniSifre"] = yeni, ["Input.YeniSifreTekrar"] = yeni },
            async () =>
            {
                // Şifre değişmedi ve sıfırlama bağlantısı kullanılmadı (kontrol isteği onu kullanabilecek).
                Assert.True(await UserManagerAsync(kullanici, (m, u) => m.CheckPasswordAsync(u, sifre)));
                Assert.False(await UserManagerAsync(kullanici, (m, u) => m.CheckPasswordAsync(u, yeni)));
            },
            KontrolSonrasi: async () =>
                // Token bağlantı denetiminden geçti: antiforgery'li istek şifreyi gerçekten değiştirdi.
                Assert.True(await UserManagerAsync(kullanici, (m, u) => m.CheckPasswordAsync(u, yeni))));
    }

    private async Task<Hazir> AuthorizeAsync()
    {
        await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (client, kullanici, _) = await GirisliAsync();
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var adres = OidcYardimcisi.AuthorizeAdresi(pkce);
        var onay = await client.GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, onay.StatusCode);
        var html = await onay.Content.ReadAsStringAsync();
        var alanlar = OidcYardimcisi.OnayFormu(html)
            .Where(a => a.Key != "__RequestVerificationToken")
            .ToDictionary(a => a.Key, a => a.Value);
        var userId = (await KullaniciAsync(kullanici)).Id;
        return new Hazir(client, adres, "/connect/authorize", alanlar,
            async () =>
            {
                // Onay verilmedi: kalıcı izin (yetkilendirme) kaydı oluşmadı.
                await using var scope = _factory.Services.CreateAsyncScope();
                var yetkilendirmeler = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
                Assert.Empty(await yetkilendirmeler.FindBySubjectAsync(userId).ToListAsync());
            },
            KontrolSonrasi: async () =>
            {
                await using var scope = _factory.Services.CreateAsyncScope();
                var yetkilendirmeler = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
                Assert.NotEmpty(await yetkilendirmeler.FindBySubjectAsync(userId).ToListAsync());
            });
    }

    private async Task<Hazir> LogoutAsync()
    {
        var (client, _, _) = await GirisliAsync();
        return new Hazir(client, "/connect/logout", "/connect/logout", [],
            async () => Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Hesabim")).StatusCode),
            KontrolSonrasi: async () => await GirisYapilmadiAsync(client));
    }
}
