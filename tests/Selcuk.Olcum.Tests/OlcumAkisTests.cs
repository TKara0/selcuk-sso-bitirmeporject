namespace Selcuk.Olcum.Tests;

/// <summary>
/// Ölçüm aracı, bellek içinde birlikte çalışan dört uygulamaya karşı (<see cref="UcanUcaOrtam"/>) tek turda (a)–(e)
/// senaryolarını ve küçük bir (f) düzeyini hatasız tamamlıyor; rapor dosyaları sır içermiyor.
/// </summary>
public sealed class OlcumAkisTests : IDisposable
{
    private readonly UcanUcaOrtam _ortam = new();
    private readonly string _cikti = Path.Combine(Path.GetTempPath(), "selcuk-olcum-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _ortam.Dispose();
        if (Directory.Exists(_cikti))
        {
            Directory.Delete(_cikti, recursive: true);
        }
    }

    private string Istekler() => "\nSon istekler:\n" + string.Join("\n", _ortam.Gunluk.TakeLast(15));

    [Fact]
    public async Task Tek_turda_a_e_senaryolari_ve_eszamanli_giris_hatasiz_tamamlaniyor()
    {
        await _ortam.HazirlaAsync(kullaniciSayisi: 2);
        var ayarlar = new OlcumAyarlari
        {
            Kimlik = UcanUcaOrtam.Kimlik,
            Obis = UcanUcaOrtam.Obis,
            Yemekhane = UcanUcaOrtam.Yemekhane,
            Api = UcanUcaOrtam.Api,
            Tekrar = 1,
            Isinma = 1,
            Eszamanlilik = [2],
            KullaniciSayisi = 2,
            Sifre = UcanUcaOrtam.Sifre,
            DemoObisSecret = _ortam.ObisSecret,
        };
        using var gunluk = new StringWriter();

        OlcumSonucu sonuc;
        try
        {
            sonuc = await new OlcumCalistirici(new Senaryolar(ayarlar, _ortam.TarayiciIsleyicisi), gunluk).CalistirAsync();
        }
        catch (OlcumAyarHatasi ex)
        {
            Assert.Fail(ex.Message + Istekler());
            throw;
        }

        var hatalar = sonuc.Kayitlar.Where(k => !k.Basarili).Select(k => $"{k.Senaryo}:{k.Hata}").ToList();
        Assert.True(hatalar.Count == 0, "Hatalı turlar: " + string.Join(", ", hatalar) + Istekler());
        var olculen = sonuc.Kayitlar.Where(k => !k.Isinma).GroupBy(k => k.Senaryo).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new Dictionary<string, int>
        {
            [Senaryolar.SogukGiris] = 1,
            [Senaryolar.TekGiris] = 1,
            [Senaryolar.TokenYenileme] = 1,
            [Senaryolar.ApiCagrisi] = 1,
            [Senaryolar.TekCikis] = 1,
            [Senaryolar.EszamanliGiris] = 1,
        }, olculen);
        Assert.All(sonuc.Kayitlar, k => Assert.True(k.SureMs > 0, $"{k.Senaryo} süresi 0"));
        // Isınma: sıralı bölümde beş senaryo bir kez, (f) düzeyinde bir giriş; özetlere girmez.
        Assert.Equal(6, sonuc.Kayitlar.Count(k => k.Isinma));
        Assert.All(Istatistik.Ozetle(sonuc.Kayitlar), o => Assert.Equal(1, o.Tur));

        // Raporlar: üç dosya, kullanıcı adı / şifre / secret yok.
        var dosyalar = Rapor.Yaz(_cikti, sonuc, OlcumOrtami.Topla());
        Assert.Equal([".csv", ".json", ".md"], dosyalar.Select(Path.GetExtension));
        foreach (var dosya in dosyalar)
        {
            var icerik = await File.ReadAllTextAsync(dosya);
            Assert.DoesNotContain("olcum.kullanici.0", icerik);
            Assert.DoesNotContain(UcanUcaOrtam.Sifre, icerik);
            Assert.DoesNotContain(_ortam.ObisSecret, icerik);
        }

        Assert.Contains("(a) Soğuk giriş", await File.ReadAllTextAsync(dosyalar[2]));
    }
}
