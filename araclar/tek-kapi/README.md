# Tek Kapı

Selçuk Üniversitesi otomasyonlarına tek ana şifreyle giriş yapan, **kendi bilgisayarında çalışan** web portalı.
Portala bir kere giriyorsun; OBİS, öğrenci e-postası, UZEM ve Yemekhane kartlarına tıklayınca site
Tek Kapı penceresinde açılıyor ve giriş kendiliğinden yapılıyor.

| Sistem | Adres | Kullanılan bilgi | Giriş türü |
|---|---|---|---|
| OBİS | obis2.selcuk.edu.tr | öğrenci no + OBİS şifresi | Yarı otomatik: CAPTCHA'yı sen yazarsın |
| Öğrenci e-postası (Zimbra) | ogrposta.selcuk.edu.tr | ogr e-postası + e-posta şifresi | Tam otomatik |
| UZEM (Moodle) | uzaktanogrenme5.selcuk.edu.tr | öğrenci no + OBİS şifresi | Tam otomatik |
| Uzaktan Sınav (Moodle) | uzaktansinav.selcuk.edu.tr | öğrenci no + OBİS şifresi | Tam otomatik |
| Yemekhane (Selçuk SSO) | yemek → sso.selcuk.edu.tr | ogr e-postası + e-posta şifresi | Yarı otomatik: "Giriş Yap" ve 2FA sende |

## Kurulum

Gerekenler: Node.js 18 veya üstü.

**En kolayı:** `TekKapi-Baslat.bat` dosyasına çift tıkla. İlk seferde bağımlılıkları kendisi kurar.
İstersen sağ tık → "Kısayol oluştur" ile masaüstüne bir kısayol koyabilirsin. Tek Kapı zaten açıksa yeni kopya başlatmaz, mevcut pencereyi öne getirir.
(Python seviyorsan `python start.py` aynı işi yapar.)

Elle kurmak istersen (SelcukSSO reposunun kökünden):

```bash
cd araclar\tek-kapi
npm install
npx playwright install chromium   # Chrome ya da Edge kuruluysa bu adım atlanabilir
npm start
```

`npm start` portalı `http://127.0.0.1:4545` adresinde açar ve bir tarayıcı penceresi başlatır.
İlk açılışta bir **ana şifre** belirlersin, ardından Ayarlar ekranında öğrenci numaranı, ogr e-postanı ve şifrelerini girersin.

Ayarlar:

| Ortam değişkeni | Varsayılan | Ne işe yarar |
|---|---|---|
| `PORT` | 4545 | Portal portu |
| `IDLE_LOCK_MIN` | 15 | Bu kadar dakika işlem olmazsa kasa kilitlenir |
| `PORTAL_BROWSER_CHANNEL` | (yok) | `chrome` veya `msedge` ile kurulu tarayıcıyı zorla |

## Özellikler

- **Tek tıkla giriş:** Kartlara tıklayınca site Tek Kapı penceresinde açılır ve giriş yapılır.
- **Hepsinden çık:** Üst menüdeki buton, Tek Kapı penceresindeki tüm Selçuk sekmelerini kapatır ve bu sitelere ait oturum çerezlerini siler.
  Bir sonraki tıklamada siteler yeniden giriş ister. Bu **yerel** bir çıkıştır: sunucu tarafındaki oturumlar kendi süreleri dolunca kapanır
  (gerçek SSO'daki tek çıkışla, yani SLO ile arasındaki fark tez için iyi bir karşılaştırma noktası).
- **Ölçüm kaydı:** Her giriş `data/metrics.csv` dosyasına yazılır, portalda özet tablo olarak görünür, "CSV indir" ile Excel'e alınır.
  Kolonlar: zaman, site, mod, kaynak (`kart` / `yonlendirme`), sonuç (`giris` / `oturum-hatirlandi` / `basarisiz`),
  toplam süre, otomatik doldurma süresi, kullanıcının harcadığı süre (CAPTCHA, 2FA), kullanıcı adımı sayısı.
  Şifre, kullanıcı adı veya e-posta kaydedilmez.
- **Kilit:** 15 dakika işlem olmazsa kasa kendiliğinden kilitlenir.

## Nasıl çalışıyor?

Sıradan bir web sitesi, başka bir sitenin (ör. OBİS'in) formunu dolduramaz; tarayıcının aynı köken politikası buna izin vermez.
Ayrıca bu sitelerin hepsinde oturuma bağlı tokenlar (`login_csrf`, `logintoken`, `__RequestVerificationToken`) veya CAPTCHA var,
bu yüzden "arka planda form POST etmek" de işe yaramaz. Tek Kapı bu nedenle iki parçadan oluşur:

1. **Portal (web arayüzü):** Kartlar, ayarlar, son işlemler. `public/`
2. **Yerel sunucu + yönetilen tarayıcı:** Express sunucusu (`src/server.js`) Playwright ile kalıcı profili olan bir tarayıcı penceresi açar.
   O penceredeki her sekmeyi izler; bilinen bir giriş sayfası yüklendiğinde ilgili **site adaptörü** (`src/sites.js`) alanları doldurur.

Kalıcı profil sayesinde sitelerin "beni hatırla" çerezleri ve SSO oturumu pencere kapansa da korunur, yani çoğu zaman tekrar giriş bile gerekmez.

## Güvenlik modeli

- **Kasa:** Ana şifreden scrypt (N=2^17, r=8, p=1) ile anahtar türetilir. Tüm bilgiler AES-256-GCM ile şifrelenip `data/vault.json`'a yazılır. Ana şifre hiçbir yerde saklanmaz.
- Anahtar yalnızca kasa açıkken bellekte durur. Kilitlenince veya 15 dakika boşta kalınca silinir. 5 hatalı denemeden sonra bekleme süresi başlar.
- Kayıtlı şifreler arayüze **hiçbir zaman geri gönderilmez**, günlüklere yazılmaz.
- Sunucu yalnızca `127.0.0.1`'de dinler. Kötü niyetli bir web sitesinin bu portala istek atmasına karşı:
  Host başlığı denetimi (DNS rebinding), özel `x-tek-kapi` başlığı (CSRF), `HttpOnly; SameSite=Strict` oturum çerezi, sıkı CSP.
- **Hesabı kilitletmeme:** Otomatik gönderimden sonra tekrar giriş sayfasına düşülürse site "başarısız" işaretlenir ve şifre düzeltilene kadar bir daha gönderilmez.
- **CAPTCHA ve 2FA atlatılmaz.** OBİS'te doğrulama kodunu, SSO'da "Giriş Yap"ı ve iki aşamalı doğrulama kodunu her zaman kullanıcı girer.

## Bilinen sınırlar

- Üniversite bir giriş formunu değiştirirse o sitenin adaptörü güncellenmelidir (form alan adları 18.10.2026'da canlı sayfalardan okundu).
  Bu, form tabanlı SSO'nun (ESSO) bilinen zayıflığıdır ve kurumsal SSO'nun (OIDC/SAML) neden daha doğru olduğunu gösterir.
- Selçuk SSO reCAPTCHA v3 kullanıyor. Otomasyonla açılmış tarayıcıya düşük puan verip girişi reddedebilir. Bu durumda giriş o pencerede elle yapılır;
  kalıcı profil sayesinde oturum sonraki açılışlarda korunur.
- Tek kullanıcılı, yerel kullanım içindir. İnternete açılmamalı, başkalarının şifrelerini toplamak için kullanılmamalıdır.

## Test

```bash
npm test
```

`test/mock-sites.js` gerçek giriş sayfalarının alan adlarını, oturuma bağlı tokenlarını, CAPTCHA ve 2FA adımlarını taklit eden sahte siteler kurar.
`test/e2e.js` bunlara karşı 33 kontrol çalıştırır: kasa şifrelemesi, otomatik ve yarı otomatik girişler, hatırlanan oturumlar, ölçüm kaydı, hesap kilitletmeme koruması, "Hepsinden çık", CSV dışa aktarma, tek kopya başlatma, kilit ekranı, Host/CSRF korumaları, günlüklerde ve ölçümlerde kimlik bilgisi olmaması.

## Klasör yapısı

```
src/server.js    Express API, oturum, güvenlik başlıkları
src/vault.js     scrypt + AES-256-GCM kasa
src/browser.js   Playwright motoru, sekme izleme, başarısızlık koruması, ölçüm, hepsinden çık
src/sites.js     Site adaptörleri (yeni sistem eklemek için buraya bir giriş eklenir)
public/          Portal arayüzü
TekKapi-Baslat.bat  Windows'ta çift tıkla başlatma
start.py         Python ile başlatma (isteğe bağlı)
data/            Kasa, tarayıcı profili ve ölçümler (git'e girmez)
test/            Sahte siteler ve uçtan uca test
```

## Bitirme projesi notu

İnceleme sırasında `yemek.selcuk.edu.tr`'nin girişi `sso.selcuk.edu.tr`'ye yönlendirdiği görüldü. Bu sunucu bir OpenID Connect sağlayıcısı
(`/connect/authorize`, `client_id=YemekApp`). Yani üniversitenin halihazırda gerçek bir SSO altyapısı var; OBİS, Zimbra ve UZEM ise henüz ona bağlı değil.
Tek Kapı "bugün, kurum desteği olmadan" çözümü; bu sistemleri `sso.selcuk.edu.tr`'ye OIDC/SAML ile bağlamak ise kalıcı çözümdür.
