using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.Staj.Sozlesme;
using Selcuk.Otomasyon.Staj.Veri;

namespace Selcuk.Otomasyon.Staj.Tests;

/// <summary>
/// Staj Otomasyonu (Mock veri): rol tabanlı menü, sunucu tarafı yetki, kapsam (öğrenci yalnızca kendi kaydı, bölüm
/// rolleri yalnızca kendi bölümü), temsili işlemler, ortak düzen ve vurgu renginin kontrastı.
/// </summary>
public sealed partial class StajEkranTests : IDisposable
{
    private static readonly string[] OgrenciSayfalari = ["/Ogrenci/Stajlarim", "/Ogrenci/Basvuru", "/Ogrenci/Defter", "/Ogrenci/Sonuc"];
    private static readonly string[] KomisyonSayfalari = ["/Komisyon/BasvuruInceleme", "/Komisyon/DefterDegerlendirme", "/Komisyon/YillaraGoreTakip"];

    private readonly StajFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Ogrenci(string no = "203011001") => _factory.Istemci(no, roller: Roller.Ogrenci);

    private HttpClient KomisyonTest() => _factory.Istemci("komisyon.test", roller:
        [Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.StajKomisyonu, Roller.MezuniyetKomisyonu, Roller.BolumKuruluUyesi]);

    private static List<string> MenuBasliklari(string html) =>
        [.. MenuGrubu().Matches(html).Select(m => m.Groups[1].Value)];

    private static List<string> MenuBaglantilari(string html)
    {
        var bas = html.IndexOf("id=\"otomasyon-menu-listesi\"", StringComparison.Ordinal);
        Assert.True(bas >= 0, "Menü yok.");
        var menu = html[bas..html.IndexOf("</nav>", bas, StringComparison.Ordinal)];
        return [.. Baglanti().Matches(menu).Select(m => m.Groups[1].Value)];
    }

    // ---------- a) Menü ----------

    [Fact]
    public async Task Ogrenci_menude_yalnizca_ogrenci_ogelerini_goruyor()
    {
        var html = await StajFactory.MetinAsync(await Ogrenci().GetAsync("/"));

        Assert.Equal(["Öğrenci"], MenuBasliklari(html));
        Assert.Equal(["/", .. OgrenciSayfalari], MenuBaglantilari(html));
    }

    [Fact]
    public async Task Komisyon_uyesi_staj_komisyonu_ogelerini_goruyor()
    {
        var html = await StajFactory.MetinAsync(await KomisyonTest().GetAsync("/"));

        // Akademisyen, intibak/mezuniyet komisyonu ve bölüm kurulu rollerinin Staj'da ekranı yok: başlıkları da yok.
        Assert.Equal(["Staj komisyonu üyesi"], MenuBasliklari(html));
        Assert.Equal(["/", .. KomisyonSayfalari], MenuBaglantilari(html));
    }

    [Fact]
    public async Task Cok_rollu_kullanici_birlesimi_rol_basliklariyla_goruyor()
    {
        var client = _factory.Istemci("cok.rollu", roller: [Roller.Akademisyen, Roller.BolumBaskani, Roller.StajKomisyonu]);

        var html = await StajFactory.MetinAsync(await client.GetAsync("/"));

        Assert.Equal(["Bölüm başkanı", "Staj komisyonu üyesi"], MenuBasliklari(html));
        Assert.Equal(["/", "/Baskan/Ozet", .. KomisyonSayfalari], MenuBaglantilari(html));
        // Ana sayfa kısayolları da aynı gruplarla.
        Assert.Contains("<h2 id=\"kisayol-0\">Bölüm başkanı</h2>", html);
        Assert.Contains("<h2 id=\"kisayol-1\">Staj komisyonu üyesi</h2>", html);
    }

    [Fact]
    public async Task Girissiz_ana_sayfa_menusuz_ve_tek_sifre_girisi_var()
    {
        var html = await StajFactory.MetinAsync(await _factory.Istemci().GetAsync("/"));

        Assert.DoesNotContain("otomasyon-menu-listesi", html);
        Assert.Contains("Tek Şifre ile giriş yap", html);
        Assert.Contains("href=\"/Giris\"", html);
    }

    // ---------- b) Yetki ----------

    [Fact]
    public async Task Ogrenci_komisyon_sayfasina_adresle_giremiyor_Turkce_sayfa_goruyor()
    {
        var client = Ogrenci();

        var yanit = await client.GetAsync("/Komisyon/BasvuruInceleme");

        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.StartsWith("/ErisimReddedildi", yanit.Headers.Location!.PathAndQuery);
        var red = await client.GetAsync(yanit.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, red.StatusCode);
        Assert.Contains("Bu ekrana erişim yetkin yok", await StajFactory.MetinAsync(red));
    }

    [Fact]
    public async Task Her_menu_sayfasi_rolu_olmayana_kapali_girissize_giris_istiyor()
    {
        var izinsiz = _factory.Istemci("iskur.test", bolum: null, roller: [Roller.Personel, Roller.IskurSorumlusu]);
        foreach (var oge in StajOtomasyonu.Menu)
        {
            var yanit = await izinsiz.GetAsync(oge.Sayfa);
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.StartsWith("/ErisimReddedildi", yanit.Headers.Location!.PathAndQuery);

            // POST işleyicileri de aynı yetkiyle korunur.
            Assert.Equal(HttpStatusCode.Redirect, (await izinsiz.PostAsync(oge.Sayfa, new FormUrlEncodedContent([]))).StatusCode);

            // Girişsiz: kimlik doğrulama istenir (sahte şemanın challenge'ı 401).
            Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.Istemci().GetAsync(oge.Sayfa)).StatusCode);
        }
    }

    [Fact]
    public async Task Her_rol_kendi_ekranlarini_aciyor()
    {
        var istemciler = new Dictionary<string, HttpClient>
        {
            [Roller.Ogrenci] = Ogrenci(),
            [Roller.StajKomisyonu] = KomisyonTest(),
            [Roller.BolumSekreteri] = _factory.Istemci("sekreter.test", roller: [Roller.Personel, Roller.BolumSekreteri]),
            [Roller.BolumBaskani] = _factory.Istemci("baskan.test", roller: [Roller.Akademisyen, Roller.BolumBaskani]),
        };
        foreach (var oge in StajOtomasyonu.Menu)
        {
            var yanit = await istemciler[oge.Roller.Single()].GetAsync(oge.Sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            var html = await StajFactory.MetinAsync(yanit);
            Assert.Single(H1().Matches(html));
            Assert.Contains("aria-current=\"page\">" + oge.Baslik + "</a>", html);
        }
    }

    // ---------- c) Kapsam ----------

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_kayitlarini_goruyor()
    {
        var html = await StajFactory.MetinAsync(await Ogrenci("203011001").GetAsync("/Ogrenci/Stajlarim"));

        Assert.Equal(["S25-01", "S26-01", "S26-02"], StajFactory.Kayitlar(html));
        Assert.Contains("Staj I", html);
        Assert.Contains("Staj II", html);
        Assert.Contains("İME", html);
        // 203011002'nin kaydı ve firması/öğrenci adı yok.
        Assert.DoesNotContain("S26-03", html);
        Assert.DoesNotContain("Test Öğrenci İki", html);

        var ikinci = await StajFactory.MetinAsync(await Ogrenci("203011002").GetAsync("/Ogrenci/Stajlarim"));
        Assert.Equal(["S25-02", "S26-03", "S26-04"], StajFactory.Kayitlar(ikinci));

        var sonuc = await StajFactory.MetinAsync(await Ogrenci("203011001").GetAsync("/Ogrenci/Sonuc"));
        Assert.Equal(["S25-01"], StajFactory.Kayitlar(sonuc));
        Assert.Contains("Kabul edilen gün: <strong>20</strong> / 20 iş günü", sonuc);
    }

    [Fact]
    public async Task Bolum_rolleri_baska_bolumun_kaydini_gormuyor()
    {
        var baskan = _factory.Istemci("baskan.test", roller: [Roller.Akademisyen, Roller.BolumBaskani]);
        var ozet = await StajFactory.MetinAsync(await baskan.GetAsync("/Baskan/Ozet"));
        var yil = DateTime.Now.Year;
        var bilgisayar = OrnekStajVerisi.Kayitlar.Where(k => k.Bolum == OrnekStajVerisi.Bilgisayar).ToList();
        Assert.Contains($"data-ozet=\"bekleyen\">{bilgisayar.Count(k => k.Durum == StajDurumu.BasvuruBekliyor)}<", ozet);
        Assert.Contains($"data-ozet=\"aktif\">{bilgisayar.Count(k => k.Durum is StajDurumu.Onaylandi or StajDurumu.DevamEdiyor)}<", ozet);
        Assert.Contains($"data-ozet=\"tamamlanan\">{bilgisayar.Count(k => k.Durum == StajDurumu.Degerlendirildi && k.Bitis.Year == yil)}<", ozet);
        // Bekleyenlerden biri (S26-10) Elektrik-Elektronik'te: Bilgisayar sayısı toplamdan az.
        Assert.True(bilgisayar.Count(k => k.Durum == StajDurumu.BasvuruBekliyor)
            < OrnekStajVerisi.Kayitlar.Count(k => k.Durum == StajDurumu.BasvuruBekliyor));

        var basvurular = StajFactory.Kayitlar(await StajFactory.MetinAsync(await KomisyonTest().GetAsync("/Komisyon/BasvuruInceleme")));
        Assert.Equal(new[] { "S26-02", "S26-08", "S26-09" }, basvurular.Order());
        Assert.DoesNotContain("S26-10", basvurular);

        var defterler = StajFactory.Kayitlar(await StajFactory.MetinAsync(await KomisyonTest().GetAsync("/Komisyon/DefterDegerlendirme")));
        Assert.DoesNotContain("S26-11", defterler);
        Assert.Contains("S26-01", defterler);

        var sekreter = _factory.Istemci("sekreter.test", roller: [Roller.Personel, Roller.BolumSekreteri]);
        var evrak = await StajFactory.MetinAsync(await sekreter.GetAsync("/Sekreter/EvrakTakibi"));
        Assert.DoesNotContain("ORN-0201", evrak);
        Assert.DoesNotContain("ORN-0202", evrak);
        Assert.Contains("ORN-0101", evrak);

        // Elektrik-Elektronik'teki bir bölüm başkanı yalnızca kendi bölümünü görür.
        var eemBaskan = _factory.Istemci("eem.baskan", bolum: OrnekStajVerisi.ElektrikElektronik,
            roller: [Roller.Akademisyen, Roller.BolumBaskani, Roller.StajKomisyonu]);
        var eem = StajFactory.Kayitlar(await StajFactory.MetinAsync(await eemBaskan.GetAsync("/Komisyon/BasvuruInceleme")));
        Assert.Equal(["S26-10"], eem);
    }

    [Fact]
    public async Task Bolumu_bos_kullanici_bolum_kapsaminda_hicbir_kayit_gormuyor()
    {
        var client = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Akademisyen, Roller.StajKomisyonu]);

        var html = await StajFactory.MetinAsync(await client.GetAsync("/Komisyon/BasvuruInceleme"));

        Assert.Empty(StajFactory.Kayitlar(html));
        Assert.Contains("Bekleyen başvuru yok.", html);
    }

    // ---------- h) Temsili işlem ----------

    [Fact]
    public async Task Temsili_islem_antiforgery_olmadan_reddediliyor()
    {
        var client = KomisyonTest();
        await client.GetAsync("/Komisyon/BasvuruInceleme");

        var yanit = await client.PostAsync("/Komisyon/BasvuruInceleme?handler=Onayla",
            new FormUrlEncodedContent([KeyValuePair.Create("id", "S26-08")]));

        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.DoesNotContain(TemsiliIslem.Mesaj, await StajFactory.MetinAsync(yanit));
    }

    [Theory]
    [InlineData("/Komisyon/BasvuruInceleme", "Onayla")]
    [InlineData("/Komisyon/BasvuruInceleme", "Reddet")]
    [InlineData("/Komisyon/DefterDegerlendirme", null)]
    public async Task Temsili_islem_mesaj_gosteriyor_veri_degismiyor(string sayfa, string? handler)
    {
        var client = KomisyonTest();
        var once = await StajFactory.MetinAsync(await client.GetAsync(sayfa));
        var kayitlar = StajFactory.Kayitlar(once);
        Assert.NotEmpty(kayitlar);
        Assert.DoesNotContain(TemsiliIslem.Mesaj, once);

        var yanit = await client.PostAsync(sayfa + (handler is null ? "" : "?handler=" + handler), new FormUrlEncodedContent(
        [
            KeyValuePair.Create("id", kayitlar[0]),
            KeyValuePair.Create("kabulGun", "5"),
            KeyValuePair.Create("__RequestVerificationToken", StajFactory.AntiforgeryTokenAl(once)),
        ]));
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Equal(sayfa, yanit.Headers.Location!.OriginalString);

        var sonra = await StajFactory.MetinAsync(await client.GetAsync(sayfa));
        Assert.Contains(TemsiliIslem.Mesaj, sonra);
        Assert.Equal(kayitlar, StajFactory.Kayitlar(sonra));
        // Mesaj bir kez gösterilir (TempData).
        Assert.DoesNotContain(TemsiliIslem.Mesaj, await StajFactory.MetinAsync(await client.GetAsync(sayfa)));
    }

    [Fact]
    public async Task Ogrenci_formlari_temsili_ve_betiksiz()
    {
        var client = Ogrenci();
        foreach (var sayfa in new[] { "/Ogrenci/Basvuru", "/Ogrenci/Defter" })
        {
            var html = await StajFactory.MetinAsync(await client.GetAsync(sayfa));
            Assert.Contains("<form method=\"post\"", html);
            Assert.DoesNotContain("<script>", html);
            var yanit = await client.PostAsync(sayfa, new FormUrlEncodedContent(
                [KeyValuePair.Create("__RequestVerificationToken", StajFactory.AntiforgeryTokenAl(html))]));
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
            Assert.Contains(TemsiliIslem.Mesaj, await StajFactory.MetinAsync(await client.GetAsync(sayfa)));
        }

        var defter = await StajFactory.MetinAsync(await client.GetAsync("/Ogrenci/Defter"));
        Assert.Contains("Deneme Elektronik Ltd.", defter);
        Assert.Equal(5, Regex.Matches(defter, "<th scope=\"row\" class=\"yigin-baslik\">").Count);
    }

    // ---------- Ortak düzen ----------

    [Fact]
    public async Task Ayni_tarayicidaki_baska_uygulamanin_TempData_cerezi_sayfayi_bozmuyor()
    {
        // Çerezler porta göre ayrılmaz: localhost'taki başka bir uygulamanın (ör. Demo Yemekhane) kendi anahtarıyla
        // şifrelediği TempData çerezi buraya da gelir.
        var client = Ogrenci();
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Mvc.CookieTempDataProvider=CfDJ8baskauygulamaninsifreliverisi");

        var yanit = await client.GetAsync("/Ogrenci/Stajlarim");

        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        Assert.Contains("<h1>Stajlarım</h1>", await StajFactory.MetinAsync(yanit));
    }

    [Fact]
    public async Task Duzen_temsili_bant_veri_rozeti_vurgu_ve_kaynaklar()
    {
        var html = await StajFactory.MetinAsync(await Ogrenci().GetAsync("/Ogrenci/Stajlarim"));

        Assert.Contains("Temsili ekran: gösterilen veriler örnektir, yapılan işlemler kaydedilmez.", html);
        Assert.Contains("Veri: örnek veri", html);
        Assert.Contains("<span class=\"uygulama-simgesi\" aria-hidden=\"true\">ST</span>", html);
        Assert.Contains("<span>Staj Otomasyonu</span>", html);
        Assert.Contains("class=\"ust-cubuk uygulama-cubugu\"", html);
        Assert.Contains("/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css", html);
        Assert.Contains("/css/site.css", html);
        // Geniş ekran: her zaman açık kenar menüsü (hidden yok); dar ekran: "Menü" düğmeli, başlangıçta kapalı liste.
        Assert.Contains("<nav class=\"otomasyon-menusu menu-genis\" aria-label=\"Staj Otomasyonu menüsü\">", html);
        Assert.Contains("<div class=\"menu-listesi\" id=\"otomasyon-menu-listesi\">", html);
        Assert.Contains("aria-controls=\"otomasyon-menu-listesi-dar\" data-menu-dugme>Menü</button>", html);
        Assert.Contains("<div class=\"menu-listesi\" id=\"otomasyon-menu-listesi-dar\" hidden>", html);
        Assert.Contains("otomasyon-betiksiz.css", html);
        // Firuze kapı işareti yalnızca hesap menüsünde (kimlik öğesi).
        Assert.Single(Regex.Matches(html, "class=\"kapi-kucuk\""));

        foreach (var dosya in new[] { "/_content/Selcuk.Otomasyon.Ortak/css/otomasyon.css",
                     "/_content/Selcuk.Otomasyon.Ortak/css/otomasyon-betiksiz.css", "/css/site.css" })
        {
            Assert.Equal(HttpStatusCode.OK, (await _factory.Istemci().GetAsync(dosya)).StatusCode);
        }
    }

    [Fact]
    public async Task Profilim_ve_bilinmeyen_sayfa()
    {
        var client = KomisyonTest();
        var profil = await StajFactory.MetinAsync(await client.GetAsync("/Profilim"));
        Assert.Contains("Staj komisyonu üyesi", profil);

        var yok = await client.GetAsync("/Olmayan/Sayfa");
        Assert.Equal(HttpStatusCode.NotFound, yok.StatusCode);
        Assert.Contains("Sayfa bulunamadı", await StajFactory.MetinAsync(yok));
    }

    // ---------- i) Güvenlik başlıkları ----------

    [Fact]
    public async Task Guvenlik_basliklari_ve_giris_isteyen_sayfalarda_no_store()
    {
        foreach (var sayfa in OgrenciSayfalari)
        {
            var yanit = await Ogrenci().GetAsync(sayfa);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            Assert.True(yanit.Headers.CacheControl?.NoStore, sayfa);
            Assert.Equal("nosniff", yanit.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("DENY", yanit.Headers.GetValues("X-Frame-Options").Single());
            Assert.Equal("no-referrer", yanit.Headers.GetValues("Referrer-Policy").Single());
            Assert.Contains("default-src 'self'", yanit.Headers.GetValues("Content-Security-Policy").Single());
        }

        var acik = await _factory.Istemci().GetAsync("/css/site.css");
        Assert.False(acik.Headers.CacheControl?.NoStore ?? false);
        Assert.Contains("frame-ancestors 'none'", (await _factory.Istemci().GetAsync("/")).Headers.GetValues("Content-Security-Policy").Single());
    }

    // ---------- j) Kontrast ----------

    [Fact]
    public async Task Zeytin_vurgu_renginin_kontrasti_AA()
    {
        var css = await (await _factory.Istemci().GetAsync("/css/site.css")).Content.ReadAsStringAsync();
        var renkler = Vurgu().Matches(css).Select(m => (m.Groups[1].Value, m.Groups[2].Value)).ToList();
        Assert.Equal(4, renkler.Count);
        var (acikVurgu, acikZemin) = (renkler[0].Item2, renkler[1].Item2);
        var (koyuVurgu, koyuZemin) = (renkler[2].Item2, renkler[3].Item2);

        Assert.Equal("#4F6B34", acikVurgu);
        Assert.True(Kontrast(acikVurgu, "#FFFFFF") >= 4.5, $"Beyaz zeminde {Kontrast(acikVurgu, "#FFFFFF"):0.00}");
        Assert.Equal(6.03, Math.Round(Kontrast(acikVurgu, "#FFFFFF"), 2));
        Assert.True(Kontrast(acikVurgu, "#EDF0EF") >= 4.5);
        Assert.True(Kontrast(acikVurgu, acikZemin) >= 4.5);
        Assert.True(Kontrast(koyuVurgu, "#15273A") >= 4.5);
        Assert.True(Kontrast(koyuVurgu, koyuZemin) >= 4.5);
    }

    /// <summary>WCAG 2.1 kontrast oranı.</summary>
    private static double Kontrast(string a, string b)
    {
        static double Parlaklik(string renk)
        {
            double Kanal(int i)
            {
                var c = int.Parse(renk.AsSpan(1 + 2 * i, 2), NumberStyles.HexNumber) / 255.0;
                return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Kanal(0) + 0.7152 * Kanal(1) + 0.0722 * Kanal(2);
        }

        var (la, lb) = (Parlaklik(a), Parlaklik(b));
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [GeneratedRegex("<p class=\"menu-grubu\" id=\"menu-grubu-\\d+\">([^<]+)</p>")]
    private static partial Regex MenuGrubu();

    [GeneratedRegex("<a href=\"([^\"]+)\"")]
    private static partial Regex Baglanti();

    [GeneratedRegex("<h1\\b")]
    private static partial Regex H1();

    [GeneratedRegex("(--uygulama-vurgu(?:-acik)?):\\s*(#[0-9A-Fa-f]{6})")]
    private static partial Regex Vurgu();
}
