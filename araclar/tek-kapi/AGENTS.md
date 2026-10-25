# AGENTS.md — Tek Kapı

Bu dosya bu projede çalışan her kod agent'ı (Copilot, Claude Code, Codex, Cursor, Cline, Gemini, Antigravity) için talimattır. Her göreve başlamadan önce oku.

## Proje nedir

Selçuk Üniversitesi otomasyonlarına (OBİS, öğrenci e-postası, UZEM, Yemekhane/SSO) tek ana şifreyle giriş yapan, **yalnızca yerel bilgisayarda çalışan** bir portal.
Bitirme projesinin "bugün kurum desteği olmadan çalışan" parçasıdır (form tabanlı SSO / ESSO).

- Node.js 18+ (CommonJS), Express, Playwright. Frontend vanilla HTML/CSS/JS, framework yok, build adımı yok.
- Geliştirme ortamı Windows (PowerShell). Komutları buna göre yaz.

## Mimari

```
src/server.js    Express API (127.0.0.1:4545), oturum çerezi, güvenlik başlıkları
src/vault.js     scrypt + AES-256-GCM şifreli kasa -> data/vault.json
src/browser.js   Playwright kalıcı profil (data/browser-profile), sekme izleme, otomatik doldurma, başarısızlık koruması,
                 ölçüm (attempt -> onMeasure), logoutAll (site çerezleri + sekmeler), focus
src/sites.js     Site adaptörleri: { id, startUrl, origins, appOrigins?, mode: 'auto'|'assist', userSteps?, needs, loginPage: { match, detect, fill } }
                 appOrigins: "giriş tamam" sayılacak kökenler (ör. yemekhane için SSO'nun ara sayfaları sayılmaz)
public/          Portal arayüzü (index.html, app.js, style.css)
test/            mock-sites.js (gerçek formları taklit eden sahte siteler) + e2e.js (uçtan uca test)
TekKapi-Baslat.bat, start.py   Başlatıcılar. Port doluysa server.js /api/focus ile mevcut pencereyi öne getirip çıkar
data/metrics.csv Ölçüm kaydı (server.js recordMetric). Sadece süre ve sonuç; kimlik bilgisi asla yazılmaz
```

API: /api/state, /api/setup, /api/unlock, /api/lock, /api/sites, /api/credentials (GET/PUT), /api/open/:id, /api/logout-all,
/api/focus, /api/metrics/summary, /api/events, GET /export/metrics.csv. Test modunda ek olarak /api/_debug/*.

Akış: Kullanıcı portalda karta tıklar → `POST /api/open/:id` → `BrowserEngine.open` siteyi yönetilen pencerede açar → her `load` olayında `_handle` URL'ye uyan adaptörü bulur → `detect` giriş formunu görürse `fill` çalışır.

## Gerçek giriş formları (18.10.2026'da canlı sayfalardan okundu)

| Site | URL | Alanlar | Not |
|---|---|---|---|
| OBİS | obis.selcuk.edu.tr → obis2.selcuk.edu.tr, POST /Home/Index | `id`, `pass`, `#TxtCaptcha` | Resimli CAPTCHA → mode `assist` |
| Zimbra | ogrposta.selcuk.edu.tr, POST / | `loginOp`, `login_csrf` (oturuma bağlı), `username`, `password`, `zrememberme`, `client` | mode `auto` |
| UZEM (Moodle) | uzaktanogrenme5.selcuk.edu.tr/login/index.php | `logintoken` (oturuma bağlı), `username` (öğrenci no), `password` (OBİS şifresi) | mode `auto` |
| Uzaktan Sınav (Moodle) | uzaktansinav.selcuk.edu.tr/login/index.php | `logintoken` (oturuma bağlı), `username` (öğrenci no), `password` (OBİS şifresi) | mode `auto` |
| Selçuk SSO | yemek.selcuk.edu.tr → sso.selcuk.edu.tr/Account/Login | `Username` (e-postanın @ öncesi), `EmailDomain` (select), `Password`, `g-recaptcha-response`, `__RequestVerificationToken` | IdentityServer OIDC + reCAPTCHA v3 + zorunlu 2FA → mode `assist` |

Bir site formunu değiştirirse yalnızca `src/sites.js` içindeki o adaptörü ve `test/mock-sites.js` içindeki karşılığını güncelle.

## Değişmez kurallar

1. **Kapsam dışına çıkma.** Sadece görevde istenen dosyalara ve satırlara dokun. İstenmeyen refactor, yeniden adlandırma, biçimlendirme, bağımlılık ekleme/güncelleme yapma. Gerekli gördüğün ek bir değişiklik varsa yapma, sonunda öneri olarak yaz.
2. **CAPTCHA, reCAPTCHA ve 2FA atlatılmaz.** OCR, CAPTCHA çözme servisi, stealth eklentisi, `navigator.webdriver` gizleme, user-agent sahteciliği gibi bot tespitini aşan hiçbir şey ekleme. Bu adımlar her zaman kullanıcıda kalır.
3. **Şifreler asla dışarı çıkmaz.** Ölçüm kaydına da şifre, kullanıcı adı veya e-posta yazma. Şifreleri loglama, `console.log` ile yazdırma, API yanıtına koyma, URL'ye koyma, dosyaya düz metin yazma. Kasa formatını (`vault.js`) değiştirirsen geriye dönük uyumluluğu düşün ve testleri güncelle.
4. **Hesap kilitletmeme koruması korunur.** `browser.js` içindeki "gönderimden sonra yine giriş sayfası → failed, bir daha gönderme" mantığını kaldırma veya zayıflatma.
5. **Sunucu yalnızca 127.0.0.1'de dinler.** Host kontrolü, `x-tek-kapi` başlığı, `HttpOnly; SameSite=Strict` çerez ve CSP kaldırılmaz.
6. **Gerçek kimlik bilgisi kullanma.** Test ve örneklerde yalnızca `test/mock-sites.js` içindeki sahte kullanıcıyı kullan. Gerçek siteye otomatik istek atan test yazma.
7. `data/` klasörü (kasa ve tarayıcı profili) git'e girmez, içeriğini okuma ve değiştirme.

## Doğrulama

Her değişiklikten sonra:

```powershell
npm test
```

33 kontrolün hepsi geçmeli. Yeni bir davranış eklediysen `test/e2e.js`'e onu doğrulayan bir kontrol ekle. Testi geçmeyen bir değişikliği "bitti" diye raporlama.

## Çıktı biçimi

Görev sonunda kısaca şunları yaz: hangi dosyalarda ne değişti, `npm test` sonucu, varsa kapsam dışı bıraktığın öneriler.
