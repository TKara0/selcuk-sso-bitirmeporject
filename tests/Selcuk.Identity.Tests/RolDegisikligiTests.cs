using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Rol değişikliği (<see cref="RolDegisikligiIslemi"/>, KNOWN_ISSUES R19): rol kümesi değişince kullanıcının kimlik
/// sunucusu oturumları kapanır, refresh token'ları iptal edilir (invalid_grant), açık istemci oturumlarına
/// Back-Channel Logout gider ve "RolDegisti" denetim kaydı yazılır (eski ve yeni rol listesi; sır yok). Küme
/// değişmiyorsa veya kurallara uymuyorsa hiçbir şey olmaz; başka kullanıcılar etkilenmez. Rol değişikliği testin içinde
/// yapılır (seed'deki rol atamaları değişmez).
/// </summary>
public sealed class RolDegisikligiTests : IDisposable
{
    private const string TumKapsamlar = "openid profile email roles offline_access notlar.oku";
    private const string Fakulte = "Teknoloji Fakültesi";
    private const string Bolum = "Bilgisayar Mühendisliği";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed record Oturum(HttpClient Client, string Refresh, string Sid, string Access);

    private async Task<string> SecretAsync() => await OidcYardimcisi.DemoObisKaydetAsync(_factory);

    /// <summary>Giriş yapar ve demo-obis için refresh token'lı bir oturum açar.</summary>
    private async Task<Oturum> OturumAcAsync(string kullaniciAdi, string sifre, string secret)
    {
        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce, Istemciler.DemoObisClientId, Istemciler.DemoObisRedirectUri, TumKapsamlar);
        var yanit = await OidcYardimcisi.TokenIsteAsync(client, code, pkce.Verifier, secret);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        var kok = json.RootElement;
        var sid = OidcYardimcisi.JwtGovdesi(kok.GetProperty("id_token").GetString()!).GetProperty("sid").GetString()!;
        return new Oturum(client, kok.GetProperty("refresh_token").GetString()!, sid, kok.GetProperty("access_token").GetString()!);
    }

    private static Task<HttpResponseMessage> YenileAsync(Oturum oturum, string secret) =>
        oturum.Client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [Parameters.GrantType] = GrantTypes.RefreshToken,
            [Parameters.RefreshToken] = oturum.Refresh,
            [Parameters.ClientId] = Istemciler.DemoObisClientId,
            [Parameters.ClientSecret] = secret,
        }));

    private static async Task InvalidGrantAsync(Oturum oturum, string secret)
    {
        var yanit = await YenileAsync(oturum, secret);
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        using var json = JsonDocument.Parse(await yanit.Content.ReadAsStringAsync());
        Assert.Equal(Errors.InvalidGrant, json.RootElement.GetProperty("error").GetString());
    }

    private static async Task YenilenirAsync(Oturum oturum, string secret) =>
        Assert.Equal(HttpStatusCode.OK, (await YenileAsync(oturum, secret)).StatusCode);

    /// <summary>Kimlik sunucusundaki oturum çerezi hâlâ geçerli mi (korumalı sayfa 200 mü).</summary>
    private static async Task<bool> KimlikSunucusundaAcikAsync(Oturum oturum) =>
        (await oturum.Client.GetAsync("/Account/Hesabim")).StatusCode == HttpStatusCode.OK;

    private async Task<(string Id, string KullaniciAdi, string Sifre)> KullaniciAsync(params string[] roller)
    {
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(roller.FirstOrDefault(), Fakulte, Bolum);
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await userManager.FindByNameAsync(kullaniciAdi))!;
        if (roller.Length > 1)
        {
            Assert.True((await userManager.AddToRolesAsync(user, roller.Skip(1))).Succeeded);
        }

        return (user.Id, kullaniciAdi, sifre);
    }

    private async Task<RolDegisikligiIslemi.Sonuc> UygulaAsync(string kullaniciAdi, string[] ekle, string[] cikar)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(kullaniciAdi))!;
        return await scope.ServiceProvider.GetRequiredService<RolDegisikligiIslemi>().UygulaAsync(user, ekle, cikar);
    }

    private async Task<string[]> RollerAsync(string kullaniciAdi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return [.. (await userManager.GetRolesAsync((await userManager.FindByNameAsync(kullaniciAdi))!)).Order(StringComparer.Ordinal)];
    }

    private IEnumerable<SahteBackChannelAlicisi.Istek> BackChannelIstekleri() =>
        _factory.BackChannel.Istekler.Where(i => i.Adres.AbsoluteUri == Istemciler.DemoObisBackChannelLogoutUri);

    /// <summary>İstenen sid'lerin hepsine (ve yalnızca onlara) imzalı logout_token gitti.</summary>
    private void BackChannelSidleri(params string[] beklenen)
    {
        var sidler = BackChannelIstekleri()
            .Select(i => OidcYardimcisi.JwtGovdesi(i.Form["logout_token"]).GetProperty("sid").GetString()!)
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(beklenen.Order(StringComparer.Ordinal), sidler);
    }

    private async Task<DenetimKaydi> RolDegistiKaydiAsync(string kullaniciId)
    {
        var kayitlar = await _factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.RolDegisti));
        var kayit = Assert.Single(kayitlar, x => x.OlayTuru == DenetimOlayi.RolDegisti);
        Assert.Equal(kullaniciId, kayit.KullaniciId);
        Assert.Equal(DenetimSonucu.Basarili, kayit.Sonuc);
        Assert.Null(kayit.IstemciId);
        return kayit;
    }

    [Fact]
    public async Task Rol_eklenince_oturumlar_kapaniyor_refresh_invalid_grant_back_channel_logout_gidiyor_denetim_yaziliyor()
    {
        var secret = await SecretAsync();
        var (id, kullaniciAdi, sifre) = await KullaniciAsync(Roller.Akademisyen);
        var a = await OturumAcAsync(kullaniciAdi, sifre, secret);
        var b = await OturumAcAsync(kullaniciAdi, sifre, secret);
        Assert.True(await KimlikSunucusundaAcikAsync(a));
        Assert.Empty(BackChannelIstekleri());

        var sonuc = await UygulaAsync(kullaniciAdi, [Roller.StajKomisyonu], []);

        Assert.True(sonuc.Degisti);
        Assert.Equal([Roller.Akademisyen], sonuc.EskiRoller);
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], sonuc.YeniRoller);
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], await RollerAsync(kullaniciAdi));
        // Kimlik sunucusu oturumları (güvenlik damgası) ve refresh token'lar iptal.
        Assert.False(await KimlikSunucusundaAcikAsync(a));
        Assert.False(await KimlikSunucusundaAcikAsync(b));
        await InvalidGrantAsync(a, secret);
        await InvalidGrantAsync(b, secret);
        // Açık istemci oturumlarının her birine Back-Channel Logout (sid başına bir imzalı logout_token).
        BackChannelSidleri(a.Sid, b.Sid);
        // Denetim: eski ve yeni rol listesi; sır yok.
        var kayit = await RolDegistiKaydiAsync(id);
        Assert.Equal("eski=akademisyen; yeni=akademisyen,staj_komisyonu", kayit.Ayrinti);
        var tablo = string.Join('\n', (await _factory.TabloAsync("DenetimKayitlari")).Select(s => string.Join('|', s)));
        foreach (var sir in new[] { sifre, secret, b.Refresh, b.Access })
        {
            Assert.DoesNotContain(sir, tablo);
        }

        // Yeni girişte yeni roller token'a yansır.
        var yeni = await OturumAcAsync(kullaniciAdi, sifre, secret);
        var roller = OidcYardimcisi.JwtGovdesi(yeni.Access).GetProperty("role");
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], roller.EnumerateArray().Select(e => e.GetString()!).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Rol_cikarilinca_oturumlar_kapaniyor_refresh_invalid_grant_back_channel_logout_gidiyor_denetim_yaziliyor()
    {
        var secret = await SecretAsync();
        var (id, kullaniciAdi, sifre) = await KullaniciAsync(Roller.Akademisyen, Roller.StajKomisyonu);
        var oturum = await OturumAcAsync(kullaniciAdi, sifre, secret);
        Assert.True(await KimlikSunucusundaAcikAsync(oturum));

        var sonuc = await UygulaAsync(kullaniciAdi, [], [Roller.StajKomisyonu]);

        Assert.True(sonuc.Degisti);
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], sonuc.EskiRoller);
        Assert.Equal([Roller.Akademisyen], sonuc.YeniRoller);
        Assert.Equal([Roller.Akademisyen], await RollerAsync(kullaniciAdi));
        Assert.False(await KimlikSunucusundaAcikAsync(oturum));
        await InvalidGrantAsync(oturum, secret);
        BackChannelSidleri(oturum.Sid);
        Assert.Equal("eski=akademisyen,staj_komisyonu; yeni=akademisyen", (await RolDegistiKaydiAsync(id)).Ayrinti);

        // Çıkarılan rol yeni girişte token'da yok (tek rol: metin).
        var yeni = await OturumAcAsync(kullaniciAdi, sifre, secret);
        Assert.Equal(Roller.Akademisyen, OidcYardimcisi.JwtGovdesi(yeni.Access).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Rol_kumesi_degismiyorsa_hicbir_sey_yapilmiyor_oturum_ve_token_etkilenmiyor()
    {
        var secret = await SecretAsync();
        var (_, kullaniciAdi, sifre) = await KullaniciAsync(Roller.Akademisyen);
        var oturum = await OturumAcAsync(kullaniciAdi, sifre, secret);

        // Zaten olan rol ekleniyor, olmayan rol çıkarılıyor: küme aynı.
        var sonuc = await UygulaAsync(kullaniciAdi, [Roller.Akademisyen], [Roller.StajKomisyonu]);

        Assert.False(sonuc.Degisti);
        Assert.Empty(sonuc.Hatalar);
        Assert.Equal(sonuc.EskiRoller, sonuc.YeniRoller);
        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        await YenilenirAsync(oturum, secret);
        Assert.Empty(BackChannelIstekleri());
        // Denetim kuyruğu sıralıdır: sonradan yapılan bir girişin kaydı yazıldığında önceki olay da yazılmış olurdu.
        await OturumAcAsync(kullaniciAdi, sifre, secret);
        var kayitlar = await _factory.DenetimKayitlariAsync(k => k.Count(x => x.OlayTuru == DenetimOlayi.GirisBasarili) >= 2);
        Assert.DoesNotContain(kayitlar, x => x.OlayTuru == DenetimOlayi.RolDegisti);
    }

    [Fact]
    public async Task Baska_kullanicilar_etkilenmiyor()
    {
        var secret = await SecretAsync();
        var (_, degisen, degisenSifre) = await KullaniciAsync(Roller.Akademisyen);
        var (_, baska, baskaSifre) = await KullaniciAsync(Roller.Akademisyen, Roller.StajKomisyonu);
        var degisenOturum = await OturumAcAsync(degisen, degisenSifre, secret);
        var baskaOturum = await OturumAcAsync(baska, baskaSifre, secret);

        await UygulaAsync(degisen, [Roller.StajKomisyonu], []);

        await InvalidGrantAsync(degisenOturum, secret);
        // Diğer kullanıcının oturumu, token'ı ve rolleri aynı; ona Back-Channel Logout gitmedi.
        Assert.True(await KimlikSunucusundaAcikAsync(baskaOturum));
        await YenilenirAsync(baskaOturum, secret);
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], await RollerAsync(baska));
        BackChannelSidleri(degisenOturum.Sid);
    }

    [Fact]
    public async Task Kurallara_uymayan_rol_degisikligi_uygulanmiyor_oturum_etkilenmiyor()
    {
        var secret = await SecretAsync();
        var (_, kullaniciAdi, sifre) = await KullaniciAsync(Roller.Akademisyen);
        var oturum = await OturumAcAsync(kullaniciAdi, sifre, secret);

        // Bir kullanıcı hem akademisyen hem öğrenci olamaz (tam bir kullanıcı türü): doğrulayıcı reddeder.
        var sonuc = await UygulaAsync(kullaniciAdi, [Roller.Ogrenci], []);

        Assert.False(sonuc.Degisti);
        Assert.NotEmpty(sonuc.Hatalar);
        Assert.Equal([Roller.Akademisyen], await RollerAsync(kullaniciAdi));
        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        await YenilenirAsync(oturum, secret);
        Assert.Empty(BackChannelIstekleri());
    }

    [Fact]
    public async Task Baslangic_verisi_eksik_rol_eklerse_oturumlar_kapaniyor_rol_ayni_kalirsa_dokunulmuyor()
    {
        var secret = await SecretAsync();
        const string KullaniciAdi = "seed.kullanici";
        var sifre = $"Aa1{Guid.NewGuid():N}";
        var ilk = new BaslangicVerisi.TestKullanicisi("Seed", KullaniciAdi, "Seed Kullanıcı", Fakulte, Bolum, Roller.Akademisyen);
        var genis = ilk with { Roller = [Roller.Akademisyen, Roller.StajKomisyonu] };

        async Task SeedAsync(BaslangicVerisi.TestKullanicisi tanim)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            await BaslangicVerisi.TestKullanicisiOlusturAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                tanim, sifre, NullLogger.Instance, scope.ServiceProvider.GetRequiredService<RolDegisikligiIslemi>());
        }

        // Oluşturma: henüz oturum yok, rol değişikliği olayı yok.
        await SeedAsync(ilk);
        var oturum = await OturumAcAsync(KullaniciAdi, sifre, secret);
        var id = (await _factory.KullaniciAsync(KullaniciAdi)).Id;

        // Açılışta aynı tanım: küme aynı, oturum açık kalır.
        await SeedAsync(ilk);
        Assert.True(await KimlikSunucusundaAcikAsync(oturum));
        await YenilenirAsync(oturum, secret);
        Assert.Empty(BackChannelIstekleri());

        // Tanıma rol eklenince (eksik rol) açılış oturumları kapatır ve denetim yazar.
        await SeedAsync(genis);
        Assert.Equal([Roller.Akademisyen, Roller.StajKomisyonu], await RollerAsync(KullaniciAdi));
        Assert.False(await KimlikSunucusundaAcikAsync(oturum));
        BackChannelSidleri(oturum.Sid);
        Assert.Equal("eski=akademisyen; yeni=akademisyen,staj_komisyonu", (await RolDegistiKaydiAsync(id)).Ayrinti);

        // Tekrar açılış: artık eksik yok, ikinci kez hiçbir şey olmaz.
        await SeedAsync(genis);
        Assert.Single(BackChannelIstekleri());
    }
}
