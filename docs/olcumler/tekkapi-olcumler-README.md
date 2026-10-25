# Tek Kapı ölçüm kayıtları

Tek Kapı (`araclar/tek-kapi`) her girişi kullanıcının bilgisayarında kendi `data/` klasörüne yazar ve portalda
özet tablo olarak gösterir. Bu kayıtlar tezin karşılaştırmasına **kullanıcı tarafından** buraya kopyalanır.

- Yapay zekâ ajanı ve otomatik araçlar Tek Kapı'nın `data/` klasörünü **okumaz**, oradan dosya kopyalamaz. Kayıt
  yalnızca portalın **"CSV indir"** düğmesiyle (oturum ve kasa açıkken) indirilir.
- `data/` klasörü `.gitignore`'dadır ve repoya girmez; buraya yalnızca indirilen CSV kopyalanır.

## Adımlar

1. Tek Kapı'yı açın, giriş yapın ve kasayı açın.
2. Ölçüm tablosundaki **"CSV indir"** düğmesine basın. Tarayıcı `tek-kapi-olcumler.csv` dosyasını indirir.
3. Dosyayı açıp yalnızca beklenen sütunların olduğunu ve kullanıcı adı, şifre veya e-posta olmadığını kontrol edin.
4. Dosyayı bu klasöre `tek-kapi-olcumler-<tarih>.csv` adıyla kopyalayın (ör. `tek-kapi-olcumler-2026-11-02.csv`).
5. `python scripts/olcum_analiz.py` çalıştırın; kayıtlar `karsilastirma.md`'deki "Tek Kapı kaydı" satırlarına girer.

## Sütunlar

Tek Kapı'nın yazdığı biçim (UTF-8, BOM'lu, virgül ayırıcı):

| Sütun | Anlamı |
|---|---|
| `at` | Zaman (ISO 8601) |
| `siteId`, `site` | `obis` (OBİS), `posta` (e-posta), `uzem` (UZEM), `sinav` (Uzaktan Sınav), `yemek` (Yemekhane) |
| `mode` | `auto` (tam otomatik) / `assist` (yarı otomatik: CAPTCHA veya 2FA kullanıcıda) |
| `kaynak` | `kart` / `yonlendirme` |
| `sonuc` | `giris` / `oturum-hatirlandi` / `basarisiz` |
| `toplamMs` | Karta tıklamadan giriş sonrası sayfaya kadar toplam süre (ms) |
| `otomatikMs` | Tek Kapı'nın formu doldurup gönderme süresi (ms) |
| `kullaniciMs` | Kullanıcının harcadığı süre (CAPTCHA, 2FA) (ms) |
| `kullaniciAdimi` | Kullanıcının yapması gereken adım sayısı |

Analiz betiği `oturum-hatirlandi` kayıtlarını `giris`'ten ayrı satırda gösterir (yeniden giriş yapılmadan oturum
hatırlandığı için soğuk girişle karşılaştırılamaz) ve `basarisiz`'ı hata sayar. Tek Kapı'da kullanıcı gerçek
sistemin şifresini yazmadığı için "şifre girilen deneme" 0 sayılır; kasa ana şifresi oturum başına bir kez yazılır
ve bu kayıtlarda yer almaz.

`ornek-tek-kapi-olcumler.csv` gerçek ölçüm değildir; yalnızca `scripts/olcum_analiz.py --self-test` kullanır.
