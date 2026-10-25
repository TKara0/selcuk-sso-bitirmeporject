using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Selcuk.Identity.Data;
using Selcuk.Identity.Denetim;
using Selcuk.Identity.Eposta;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Güvenlik olaylarının denetim kaydı: olayların yazılması, sırların hiçbir sütuna girmemesi, KVKK saklama süresi
/// temizliği ve kullanıcının yalnızca kendi geçmişini görmesi. Kayıtlar arka planda yazıldığı için testler
/// <see cref="SelcukIdentityFactory.DenetimKayitlariAsync"/> ile bekler.
/// </summary>
public sealed partial class DenetimKaydiTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private string Ozet(string metin) => BeklenenOzet(_factory.OzetAnahtari, metin);

    /// <summary>Uygulamadan bağımsız hesaplanan beklenen değer: "h1:" + HMAC-SHA256(anahtar, amaç ‖ 0x00 ‖ ad).</summary>
    private static string BeklenenOzet(byte[] anahtar, string normalizeAd) =>
        "kullanici_adi_ozeti=h1:" + Convert.ToHexStringLower(HMACSHA256.HashData(anahtar,
            Encoding.UTF8.GetBytes(AnahtarliOzet.KullaniciAdiAmaci + "\0" + normalizeAd)));

    private static IEnumerable<Exception> Zincir(Exception? hata)
    {
        for (; hata is not null; hata = hata.InnerException)
        {
            yield return hata;
            if (hata is AggregateException toplu)
            {
                foreach (var ic in toplu.InnerExceptions.SelectMany(Zincir))
                {
                    yield return ic;
                }
            }
        }
    }

    private async Task KayitEkleAsync(params DenetimKaydi[] kayitlar)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.DenetimKayitlari.AddRange(kayitlar);
        await db.SaveChangesAsync();
    }

    private static string? OturumCerezi(HttpResponseMessage yanit) =>
        yanit.Headers.TryGetValues("Set-Cookie", out var cerezler)
            ? cerezler.Where(c => c.StartsWith("SelcukSSO.Oturum=", StringComparison.Ordinal))
                .Select(c => c["SelcukSSO.Oturum=".Length..c.IndexOf(';')]).LastOrDefault()
            : null;

    [Fact]
    public async Task Basarili_ve_basarisiz_giriste_kayit_olusuyor_olmayan_kullanici_adi_yalnizca_ozetle_saklaniyor()
    {
        const string olmayan = "Olmayan.Kullanici";
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync();
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;

        var client = _factory.HttpsIstemcisi();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SelcukTest/1.0");
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, "YanlisSifre123");
        await SelcukIdentityFactory.GirisYapAsync(client, olmayan, "YanlisSifre123");
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);

        var uzunUa = _factory.HttpsIstemcisi();
        uzunUa.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Uzun/" + new string('x', 400));
        await SelcukIdentityFactory.GirisYapAsync(uzunUa, olmayan, "YanlisSifre123");

        var kayitlar = await _factory.DenetimKayitlariAsync(k =>
            k.Count(x => x.OlayTuru == DenetimOlayi.GirisBasarisiz) == 3 && k.Any(x => x.OlayTuru == DenetimOlayi.GirisBasarili));

        var basarili = kayitlar.Single(k => k.OlayTuru == DenetimOlayi.GirisBasarili);
        Assert.Equal(kullaniciId, basarili.KullaniciId);
        Assert.Equal(DenetimSonucu.Basarili, basarili.Sonuc);
        Assert.Equal("amr=pwd", basarili.Ayrinti);
        Assert.Equal("SelcukTest/1.0", basarili.KisaUserAgent);
        Assert.True(basarili.ZamanUtc > DateTime.UtcNow.AddMinutes(-1));

        var yanlisSifre = kayitlar.Single(k => k.OlayTuru == DenetimOlayi.GirisBasarisiz && k.KullaniciId == kullaniciId);
        Assert.Equal(DenetimSonucu.Basarisiz, yanlisSifre.Sonuc);
        Assert.Equal("neden=sifre-yanlis", yanlisSifre.Ayrinti);

        // Olmayan kullanıcı: kullanıcı kimliği yok, yazılan ad hiçbir sütunda yok, normalize hâlinin özeti var.
        var olmayanlar = kayitlar.Where(k => k.OlayTuru == DenetimOlayi.GirisBasarisiz && k.KullaniciId is null).ToList();
        Assert.Equal(2, olmayanlar.Count);
        Assert.All(olmayanlar, k => Assert.Contains(Ozet(olmayan.ToUpperInvariant()), k.Ayrinti));
        Assert.Equal(DenetimKaydedici.UserAgentUzunlugu, olmayanlar[1].KisaUserAgent!.Length);

        var tablo = await _factory.TabloAsync("DenetimKayitlari");
        Assert.DoesNotContain(tablo.SelectMany(s => s).OfType<string>(),
            d => d.Contains(olmayan, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Olmayan_kullanici_adi_h1_onekli_HMAC_ile_yaziliyor_ayni_ad_ayni_ozet_farkli_anahtar_farkli_ozet()
    {
        const string olmayan = "hayalet.kullanici";
        var anahtar1 = RandomNumberGenerator.GetBytes(32);
        var anahtar2 = RandomNumberGenerator.GetBytes(32);

        async Task<List<string>> OzetlerAsync(byte[] anahtar)
        {
            using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
            {
                [AnahtarliOzet.AyarAdi] = Convert.ToBase64String(anahtar),
            });
            var client = factory.HttpsIstemcisi();
            await SelcukIdentityFactory.GirisYapAsync(client, olmayan, "YanlisSifre123");
            // Aynı ad farklı yazılışla (normalize edilince aynı).
            await SelcukIdentityFactory.GirisYapAsync(client, " Hayalet.KULLANICI ", "YanlisSifre123");
            var kayitlar = await factory.DenetimKayitlariAsync(k => k.Count(x => x.OlayTuru == DenetimOlayi.GirisBasarisiz) == 2);
            return [.. kayitlar.Where(k => k.OlayTuru == DenetimOlayi.GirisBasarisiz)
                .Select(k => KullaniciAdiOzeti().Match(k.Ayrinti!).Value)];
        }

        var ozetler1 = await OzetlerAsync(anahtar1);
        Assert.All(ozetler1, o => Assert.Matches("^kullanici_adi_ozeti=h1:[0-9a-f]{64}$", o));
        Assert.Equal(ozetler1[0], ozetler1[1]);
        Assert.Equal(BeklenenOzet(anahtar1, olmayan.ToUpperInvariant()), ozetler1[0]);
        // Anahtarsız SHA-256 değil.
        Assert.DoesNotContain(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(olmayan.ToUpperInvariant()))),
            ozetler1[0]);

        var ozetler2 = await OzetlerAsync(anahtar2);
        Assert.Equal(ozetler2[0], ozetler2[1]);
        Assert.Equal(BeklenenOzet(anahtar2, olmayan.ToUpperInvariant()), ozetler2[0]);
        Assert.NotEqual(ozetler1[0], ozetler2[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bm90LWVub3VnaA==")]
    [InlineData("base64-degil!")]
    public void Development_disinda_HMAC_anahtari_yoksa_veya_gecersizse_uygulama_baslamiyor(string anahtar)
    {
        // Fabrika "Testing" ortamında çalışır (Development değil); anahtar boş, 32 bayttan kısa veya base64 değil.
        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            [AnahtarliOzet.AyarAdi] = anahtar,
        });

        var hata = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(hata);
        Assert.Contains(Zincir(hata), e => e is InvalidOperationException && e.Message.Contains(AnahtarliOzet.AyarAdi));
    }

    [Fact]
    public async Task Denetim_tablosunun_hicbir_sutununda_sifre_kod_veya_token_bulunmuyor()
    {
        var secret = await OidcYardimcisi.DemoObisKaydetAsync(_factory);
        var (kullaniciAdi, sifre) = await _factory.KullaniciOlusturAsync(Roller.Ogrenci);
        var kullaniciId = (await _factory.KullaniciAsync(kullaniciAdi)).Id;
        var sirlar = new List<string> { sifre };
        var client = _factory.HttpsIstemcisi();

        // Kullanıcı adı alanına şifre yazılmış başarısız giriş.
        await SelcukIdentityFactory.GirisYapAsync(client, sifre, sifre);

        // Şifreyle giriş, kurulum sayfasından 2FA açma (TOTP + 10 kurtarma kodu), çıkış.
        var giris = await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        sirlar.Add(OturumCerezi(giris)!);
        await SelcukIdentityFactory.FormGonderAsync(client, "/Account/IkiAdimDogrulama", [],
            "/Account/IkiAdimDogrulama?handler=Baslat");
        var anahtar = (await _factory.AnahtarAsync(kullaniciAdi))!;
        sirlar.Add(anahtar);
        // Kurulumdaki kod kullanılmış sayılır: giriş başka bir zaman adımının koduyla yapılır.
        var kodlar = Totp.Kodlar(anahtar, 0, 1);
        var acmaKodu = kodlar[0];
        var acma = await SelcukIdentityFactory.FormGonderAsync(client, "/Account/IkiAdimDogrulama",
            new() { ["Etkinlestirme.Kod"] = acmaKodu }, "/Account/IkiAdimDogrulama?handler=Etkinlestir");
        var kurtarmaKodlari = KurtarmaKodu().Matches(await acma.Content.ReadAsStringAsync()).Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(10, kurtarmaKodlari.Count);
        sirlar.Add(acmaKodu);
        sirlar.AddRange(kurtarmaKodlari);
        sirlar.AddRange(kurtarmaKodlari.Select(k => k.Replace("-", "")));
        await SelcukIdentityFactory.FormGonderAsync(client, "/connect/logout", []);

        // Şifre + TOTP ile giriş, OIDC kodu ve token'lar, sonra çıkış (demo-obis'e logout_token gider).
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var girisKodu = kodlar[1];
        var ikinci = await SelcukIdentityFactory.IkinciAdimAsync(client, girisKodu);
        sirlar.Add(girisKodu);
        sirlar.Add(OturumCerezi(ikinci)!);
        var pkce = OidcYardimcisi.Pkce.Yeni();
        var code = await OidcYardimcisi.KodAlAsync(client, pkce);
        var tokenYaniti = await OidcYardimcisi.TokenIsteAsync(client, code, pkce.Verifier, secret);
        var token = System.Text.Json.JsonDocument.Parse(await tokenYaniti.Content.ReadAsStringAsync()).RootElement;
        sirlar.AddRange([code, pkce.Verifier, secret,
            token.GetProperty("access_token").GetString()!, token.GetProperty("id_token").GetString()!]);
        await SelcukIdentityFactory.FormGonderAsync(client, "/connect/logout", []);
        sirlar.Add(_factory.BackChannel.Istekler.Single().Form["logout_token"]);

        // Kurtarma koduyla giriş ve yanlış TOTP denemesi.
        await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre);
        var yanlisKod = Totp.Gecersiz(anahtar);
        sirlar.Add(yanlisKod);
        await SelcukIdentityFactory.IkinciAdimAsync(client, yanlisKod);
        await SelcukIdentityFactory.IkinciAdimAsync(client, kurtarmaKodlari[0]);

        // Şifre sıfırlama: e-postadaki bağlantı (kod) ve yeni şifre.
        await SelcukIdentityFactory.FormGonderAsync(_factory.HttpsIstemcisi(), "/Account/SifremiUnuttum",
            new() { ["Input.KullaniciAdiVeyaEposta"] = kullaniciAdi });
        var sifirlamaEpostasi = (await TumEpostalarAsync(EpostaSablonlari.SifreSifirlamaKonusu)).Single();
        var baglanti = new Uri(SifirlamaBaglantisi().Match(sifirlamaEpostasi.Govde).Value);
        var sorgu = QueryHelpers.ParseQuery(baglanti.Query);
        var yeniSifre = $"Bb2{Guid.NewGuid():N}";
        sirlar.AddRange([sorgu["kod"].ToString(),
            Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(sorgu["kod"].ToString())), yeniSifre]);
        var sifirlama = await SelcukIdentityFactory.FormGonderAsync(_factory.HttpsIstemcisi(), baglanti.PathAndQuery, new()
        {
            ["Input.Kullanici"] = sorgu["kullanici"].ToString(),
            ["Input.Kod"] = sorgu["kod"].ToString(),
            ["Input.YeniSifre"] = yeniSifre,
            ["Input.YeniSifreTekrar"] = yeniSifre,
        });
        Assert.Contains("Şifreniz değiştirildi", WebUtility.HtmlDecode(await sifirlama.Content.ReadAsStringAsync()));

        var kayitlar = await _factory.DenetimKayitlariAsync(k => k.Any(x => x.OlayTuru == DenetimOlayi.SifreSifirlandi));
        var olaylar = kayitlar.Select(k => k.OlayTuru).ToHashSet();
        Assert.Superset(olaylar, new HashSet<DenetimOlayi>(
        [
            DenetimOlayi.GirisBasarisiz, DenetimOlayi.GirisBasarili, DenetimOlayi.IkiAdimKurulumuBaslatildi,
            DenetimOlayi.IkiAdimAcildi, DenetimOlayi.Cikis,
            DenetimOlayi.IkiAdimBasarili, DenetimOlayi.IkiAdimBasarisiz, DenetimOlayi.KurtarmaKoduKullanildi,
            DenetimOlayi.OnayVerildi, DenetimOlayi.TokenVerildi, DenetimOlayi.BackChannelLogoutGonderildi,
            DenetimOlayi.SifreSifirlamaIstendi, DenetimOlayi.SifreSifirlandi,
        ]));
        Assert.Contains(kayitlar, k => k.OlayTuru == DenetimOlayi.BackChannelLogoutGonderildi
            && k.KullaniciId == kullaniciId && k.IstemciId == Istemciler.DemoObisClientId);

        // Tüm tablo, tüm sütunlar: hiçbir sır geçmiyor.
        var degerler = (await _factory.TabloAsync("DenetimKayitlari")).SelectMany(s => s).OfType<string>().ToList();
        Assert.True(degerler.Count > 50, $"Tabloda beklenenden az değer var ({degerler.Count}).");
        foreach (var sir in sirlar.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            Assert.DoesNotContain(degerler, d => d.Contains(sir, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<List<EpostaMesaji>> TumEpostalarAsync(string konu)
    {
        var bitis = DateTime.UtcNow.AddSeconds(10);
        while (!_factory.Eposta.Gonderilenler.Any(e => e.Konu == konu) && DateTime.UtcNow < bitis)
        {
            await Task.Delay(50);
        }

        return [.. _factory.Eposta.Gonderilenler.Where(e => e.Konu == konu)];
    }

    [Fact]
    public async Task Saklama_suresini_asan_kayitlar_temizlik_isiyle_siliniyor()
    {
        Assert.Equal(DenetimTemizlemeHizmeti.VarsayilanSaklamaGun,
            _factory.Services.GetRequiredService<DenetimTemizlemeHizmeti>().SaklamaGun);

        using var factory = new SelcukIdentityFactory(ayarlar: new Dictionary<string, string?>
        {
            [DenetimTemizlemeHizmeti.AyarAdi] = "7",
        });
        var hizmet = factory.Services.GetRequiredService<DenetimTemizlemeHizmeti>();
        Assert.Equal(7, hizmet.SaklamaGun);

        var simdi = DateTime.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DenetimKayitlari.AddRange(
                new DenetimKaydi { ZamanUtc = simdi.AddDays(-30), OlayTuru = DenetimOlayi.Cikis, Ayrinti = "cok-eski" },
                new DenetimKaydi { ZamanUtc = simdi.AddDays(-8), OlayTuru = DenetimOlayi.Cikis, Ayrinti = "eski" },
                new DenetimKaydi { ZamanUtc = simdi.AddDays(-6), OlayTuru = DenetimOlayi.Cikis, Ayrinti = "sinirda" },
                new DenetimKaydi { ZamanUtc = simdi, OlayTuru = DenetimOlayi.Cikis, Ayrinti = "yeni" });
            await db.SaveChangesAsync();
        }

        // Açılıştaki ilk çalışma bu kayıtlardan önce veya sonra bitmiş olabilir; sonuç aynı olmalı.
        await hizmet.TemizleAsync();

        var kalanlar = await factory.DenetimKayitlariAsync(_ => true);
        Assert.Equal(["sinirda", "yeni"], kalanlar.Where(k => k.OlayTuru == DenetimOlayi.Cikis).Select(k => k.Ayrinti).Order());
    }

    [Fact]
    public async Task Guvenlik_gecmisi_yalnizca_kullanicinin_kendi_son_20_kaydini_gosteriyor()
    {
        await _factory.IstemcileriKaydetAsync();
        var (a, sifreA) = await _factory.KullaniciOlusturAsync();
        var (b, _) = await _factory.KullaniciOlusturAsync();
        var aId = (await _factory.KullaniciAsync(a)).Id;
        var bId = (await _factory.KullaniciAsync(b)).Id;

        await KayitEkleAsync([.. Enumerable.Range(0, 25).Select(_ => new DenetimKaydi
        {
            ZamanUtc = DateTime.UtcNow, OlayTuru = DenetimOlayi.TokenVerildi, Sonuc = DenetimSonucu.Basarili,
            KullaniciId = aId, IstemciId = Istemciler.DemoObisClientId, IpAdresi = "192.0.2.10",
        })]);
        await KayitEkleAsync(new DenetimKaydi
        {
            ZamanUtc = DateTime.UtcNow, OlayTuru = DenetimOlayi.TokenVerildi, Sonuc = DenetimSonucu.Basarili,
            KullaniciId = bId, IstemciId = Istemciler.DemoYemekhaneClientId, IpAdresi = "198.51.100.77",
        });

        var client = _factory.HttpsIstemcisi();
        await SelcukIdentityFactory.GirisYapAsync(client, a, sifreA);
        var yanit = await client.GetAsync("/Account/GuvenlikGecmisi");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        var html = WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
        Assert.Contains("Uygulamaya giriş yapıldı", html);
        Assert.Contains("Uygulama: Demo OBİS", html);
        Assert.Contains("IP: 192.0.2.10", html);
        Assert.DoesNotContain("Demo Yemekhane", html);
        Assert.DoesNotContain("198.51.100.77", html);
        Assert.Equal(20, Satir().Matches(GecmisListesi().Match(html).Value).Count);
    }

    [GeneratedRegex("<li><code>([A-Z0-9]{5}-[A-Z0-9]{5})</code></li>")]
    private static partial Regex KurtarmaKodu();

    [GeneratedRegex(@"https://\S+/Account/SifreSifirla\?\S+")]
    private static partial Regex SifirlamaBaglantisi();

    [GeneratedRegex("<ul class=\"kapsamlar gecmis\">.*?</ul>", RegexOptions.Singleline)]
    private static partial Regex GecmisListesi();

    [GeneratedRegex("<li>")]
    private static partial Regex Satir();

    [GeneratedRegex(@"kullanici_adi_ozeti=\S+")]
    private static partial Regex KullaniciAdiOzeti();
}
