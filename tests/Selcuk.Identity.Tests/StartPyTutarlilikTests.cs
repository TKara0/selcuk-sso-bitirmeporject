using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Selcuk.Identity.Data;
using Selcuk.Identity.Oidc;

namespace Selcuk.Identity.Tests;

/// <summary>
/// start.py'deki (geliştirici başlatma betiği) test kullanıcıları ve kullanıcı-uygulama eşlemesi, BaslangicVerisi.cs ve
/// Istemciler.cs'in elle yazılmış kopyasıdır. Bu test start.py'yi okur ve koddaki gerçek değerlerle karşılaştırır:
/// test kullanıcı adları, user-secrets anahtarları, roller, her uygulamanın izinliRoller listesi ve bundan çıkan "hangi
/// kullanıcı hangi uygulamayı görür" tablosu. Uyuşmazlıkta hangi kullanıcıda veya uygulamada neyin farklı olduğunu yazar.
/// </summary>
public sealed partial class StartPyTutarlilikTests
{
    private static string DepoKoku()
    {
        for (var dizin = new DirectoryInfo(AppContext.BaseDirectory); dizin is not null; dizin = dizin.Parent)
        {
            if (File.Exists(Path.Combine(dizin.FullName, "SelcukSSO.sln")) && File.Exists(Path.Combine(dizin.FullName, "start.py")))
            {
                return dizin.FullName;
            }
        }

        throw new FileNotFoundException("Depo kökü (SelcukSSO.sln ve start.py) bulunamadı.");
    }

    /// <summary>start.py'de "AD = [" veya "AD = {" ile başlayıp ilk satır başındaki kapanışa kadar olan blok.</summary>
    private static string Blok(string py, string ad)
    {
        py = "\n" + py;
        var bas = py.IndexOf($"\n{ad} = ", StringComparison.Ordinal);
        Assert.True(bas >= 0, $"start.py'de {ad} bulunamadı.");
        // Kapanış: satır başındaki "]" veya "}".
        var kapanis = Regex.Match(py[bas..], @"\n[\]\}]");
        Assert.True(kapanis.Success, $"start.py'de {ad} bloğu kapanmıyor.");
        return py[bas..(bas + kapanis.Index + 2)];
    }

    private static string[] Liste(string icerik) =>
        [.. StringTirnakli().Matches(icerik).Select(m => m.Groups[1].Value)];

    private sealed record PyKullanici(string AyarAnahtari, string KullaniciAdi, string[] Roller);

    private static List<PyKullanici> PyKullanicilari(string py) =>
        [.. PyKullaniciSatiri().Matches(Blok(py, "TEST_KULLANICILARI"))
            .Select(m => new PyKullanici(m.Groups[1].Value, m.Groups[2].Value, Liste(m.Groups[3].Value)))];

    private static Dictionary<string, string[]> PyUygulamaRolleri(string py) =>
        PyUygulamaSatiri().Matches(Blok(py, "UYGULAMA_ROLLERI"))
            .ToDictionary(m => m.Groups[1].Value, m => Liste(m.Groups[2].Value));

    private static Dictionary<string, string> PyIstemciAnahtarlari(string py) =>
        PyIstemciSatiri().Matches(Blok(py, "ISTEMCI_ANAHTARLARI"))
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);

    /// <summary>Koddaki istemci tanımı: uygulama adı (run-all adı) → izinliRoller ve izinliKullaniciAdlari ile görünürlük.</summary>
    private static OpenIddictApplicationDescriptor Tanim(string istemciAnahtari)
    {
        var yontem = typeof(Istemciler).GetMethod(istemciAnahtari, BindingFlags.Public | BindingFlags.Static, [typeof(string)]);
        Assert.True(yontem is not null, $"Istemciler.{istemciAnahtari}(string) bulunamadı.");
        var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (OpenIddictApplicationDescriptor)yontem.Invoke(null, [secret])!;
    }

    private static HashSet<string> KodGorunenler(Dictionary<string, string> istemciAnahtarlari, string kullaniciAdi, string[] roller) =>
        [.. istemciAnahtarlari
            .Where(k => IstemciErisimi.Izinli([.. Tanim(k.Value).Properties.ToImmutableDictionary()], kullaniciAdi, roller))
            .Select(k => k.Key)];

    private static string[] KodIzinliRoller(string istemciAnahtari)
    {
        var tanim = Tanim(istemciAnahtari);
        return tanim.Properties.TryGetValue(Istemciler.IzinliRollerOzelligi, out var deger) && deger.ValueKind == JsonValueKind.Array
            ? [.. deger.EnumerateArray().Select(e => e.GetString()!)]
            : [];
    }

    [Fact]
    public void Start_py_test_kullanicilari_roller_ve_uygulama_eslemesi_koddakiyle_ayni()
    {
        var py = File.ReadAllText(Path.Combine(DepoKoku(), "start.py"));
        var farklar = new List<string>();

        // 1) Test kullanıcıları: kullanıcı adı, user-secrets anahtarı ve roller.
        var pyKullanicilar = PyKullanicilari(py);
        var kodKullanicilar = BaslangicVerisi.TestKullanicilari;
        foreach (var kod in kodKullanicilar)
        {
            var pyK = pyKullanicilar.FirstOrDefault(p => p.KullaniciAdi == kod.KullaniciAdi);
            if (pyK is null)
            {
                farklar.Add($"Kullanıcı '{kod.KullaniciAdi}': kodda var, start.py TEST_KULLANICILARI'nda yok.");
                continue;
            }

            if (pyK.AyarAnahtari != kod.AyarAnahtari)
            {
                farklar.Add($"Kullanıcı '{kod.KullaniciAdi}': user-secrets anahtarı start.py'de '{pyK.AyarAnahtari}', kodda '{kod.AyarAnahtari}'.");
            }

            if (!pyK.Roller.ToHashSet().SetEquals(kod.Roller))
            {
                farklar.Add($"Kullanıcı '{kod.KullaniciAdi}': roller start.py'de [{string.Join(", ", pyK.Roller)}], kodda [{string.Join(", ", kod.Roller)}].");
            }
        }

        foreach (var pyK in pyKullanicilar.Where(p => kodKullanicilar.All(k => k.KullaniciAdi != p.KullaniciAdi)))
        {
            farklar.Add($"Kullanıcı '{pyK.KullaniciAdi}': start.py'de var, kodda (BaslangicVerisi.TestKullanicilari) yok.");
        }

        if (pyKullanicilar.Count != pyKullanicilar.Select(p => p.KullaniciAdi).Distinct().Count())
        {
            farklar.Add("start.py TEST_KULLANICILARI'nda yinelenen kullanıcı adı var.");
        }

        // 2) Uygulama adları ve istemci anahtarları (run-all adı → Istemciler:<anahtar>).
        var istemciAnahtarlari = PyIstemciAnahtarlari(py);
        var pyUygulamaRolleri = PyUygulamaRolleri(py);
        foreach (var (uygulama, anahtar) in istemciAnahtarlari)
        {
            if (typeof(Istemciler).GetMethod(anahtar, BindingFlags.Public | BindingFlags.Static, [typeof(string)]) is null)
            {
                farklar.Add($"Uygulama '{uygulama}': ISTEMCI_ANAHTARLARI '{anahtar}' diyor, kodda Istemciler.{anahtar}(string) yok.");
            }

            if (!pyUygulamaRolleri.ContainsKey(uygulama))
            {
                farklar.Add($"Uygulama '{uygulama}': ISTEMCI_ANAHTARLARI'nda var, UYGULAMA_ROLLERI'nde yok.");
            }
        }

        foreach (var uygulama in pyUygulamaRolleri.Keys.Where(u => u != "demo-api" && !istemciAnahtarlari.ContainsKey(u)))
        {
            farklar.Add($"Uygulama '{uygulama}': UYGULAMA_ROLLERI'nde var, ISTEMCI_ANAHTARLARI'nda yok.");
        }

        if (farklar.Count > 0)
        {
            Assert.Fail("start.py koddan farklı:\n" + string.Join("\n", farklar));
        }

        // 3) Her uygulamanın izinliRoller listesi (boş = herkes) koddakiyle aynı mı.
        foreach (var (uygulama, anahtar) in istemciAnahtarlari)
        {
            var kodRolleri = KodIzinliRoller(anahtar);
            var pyRolleri = pyUygulamaRolleri[uygulama];
            if (!kodRolleri.ToHashSet().SetEquals(pyRolleri))
            {
                farklar.Add($"Uygulama '{uygulama}': izinliRoller start.py'de [{string.Join(", ", pyRolleri)}], kodda [{string.Join(", ", kodRolleri)}].");
            }

            if (Tanim(anahtar).Properties.TryGetValue(Istemciler.IzinliKullaniciAdlariOzelligi, out var adlar)
                && adlar.ValueKind == JsonValueKind.Array && adlar.GetArrayLength() > 0)
            {
                farklar.Add($"Uygulama '{uygulama}': kodda izinliKullaniciAdlari dolu; start.py yalnızca rollere göre eşler.");
            }
        }

        // 4) Sonuç: her kullanıcının görebildiği uygulamalar (start.py'nin kullanicilari_yaz hesabı ile kodun erişim kuralı).
        foreach (var kod in kodKullanicilar)
        {
            var kodGorunen = KodGorunenler(istemciAnahtarlari, kod.KullaniciAdi, kod.Roller);
            var pyK = pyKullanicilar.First(p => p.KullaniciAdi == kod.KullaniciAdi);
            var pyGorunen = pyUygulamaRolleri
                .Where(u => istemciAnahtarlari.ContainsKey(u.Key) && (u.Value.Length == 0 || pyK.Roller.Intersect(u.Value).Any()))
                .Select(u => u.Key).ToHashSet();
            if (!pyGorunen.SetEquals(kodGorunen))
            {
                farklar.Add($"Kullanıcı '{kod.KullaniciAdi}': start.py'de görünen uygulamalar [{string.Join(", ", pyGorunen.Order())}], "
                    + $"kodda [{string.Join(", ", kodGorunen.Order())}]; start.py'de fazla: [{string.Join(", ", pyGorunen.Except(kodGorunen).Order())}], "
                    + $"eksik: [{string.Join(", ", kodGorunen.Except(pyGorunen).Order())}].");
            }
        }

        Assert.True(farklar.Count == 0, "start.py koddan farklı:\n" + string.Join("\n", farklar));
    }

    [Fact]
    public void Karsilastirma_gercek_bir_uyusmazligi_yakalar_ve_kullaniciyi_adlandirir()
    {
        // Sahte start.py metni: baskan.test'in bir rolü eksik, staj'ın izinli rolleri farklı.
        var py = """
            TEST_KULLANICILARI = [
                ("BolumBaskani", "baskan.test", ["akademisyen", "bolum_baskani"]),
            ]
            UYGULAMA_ROLLERI = {
                "staj": ["ogrenci"],
            }
            """;

        var k = PyKullanicilari(py).Single();
        Assert.Equal(new PyKullanici("BolumBaskani", "baskan.test", ["akademisyen", "bolum_baskani"]).KullaniciAdi, k.KullaniciAdi);
        var kod = BaslangicVerisi.TestKullanicilari.Single(t => t.KullaniciAdi == "baskan.test");
        Assert.False(k.Roller.ToHashSet().SetEquals(kod.Roller));
        Assert.False(KodIzinliRoller("StajOtomasyonu").ToHashSet().SetEquals(PyUygulamaRolleri(py)["staj"]));
    }

    [Fact]
    public void Start_py_gosterim_hesaplari_koddakiyle_ayni()
    {
        var py = File.ReadAllText(Path.Combine(DepoKoku(), "start.py"));
        var kullanicilar = PyKullanicilari(py).ToDictionary(k => k.AyarAnahtari, k => k.KullaniciAdi);
        var hesaplar = PyHesapSatiri().Matches(Blok(py, "GOSTERIM_HESAPLARI"))
            .Select(m => (Anahtar: m.Groups[1].Value, GorunenAd: m.Groups[3].Value)).ToList();

        Assert.Equal(BaslangicVerisi.GosterimHesaplari.Order(StringComparer.Ordinal),
            hesaplar.Select(h => kullanicilar[h.Anahtar]).Order(StringComparer.Ordinal));
        foreach (var (anahtar, gorunenAd) in hesaplar)
        {
            Assert.Equal(BaslangicVerisi.TestKullanicilari.Single(t => t.AyarAnahtari == anahtar).AdSoyad, gorunenAd);
        }

        // Her kullanıcı kategorisinden bir hesap; önerilen iki hesap listenin en üstünde.
        Assert.Equal(["Ogrenci2", "BolumBaskani"], hesaplar.Take(2).Select(h => h.Anahtar));
        Assert.Contains("ONERILEN_HESAPLAR = (\"Ogrenci2\", \"BolumBaskani\")", py);
    }

    [Fact]
    public void Hesap_listesi_dosyasi_gitignore_da_ve_izlenen_dosyalar_arasinda_yok()
    {
        const string Dosya = "TEST_KULLANICILARI.md";
        var kok = DepoKoku();

        Assert.Contains(Dosya, File.ReadAllLines(Path.Combine(kok, ".gitignore")).Select(s => s.Trim()));
        // Dosya henüz yokken de git onu yok saymalı (check-ignore 0); hiçbir klasörde izlenen kopyası olmamalı.
        Assert.Equal((0, Dosya), Git(kok, "check-ignore", Dosya));
        Assert.Equal((0, string.Empty), Git(kok, "ls-files", "--", $":(glob)**/{Dosya}"));
        // start.py dosyayı yazmadan önce aynı denetimi yapar.
        Assert.Contains("\"git\", \"check-ignore\"", File.ReadAllText(Path.Combine(kok, "start.py")));
    }

    private static (int Kod, string Cikti) Git(string kok, params string[] argumanlar)
    {
        var bilgi = new ProcessStartInfo("git", argumanlar) { WorkingDirectory = kok, RedirectStandardOutput = true };
        using var islem = Process.Start(bilgi)!;
        var cikti = islem.StandardOutput.ReadToEnd();
        islem.WaitForExit();
        return (islem.ExitCode, cikti.Trim());
    }

    [GeneratedRegex("""\s"(\w+)":\s*\("([^"]+)",\s*"([^"]+)"\)""")]
    private static partial Regex PyHesapSatiri();

    [GeneratedRegex("\"([^\"]*)\"")]
    private static partial Regex StringTirnakli();

    [GeneratedRegex("""\(\s*"([^"]+)",\s*"([^"]+)",\s*\[([^\]]*)\]\s*\)""")]
    private static partial Regex PyKullaniciSatiri();

    [GeneratedRegex("""\s"([a-z\-]+)":\s*\[([^\]]*)\]""")]
    private static partial Regex PyUygulamaSatiri();

    [GeneratedRegex(@"\s""([a-z\-]+)"":\s*""([A-Za-z]+)""")]
    private static partial Regex PyIstemciSatiri();
}
