using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Pages;

namespace Selcuk.Identity.Tests;

/// <summary>
/// Uygulamalarım'ın grupları: istemci kaydındaki "grup" özelliği, grupların ve grup içi kartların sırası, kartı
/// olmayan grubun gösterilmemesi, grubu olmayan istemcinin "Diğer" altında görünmesi ve başlıkların erişilebilirliği.
/// Kullanıcılar Development başlangıç verisinin tanımlarıyla (rastgele şifreyle) oluşturulur.
/// </summary>
public sealed partial class UygulamalarimGruplariTests : IDisposable
{
    private readonly SelcukIdentityFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Secret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    private async Task KaydetAsync(OpenIddictApplicationDescriptor tanim)
    {
        Istemciler.AdresleriDogrula(tanim);
        await using var scope = _factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(tanim);
    }

    /// <summary>İki demo ve on bir otomasyon (Development'ta açılışta kaydedilenlerin hepsi).</summary>
    private static List<OpenIddictApplicationDescriptor> TumUygulamalar() =>
    [
        Istemciler.DemoObis(Secret()), Istemciler.DemoYemekhane(Secret()),
        Istemciler.IntibakOtomasyonu(Secret()), Istemciler.StajOtomasyonu(Secret()), Istemciler.MezuniyetOtomasyonu(Secret()),
        Istemciler.CapYandalOtomasyonu(Secret()), Istemciler.DanismanlikOtomasyonu(Secret()),
        Istemciler.LisansProjeleriOtomasyonu(Secret()), Istemciler.IskurOtomasyonu(Secret()),
        Istemciler.EtikKurulOtomasyonu(Secret()), Istemciler.BolumKurulOtomasyonu(Secret()),
        Istemciler.MudekOtomasyonu(Secret()), Istemciler.SinavAnaliziOtomasyonu(Secret()),
    ];

    private async Task<string> UygulamalarimAsync(string kullaniciAdi)
    {
        var sifre = $"Aa1{Guid.NewGuid():N}";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await BaslangicVerisi.TestKullanicisiOlusturAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == kullaniciAdi), sifre, NullLogger.Instance);
        }

        var client = _factory.HttpsIstemcisi();
        Assert.Equal(HttpStatusCode.Redirect, (await SelcukIdentityFactory.GirisYapAsync(client, kullaniciAdi, sifre)).StatusCode);
        var yanit = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    /// <summary>Sayfadaki grupları sırasıyla (başlık, kart adları) okur.</summary>
    private static List<(string Baslik, List<string> Kartlar)> Gruplar(string html)
    {
        var sonuc = new List<(string, List<string>)>();
        foreach (Match bolum in Bolum().Matches(html))
        {
            var kartlar = Kart().Matches(bolum.Groups["icerik"].Value).Select(m => m.Groups["ad"].Value).ToList();
            sonuc.Add((bolum.Groups["baslik"].Value, kartlar));
        }

        return sonuc;
    }

    [Fact]
    public async Task Baskan_dokuz_otomasyon_kartini_dogru_gruplarda_goruyor_Mezuniyet_ve_ISKUR_yok()
    {
        foreach (var tanim in TumUygulamalar())
        {
            await KaydetAsync(tanim);
        }

        var html = await UygulamalarimAsync("baskan.test");
        var gruplar = Gruplar(html);

        Assert.Equal(
            [
                ("Kampüs uygulamaları", ["Demo OBİS", "Demo Yemekhane"]),
                ("Öğrenci işleri", ["İntibak Otomasyonu", "Staj Otomasyonu", "ÇAP-Yandal Otomasyonu",
                    "Öğrenci Danışmanlık Otomasyonu", "Lisans Projeleri Otomasyonu"]),
                ("Kurullar ve komisyonlar", ["Etik Kurul Otomasyonu", "Bölüm Kurul Karar Otomasyonu"]),
                ("Kalite ve analiz", ["MÜDEK Otomasyonu", "Sınav Başarısı Analizi Otomasyonu"]),
            ],
            gruplar.Select(g => (g.Baslik, g.Kartlar.ToArray())).ToArray());
        // Dokuz otomasyon kartı (iki demo hariç); Mezuniyet ve İŞKUR görünmez.
        Assert.Equal(9, gruplar.SelectMany(g => g.Kartlar).Count(ad => ad.EndsWith("Otomasyonu", StringComparison.Ordinal)));
        Assert.DoesNotContain("Mezuniyet Otomasyonu", html);
        Assert.DoesNotContain("İŞKUR Otomasyonu", html);
        Assert.DoesNotContain("localhost:5304", html);
        Assert.DoesNotContain("localhost:5307", html);
    }

    [Fact]
    public async Task Ogrenci_yalnizca_izinli_kartlari_goruyor_bos_grup_basligi_cikmiyor()
    {
        foreach (var tanim in TumUygulamalar())
        {
            await KaydetAsync(tanim);
        }

        var html = await UygulamalarimAsync("203011001");
        var gruplar = Gruplar(html);

        Assert.Equal(
            [
                ("Kampüs uygulamaları", ["Demo OBİS", "Demo Yemekhane"]),
                ("Öğrenci işleri", ["İntibak Otomasyonu", "Staj Otomasyonu", "Mezuniyet Otomasyonu", "ÇAP-Yandal Otomasyonu",
                    "Öğrenci Danışmanlık Otomasyonu", "Lisans Projeleri Otomasyonu", "İŞKUR Otomasyonu"]),
            ],
            gruplar.Select(g => (g.Baslik, g.Kartlar.ToArray())).ToArray());
        Assert.All(gruplar, g => Assert.NotEmpty(g.Kartlar));
        // Öğrencinin hiçbir kartı olmayan gruplar başlıkla da görünmez.
        Assert.DoesNotContain("Kurullar ve komisyonlar", html);
        Assert.DoesNotContain("Kalite ve analiz", html);
        Assert.DoesNotContain("Diğer", html);
        Assert.DoesNotContain("Etik Kurul", html);
        Assert.DoesNotContain("MÜDEK", html);
    }

    [Fact]
    public async Task Grubu_olmayan_veya_bilinmeyen_gruplu_istemci_sonda_Diger_altinda_gorunuyor()
    {
        await KaydetAsync(Istemciler.DemoObis(Secret()));
        await KaydetAsync(Istemciler.StajOtomasyonu(Secret()));
        // Tek Kapı'nın kaydında grup özelliği yoktur.
        var tekKapi = Istemciler.TekKapi(["203011001"]);
        Assert.False(tekKapi.Properties.ContainsKey(Istemciler.GrupOzelligi));
        await KaydetAsync(tekKapi);
        // Bilinmeyen grup adı da "Diğer"e düşer (sayfa yalnızca tanımlı grupları başlık yapar).
        var bilinmeyen = Istemciler.IntibakOtomasyonu(Secret());
        bilinmeyen.Properties[Istemciler.GrupOzelligi] = System.Text.Json.JsonSerializer.SerializeToElement("Uydurma grup");
        await KaydetAsync(bilinmeyen);

        var html = await UygulamalarimAsync("203011001");
        var gruplar = Gruplar(html);

        Assert.Equal(["Kampüs uygulamaları", "Öğrenci işleri", "Diğer"], gruplar.Select(g => g.Baslik));
        Assert.Equal(["Staj Otomasyonu"], gruplar[1].Kartlar);
        Assert.Equal(["İntibak Otomasyonu", "Tek Kapı — gerçek Selçuk sistemleri (yerel)"], gruplar[2].Kartlar);
        Assert.DoesNotContain("Uydurma grup", html);
        // Kart bugünkü gibi: açıklama, 2FA etiketi ve iss'li giriş bağlantısı.
        Assert.Contains("2FA gerekli", html);
        Assert.Contains("href=\"http://127.0.0.1:4545/auth/login?iss=https%3A%2F%2Flocalhost%3A5001%2F\"", html);
    }

    [Fact]
    public async Task Basliklar_h1_altinda_h2_bolumler_kart_kimlikleri_tek_ve_Ac_baglantilari_karti_tarif_ediyor()
    {
        foreach (var tanim in TumUygulamalar())
        {
            await KaydetAsync(tanim);
        }

        var html = await UygulamalarimAsync("baskan.test");

        // Başlık sırası: tek h1, ardından yalnızca grup h2'leri (gizli "Uygulamalarım" h2'si yok); h3 yok.
        Assert.Single(Regex.Matches(html, "<h1[ >]"));
        Assert.Equal(4, Regex.Matches(html, "<h2[ >]").Count);
        Assert.DoesNotContain("<h3", html);
        Assert.True(html.IndexOf("<h1", StringComparison.Ordinal) < html.IndexOf("<h2", StringComparison.Ordinal));
        foreach (Match bolum in Regex.Matches(html, "<section class=\"uygulama-grubu\" aria-labelledby=\"(?<kimlik>[^\"]+)\">"))
        {
            Assert.Contains($"<h2 id=\"{bolum.Groups["kimlik"].Value}\" class=\"grup-baslik\">", html);
        }

        // Kart kimlikleri bütün sayfada tek; her "Aç" bağlantısı var olan bir kartı tarif eder.
        var kimlikler = Regex.Matches(html, "<span id=\"(uygulama-\\d+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(11, kimlikler.Count); // iki demo + dokuz otomasyon
        Assert.Equal(kimlikler.Count, kimlikler.Distinct().Count());
        var tarifler = Regex.Matches(html, "class=\"dugme dugme-ikincil\" href=\"[^\"]+\" aria-describedby=\"(uygulama-\\d+)\">Aç</a>")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(kimlikler, tarifler);
    }

    [Fact]
    public void Her_kayitli_uygulamanin_grubu_tanimli_Tek_Kapi_gruplu_degil()
    {
        var beklenen = new Dictionary<string, string>
        {
            [Istemciler.DemoObisClientId] = Istemciler.GrupKampus,
            [Istemciler.DemoYemekhaneClientId] = Istemciler.GrupKampus,
            [Istemciler.IntibakOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.StajOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.MezuniyetOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.CapYandalOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.DanismanlikOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.LisansProjeleriOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.IskurOtomasyonuClientId] = Istemciler.GrupOgrenciIsleri,
            [Istemciler.EtikKurulOtomasyonuClientId] = Istemciler.GrupKurullar,
            [Istemciler.BolumKurulOtomasyonuClientId] = Istemciler.GrupKurullar,
            [Istemciler.MudekOtomasyonuClientId] = Istemciler.GrupKalite,
            [Istemciler.SinavAnaliziOtomasyonuClientId] = Istemciler.GrupKalite,
        };

        foreach (var tanim in TumUygulamalar())
        {
            Assert.Equal(beklenen[tanim.ClientId!], tanim.Properties[Istemciler.GrupOzelligi].GetString());
        }

        Assert.Equal(beklenen.Count, TumUygulamalar().Count);
        // Grup içi kart sırası her kayıtlı istemciyi tam bir kez kapsar.
        Assert.Equal(beklenen.Keys.Order(), Istemciler.KartSirasi.Order());
        Assert.Equal(["Kampüs uygulamaları", "Öğrenci işleri", "Kurullar ve komisyonlar", "Kalite ve analiz"], Istemciler.Gruplar);
        Assert.Equal(Istemciler.GrupKalite, IndexModel.GecerliGrup(Istemciler.GrupKalite));
        Assert.Null(IndexModel.GecerliGrup("Diğer"));
        Assert.Null(IndexModel.GecerliGrup("Başka"));
        Assert.Null(IndexModel.GecerliGrup(null));
    }

    [GeneratedRegex("<section class=\"uygulama-grubu\" aria-labelledby=\"[^\"]+\">\\s*<h2 id=\"[^\"]+\" class=\"grup-baslik\">(?<baslik>[^<]+)</h2>(?<icerik>.*?)</section>", RegexOptions.Singleline)]
    private static partial Regex Bolum();

    [GeneratedRegex("<span id=\"uygulama-\\d+\">(?<ad>[^<]+)</span>")]
    private static partial Regex Kart();
}
