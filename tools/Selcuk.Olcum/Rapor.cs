using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Selcuk.Olcum;

/// <summary>
/// Çıktılar: sso-olcumler-&lt;tarih&gt;.csv (ham turlar), .json (özet) ve .md (okunabilir tablo). Ortam bilgisi ve
/// ayarlar yazılır; kullanıcı adı, şifre, secret, token veya adres yazılmaz (hatalar yalnızca kategori kodudur).
/// </summary>
public static class Rapor
{
    public static readonly IReadOnlyDictionary<string, string> SenaryoAdlari = new Dictionary<string, string>
    {
        [Senaryolar.SogukGiris] = "(a) Soğuk giriş: OBİS → giriş formu → kod → token → Notlarım 200",
        [Senaryolar.TekGiris] = "(b) Tek giriş: OBİS'ten sonra Yemekhane Rezervasyonlarım 200 (şifresiz)",
        [Senaryolar.TokenYenileme] = "(c) Token yenileme: refresh token → yeni access token",
        [Senaryolar.TekCikis] = "(d) Tek çıkış: çıkış isteği → OBİS ve Yemekhane oturumu reddediyor",
        [Senaryolar.ApiCagrisi] = "(e) API çağrısı: Demo API GET /api/notlar",
        [Senaryolar.EszamanliGiris] = "(f) Eşzamanlı soğuk giriş",
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IReadOnlyList<string> Yaz(string klasor, OlcumSonucu sonuc, OlcumOrtami ortam)
    {
        Directory.CreateDirectory(klasor);
        var ad = "sso-olcumler-" + sonuc.Baslangic.ToString("yyyy-MM-dd-HHmm", Inv);
        var ozetler = Istatistik.Ozetle(sonuc.Kayitlar);
        var dosyalar = new[] { ".csv", ".json", ".md" }.Select(u => Path.Combine(klasor, ad + u)).ToArray();

        File.WriteAllText(dosyalar[0], Csv(sonuc.Kayitlar), new UTF8Encoding(false));
        File.WriteAllText(dosyalar[1], Json(sonuc, ortam, ozetler), new UTF8Encoding(false));
        File.WriteAllText(dosyalar[2], Markdown(sonuc, ortam, ozetler), new UTF8Encoding(false));
        return dosyalar;
    }

    public static string Csv(IEnumerable<OlcumKaydi> kayitlar)
    {
        var sb = new StringBuilder("senaryo,eszamanlilik,tur,isinma,basarili,sure_ms,hata\n");
        foreach (var k in kayitlar)
        {
            sb.Append(Inv, $"{k.Senaryo},{k.Eszamanlilik},{k.Tur},{(k.Isinma ? 1 : 0)},{(k.Basarili ? 1 : 0)},{k.SureMs:F3},{k.Hata}\n");
        }

        return sb.ToString();
    }

    private static double? Sayi(double d) => double.IsNaN(d) ? null : Math.Round(d, 3);

    private static string Json(OlcumSonucu sonuc, OlcumOrtami ortam, List<Ozet> ozetler)
    {
        var a = sonuc.Ayarlar;
        var belge = new
        {
            baslangic = sonuc.Baslangic.ToString("o", Inv),
            bitis = sonuc.Bitis.ToString("o", Inv),
            toplam_sure_sn = Math.Round((sonuc.Bitis - sonuc.Baslangic).TotalSeconds, 1),
            ortam = new
            {
                isletim_sistemi = ortam.IsletimSistemi,
                dotnet = ortam.DotNet,
                islemci = ortam.Islemci,
                mantiksal_cekirdek = ortam.MantiksalCekirdek,
                ram_gb = ortam.RamGb,
            },
            ayarlar = new
            {
                tekrar = a.Tekrar,
                isinma = a.Isinma,
                eszamanlilik = a.Eszamanlilik,
                olcum_kullanici_sayisi = a.KullaniciSayisi,
                yuzdelik_yontemi = "nearest-rank",
                birim = "ms",
            },
            senaryolar = ozetler.Select(o => new
            {
                senaryo = o.Senaryo,
                aciklama = SenaryoAdlari.GetValueOrDefault(o.Senaryo),
                eszamanlilik = o.Eszamanlilik,
                tur = o.Tur,
                basarili = o.Basarili,
                hata = o.Hata,
                hata_orani = Math.Round(o.HataOrani, 4),
                p50 = Sayi(o.P50),
                p95 = Sayi(o.P95),
                p99 = Sayi(o.P99),
                ortalama = Sayi(o.Ortalama),
                min = Sayi(o.Min),
                max = Sayi(o.Max),
                sure_sn = sonuc.EszamanliDuzeyler.FirstOrDefault(d => o.Senaryo == Senaryolar.EszamanliGiris
                    && d.Eszamanlilik == o.Eszamanlilik) is { } d ? Math.Round(d.SureSn, 3) : (double?)null,
                saniyede_basarili_giris = Throughput(sonuc, o) is { } t ? Math.Round(t, 2) : (double?)null,
            }),
            hata_kategorileri = sonuc.Kayitlar.Where(k => !k.Isinma && !k.Basarili)
                .GroupBy(k => (k.Senaryo, k.Eszamanlilik, k.Hata))
                .Select(g => new { senaryo = g.Key.Senaryo, eszamanlilik = g.Key.Eszamanlilik, kategori = g.Key.Hata, adet = g.Count() }),
        };

        return JsonSerializer.Serialize(belge, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }) + "\n";
    }

    private static double? Throughput(OlcumSonucu sonuc, Ozet o) =>
        o.Senaryo == Senaryolar.EszamanliGiris
        && sonuc.EszamanliDuzeyler.FirstOrDefault(d => d.Eszamanlilik == o.Eszamanlilik) is { SureSn: > 0 } d
            ? o.Basarili / d.SureSn
            : null;

    private static string Ms(double d) => double.IsNaN(d) ? "–" : d.ToString("F1", Inv);

    private static string Markdown(OlcumSonucu sonuc, OlcumOrtami ortam, List<Ozet> ozetler)
    {
        var a = sonuc.Ayarlar;
        var sb = new StringBuilder();
        sb.Append(Inv, $"# SSO ölçümleri ({sonuc.Baslangic:yyyy-MM-dd HH:mm})\n\n");
        sb.Append("Otomatik ölçüm aracı `tools/Selcuk.Olcum` ile, yalnızca bu bilgisayardaki (localhost) kimlik sunucusu, ");
        sb.Append("Demo OBİS, Demo Yemekhane ve Demo API üzerinde alındı. Gerçek Selçuk sistemlerine istek atılmadı.\n\n");

        sb.Append("## Ortam\n\n| Alan | Değer |\n|---|---|\n");
        sb.Append(Inv, $"| İşletim sistemi | {ortam.IsletimSistemi} |\n");
        sb.Append(Inv, $"| .NET | {ortam.DotNet} |\n");
        sb.Append(Inv, $"| İşlemci | {ortam.Islemci} |\n");
        sb.Append(Inv, $"| Mantıksal çekirdek | {ortam.MantiksalCekirdek} |\n");
        sb.Append(Inv, $"| RAM | {ortam.RamGb:F1} GB |\n");
        sb.Append(Inv, $"| Uygulamalar | Development ortamı, Debug derleme, LocalDB; dört uygulama ve ölçüm aracı aynı makinede |\n");
        sb.Append(Inv, $"| Başlangıç / bitiş | {sonuc.Baslangic:yyyy-MM-dd HH:mm:ss} / {sonuc.Bitis:HH:mm:ss} ");
        sb.Append(Inv, $"({(sonuc.Bitis - sonuc.Baslangic).TotalMinutes:F1} dk) |\n\n");

        sb.Append(Inv, $"Ayarlar: tekrar {a.Tekrar}, ısınma {a.Isinma} (özetlere girmez), eşzamanlılık ");
        sb.Append(Inv, $"{(a.Eszamanlilik.Count == 0 ? "–" : string.Join("/", a.Eszamanlilik))}, ölçüm kullanıcısı {a.KullaniciSayisi}. ");
        sb.Append("Süreler milisaniye; yüzdelikler en yakın sıra yöntemiyle, yalnızca başarılı turlardan.\n\n");

        sb.Append("## (a)–(e) Sıralı senaryolar\n\n");
        sb.Append("| Senaryo | n | Hata | p50 | p95 | p99 | Ortalama | Min | Max |\n|---|---:|---:|---:|---:|---:|---:|---:|---:|\n");
        foreach (var o in ozetler.Where(o => o.Senaryo != Senaryolar.EszamanliGiris))
        {
            sb.Append(Inv, $"| {SenaryoAdlari.GetValueOrDefault(o.Senaryo, o.Senaryo)} | {o.Tur} | {o.Hata} | {Ms(o.P50)} | {Ms(o.P95)} | ");
            sb.Append(Inv, $"{Ms(o.P99)} | {Ms(o.Ortalama)} | {Ms(o.Min)} | {Ms(o.Max)} |\n");
        }

        var eszamanli = ozetler.Where(o => o.Senaryo == Senaryolar.EszamanliGiris).ToList();
        sb.Append("\n## (f) Eşzamanlı soğuk giriş\n\n");
        if (eszamanli.Count == 0)
        {
            sb.Append("Bu çalıştırmada ölçülmedi.\n");
        }
        else
        {
            sb.Append("| Eşzamanlı kullanıcı | Giriş | Başarılı | Hata | Hata oranı | Süre (sn) | Saniyede başarılı giriş | p50 | p95 | p99 | Max |\n");
            sb.Append("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|\n");
            foreach (var o in eszamanli)
            {
                var sure = sonuc.EszamanliDuzeyler.First(d => d.Eszamanlilik == o.Eszamanlilik).SureSn;
                sb.Append(Inv, $"| {o.Eszamanlilik} | {o.Tur} | {o.Basarili} | {o.Hata} | {o.HataOrani:P1} | {sure:F2} | ");
                sb.Append(Inv, $"{Throughput(sonuc, o):F2} | {Ms(o.P50)} | {Ms(o.P95)} | {Ms(o.P99)} | {Ms(o.Max)} |\n");
            }
        }

        var hatalar = sonuc.Kayitlar.Where(k => !k.Isinma && !k.Basarili)
            .GroupBy(k => (k.Senaryo, k.Eszamanlilik, k.Hata)).ToList();
        sb.Append("\n## Hatalar\n\n");
        if (hatalar.Count == 0)
        {
            sb.Append("Ölçülen turlarda hata olmadı.\n");
        }
        else
        {
            sb.Append("| Senaryo | Eşzamanlılık | Kategori | Adet |\n|---|---:|---|---:|\n");
            foreach (var g in hatalar)
            {
                sb.Append(Inv, $"| {g.Key.Senaryo} | {g.Key.Eszamanlilik} | {g.Key.Hata} | {g.Count()} |\n");
            }
        }

        sb.Append("\n## Yöntem notları\n\n");
        sb.Append("- Tarayıcı açılmaz: HttpClient ve çerez kabıyla tarayıcının OIDC akışı (yönlendirmeler, giriş ve onay ");
        sb.Append("formları, antiforgery token'ı) programla izlenir. Sayfa kaynakları (CSS) indirilmez, JavaScript yoktur.\n");
        sb.Append("- Hazırlık: her ölçüm kullanıcısı ölçümden önce bir kez iki uygulamaya girip çıkar; onay ekranları ");
        sb.Append("ölçülen turlarda çıkmaz.\n");
        sb.Append("- (a) her turda boş çerez kabıyla başlar (yeni TLS bağlantıları dahil). (b) aynı çerez kabıyla.\n");
        sb.Append("- (c) ve (e): araç, açık kimlik sunucusu oturumuyla demo-obis istemcisi olarak kendi token'ını alır ");
        sb.Append("(süreye girmez); ölçülen yalnızca token uç noktasına refresh isteği ve Demo API çağrısıdır.\n");
        sb.Append("- (d) Demo OBİS çıkış formunun gönderilmesinden (kimlik sunucusunun çıkış onayı ve Back-Channel ");
        sb.Append("Logout dahil) iki uygulamanın korumalı sayfasının giriş için yönlendirmesine kadar.\n");
        sb.Append("- (f) her düzeyde işçi sayısı kadar paralel kullanıcı; saniyede başarılı giriş = başarılı giriş / ");
        sb.Append("ölçülen bölümün duvar saati süresi.\n");
        sb.Append("- `scripts/olcum-calistir.ps1` kimlik sunucusunu `Olcum:HizSiniriGevset=true` ile başlatır: giriş ");
        sb.Append("sayfasının istek sınırı yalnızca Development'ta ve olcum.kullanici.* hesapları için devre dışıdır ");
        sb.Append("(README > Ölçüm ve değerlendirme).\n");
        return sb.ToString();
    }
}
