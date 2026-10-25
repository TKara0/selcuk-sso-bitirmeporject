using System.Net;
using Microsoft.Extensions.Configuration;

namespace Selcuk.Olcum.Tests;

/// <summary>Ölçüm aracı yalnızca localhost'a istek atar: yapılandırmada ve her giden istekte.</summary>
public sealed class YerelAgTests
{
    private static Dictionary<string, string?> Ayarlar(string? kimlik = null) => new()
    {
        ["Olcum:Hedefler:Kimlik"] = kimlik ?? "https://localhost:5001/",
        ["Olcum:Hedefler:Obis"] = "https://localhost:5101/",
        ["Olcum:Hedefler:Yemekhane"] = "https://127.0.0.1:5102/",
        ["Olcum:Hedefler:Api"] = "https://[::1]:5201/",
        [OlcumAyarlari.SifreAyari] = "test-sifresi",
        [OlcumAyarlari.DemoObisSecretAyari] = "test-secret",
    };

    private static OlcumAyarlari Oku(Dictionary<string, string?> ayarlar) =>
        OlcumAyarlari.Oku(new ConfigurationBuilder().AddInMemoryCollection(ayarlar).Build());

    [Fact]
    public void Yerel_hedefler_kabul_ediliyor_varsayilanlar_dogru()
    {
        var ayarlar = Oku(Ayarlar());

        Assert.Equal(new Uri("https://localhost:5001/"), ayarlar.Kimlik);
        Assert.Equal(new Uri("https://[::1]:5201/"), ayarlar.Api);
        Assert.Equal((100, 5, 50), (ayarlar.Tekrar, ayarlar.Isinma, ayarlar.KullaniciSayisi));
        Assert.Equal([1, 10, 25, 50], ayarlar.Eszamanlilik);
    }

    [Theory]
    [InlineData("https://obis.selcuk.edu.tr/")]
    [InlineData("https://example.com/")]
    [InlineData("https://localhost.example.com/")]
    [InlineData("https://192.168.1.10:5001/")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("https://0.0.0.0:5001/")]
    [InlineData("http://localhost:5001/")]
    [InlineData("localhost:5001")]
    public void Localhost_disi_veya_https_olmayan_hedef_reddediliyor(string adres)
    {
        var hata = Assert.Throws<OlcumAyarHatasi>(() => Oku(Ayarlar(adres)));
        Assert.Contains("Kimlik", hata.Message);
    }

    [Fact]
    public void Hedef_yerel_degilse_sirlar_eksik_olsa_da_once_hedef_reddediliyor()
    {
        var ayarlar = Ayarlar("https://obis.selcuk.edu.tr/");
        ayarlar.Remove(OlcumAyarlari.SifreAyari);

        Assert.Contains("yerel değil", Assert.Throws<OlcumAyarHatasi>(() => Oku(ayarlar)).Message);
    }

    [Theory]
    [InlineData("https://www.selcuk.edu.tr/")]
    [InlineData("https://1.1.1.1/")]
    public async Task Yonlendirmeyle_bile_yerel_olmayan_adrese_istek_gitmiyor(string adres)
    {
        var ic = new SayanHandler();
        using var oturum = new TarayiciOturumu(_ => ic, new Uri("https://localhost:5001/"), TimeSpan.FromSeconds(5));

        // Yerel sayfa dış adrese yönlendiriyor; araç yönlendirmeyi izlerken isteği göndermeden durur.
        ic.Yonlendir = new Uri(adres);
        var hata = await Assert.ThrowsAsync<OlcumHatasi>(() =>
            oturum.IzleAsync(() => oturum.GetAsync(new Uri("https://localhost:5101/")), new AkisSecenekleri()));

        Assert.Equal("yerel-olmayan-adres", hata.Kategori);
        Assert.Equal(["https://localhost:5101/"], ic.Adresler);
    }

    [Fact]
    public void Gizli_form_alanlari_ilk_formdan_oznitelik_sirasindan_bagimsiz_ve_cozulmus_okunuyor()
    {
        const string html = """
            <form><input type="hidden" id="ReturnUrl" name="ReturnUrl" value="/connect/authorize?a=1&amp;b=2" />
            <input name="__RequestVerificationToken" type="hidden" value="tok" />
            <input type="text" name="Input.KullaniciAdi" value="" /></form>
            <form method="post"><input name="__RequestVerificationToken" type="hidden" value="ikinci-form" /></form>
            """;

        Assert.Equal(
            [KeyValuePair.Create("ReturnUrl", "/connect/authorize?a=1&b=2"), KeyValuePair.Create("__RequestVerificationToken", "tok")],
            TarayiciOturumu.GizliAlanlar(html));
    }

    [Fact]
    public void Yuzdelikler_en_yakin_sira_yontemiyle_isinma_turlari_ozete_girmiyor()
    {
        var kayitlar = Enumerable.Range(1, 100)
            .Select(i => new OlcumKaydi("x", 1, i, false, true, i, null))
            .Append(new OlcumKaydi("x", 1, 1, true, true, 100_000, null))
            .Append(new OlcumKaydi("x", 1, 101, false, false, 5, "zaman-asimi"))
            .ToList();

        var ozet = Assert.Single(Istatistik.Ozetle(kayitlar));

        Assert.Equal((101, 100, 1), (ozet.Tur, ozet.Basarili, ozet.Hata));
        Assert.Equal((50d, 95d, 99d, 1d, 100d, 50.5), (ozet.P50, ozet.P95, ozet.P99, ozet.Min, ozet.Max, ozet.Ortalama));
    }

    private sealed class SayanHandler : HttpMessageHandler
    {
        public List<string> Adresler { get; } = [];

        public Uri? Yonlendir { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Adresler.Add(request.RequestUri!.AbsoluteUri);
            var yanit = new HttpResponseMessage(HttpStatusCode.Found);
            yanit.Headers.Location = Yonlendir;
            return Task.FromResult(yanit);
        }
    }
}
