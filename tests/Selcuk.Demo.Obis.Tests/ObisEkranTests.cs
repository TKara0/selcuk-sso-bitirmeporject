using System.Net;
using System.Text.RegularExpressions;
using Selcuk.Demo.Obis.Data;

namespace Selcuk.Demo.Obis.Tests;

/// <summary>
/// Demo OBİS'in yeni arayüzü: öğrenci ve akademisyen sekmeleri ve rol yetkisi, Anasayfa bilgilerinin token claim'lerinden
/// gelmesi, öğrencinin yalnızca kendi örnek verisini görmesi, her sayfada "Demo ekran" bandı / "Test" rozeti / "resmî
/// sistem değildir" ibaresi, dış kaynak olmaması, no-store ve güvenlik başlıkları.
/// </summary>
public sealed partial class ObisEkranTests
{
    private const string DemoBandi = "Demo ekran: gösterilen veriler örnektir, yapılan işlemler kaydedilmez.";

    private static HttpClient Ogrenci(ObisFactory factory, string kullanici = "203011001", string ad = "Test Öğrenci")
    {
        var client = factory.HttpsIstemcisi(Roller.Ogrenci);
        client.DefaultRequestHeaders.Add(ObisFactory.KullaniciBasligi, kullanici);
        client.DefaultRequestHeaders.Add(ObisFactory.AdBasligi, ad);
        client.DefaultRequestHeaders.Add(ObisFactory.FakulteBasligi, "Teknoloji Fakültesi");
        client.DefaultRequestHeaders.Add(ObisFactory.BolumBasligi, "Bilgisayar Mühendisliği");
        client.DefaultRequestHeaders.Add(ObisFactory.EpostaBasligi, kullanici + "@test.local");
        return client;
    }

    private static HttpClient Akademisyen(ObisFactory factory)
    {
        var client = factory.HttpsIstemcisi(Roller.Akademisyen);
        client.DefaultRequestHeaders.Add(ObisFactory.KullaniciBasligi, "hoca.test");
        client.DefaultRequestHeaders.Add(ObisFactory.AdBasligi, "Test Akademisyen");
        return client;
    }

    private static async Task<string> SayfaAsync(HttpClient client, string adres)
    {
        var yanit = await client.GetAsync(adres);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync());
    }

    /// <summary>Sekme çubuğundaki (nav.obis-sekmeler) bağlantıların metinleri ve etkin sekmeler (aria-current="page").</summary>
    private static (List<string> Metinler, List<string> Etkin) Sekmeler(string html)
    {
        var nav = SekmeNavi().Match(html);
        Assert.True(nav.Success, "Sekme çubuğu yok.");
        var baglantilar = SekmeBaglantisi().Matches(nav.Groups[1].Value);
        var metinler = baglantilar.Select(m => m.Groups[2].Value).ToList();
        var etkin = baglantilar.Where(m => m.Groups[1].Value.Contains("aria-current=\"page\"", StringComparison.Ordinal))
            .Select(m => m.Groups[2].Value).ToList();
        return (metinler, etkin);
    }

    [Fact]
    public async Task Ogrenci_her_sekmeyi_acabiliyor_sekme_sirasi_ve_etkin_sekme_dogru()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Ogrenci(factory);
        var beklenen = new[]
        {
            "Anasayfa", "Onay Takip", "Son Yıl Notları", "Not Durumu", "Transkript", "Harç Durumu", "Sınav Tarihleri",
            "Uzaktan Eğitim", "Uzaktan Sınav", "YKK Kararı",
        };
        Assert.Equal(beklenen, ObisMenusu.Ogrenci.Select(s => s.Metin));

        foreach (var sekme in ObisMenusu.Ogrenci)
        {
            var adres = sekme.Sayfa == "/Index" ? "/" : sekme.Sayfa;
            var html = await SayfaAsync(client, adres);

            var (metinler, etkin) = Sekmeler(html);
            Assert.Equal(beklenen, metinler);
            Assert.Equal([sekme.Metin], etkin);
            Assert.Single(H1().Matches(html));
            Assert.Contains($"<h1>{(sekme.Sayfa == "/Index" ? "Anasayfa" : sekme.Metin)}</h1>", html);
        }
    }

    [Fact]
    public async Task Ogrenci_akademisyen_sekmesini_gormuyor_akademisyen_ogrenci_sayfalarina_giremiyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var ogrenci = Sekmeler(await SayfaAsync(Ogrenci(factory), "/")).Metinler;
        Assert.DoesNotContain("Ders Yönetimi", ogrenci);

        var hoca = Akademisyen(factory);
        var (metinler, etkin) = Sekmeler(await SayfaAsync(hoca, "/"));
        Assert.Equal(["Anasayfa", "Ders Yönetimi"], metinler);
        Assert.Equal(["Anasayfa"], etkin);
        Assert.Equal(["Anasayfa", "Ders Yönetimi"], Sekmeler(await SayfaAsync(hoca, "/DersYonetimi")).Metinler);
        Assert.Equal(["Ders Yönetimi"], Sekmeler(await SayfaAsync(hoca, "/DersYonetimi")).Etkin);

        // Adresle gidilince mevcut davranış: Erişim reddedildi sayfasına yönlendirme.
        foreach (var sekme in ObisMenusu.Ogrenci.Skip(1))
        {
            var yanit = await hoca.GetAsync(sekme.Sayfa);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith("https://localhost/ErisimReddedildi", yanit.Headers.Location!.ToString());
        }

        var ogrenciIstemcisi = Ogrenci(factory);
        var reddedilen = await ogrenciIstemcisi.GetAsync("/DersYonetimi");
        Assert.Equal(HttpStatusCode.Redirect, reddedilen.StatusCode);
        Assert.StartsWith("https://localhost/ErisimReddedildi", reddedilen.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Token_Inceleme_sekmede_degil_hesap_menusunde_Development_ta()
    {
        using var factory = new ObisFactory(sahteKimlik: true, ortam: "Development");

        foreach (var client in new[] { Ogrenci(factory), Akademisyen(factory) })
        {
            var html = await SayfaAsync(client, "/");
            Assert.DoesNotContain("Token İnceleme", Sekmeler(html).Metinler);
            var menu = HesapListesi().Match(html).Groups[1].Value;
            Assert.Contains(">Profilim</a>", menu);
            Assert.Contains(">Token İnceleme</a>", menu);
            Assert.Contains("<button type=\"submit\">Çıkış yap</button>", menu);
        }

        // Development dışında menüde de yok.
        using var uretim = new ObisFactory(sahteKimlik: true);
        Assert.DoesNotContain("Token İnceleme", await SayfaAsync(Ogrenci(uretim), "/"));
    }

    [Fact]
    public async Task Anasayfa_bilgileri_token_claim_lerinden_geliyor_danisman_ve_sinif_ornek_veriden()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = factory.HttpsIstemcisi(Roller.Ogrenci);
        client.DefaultRequestHeaders.Add(ObisFactory.KullaniciBasligi, "203011002");
        client.DefaultRequestHeaders.Add(ObisFactory.AdBasligi, "Başka Bir Ad");
        client.DefaultRequestHeaders.Add(ObisFactory.FakulteBasligi, "Deneme Fakültesi");
        client.DefaultRequestHeaders.Add(ObisFactory.BolumBasligi, "Deneme Bölümü");
        client.DefaultRequestHeaders.Add(ObisFactory.EpostaBasligi, "deneme@test.local");

        var html = await SayfaAsync(client, "/");

        Assert.Matches(Satir("Öğrenci No", "203011002"), html);
        Assert.Matches(Satir("Adı Soyadı", "Başka Bir Ad"), html);
        Assert.Matches(Satir("Fakülte", "Deneme Fakültesi"), html);
        Assert.Matches(Satir("Bölüm", "Deneme Bölümü"), html);
        Assert.Matches(Satir("Eposta", "deneme@test.local"), html);
        // Danışman ve sınıf claim'den değil, öğrencinin örnek kaydından.
        Assert.Matches(Satir("Danışman", OrnekOgrenciVerisi.Ogrenci2.Danisman), html);
        Assert.Matches(Satir("Sınıf", "2"), html);
        Assert.Contains("Danışman İletişim", html);
        Assert.Contains("Danışmanıma Mail At", html);
        // Fotoğraf yerine baş harfler (gerçek fotoğraf yok).
        Assert.Contains("<span aria-hidden=\"true\">BA</span>", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public async Task Anasayfa_akademisyen_bilgilerini_ve_kendi_sekmelerini_gosteriyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Akademisyen(factory);
        client.DefaultRequestHeaders.Add(ObisFactory.FakulteBasligi, "Teknoloji Fakültesi");
        client.DefaultRequestHeaders.Add(ObisFactory.BolumBasligi, "Bilgisayar Mühendisliği");
        client.DefaultRequestHeaders.Add(ObisFactory.EpostaBasligi, "hoca.test@test.local");

        var html = await SayfaAsync(client, "/");

        Assert.Matches(Satir("Kullanıcı Adı", "hoca.test"), html);
        Assert.Matches(Satir("Adı Soyadı", "Test Akademisyen"), html);
        Assert.Matches(Satir("Bölüm", "Bilgisayar Mühendisliği"), html);
        Assert.Matches(Satir("Eposta", "hoca.test@test.local"), html);
        Assert.DoesNotContain("Danışmanıma Mail At", html);
        Assert.DoesNotContain("Öğrenci No", html);
        foreach (var ders in SahteVeri.Dersler)
        {
            Assert.Contains(ders.Ad, html);
        }
    }

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_ornek_verisini_goruyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var bir = await SayfaAsync(Ogrenci(factory, "203011001"), "/Transkript");
        var iki = await SayfaAsync(Ogrenci(factory, "203011002"), "/Transkript");
        var uc = await SayfaAsync(Ogrenci(factory, "203011003"), "/Transkript");

        Assert.Contains("<h2 id=\"donem-2024-2025-Güz\">2024-2025 Güz</h2>", bir);
        Assert.DoesNotContain("2023-2024 Güz", bir);
        Assert.DoesNotContain("2024-2025 Güz", iki);
        Assert.Contains("2025-2026 Güz", iki);
        Assert.Contains("2023-2024 Güz", uc);
        Assert.Contains("BIL302", uc);
        Assert.DoesNotContain("BIL302", bir);
        Assert.DoesNotContain("BIL302", iki);

        // Genel ortalama kendi kaydından hesaplanır.
        Assert.Matches(new Regex($@"<dt>Genel ortalama \(4'lük\)</dt>\s*<dd>{OrnekOgrenciVerisi.Ortalama(OrnekOgrenciVerisi.Ogrenci2.GenelOrtalama)}</dd>"), iki);
        Assert.NotEqual(OrnekOgrenciVerisi.Ogrenci1.GenelOrtalama, OrnekOgrenciVerisi.Ogrenci3.GenelOrtalama);

        // Başka ekranlar da kişiye özel: sınav ve karar kayıtları.
        var sinavlar1 = await SayfaAsync(Ogrenci(factory, "203011001"), "/SinavTarihleri");
        var sinavlar3 = await SayfaAsync(Ogrenci(factory, "203011003"), "/SinavTarihleri");
        Assert.Contains("Veritabanı Sistemleri", sinavlar1);
        Assert.DoesNotContain("Veritabanı Sistemleri", sinavlar3);
        Assert.Contains("Yazılım Mimarisi", sinavlar3);
        Assert.Contains("Kayıt yok.", await SayfaAsync(Ogrenci(factory, "203011002"), "/YkkKarari"));
        Assert.Contains("Örnek karar", await SayfaAsync(Ogrenci(factory, "203011001"), "/YkkKarari"));
        Assert.Contains("Bekliyor", await SayfaAsync(Ogrenci(factory, "203011002"), "/HarcDurumu"));
        Assert.DoesNotContain("Bekliyor", await SayfaAsync(Ogrenci(factory, "203011003"), "/HarcDurumu"));
    }

    [Fact]
    public async Task Tanimlanmamis_ogrenci_kaydi_bos_ekranlarla_cokmeden_aciliyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var client = Ogrenci(factory, "yok-ogrenci", "Bilinmeyen Öğrenci");

        foreach (var sekme in ObisMenusu.Ogrenci)
        {
            await SayfaAsync(client, sekme.Sayfa == "/Index" ? "/" : sekme.Sayfa);
        }

        Assert.Contains("Kayıt yok.", await SayfaAsync(client, "/Transkript"));
    }

    [Fact]
    public async Task Her_sayfada_Demo_ekran_bandi_Test_rozeti_ve_resmi_sistem_degildir_ibaresi_var()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var girissiz = factory.HttpsIstemcisi();
        var sayfalar = new List<(HttpClient Istemci, string Adres)>
        {
            (girissiz, "/"), (girissiz, "/GirisHatasi"), (girissiz, "/Hata"),
        };
        sayfalar.AddRange(ObisMenusu.Ogrenci.Select(s => (Ogrenci(factory), s.Sayfa == "/Index" ? "/" : s.Sayfa)));
        sayfalar.Add((Ogrenci(factory), "/Profilim"));
        sayfalar.Add((Ogrenci(factory), "/Cikis"));
        sayfalar.Add((Akademisyen(factory), "/"));
        sayfalar.Add((Akademisyen(factory), "/DersYonetimi"));
        sayfalar.Add((Akademisyen(factory), "/ErisimReddedildi"));

        foreach (var (istemci, adres) in sayfalar)
        {
            var html = await SayfaAsync(istemci, adres);

            Assert.Contains(DemoBandi, html);
            Assert.Contains("<span class=\"etiket etiket-notr\">Test</span>", html);
            Assert.Contains("resmî sistem değildir", html);
            Assert.Contains("Demo OBİS · Öğrenci Bilgi Sistemi", html);
            Assert.Contains("class=\"ust-cubuk uygulama-cubugu\"", html);
        }
    }

    [Fact]
    public async Task Sayfalarda_dis_kaynak_satir_ici_stil_ve_betik_yok_baglantilar_uygulama_ici()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var istemci = Ogrenci(factory);
        var adresler = ObisMenusu.Ogrenci.Select(s => s.Sayfa == "/Index" ? "/" : s.Sayfa).Append("/Profilim").ToList();

        foreach (var adres in adresler)
        {
            var html = await SayfaAsync(istemci, adres);

            foreach (Match m in KaynakOzniteligi().Matches(html))
            {
                Assert.StartsWith("/", m.Groups[1].Value);
                Assert.False(m.Groups[1].Value.StartsWith("//", StringComparison.Ordinal), m.Groups[1].Value);
            }

            Assert.DoesNotContain("<img", html);
            Assert.DoesNotContain("<iframe", html);
            Assert.DoesNotContain("<style", html);
            Assert.DoesNotContain(" style=", html);
            Assert.DoesNotContain(" onclick=", html);
            Assert.DoesNotContain("selcuk.edu.tr", html, StringComparison.OrdinalIgnoreCase);
            // Mutlak adresli tek bağlantı: kimlik sunucusundaki "Uygulamalarım" (yapılandırılmış Authority).
            var mutlak = MutlakBaglanti().Matches(html).Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.All(mutlak, b => Assert.Equal("https://localhost:5001/", b));
        }

        // Stil dosyası da dış kaynak yüklemez.
        var css = await (await factory.HttpsIstemcisi().GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        Assert.DoesNotMatch(new Regex(@"https?://|@import|url\s*\(\s*['""]?//", RegexOptions.IgnoreCase), css);
        Assert.DoesNotContain("@font-face", css);
    }

    [Fact]
    public async Task Ekranlar_no_store_ve_guvenlik_basliklariyla_donuyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);
        var istemci = Ogrenci(factory);

        foreach (var sekme in ObisMenusu.Ogrenci)
        {
            var yanit = await istemci.GetAsync(sekme.Sayfa == "/Index" ? "/" : sekme.Sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.True(yanit.Headers.CacheControl?.NoStore, sekme.Sayfa);
            Assert.Contains("nosniff", yanit.Headers.GetValues("X-Content-Type-Options"));
            Assert.Contains("no-referrer", yanit.Headers.GetValues("Referrer-Policy"));
            var csp = Assert.Single(yanit.Headers.GetValues("Content-Security-Policy"));
            Assert.Contains("default-src 'self'", csp);
            Assert.Contains("frame-ancestors 'none'", csp);
            Assert.DoesNotContain("unsafe-inline", csp);
        }

        var hoca = await Akademisyen(factory).GetAsync("/DersYonetimi");
        Assert.True(hoca.Headers.CacheControl?.NoStore);
    }

    [Theory]
    [InlineData("Test Öğrenci İki", "Tİ")]
    [InlineData("Ayşe Kara Yılmaz", "AY")]
    [InlineData("ışıl kara", "IK")]
    [InlineData("Tek", "T")]
    [InlineData("  ", "?")]
    [InlineData(null, "?")]
    public void Bas_harfler_ilk_ve_son_sozcuk_Turkce_buyuk_harfle(string? ad, string beklenen) =>
        Assert.Equal(beklenen, ObisMenusu.BasHarfler(ad));

    [Fact]
    public void Ornek_veri_tutarli_donem_30_AKTS_ders_kodlari_tekil_ortalamalar_hesapli()
    {
        foreach (var ogrenci in new[] { OrnekOgrenciVerisi.Ogrenci1, OrnekOgrenciVerisi.Ogrenci2, OrnekOgrenciVerisi.Ogrenci3 })
        {
            Assert.All(ogrenci.Donemler, d => Assert.Equal(30, d.ToplamAkts));
            Assert.Equal(ogrenci.TumDersler.Count, ogrenci.TumDersler.Select(d => d.Kod).Distinct().Count());
            Assert.Equal(30, ogrenci.SecilenDersler.Sum(d => d.Akts));
            Assert.All(ogrenci.Donemler.SelectMany(d => d.Dersler), d => Assert.Contains(d.Harf, NotOzeti.Katsayilar.Keys));
            Assert.StartsWith("2030110", ogrenci.KullaniciAdi);
            Assert.StartsWith("Örnek Akademisyen", ogrenci.Danisman);
            Assert.NotEmpty(ogrenci.SonYil);
        }

        // (6×4 + 6×4 + 6×3,5 + 4×3,5 + 4×4 + 4×4) / 30 = 115 / 30
        Assert.Equal(115m / 30, OrnekOgrenciVerisi.Ogrenci2.Donemler[0].Ortalama);
        Assert.Equal("3,83", OrnekOgrenciVerisi.Ortalama(OrnekOgrenciVerisi.Ogrenci2.Donemler[0].Ortalama));
        Assert.Equal("—", OrnekOgrenciVerisi.Ortalama(null));
        Assert.Equal(60, OrnekOgrenciVerisi.Ogrenci2.ToplamAkts);
        Assert.Null(OrnekOgrenciVerisi.Varsayilan.GenelOrtalama);
    }

    [Fact]
    public void Onay_adimlari_tamamlanan_sayiya_gore_durum_aliyor()
    {
        var adimlar = OrnekOgrenciVerisi.Ogrenci3.Adimlar();

        Assert.Equal(
            [AdimDurumu.Tamamlandi, AdimDurumu.Tamamlandi, AdimDurumu.Bekliyor, AdimDurumu.Baslamadi],
            adimlar.Select(a => a.Durum));
        Assert.All(OrnekOgrenciVerisi.Ogrenci2.Adimlar(), a => Assert.Equal(AdimDurumu.Tamamlandi, a.Durum));
    }

    [Fact]
    public async Task Onay_Takip_adimlari_aria_current_step_ve_durum_metniyle_gosteriyor()
    {
        using var factory = new ObisFactory(sahteKimlik: true);

        var html = await SayfaAsync(Ogrenci(factory, "203011003"), "/OnayTakip");

        Assert.Contains("<ol class=\"obis-adimlar\">", html);
        Assert.Single(Regex.Matches(html, "aria-current=\"step\""));
        Assert.Contains("Bekliyor", html);
        Assert.Contains("Tamamlandı", html);
        Assert.Contains("Henüz başlamadı", html);
    }

    private static Regex Satir(string etiket, string deger) =>
        new($@"<th scope=""row"">{Regex.Escape(etiket)}</th><td[^>]*>(?:<svg.*?</svg>)?{Regex.Escape(deger)}</td>", RegexOptions.Singleline);

    [GeneratedRegex("<nav class=\"obis-sekmeler\"[^>]*>(.*?)</nav>", RegexOptions.Singleline)]
    private static partial Regex SekmeNavi();

    [GeneratedRegex("<a ([^>]*)>(?:<svg.*?</svg>)?<span>(.*?)</span></a>", RegexOptions.Singleline)]
    private static partial Regex SekmeBaglantisi();

    [GeneratedRegex("<ul class=\"hesap-listesi\"[^>]*>(.*?)</ul>", RegexOptions.Singleline)]
    private static partial Regex HesapListesi();

    [GeneratedRegex("<h1\\b")]
    private static partial Regex H1();

    [GeneratedRegex("<(?:link|script|source|embed|object|video|audio)\\b[^>]*\\b(?:href|src|data)=\"([^\"]+)\"")]
    private static partial Regex KaynakOzniteligi();

    [GeneratedRegex("<a\\b[^>]*\\bhref=\"((?:https?:)?//[^\"]*)\"")]
    private static partial Regex MutlakBaglanti();
}
