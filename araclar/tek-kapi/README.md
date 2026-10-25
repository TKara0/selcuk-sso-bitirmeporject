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

Gerekenler: Node.js 18 veya üstü. `npm start` ve `TekKapi-Baslat.bat` `--use-system-ca` bayrağını kullandığı için
Node 22.15 veya üstü gerekir (daha eski sürümde bağımsız mod `node src/server.js` ile çalışır).

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
| `SSO_ISSUER`, `SSO_CLIENT_ID`, `SSO_IZINLI_KULLANICILAR` | (yok) | SSO modu, bkz. [SSO modu](#sso-modu) |

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

## SSO modu

Tek Kapı isteğe bağlı olarak **Tek Şifre Giriş Sistemi**'ne (bu reponun kimlik sunucusu, `https://localhost:5001`)
OpenID Connect ile bağlanabilir. Bu modda:

- Portala kimlik sunucusunun Uygulamalarım sayfasındaki **Tek Kapı** kartından (veya doğrudan) girilir; giriş
  ekranı kimlik sunucusunundur (tek giriş).
- Kimlik sunucusundan, Demo OBİS'ten veya başka bir uygulamadan çıkış yapılınca kimlik sunucusu Tek Kapı'ya
  **Back-Channel Logout** bildirimi gönderir: kasa kilitlenir, yönetilen penceredeki gerçek Selçuk sitelerinin sekmeleri
  kapanır ve çerezleri silinir ("Hepsinden çık"), SSO oturumu silinir (tek çıkış).

**İki katmanlı erişim:** önce SSO oturumu (yoksa portal `/auth/login`'e yönlendirir), sonra kasa **ana şifresi**
(kilit ekranı değişmedi). Kasa anahtarı kimlik sunucusundan türetilmez; ana şifre yine gereklidir. Kimlik sunucusu
üniversite şifrelerini **hiçbir zaman görmez**: onlar yalnızca bu bilgisayardaki şifreli kasadadır. Kimlik sunucusundan
Tek Kapı'ya yalnızca standart OIDC token'ları gelir; bunlar da yalnızca bellekte tutulur, diske ve günlüğe yazılmaz.

**Güvenlik notu:** Tek Kapı public bir OIDC istemcisidir (RFC 8252, yerel uygulama): `client_secret` yoktur, çünkü
bilgisayardaki bir uygulamada gizli tutulamaz; bunun yerine PKCE (`S256`) zorunludur. Adresler yalnızca
`http://127.0.0.1:4545` loopback'tir. state, nonce ve PKCE doğrulayıcısı sunucuda kısa süreli (10 dk) ve tek
kullanımlık tutulur; tarayıcıya yalnızca rastgele bir anahtar içeren çerez verilir. id_token imzası, `iss`, `aud`,
`nonce` ve `exp` openid-client ile; `logout_token` imzası (kimlik sunucusunun JWKS'i), `iss`, `aud`, `iat`, `typ`
(`logout+jwt`), `events`, `sid`/`sub`, `nonce` yokluğu ve `jti` tekrarı jose ile doğrulanır.

### Nasıl açılır

SSO modu `SSO_ISSUER` tanımlıysa açılır; tanımlı değilse Tek Kapı eskisi gibi tek başına (bağımsız mod) çalışır.
Ayarlar ortam değişkeninden veya `data/sso.json`'dan okunur (ortam değişkeni önceliklidir; `data/` git'e girmez):

| Ayar | Örnek | Açıklama |
|---|---|---|
| `SSO_ISSUER` | `https://localhost:5001/` | Kimlik sunucusu. https olmalı |
| `SSO_CLIENT_ID` | `tek-kapi` | Kimlik sunucusundaki istemci kaydı (varsayılan `tek-kapi`) |
| `SSO_IZINLI_KULLANICILAR` | `203011001` | Virgülle ayrılmış kullanıcı adları (varsayılan `203011001`). Kimlik sunucusu da `Istemciler:TekKapi:IzinliKullaniciAdlari` ile ayrıca kısıtlar |

```json
{ "SSO_ISSUER": "https://localhost:5001/", "SSO_CLIENT_ID": "tek-kapi", "SSO_IZINLI_KULLANICILAR": "203011001" }
```

En kolayı, reponun kökünden üç uygulamayla birlikte başlatmaktır:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi
```

Bu, Tek Kapı'yı SSO modunda ve `--no-browser` ile ayrı bir pencerede başlatır; portal Uygulamalarım'daki kartla açılır.

### TLS (kimlik sunucusunun sertifikası)

Kimlik sunucusu .NET geliştirme sertifikasıyla HTTPS kullanır. Node bu sertifikaya `--use-system-ca` bayrağıyla,
Windows sertifika deposu üzerinden güvenir (`npm start`, `TekKapi-Baslat.bat` ve `run-all.ps1 -TekKapi` bayrağı
kullanır; Node 22.15 veya üstü gerekir, v22.23.2 ile denendi). Sertifikanın depoda güvenilir olması için bir kez
`dotnet dev-certs https --trust` çalıştırılmış olmalıdır (demo uygulamalar için de gerekli). **TLS doğrulaması
hiçbir koşulda kapatılmaz** (`NODE_TLS_REJECT_UNAUTHORIZED=0` vb. kullanılmaz). Düz http yalnızca otomatik testte,
127.0.0.1'deki sahte kimlik sunucusu için açılır.

### Rotalar

| Rota | Açıklama |
|---|---|
| `GET /auth/login` | Discovery, PKCE (S256), state, nonce; kimlik sunucusuna yönlendirir. Uygulamalarım kartından gelen `iss` `SSO_ISSUER` değilse 400 |
| `GET /auth/callback` | state, PKCE ve id_token doğrulanır; kullanıcı izinli listede değilse 403; başarılıysa SSO oturumu ve ana sayfa. `error=access_denied` gelirse "Bu hesabın Tek Kapı'ya erişim izni yok" |
| `POST /auth/logout` | `x-tek-kapi` başlığı gerekir. Kasayı kilitler, "Hepsinden çık", SSO oturumunu siler; kimlik sunucusunun çıkış adresini (`id_token_hint`, `post_logout_redirect_uri`) döndürür |
| `POST /auth/backchannel-logout` | Kimlik sunucusundan gelen `logout_token` (sunucudan sunucuya; başlık ve oturum gerekmez). Geçersizse 400, JWKS'e ulaşılamazsa 503. Geçerli ve oturumla eşleşiyorsa kilitler ve çıkar; eşleşmiyorsa bir şey yapmadan 200 |

SSO çerezleri (`tk_sso_giris`, `tk_sso`) `HttpOnly; SameSite=Lax`'tir: callback kimlik sunucusundan (başka siteden)
gelen üst düzey bir yönlendirmedir ve `Strict` çerez o istekte gönderilmezdi. Kasa oturum çerezi (`tk_session`)
`SameSite=Strict` kalır; API çağrıları yine `x-tek-kapi` başlığı ister.

### Taşıma sonrası kasa

Tek Kapı `Desktop\tek-kapi`'den bu repoya (`araclar/tek-kapi`) taşındı. `data/` (kasa, tarayıcı profili,
ölçümler) bilerek taşınmadı. Yeni konumda ilk açılışta **kasa yeniden kurulur**: yeni bir ana şifre belirlenir ve
Ayarlar'dan bilgiler yeniden girilir. Eski kasa `Desktop\tek-kapi\data\` altında yedek olarak durur.

### Elle deneme

1. Reponun kökünden: `powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi`.
2. Açılan `https://localhost:5001`'de **203011001** ile giriş yapın → Uygulamalarım → **Tek Kapı** kartı.
3. Kimlik sunucusunun onay ekranında **İzin ver** → Tek Kapı'ya dönülür, üst menüde adınız ve "SSO'dan çık" görünür.
4. Kasa ana şifresi (ilk seferde kasayı oluşturun ve Ayarlar'dan bilgilerinizi girin) → ana ekran.
5. **OBİS** kartı → gerçek OBİS Tek Kapı penceresinde açılır (CAPTCHA'yı siz yazarsınız).
6. Kimlik sunucusundan (Uygulamalarım → Çıkış yap) veya Demo OBİS'ten çıkış yapın.
7. Tek Kapı penceresine dönün: kasa kilitlenmiş, gerçek site sekmeleri kapanmış olmalı; portal bir sonraki yoklamada
   SSO girişine döner. Son işlemlerde "SSO'dan çıkış yapıldı, kasa kilitlendi" görünür.

## Test

```bash
npm test
```

`test/mock-sites.js` gerçek giriş sayfalarının alan adlarını, oturuma bağlı tokenlarını, CAPTCHA ve 2FA adımlarını taklit eden sahte siteler kurar.
`test/e2e.js` bunlara karşı bağımsız modda 33 kontrol çalıştırır: kasa şifrelemesi, otomatik ve yarı otomatik girişler, hatırlanan oturumlar, ölçüm kaydı, hesap kilitletmeme koruması, "Hepsinden çık", CSV dışa aktarma, tek kopya başlatma, kilit ekranı, Host/CSRF korumaları, günlüklerde ve ölçümlerde kimlik bilgisi olmaması.

`test/sso.js` SSO modunu 13 kontrolle dener. Gerçek kimlik sunucusuna bağlanmaz: `test/fake-oidc.js` jose ile anahtar
üreten, PKCE'yi gerçekten doğrulayan sahte bir OIDC sağlayıcı (yalnızca HTTP, yalnızca test) kurar. Kontroller:
girişe yönlendirme (S256, state, nonce), tam akış ve ardından kasa kilidi, yanlış/tekrar kullanılan state, yanlış
aud/nonce/imzalı id_token, izinsiz kullanıcı ve `access_denied`, yanlış `iss`, JWKS yokken 503, geçersiz `logout_token`'lar
(imza, aud, iss, events, nonce, typ, sid/sub, iat, tekrar eden jti), başka sid'e ait bildirim, geçerli bildirimle kilit
ve sekmelerin kapanması, `POST /auth/logout`, günlüklerde ve `data/`'da token/kod/şifre olmaması. `npm test` ikisini
sırayla çalıştırır.

## Klasör yapısı

```
src/server.js    Express API, oturum, güvenlik başlıkları
src/vault.js     scrypt + AES-256-GCM kasa
src/sso.js       SSO modu: OIDC girişi (openid-client), Back-Channel Logout (jose)
src/browser.js   Playwright motoru, sekme izleme, başarısızlık koruması, ölçüm, hepsinden çık
src/sites.js     Site adaptörleri (yeni sistem eklemek için buraya bir giriş eklenir)
public/          Portal arayüzü
TekKapi-Baslat.bat  Windows'ta çift tıkla başlatma
start.py         Python ile başlatma (isteğe bağlı)
data/            Kasa, tarayıcı profili, ölçümler ve sso.json (git'e girmez)
test/            Sahte siteler, sahte OIDC sağlayıcı ve uçtan uca testler (e2e.js, sso.js)
```

## Bitirme projesi notu

İnceleme sırasında `yemek.selcuk.edu.tr`'nin girişi `sso.selcuk.edu.tr`'ye yönlendirdiği görüldü. Bu sunucu bir OpenID Connect sağlayıcısı
(`/connect/authorize`, `client_id=YemekApp`). Yani üniversitenin halihazırda gerçek bir SSO altyapısı var; OBİS, Zimbra ve UZEM ise henüz ona bağlı değil.
Tek Kapı "bugün, kurum desteği olmadan" çözümü; bu sistemleri `sso.selcuk.edu.tr`'ye OIDC/SAML ile bağlamak ise kalıcı çözümdür.
