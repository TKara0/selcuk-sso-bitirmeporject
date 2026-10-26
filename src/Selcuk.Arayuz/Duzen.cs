namespace Selcuk.Arayuz;

/// <summary>
/// Ortak düzenin (_SelcukDuzen) sabitleri. Razor dışında üretilen sayfalar (ör. kimlik sunucusunun 429 sayfası) aynı
/// metinleri ve dosya yollarını buradan alır.
/// </summary>
public static class Duzen
{
    public const string AltBilgi =
        "Selçuk Üniversitesi (Test) — Tek Şifre Giriş Sistemi. Bu bir bitirme projesidir, resmî sistem değildir.";

    public const string StilDosyasi = "/_content/Selcuk.Arayuz/css/selcuk.css";


    /// <summary>ViewData anahtarları.</summary>
    public static class Ayar
    {
        /// <summary>Üst çubukta ve sayfa başlığında uygulama adı (varsayılan "Tek Şifre Giriş").</summary>
        public const string UygulamaAdi = "UygulamaAdi";

        /// <summary>İçerik genişliği: "dar" (560px), "genis" (1040px), "giris" (kapı panelli giriş) veya boş (720px).</summary>
        public const string Genislik = "Duzen";

        /// <summary>Giriş düzeninde kapı panelindeki ad ve cümle.</summary>
        public const string KapiBasligi = "KapiBasligi";

        public const string KapiCumlesi = "KapiCumlesi";
    }
}
