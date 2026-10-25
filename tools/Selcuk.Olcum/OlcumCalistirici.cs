using System.Diagnostics;
using System.Net;

namespace Selcuk.Olcum;

/// <summary>Tek bir ölçüm turu (ham veri satırı). Hata bir kategori kodudur; kişisel veri veya sır içermez.</summary>
public sealed record OlcumKaydi(string Senaryo, int Eszamanlilik, int Tur, bool Isinma, bool Basarili, double SureMs, string? Hata);

/// <summary>(f) bir eşzamanlılık düzeyinin ölçülen bölümünün duvar saati süresi.</summary>
public sealed record EszamanliDuzey(int Eszamanlilik, double SureSn);

public sealed record OlcumSonucu(
    DateTimeOffset Baslangic, DateTimeOffset Bitis, OlcumAyarlari Ayarlar, IReadOnlyList<OlcumKaydi> Kayitlar,
    IReadOnlyList<EszamanliDuzey> EszamanliDuzeyler);

/// <summary>
/// Ölçüm sırası: (1) hazırlık: her ölçüm kullanıcısı bir kez OBİS ve Yemekhane'ye girip çıkar (onaylar verilir; süre
/// kaydedilmez). (2) Sıralı turlar: her turda yeni çerez kabıyla (a) soğuk giriş, (b) tek giriş, aracın kendi
/// token'ıyla (c) yenileme ve (e) API çağrısı, sonra (d) tek çıkış. İlk Isinma turu "isinma" olarak işaretlenir,
/// özetlere girmez. (3) Her eşzamanlılık düzeyinde (f): önce Isinma kadar ısınma girişi, sonra Tekrar kadar soğuk giriş
/// düzeydeki kullanıcı sayısı kadar paralel çalışan işçiyle (her işçi kendi kullanıcısıyla) yapılır.
/// </summary>
public sealed class OlcumCalistirici(Senaryolar senaryolar, TextWriter gunluk)
{
    private readonly List<OlcumKaydi> _kayitlar = [];
    private readonly Lock _kilit = new();

    private OlcumAyarlari Ayarlar => senaryolar.Ayarlar;

    public static string KullaniciAdi(int no) => $"olcum.kullanici.{no:00}";

    public async Task<OlcumSonucu> CalistirAsync()
    {
        var baslangic = DateTimeOffset.Now;
        await HazirlaAsync();
        await SiraliAsync();

        var duzeyler = new List<EszamanliDuzey>();
        foreach (var c in Ayarlar.Eszamanlilik)
        {
            duzeyler.Add(await EszamanliAsync(c));
        }

        return new OlcumSonucu(baslangic, DateTimeOffset.Now, Ayarlar, [.. _kayitlar], duzeyler);
    }

    private async Task HazirlaAsync()
    {
        gunluk.WriteLine($"Hazırlık: {Ayarlar.KullaniciSayisi} ölçüm kullanıcısı için ilk giriş ve onaylar...");
        for (var no = 1; no <= Ayarlar.KullaniciSayisi; no++)
        {
            try
            {
                using var oturum = senaryolar.YeniOturum();
                await senaryolar.SogukGirisAsync(oturum, KullaniciAdi(no));
                await senaryolar.TekGirisAsync(oturum);
                await senaryolar.TekCikisAsync(oturum);
            }
            catch (Exception ex) when (Kategori(ex) is { } kategori)
            {
                // Hazırlık başarısızsa (uygulama kapalı, sertifika, şifre, hesap yok) ölçüm anlamsız olur.
                throw new OlcumAyarHatasi(
                    $"Hazırlık başarısız ({kategori}, kullanıcı no {no:00}). Uygulamalar çalışıyor mu, ölçüm kullanıcıları " +
                    "seed edildi mi (OlcumKullanicilari:Sifre), geliştirme sertifikasına güveniliyor mu?");
            }
        }
    }

    private async Task SiraliAsync()
    {
        var toplam = Ayarlar.Isinma + Ayarlar.Tekrar;
        gunluk.WriteLine($"Sıralı senaryolar (a)-(e): {Ayarlar.Isinma} ısınma + {Ayarlar.Tekrar} tur...");
        for (var i = 0; i < toplam; i++)
        {
            var isinma = i < Ayarlar.Isinma;
            var tur = isinma ? i + 1 : i - Ayarlar.Isinma + 1;
            using var oturum = senaryolar.YeniOturum();
            var kullanici = KullaniciAdi(i % Ayarlar.KullaniciSayisi + 1);

            if (!await OlcAsync(Senaryolar.SogukGiris, 1, tur, isinma, () => senaryolar.SogukGirisAsync(oturum, kullanici)))
            {
                Atla(tur, isinma, Senaryolar.TekGiris, Senaryolar.TokenYenileme, Senaryolar.ApiCagrisi, Senaryolar.TekCikis);
                continue;
            }

            if (!await OlcAsync(Senaryolar.TekGiris, 1, tur, isinma, () => senaryolar.TekGirisAsync(oturum)))
            {
                Atla(tur, isinma, Senaryolar.TokenYenileme, Senaryolar.ApiCagrisi, Senaryolar.TekCikis);
                continue;
            }

            string? access = null;
            var tokenAlindi = await OlcAsync(Senaryolar.TokenYenileme, 1, tur, isinma, async () =>
            {
                var (_, refresh) = await senaryolar.TokenAlAsync(oturum);
                var (sure, yeniAccess, _) = await senaryolar.TokenYenileAsync(oturum, refresh);
                access = yeniAccess;
                return sure;
            });
            if (tokenAlindi)
            {
                await OlcAsync(Senaryolar.ApiCagrisi, 1, tur, isinma, () => senaryolar.ApiAsync(oturum, access!));
            }
            else
            {
                Atla(tur, isinma, Senaryolar.ApiCagrisi);
            }

            await OlcAsync(Senaryolar.TekCikis, 1, tur, isinma, () => senaryolar.TekCikisAsync(oturum));

            if ((i + 1) % 10 == 0)
            {
                gunluk.WriteLine($"  {i + 1}/{toplam} tur");
            }
        }
    }

    private async Task<EszamanliDuzey> EszamanliAsync(int c)
    {
        gunluk.WriteLine($"Eşzamanlı soğuk giriş (f): {c} kullanıcı, {Ayarlar.Isinma} ısınma + {Ayarlar.Tekrar} giriş...");
        await IsciCalistirAsync(c, Ayarlar.Isinma, isinma: true);
        var baslangic = Stopwatch.GetTimestamp();
        await IsciCalistirAsync(c, Ayarlar.Tekrar, isinma: false);
        return new EszamanliDuzey(c, Stopwatch.GetElapsedTime(baslangic).TotalSeconds);
    }

    // c işçi, ortak sayaçtan sıradaki turu alır; her işçi kendi ölçüm kullanıcısıyla, her girişte yeni çerez kabıyla.
    private Task IsciCalistirAsync(int c, int adet, bool isinma)
    {
        var sayac = 0;
        return Task.WhenAll(Enumerable.Range(1, c).Select(isci => Task.Run(async () =>
        {
            while (Interlocked.Increment(ref sayac) is var tur && tur <= adet)
            {
                using var oturum = senaryolar.YeniOturum();
                await OlcAsync(Senaryolar.EszamanliGiris, c, tur, isinma,
                    () => senaryolar.SogukGirisAsync(oturum, KullaniciAdi(isci)));
            }
        })));
    }

    private async Task<bool> OlcAsync(string senaryo, int c, int tur, bool isinma, Func<Task<double>> islem)
    {
        var baslangic = Stopwatch.GetTimestamp();
        OlcumKaydi kayit;
        try
        {
            kayit = new OlcumKaydi(senaryo, c, tur, isinma, true, await islem(), null);
        }
        catch (Exception ex) when (Kategori(ex) is { } kategori)
        {
            kayit = new OlcumKaydi(senaryo, c, tur, isinma, false, Stopwatch.GetElapsedTime(baslangic).TotalMilliseconds, kategori);
        }

        lock (_kilit)
        {
            _kayitlar.Add(kayit);
        }

        return kayit.Basarili;
    }

    // Önceki adım başarısız olduğu için çalıştırılamayan senaryolar da hata sayılır (gizlenmez).
    private void Atla(int tur, bool isinma, params string[] senaryoAdlari)
    {
        lock (_kilit)
        {
            _kayitlar.AddRange(senaryoAdlari.Select(s => new OlcumKaydi(s, 1, tur, isinma, false, 0, "onceki-adim-basarisiz")));
        }
    }

    private static string? Kategori(Exception ex) => ex switch
    {
        OlcumHatasi h => h.Kategori,
        TaskCanceledException or TimeoutException => "zaman-asimi",
        HttpRequestException { StatusCode: { } durum } => $"durum-{(int)durum}",
        HttpRequestException => "baglanti",
        System.Text.Json.JsonException => "gecersiz-json",
        WebException => "baglanti",
        _ => null,
    };
}
