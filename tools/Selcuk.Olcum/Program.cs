using System.Net;
using Microsoft.Extensions.Configuration;
using Selcuk.Olcum;

// Otomatik SSO ölçümü. Yalnızca localhost'taki kendi uygulamalarımıza istek atar (YerelAg). Ayarlar: appsettings.json,
// kimlik sunucusunun user-secrets'ı (ölçüm şifresi, demo-obis secret'ı), ortam değişkenleri (SELCUK_OLCUM_) ve komut
// satırı: --tekrar 100 --isinma 5 --eszamanlilik 1,10,25,50 --kullanici-sayisi 50 --cikti docs/olcumler
// Kullanım: dotnet run --project tools/Selcuk.Olcum -- --tekrar 100   (README > Ölçüm ve değerlendirme)

// Kimlik sunucusunun (src/Selcuk.Identity) UserSecretsId'si: şifre ve secret tek yerde tanımlanır.
const string KimlikSunucusuUserSecretsId = "9d8174b9-2529-44cd-aeb2-6d4da03838cc";

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
    .AddUserSecrets(KimlikSunucusuUserSecretsId)
    .AddEnvironmentVariables("SELCUK_OLCUM_")
    .AddCommandLine(args, new Dictionary<string, string>
    {
        ["--tekrar"] = "Olcum:Tekrar",
        ["--isinma"] = "Olcum:Isinma",
        ["--eszamanlilik"] = "Olcum:Eszamanlilik",
        ["--kullanici-sayisi"] = "Olcum:KullaniciSayisi",
        ["--cikti"] = "Olcum:Cikti",
    })
    .Build();

OlcumAyarlari ayarlar;
try
{
    ayarlar = OlcumAyarlari.Oku(configuration);
}
catch (OlcumAyarHatasi ex)
{
    Console.Error.WriteLine("Ölçüm başlatılmadı: " + ex.Message);
    return 2;
}

// Gerçek tarayıcı gibi: kendi çerez kabı, otomatik yönlendirme yok (akış elle izlenir), sistem proxy'si kullanılmaz
// (istekler makineden çıkmaz), TLS doğrulaması açık (geliştirme sertifikasına güvenilmiş olmalı).
static HttpMessageHandler Handler(CookieContainer cerezler) => new SocketsHttpHandler
{
    CookieContainer = cerezler,
    UseCookies = true,
    AllowAutoRedirect = false,
    UseProxy = false,
    ConnectTimeout = TimeSpan.FromSeconds(10),
    AutomaticDecompression = DecompressionMethods.All,
};

var ortam = OlcumOrtami.Topla();
Console.WriteLine($"Ortam: {ortam.IsletimSistemi}, {ortam.DotNet}, {ortam.Islemci}, {ortam.MantiksalCekirdek} mantıksal çekirdek, {ortam.RamGb:F1} GB");

OlcumSonucu sonuc;
try
{
    sonuc = await new OlcumCalistirici(new Senaryolar(ayarlar, Handler), Console.Out).CalistirAsync();
}
catch (OlcumAyarHatasi ex)
{
    Console.Error.WriteLine("Ölçüm durduruldu: " + ex.Message);
    return 3;
}

var klasor = configuration["Olcum:Cikti"] is { Length: > 0 } cikti
    ? Path.GetFullPath(cikti)
    : Path.Combine(DepoKoku() ?? Directory.GetCurrentDirectory(), "docs", "olcumler");
foreach (var dosya in Rapor.Yaz(klasor, sonuc, ortam))
{
    Console.WriteLine("Yazıldı: " + Path.GetRelativePath(Directory.GetCurrentDirectory(), dosya));
}

var hata = sonuc.Kayitlar.Count(k => !k.Isinma && !k.Basarili);
Console.WriteLine($"Toplam süre {(sonuc.Bitis - sonuc.Baslangic).TotalMinutes:F1} dk, ölçülen turlarda {hata} hata.");
return 0;

static string? DepoKoku()
{
    for (var dizin = new DirectoryInfo(Directory.GetCurrentDirectory()); dizin is not null; dizin = dizin.Parent)
    {
        if (File.Exists(Path.Combine(dizin.FullName, "SelcukSSO.sln")))
        {
            return dizin.FullName;
        }
    }

    return null;
}
