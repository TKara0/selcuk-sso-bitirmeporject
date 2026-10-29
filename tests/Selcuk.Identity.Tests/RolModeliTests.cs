using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Selcuk.Identity.Data;
using Selcuk.Identity.Hesap;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Rol modeli: katalog (Selcuk.Roller), tutarlılık doğrulayıcısı, test kullanıcılarının seed'i, token'lardaki role
/// claim'i (tek değer / JSON dizisi), Hesabım'daki gruplu roller ve 2FA'nın hiçbir rol için zorunlu olmaması.
/// </summary>
public sealed class RolModeliTests : IDisposable
{
    private const string Teknoloji = "Teknoloji Fakültesi";
    private const string Bilgisayar = "Bilgisayar Mühendisliği";

    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private Task<(string KullaniciAdi, string Sifre)> CokRolluKullaniciAsync(
        string? fakulte, string? bolum, params string[] roller) => CokRolluKullaniciAsync(_factory, fakulte, bolum, roller);

    private static async Task<(string KullaniciAdi, string Sifre)> CokRolluKullaniciAsync(
        SelcukIdentityFactory factory, string? fakulte, string? bolum, params string[] roller)
    {
        var (kullaniciAdi, sifre) = await factory.KullaniciOlusturAsync(roller[0], fakulte, bolum);
        if (roller.Length > 1)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var sonuc = await userManager.AddToRolesAsync((await userManager.FindByNameAsync(kullaniciAdi))!, roller[1..]);
            Assert.True(sonuc.Succeeded);
        }

        return (kullaniciAdi, sifre);
    }

    private Task<HttpClient> GirisYapmisIstemciAsync(string kullaniciAdi, string sifre) =>
        GirisYapmisIstemciAsync(_factory, kullaniciAdi, sifre);

    private static async Task<HttpClient> GirisYapmisIstemciAsync(SelcukIdentityFactory factory, string kullaniciAdi,
        string sifre)
    {
        var client = factory.HttpsIstemcisi();
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        Assert.Equal(HttpStatusCode.Redirect, giris.StatusCode);
        Assert.Equal("/", giris.Headers.Location!.OriginalString);
        return client;
    }

    private static string[] RolDegerleri(JsonElement govde) =>
        govde.GetProperty(Claims.Role).ValueKind == JsonValueKind.Array
            ? [.. govde.GetProperty(Claims.Role).EnumerateArray().Select(e => e.GetString()!)]
            : [govde.GetProperty(Claims.Role).GetString()!];

    // ---------- Doğrulayıcı ----------

    [Fact]
    public void Dogrulayici_iki_kullanici_turunu_reddediyor() =>
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Akademisyen, Roller.Personel], Teknoloji, Bilgisayar));

    [Fact]
    public void Dogrulayici_kullanici_turu_olmayan_kumeyi_reddediyor()
    {
        Assert.NotEmpty(RolDogrulayici.Hatalar([], Teknoloji, Bilgisayar));
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Danisman], Teknoloji, Bilgisayar));
    }

    [Theory]
    [InlineData(Roller.Ogrenci, Roller.BolumBaskani)]
    [InlineData(Roller.Ogrenci, Roller.StajKomisyonu)]
    [InlineData(Roller.Personel, Roller.StajKomisyonu)]
    [InlineData(Roller.Personel, Roller.BolumBaskani)]
    [InlineData(Roller.Personel, Roller.Danisman)]
    [InlineData(Roller.Akademisyen, Roller.BolumSekreteri)]
    [InlineData(Roller.Akademisyen, Roller.FakulteSekreteri)]
    [InlineData(Roller.Akademisyen, Roller.IskurSorumlusu)]
    public void Dogrulayici_kullanici_turune_uymayan_gorevi_reddediyor(string tur, string gorev)
    {
        var hatalar = RolDogrulayici.Hatalar([tur, gorev], Teknoloji, Bilgisayar);
        Assert.Contains(hatalar, h => h.Contains(gorev, StringComparison.Ordinal));
    }

    [Fact]
    public void Dogrulayici_ogrenciye_hicbir_gorev_veya_komisyon_rolu_vermiyor()
    {
        foreach (var rol in Roller.Katalog.Where(r => r.Katman != RolKatmani.KullaniciTuru))
        {
            Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Ogrenci, rol.Kimlik], Teknoloji, Bilgisayar));
        }
    }

    [Fact]
    public void Dogrulayici_kapsamli_rolu_birimi_bos_kullaniciya_vermiyor()
    {
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Akademisyen, Roller.BolumBaskani], Teknoloji, null));
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Personel, Roller.BolumSekreteri], Teknoloji, " "));
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Personel, Roller.FakulteSekreteri], null, Bilgisayar));
        // Fakülte sekreterinin bölümü boş olabilir.
        Assert.Empty(RolDogrulayici.Hatalar([Roller.Personel, Roller.FakulteSekreteri], Teknoloji, null));
    }

    [Fact]
    public void Dogrulayici_katalogda_olmayan_rolu_reddediyor() =>
        Assert.NotEmpty(RolDogrulayici.Hatalar([Roller.Akademisyen, "dekan"], Teknoloji, Bilgisayar));

    [Fact]
    public void Dogrulayici_gecerli_kombinasyonlari_kabul_ediyor()
    {
        Assert.Empty(RolDogrulayici.Hatalar([Roller.Ogrenci], Teknoloji, Bilgisayar));
        Assert.Empty(RolDogrulayici.Hatalar([Roller.Personel], Teknoloji, null));
        Assert.Empty(RolDogrulayici.Hatalar(
            [Roller.Akademisyen, Roller.BolumBaskani, Roller.Danisman, Roller.CapYandalKoordinatoru,
             .. Roller.Katalog.Where(r => r.Katman == RolKatmani.KomisyonVeKurul).Select(r => r.Kimlik)],
            Teknoloji, Bilgisayar));
        Assert.Empty(RolDogrulayici.Hatalar(
            [Roller.Personel, Roller.BolumSekreteri, Roller.FakulteSekreteri, Roller.IskurSorumlusu], Teknoloji, Bilgisayar));
        // Her görev rolü, katalogdaki kullanıcı türüyle geçerli.
        foreach (var rol in Roller.Katalog.Where(r => r.Katman != RolKatmani.KullaniciTuru))
        {
            Assert.Empty(RolDogrulayici.Hatalar([rol.VerilebilecekTur!, rol.Kimlik], Teknoloji, Bilgisayar));
        }
    }

    [Fact]
    public void Dogrula_ihlalde_kullanici_adiyla_acik_hata_veriyor()
    {
        var hata = Assert.Throws<InvalidOperationException>(() =>
            RolDogrulayici.Dogrula("hatali.test", [Roller.Ogrenci, Roller.BolumBaskani], Teknoloji, Bilgisayar));
        Assert.Contains("hatali.test", hata.Message);
        Assert.Contains(Roller.BolumBaskani, hata.Message);
    }

    // ---------- Seed ----------

    [Fact]
    public void Seed_tanimlari_kurallara_uyuyor_hatali_tanim_acilisi_durduruyor()
    {
        BaslangicVerisi.TanimlariDogrula(BaslangicVerisi.TestKullanicilari);

        var hatali = new BaslangicVerisi.TestKullanicisi("Hatali", "hatali.test", "Test Hatalı", Teknoloji, Bilgisayar,
            Roller.Personel, Roller.StajKomisyonu);
        var hata = Assert.Throws<InvalidOperationException>(() =>
            BaslangicVerisi.TanimlariDogrula([.. BaslangicVerisi.TestKullanicilari, hatali]));
        Assert.Contains("hatali.test", hata.Message);
    }

    [Fact]
    public void Seed_tanimlari_beklenen_kullanicilari_ve_rolleri_iceriyor()
    {
        var beklenen = new Dictionary<string, string[]>
        {
            ["203011001"] = [Roller.Ogrenci],
            ["203011002"] = [Roller.Ogrenci],
            ["203011003"] = [Roller.Ogrenci],
            ["hoca.test"] = [Roller.Akademisyen, Roller.Danisman],
            ["personel.test"] = [Roller.Personel],
            ["baskan.test"] = [Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi],
            ["sekreter.test"] = [Roller.Personel, Roller.BolumSekreteri],
            ["fakultesekreter.test"] = [Roller.Personel, Roller.FakulteSekreteri],
            ["komisyon.test"] = [Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.StajKomisyonu,
                Roller.MezuniyetKomisyonu, Roller.BolumKuruluUyesi],
            ["mudek.test"] = [Roller.Akademisyen, Roller.MudekKomisyonu, Roller.BolumKuruluUyesi],
            ["etik.test"] = [Roller.Akademisyen, Roller.EtikKurulUyesi],
            ["koordinator.test"] = [Roller.Akademisyen, Roller.CapYandalKoordinatoru, Roller.Danisman],
            ["iskur.test"] = [Roller.Personel, Roller.IskurSorumlusu],
        };

        var tanimlar = BaslangicVerisi.TestKullanicilari.ToDictionary(t => t.KullaniciAdi);
        Assert.Equal(beklenen.Keys.Order(), tanimlar.Keys.Order());
        foreach (var (kullaniciAdi, roller) in beklenen)
        {
            Assert.Equal(roller.Order(), tanimlar[kullaniciAdi].Roller.Order());
            Assert.EndsWith("@test.local", tanimlar[kullaniciAdi].Email);
            Assert.StartsWith("Test ", tanimlar[kullaniciAdi].AdSoyad);
        }
    }

    [Fact]
    public async Task Her_test_kullanicisi_beklenen_rollerle_seed_ediliyor()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var tanim in BaslangicVerisi.TestKullanicilari)
        {
            await BaslangicVerisi.TestKullanicisiOlusturAsync(userManager, tanim, $"Aa1{Guid.NewGuid():N}",
                NullLogger.Instance);
        }

        foreach (var tanim in BaslangicVerisi.TestKullanicilari)
        {
            var kullanici = await userManager.FindByNameAsync(tanim.KullaniciAdi);
            Assert.NotNull(kullanici);
            Assert.Equal(tanim.Roller.Order(), (await userManager.GetRolesAsync(kullanici)).Order());
            Assert.Equal(tanim.Fakulte, kullanici.Fakulte);
            Assert.Equal(tanim.Bolum, kullanici.Bolum);
            Assert.Empty(RolDogrulayici.Hatalar(await userManager.GetRolesAsync(kullanici), kullanici.Fakulte, kullanici.Bolum));
        }
    }

    [Fact]
    public async Task Var_olan_test_kullanicisina_yalnizca_eksik_roller_ekleniyor_sifre_ve_2FA_degismiyor()
    {
        var hoca = BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == "hoca.test");
        const string EskiSifre = "Aa1-eski-sifre-test";
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var mevcut = new ApplicationUser
        {
            UserName = hoca.KullaniciAdi, Email = hoca.Email, EmailConfirmed = true, AdSoyad = hoca.AdSoyad,
            Fakulte = hoca.Fakulte, Bolum = hoca.Bolum,
        };
        Assert.True((await userManager.CreateAsync(mevcut, EskiSifre)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(mevcut, Roller.Akademisyen)).Succeeded);
        Assert.True((await userManager.SetTwoFactorEnabledAsync(mevcut, true)).Succeeded);

        await BaslangicVerisi.TestKullanicisiOlusturAsync(userManager, hoca, "Aa1-yeni-sifre-test", NullLogger.Instance);

        var sonra = (await userManager.FindByNameAsync(hoca.KullaniciAdi))!;
        Assert.Equal([Roller.Akademisyen, Roller.Danisman], (await userManager.GetRolesAsync(sonra)).Order());
        Assert.True(await userManager.CheckPasswordAsync(sonra, EskiSifre));
        Assert.True(await userManager.GetTwoFactorEnabledAsync(sonra));
    }

    // ---------- Token içeriği ----------

    [Fact]
    public async Task Cok_rollu_kullanicinin_id_token_ve_access_tokeninda_role_dizi_tek_rolluda_metin()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        string[] roller = [Roller.Akademisyen, Roller.BolumBaskani, Roller.BolumKuruluUyesi, Roller.Danisman];
        var (cokRollu, sifre) = await CokRolluKullaniciAsync(Teknoloji, Bilgisayar, roller);

        var token = await OidcYardimcisi.TokenAlAsync(await GirisYapmisIstemciAsync(cokRollu, sifre), secret);
        foreach (var ad in new[] { "id_token", "access_token" })
        {
            var govde = OidcYardimcisi.JwtGovdesi(token.GetProperty(ad).GetString()!);
            Assert.Equal(JsonValueKind.Array, govde.GetProperty(Claims.Role).ValueKind);
            Assert.Equal(roller.Order(), RolDegerleri(govde).Order());
        }

        var (tekRollu, tekSifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, Teknoloji, Bilgisayar);
        var tekToken = await OidcYardimcisi.TokenAlAsync(await GirisYapmisIstemciAsync(tekRollu, tekSifre), secret);
        foreach (var ad in new[] { "id_token", "access_token" })
        {
            var govde = OidcYardimcisi.JwtGovdesi(tekToken.GetProperty(ad).GetString()!);
            Assert.Equal(JsonValueKind.String, govde.GetProperty(Claims.Role).ValueKind);
            Assert.Equal([Roller.Ogrenci], RolDegerleri(govde));
        }
    }

    // ---------- Görünüm ----------

    [Fact]
    public async Task Hesabim_rolleri_katmana_gore_turkce_adlar_ve_kapsamla_gosteriyor()
    {
        var (kullaniciAdi, sifre) = await CokRolluKullaniciAsync(Teknoloji, Bilgisayar,
            Roller.Akademisyen, Roller.BolumBaskani, Roller.Danisman, Roller.MudekKomisyonu, Roller.BolumKuruluUyesi);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/Account/Hesabim")).Content.ReadAsStringAsync());

        Assert.Contains("<h2>Rollerin</h2>", html);
        var bolum = html[html.IndexOf("<h2>Rollerin</h2>", StringComparison.Ordinal)..html.IndexOf("<h2>Güvenlik</h2>", StringComparison.Ordinal)];
        Assert.Contains("<dt>Kullanıcı türü</dt>", bolum);
        Assert.Contains("<dt>İdari görevler</dt>", bolum);
        Assert.Contains("<dt>Komisyon ve kurul üyelikleri</dt>", bolum);
        // Katman sırası: kullanıcı türü, idari görevler, komisyonlar.
        Assert.True(bolum.IndexOf("Kullanıcı türü", StringComparison.Ordinal) < bolum.IndexOf("İdari görevler", StringComparison.Ordinal));
        Assert.True(bolum.IndexOf("İdari görevler", StringComparison.Ordinal) < bolum.IndexOf("Komisyon ve kurul", StringComparison.Ordinal));
        Assert.Contains("Akademisyen", bolum);
        Assert.Contains("Bölüm başkanı (Bilgisayar Mühendisliği)", bolum);
        Assert.Contains("Danışman", bolum);
        Assert.Contains("MÜDEK komisyonu üyesi", bolum);
        Assert.Contains("Bölüm kurulu üyesi", bolum);
        // Ham kimlikler görünmez.
        Assert.DoesNotContain("bolum_baskani", bolum);

        // Hesap menüsünde kullanıcı adının altında kullanıcı türü.
        Assert.Matches("<li class=\"hesap-basligi\">\\s*<p><strong class=\"mono\">" + kullaniciAdi
            + "</strong></p>\\s*<p class=\"ipucu\">Akademisyen</p>", html);
    }

    [Fact]
    public async Task Hesap_menusu_idari_personeli_gorunen_adiyla_gosteriyor()
    {
        var (kullaniciAdi, sifre) = await CokRolluKullaniciAsync(Teknoloji, null, Roller.Personel, Roller.FakulteSekreteri);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/Account/Hesabim")).Content.ReadAsStringAsync());

        Assert.Contains("<p class=\"ipucu\">İdari personel</p>", html);
        Assert.Contains("Fakülte sekreteri (Teknoloji Fakültesi)", html);
    }

    [Fact]
    public async Task Hesabim_2FA_kapaliyken_notr_oneri_bandi_aciksa_bant_yok()
    {
        const string Oneri = "İki aşamalı doğrulama isteğe bağlıdır. Açarsan hesabın şifren ele geçse bile korunur.";
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci, Teknoloji, Bilgisayar);
        var client = await GirisYapmisIstemciAsync(kullaniciAdi, sifre);

        var html = WebUtility.HtmlDecode(await (await client.GetAsync("/Account/Hesabim")).Content.ReadAsStringAsync());
        Assert.Contains(Oneri, html);
        Assert.Contains("href=\"/Account/IkiAdimDogrulama\">İki aşamalı doğrulamayı aç</a>", html);
        Assert.DoesNotContain("zorunlu", html, StringComparison.OrdinalIgnoreCase);

        var (ikiAdimli, ikiAdimliSifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var (anahtar, _) = await _factory.IkiAdimAcAsync(ikiAdimli);
        var ikinci = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(ikinci, ikiAdimli, ikiAdimliSifre);
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.IkinciAdimAsync(ikinci, Totp.Uret(anahtar))).StatusCode);
        var acikHtml = WebUtility.HtmlDecode(await (await ikinci.GetAsync("/Account/Hesabim")).Content.ReadAsStringAsync());
        Assert.DoesNotContain(Oneri, acikHtml);
    }

    // ---------- 2FA isteğe bağlı ----------

    [Fact]
    public async Task IkiAdimZorunluRoller_bos_her_roldeki_2FA_kapali_kullanici_demo_obise_kurulumsuz_giriyor()
    {
        Assert.Empty(_factory.Services.GetRequiredService<IOptions<IkiAdimAyarlari>>().Value.ZorunluRoller);

        // Her rol için ayrı giriş yapılır; giriş POST sınırı (dakikada 10, IP + sayfa) aşılmasın diye roller 8'lik
        // gruplar hâlinde ayrı fabrikalarda (varsayılan ayarlarla) denenir.
        foreach (var grup in Roller.Katalog.Chunk(8))
        {
            using var factory = new SelcukIdentityFactory();
            Assert.Empty(factory.Services.GetRequiredService<IOptions<IkiAdimAyarlari>>().Value.ZorunluRoller);
            var secret = await OidcYardimcisi.DemoObisKaydetAsync(factory);
            foreach (var rol in grup)
            {
                string[] roller = rol.Katman == RolKatmani.KullaniciTuru ? [rol.Kimlik] : [rol.VerilebilecekTur!, rol.Kimlik];
                var (kullaniciAdi, sifre) = await CokRolluKullaniciAsync(factory, Teknoloji, Bilgisayar, roller);
                Assert.False((await factory.KullaniciAsync(kullaniciAdi)).TwoFactorEnabled);

                // Giriş Uygulamalarım'a gider (kurulum sayfasına değil); demo-obis kod ve token verir.
                var client = await GirisYapmisIstemciAsync(factory, kullaniciAdi, sifre);
                var token = await OidcYardimcisi.TokenAlAsync(client, secret);
                Assert.Contains(rol.Kimlik, RolDegerleri(OidcYardimcisi.JwtGovdesi(token.GetProperty("id_token").GetString()!)));
            }
        }
    }
}
