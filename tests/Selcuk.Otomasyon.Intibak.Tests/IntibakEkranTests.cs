using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Selcuk.Otomasyon.Intibak.Veri;
using Selcuk.Otomasyon.Ortak;
using Selcuk.Otomasyon.TestOrtak;

namespace Selcuk.Otomasyon.Intibak.Tests;

/// <summary>
/// İntibak'a özgü testler: kapsam (öğrenci yalnızca kendi başvurusu, bölüm ve fakülte süzmesi), ortak parçaların
/// çıktısı (durum rozeti, aşamaların aria-current'ı, tablonun boş durumu), ayardan gelen şartlar ve öğrenci formu.
/// </summary>
public sealed class IntibakEkranTests : IDisposable
{
    private readonly OtomasyonFactory<Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    private HttpClient Kullanici(string rol, OtomasyonFactory<Program>? factory = null)
    {
        var k = IntibakKullanicilari.Hepsi[rol];
        return (factory ?? _factory).Istemci(k.KullaniciAdi, k.Bolum, k.Fakulte, k.Roller);
    }

    private static async Task<string> Html(HttpClient client, string sayfa)
    {
        var yanit = await client.GetAsync(sayfa);
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return await OtomasyonFactory.MetinAsync(yanit);
    }

    private static IReadOnlyList<string> Kayitlar(string html) => OtomasyonFactory.Kayitlar(html, "intibak");

    // ---------- Kapsam ----------

    [Fact]
    public async Task Ogrenci_yalnizca_kendi_basvurusunu_ve_sonucunu_goruyor()
    {
        var ogrenci1 = _factory.Istemci("203011001", roller: Roller.Ogrenci);
        Assert.Equal(["I25-01"], Kayitlar(await Html(ogrenci1, "/Ogrenci/Basvurum")));
        var sonuc = await Html(ogrenci1, "/Ogrenci/Sonuc");
        Assert.Equal(["I25-01"], Kayitlar(sonuc));
        // Programlamaya Giriş (BM101, 6) ve Matematik I (MAT101, 5) muaf; Fizik I muaf değil, Teknik Resim karşılıksız.
        Assert.Contains("data-muaf-akts=\"I25-01\">11 AKTS", sonuc);
        Assert.Contains("Karşılığı yok", sonuc);
        Assert.Contains("data-durum=\"olumsuz\">Muaf değil</span>", sonuc);
        Assert.DoesNotContain("Test Öğrenci İki", sonuc);

        var ogrenci2 = Kullanici(Roller.Ogrenci);
        Assert.Equal(["I26-01"], Kayitlar(await Html(ogrenci2, "/Ogrenci/Basvurum")));
        var bos = await Html(ogrenci2, "/Ogrenci/Sonuc");
        Assert.Empty(Kayitlar(bos));
        Assert.Contains("Sonuçlanmış bir intibak başvurun henüz yok.", bos);
    }

    [Fact]
    public async Task Bolum_rolleri_yalnizca_kendi_bolumunu_goruyor()
    {
        var komisyon = Kullanici(Roller.IntibakKomisyonu);
        Assert.Equal(["I26-04", "I26-02", "I26-03"], Kayitlar(await Html(komisyon, "/Komisyon/BekleyenBasvurular")));
        var kararlar = Kayitlar(await Html(komisyon, "/Komisyon/Kararlar"));
        Assert.DoesNotContain("I26-07", kararlar);
        Assert.DoesNotContain("I24-04", kararlar);
        Assert.Contains("I24-01", kararlar);

        Assert.Equal(["I26-01", "I26-10"], Kayitlar(await Html(Kullanici(Roller.BolumBaskani), "/Baskan/OnayListesi")));
        var rapor = Kayitlar(await Html(Kullanici(Roller.BolumSekreteri), "/Sekreter/Raporlar"));
        Assert.All(rapor, id => Assert.Equal(OrnekIntibakVerisi.Bilgisayar, OrnekIntibakVerisi.Kayitlar.Single(k => k.Id == id).Bolum));

        // Elektrik-Elektronik'teki komisyon üyesi ve bölüm başkanı yalnızca kendi bölümünü görür.
        var eem = _factory.Istemci("eem.komisyon", bolum: OtomasyonFactory.ElektrikElektronik,
            roller: [Roller.Akademisyen, Roller.IntibakKomisyonu, Roller.BolumBaskani]);
        Assert.Equal(["I26-08"], Kayitlar(await Html(eem, "/Komisyon/BekleyenBasvurular")));
        Assert.Equal(["I26-12"], Kayitlar(await Html(eem, "/Baskan/OnayListesi")));
    }

    [Fact]
    public async Task Baska_bolumun_basvurusu_adresle_eslestirmede_acilmiyor()
    {
        var html = await Html(Kullanici(Roller.IntibakKomisyonu), "/Komisyon/DersEslestirme?id=I26-08");

        Assert.Contains("Bu başvuru bölümünün eşleştirme bekleyen başvuruları arasında yok.", html);
        Assert.Empty(Kayitlar(html));
        Assert.DoesNotContain("Örnek Öğrenci EEM Dört", html);
    }

    [Fact]
    public async Task Fakulte_sekreteri_yalnizca_kendi_fakultesini_bolumlere_gore_goruyor()
    {
        var html = await Html(Kullanici(Roller.FakulteSekreteri), "/Fakulte/YonetimKuruluEvraki");

        Assert.Equal(["I26-05", "I26-06", "I26-07"], Kayitlar(html));
        Assert.DoesNotContain("I26-09", html);
        Assert.Contains("<h2 id=\"tablo-bilgisayar-muhendisligi\">Bilgisayar Mühendisliği</h2>", html);
        Assert.Contains("<h2 id=\"tablo-elektrik-elektronik-muhendisligi\">Elektrik-Elektronik Mühendisliği</h2>", html);
        Assert.Contains("data-ozet=\"toplam\">3<", html);

        // Mühendislik Fakültesi'nin sekreteri yalnızca kendi kaydını görür.
        var muhendislik = _factory.Istemci("muh.sekreter", bolum: null, fakulte: OrnekIntibakVerisi.Muhendislik,
            roller: [Roller.Personel, Roller.FakulteSekreteri]);
        Assert.Equal(["I26-09"], Kayitlar(await Html(muhendislik, "/Fakulte/YonetimKuruluEvraki")));
    }

    // ---------- Ortak parçalar ----------

    [Fact]
    public async Task Bolumu_bos_kullanici_kayit_gormuyor_tablo_bos_durumu_gosteriliyor()
    {
        var client = _factory.Istemci("bolumsuz", bolum: null, roller: [Roller.Akademisyen, Roller.IntibakKomisyonu]);

        var html = await Html(client, "/Komisyon/BekleyenBasvurular");

        Assert.Empty(Kayitlar(html));
        Assert.Contains("<h2 id=\"tablo-komisyon-incelemesindeki-basvurular\">Komisyon incelemesindeki başvurular</h2>", html);
        Assert.Contains($"<p class=\"bos-durum\">{KayitTablosu.VarsayilanBosMetin}</p>", html);
        Assert.DoesNotContain("<table", html);
    }

    [Fact]
    public async Task Kayit_tablosu_baslikli_kaydirilabilir_bolge()
    {
        var html = await Html(Kullanici(Roller.IntibakKomisyonu), "/Komisyon/BekleyenBasvurular");

        Assert.Contains("<div class=\"tablo-kabi kayit-tablosu\" role=\"region\" aria-labelledby=\"tablo-komisyon-incelemesindeki-basvurular\" tabindex=\"0\">", html);
        Assert.Contains("<th scope=\"col\">Öğrenci</th>", html);
        Assert.Contains("<tr data-intibak=\"I26-02\"><th scope=\"row\">", Regex.Replace(html, ">\\s+<", "><"));
    }

    [Fact]
    public async Task Basvuru_asamalari_su_anki_adimi_aria_current_ile_ve_durumu_metinle_gosteriyor()
    {
        var html = Regex.Replace(await Html(Kullanici(Roller.Ogrenci), "/Ogrenci/Basvurum"), "\\s+", " ");

        Assert.Contains("<ol class=\"basvuru-adimlari\" aria-label=\"İntibak başvurusunun aşamaları\">", html);
        Assert.Single(Regex.Matches(html, "aria-current=\"step\""));
        Assert.Contains("<li class=\"adim-su-an\" aria-current=\"step\"> <span class=\"adim-adi\">Bölüm başkanı onayı</span> <span class=\"adim-durumu\">Şu an</span>", html);
        Assert.Equal(2, Regex.Matches(html, "<li class=\"adim-tamamlandi\">").Count);
        Assert.Equal(2, Regex.Matches(html, "<li class=\"adim-bekliyor\">").Count);
        // Durum rozeti: anlam metinde, tür data-durum'da.
        Assert.Contains("<span class=\"etiket etiket-notr durum-rozeti\" data-durum=\"bekliyor\">Bölüm başkanı onayında</span>", html);

        // Sonuçlanan başvuruda bütün aşamalar tamamlanmıştır; şu anki aşama yok.
        var sonuclanan = await Html(_factory.Istemci("203011001", roller: Roller.Ogrenci), "/Ogrenci/Basvurum");
        Assert.DoesNotContain("aria-current=\"step\"", sonuclanan);
        Assert.Equal(5, Regex.Matches(sonuclanan, "<li class=\"adim-tamamlandi\">").Count);
        Assert.Contains("data-durum=\"olumlu\">Sonuçlandı</span>", sonuclanan);
    }

    // ---------- Ayardan gelen şartlar ----------

    private static string Karar(string html, int satir) =>
        Regex.Match(html, $"<select id=\"karar-{satir}\"[\\s\\S]*?</select>").Value;

    [Fact]
    public async Task Muafiyet_notu_sarti_ayardan_geliyor_ve_onerilen_karari_degistiriyor()
    {
        // I26-02 (dikey geçiş): 4. satır İşletim Sistemleri, not BA, eşdeğeri BM303.
        var varsayilan = await Html(Kullanici(Roller.IntibakKomisyonu), "/Komisyon/DersEslestirme?id=I26-02");
        Assert.Contains("Muafiyet için en düşük harf notu: <strong>CC</strong>", varsayilan);
        Assert.Contains("Şart değerleri örnektir.", varsayilan);
        Assert.Contains("<option value=\"muaf\" selected=\"selected\">", Karar(varsayilan, 3));

        using var siki = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["IntibakSartlari:MuafiyetIcinEnDusukNot"] = "AA",
            ["IntibakSartlari:EnFazlaMuafAkts"] = "60",
        });
        var html = await Html(Kullanici(Roller.IntibakKomisyonu, siki), "/Komisyon/DersEslestirme?id=I26-02");
        Assert.Contains("Muafiyet için en düşük harf notu: <strong>AA</strong>", html);
        Assert.Contains("<strong>60 AKTS</strong>", html);
        Assert.Contains("<option value=\"muaf-degil\" selected=\"selected\">", Karar(html, 3));
    }

    [Fact]
    public void Gecersiz_sart_ayari_acilista_duruyor()
    {
        using var factory = new OtomasyonFactory<Program>(ayarlar: new Dictionary<string, string?>
        {
            ["IntibakSartlari:MuafiyetIcinEnDusukNot"] = "XX",
        });

        var hata = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("'IntibakSartlari' ayarı geçersiz", hata.Message);
    }

    // ---------- Öğrenci formu ----------

    [Fact]
    public async Task Basvuru_formu_dosya_alanli_temsili_ve_antiforgery_korumali()
    {
        var client = Kullanici(Roller.Ogrenci);
        var html = await Html(client, "/Ogrenci/Basvuru");
        Assert.Contains("<form method=\"post\" enctype=\"multipart/form-data\"", html);
        Assert.Contains("type=\"file\"", html);

        using var eksik = new MultipartFormDataContent { { new StringContent("YatayGecis"), "gelisTuru" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Ogrenci/Basvuru", eksik)).StatusCode);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(OtomasyonFactory.AntiforgeryTokenAl(html)), "__RequestVerificationToken" },
            { new StringContent("YatayGecis"), "gelisTuru" },
            { new ByteArrayContent("%PDF-örnek"u8.ToArray()), "transkript", "transkript.pdf" },
        };
        var yanit = await client.PostAsync("/Ogrenci/Basvuru", form);
        Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        Assert.Contains(TemsiliIslem.Mesaj, await Html(client, "/Ogrenci/Basvuru"));
    }
}
