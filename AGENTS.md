# AGENTS.md

Bu dosya, bu depoda çalışan yapay zekâ kodlama ajanları ve geliştiriciler için yönergeleri içerir.

## Projenin amacı

"Tek Şifre Giriş Sistemi" bir bitirme projesidir: fakülte ve bölüm web uygulamaları için merkezi bir kimlik
doğrulama ve yetkilendirme sunucusu. Öğrenciler, akademisyenler ve idari personel tek kullanıcı adı ve şifreyle tüm
alt uygulamalara OpenID Connect üzerinden giriş yapar. Oturum yönetimi token tabanlıdır (JWT).

## Mimari

- `src/Selcuk.Identity`: ASP.NET Core (.NET 10) kimlik sunucusu.
  - **ASP.NET Core Identity**: kullanıcılar (`ApplicationUser`: `AdSoyad`, `Fakulte`, `Bolum`), roller
    (`ogrenci`, `akademisyen`, `personel`), şifre politikası, hesap kilitleme.
  - **OpenIddict Server**: authorize, token, userinfo, end-session ve discovery uç noktaları.
    Authorization Code + PKCE (zorunlu, yalnızca S256) ve Refresh Token akışları. Access token'lar imzalı JWT.
    `profile` kapsamıyla `name`, `fakulte`, `bolum`; `roles` kapsamıyla `role` claim'leri verilir.
  - **Oturum ve tek çıkış**: her kimlik sunucusu oturumuna bir `sid` verilir (`Oidc/OturumKimligi.cs`) ve id_token'a
    yazılır. Token uç noktası hangi istemciye token verildiğini `IstemciOturumlari` tablosuna kaydeder. Çıkışta
    `BackChannelLogoutGonderici` o sid'deki her istemcinin `backchannel_logout_uri`'sine imzalı `logout+jwt`
    gönderir (OIDC Back-Channel Logout 1.0). Discovery'de `backchannel_logout_supported` ilan edilir.
  - **İstemciler**: tanımlar `Data/Istemciler.cs` içinde. `demo-obis` ve `demo-yemekhane` (gizli istemci, PKCE
    zorunlu, explicit onay) Development'ta açılışta oluşturulur veya güncellenir; secret'lar user-secrets'tan
    okunur. `tek-kapi`: kullanıcının bilgisayarındaki ayrı Node.js köprüsü (bu depoda değil; ESSO geçiş aracı, kimlik
    sunucusu üniversite şifrelerini görmez); public istemci (secret yok), native, PKCE zorunlu, explicit onay,
    `openid profile roles`, adresler `http://127.0.0.1:4545/...` (RFC 8252 loopback); izinli kullanıcılar
    `Istemciler:TekKapi:IzinliKullaniciAdlari` (appsettings.Development.json; boşsa varsayılan `["203011001"]`).
    Özel ayarlar (`backchannel_logout_uri`, `client_uri`, `initiate_login_uri`, `aciklama`, `izinliRoller`,
    `izinliKullaniciAdlari`) OpenIddict `Properties` alanında. Adres kuralı: https veya yalnızca `http://127.0.0.1`
    (kayıtta `Istemciler.AdresleriDogrula`; authorize `redirect_uri` ve end-session `post_logout_redirect_uri` için
    OpenIddict doğrulama işleyicisi, `Program.cs`). Back-Channel Logout da yalnızca https veya `http://127.0.0.1`'e
    gönderilir.
  - **Uygulama bazlı erişim izni** (`Oidc/IstemciErisimi`): `izinliRoller` ve `izinliKullaniciAdlari` boşsa herkes,
    doluysa en az birine uyan kullanıcı (harf duyarsız; dizi olmayan ayar kimseye izin vermez). Authorize'da izinsiz
    kullanıcı istemciye `error=access_denied` ile döner, token uç noktası `invalid_grant` verir, Uygulamalarım kartı
    gizler; her ret `IstemciErisimiReddedildi` denetim kaydı (`client_id`, `uc=authorize|token`).
  - **EF Core + SQL Server** (geliştirmede LocalDB, veritabanı `SelcukSSO`). Identity, OpenIddict,
    `IstemciOturumlari` ve `DenetimKayitlari` tabloları aynı `ApplicationDbContext` içinde.
  - **Razor Pages** (Türkçe arayüz): Uygulamalarım (ana sayfa; kartlar `initiate_login_uri?iss=…`), Giriş, Girişin
    ikinci adımı (2FA), Şifremi unuttum, Şifre sıfırlama, Hesabım, Şifre değiştir, İki aşamalı doğrulama, Güvenlik
    geçmişi, Onay, Çıkış, Erişim reddedildi. Tüm yanıtlarda güvenlik
    başlıkları (`GuvenlikBasliklari.cs`: nosniff, no-referrer, X-Frame-Options DENY, CSP).
  - **Şifre sıfırlama ve değiştirme** (`Hesap/`): Identity'nin yerleşik `DataProtectorTokenProvider`'ı ayrı adla
    (`SifreSifirlamaTokenSaglayici`, 30 dk, güvenlik damgasına bağlı, tek kullanımlık). Bağlantı `OpenIddict:Issuer`
    adresinden üretilir. Başarıda `SifreDegisikligi`: kilit/sayaç sıfırlama (yalnızca sıfırlamada), kullanıcının
    oturumlarına Back-Channel Logout (`BackChannelLogoutGonderici.KullaniciOturumlariniKapatAsync`; değiştirmede
    mevcut sid hariç), bilgilendirme e-postası. `OturumDamgasi` kimlik sunucusu çerezinin güvenlik damgasını her
    istekte doğrular (çerezi yenilemeden).
  - **Kaba kuvvet koruması**: `HizSiniri` (ASP.NET Core rate limiter, IP + sayfa başına dakikada 10 POST: giriş,
    girişin ikinci adımı, 2FA kurulum/yönetim, şifremi unuttum, sıfırlama, şifre değiştirme; 429 + Türkçe sayfa +
    denetim kaydı). Girişte kullanıcı yoksa veya hesap kilitliyse `SahteSifreDogrulayici` aynı maliyette sahte PBKDF2
    doğrulaması yapar; şifremi unuttum her durumda aynı işi yapar. 5 deneme / 15 dk kilitleme; yanlış TOTP/kurtarma
    kodu ve şifre değiştirmede yanlış mevcut şifre de sayılır. Kilit yalnızca yeni girişleri engeller: authorize ve
    token uç noktası kilidi kontrol etmez, kilitten önce açılmış oturum token almaya devam eder (hizmet engellemeye karşı). Identity'nin şifre hataları Türkçe
    (`TurkceIdentityHatalari`).
  - **İki aşamalı doğrulama** (`Hesap/IkiAdimIslemleri`, sayfalar `Account/IkiAdim` ve `Account/IkiAdimDogrulama`):
    Identity'nin `AuthenticatorTokenProvider`'ı (TOTP). Anahtar metin + QR (QRCoder, sunucuda SVG, data URI; CSP
    değişmedi), kod doğrulanmadan açılmaz, 10 kurtarma kodu bir kez gösterilir. `KurtarmaKoduOzetDeposu`
    (`UserStore` alt sınıfı) kodları `IPasswordHasher` özetiyle saklar. Açma/kapatma/kod yenilemede damga yenilenir,
    diğer oturumlara Back-Channel Logout, e-posta; "Kurulumu başlat" (anahtar sıfırlama) da diğer oturumlara
    Back-Channel Logout gönderir, `IkiAdimKurulumuBaslatildi` denetim kaydı ve e-posta. Kapatma ve yenileme şifre +
    TOTP ister. `TekKullanimlikTotpSaglayici` (`DefaultAuthenticatorProvider`): kabul edilen TOTP kodu 3 dk tekrar
    kabul edilmez (kurulumdaki doğrulama, giriş, kapatma, yenileme);
    kodun HMAC'i ve kabul zamanı `AspNetUserTokens`'ta (`[SelcukSSO]`/`KullanilanTotpKodlari`). "Bu cihazı hatırla":
    Identity `TwoFactorRememberMe` (`IkiAdimCihazHatirlamaGun`, 30). `IkiAdimZorunluRoller`: kurulum bitmeden
    authorize ve token uç noktası reddeder. id_token'da `amr` JSON dizisi: `["pwd"]` / `["pwd","mfa"]`
    (`OidcKimlik.AmrDegerleri`); oturum yenilemede `OturumKimligi` korur.
  - **Denetim kaydı** (`Denetim/`, tablo `DenetimKayitlari`): `DenetimKaydedici` kuyruğa bırakır,
    `DenetimYazmaHizmeti` arka planda yazar (hata yalnızca loglanır), `DenetimTemizlemeHizmeti` günde bir kez
    `DenetimKaydiSaklamaGun` (180) günden eskileri siler. Sır (şifre, kod, token, çerez) yazılmaz; olmayan kullanıcı adı
    yalnızca `AnahtarliOzet` ile (`h1:` + HMAC-SHA256; anahtar `Denetim:KullaniciAdiOzetAnahtari`, user-secrets;
    Development dışında yoksa uygulama açılmaz; eski kayıtlarda `kullanici_adi_sha256=`). Hesabım > Güvenlik geçmişi: kullanıcının son 20 olayı.
  - **İstemci IP'si ve şema**: `GuvenilenProxyler` (varsayılan boş) dışındaki adreslerden gelen `X-Forwarded-For` ve
    `X-Forwarded-Proto` yok sayılır; istek sınırı ve denetim kaydı `RemoteIpAddress`'i kullanır.
  - **E-posta** (`Eposta/`): `IEpostaGonderici`; Development'ta `DosyaEpostaGonderici` (`App_Data/eposta/*.eml`,
    .gitignore'da), diğer ortamlarda `SmtpEpostaGonderici` (`Eposta:Smtp`, şifre user-secrets/ortam değişkeni).
    Sayfalar `EpostaKuyrugu`'na bırakır, `EpostaGonderimHizmeti` arka planda gönderir. Şablonlar düz metin, Türkçe.
  - **Oturum süresi**: kimlik sunucusu çerezi `Oturum:Sure` (varsayılan 8 saat, kayan).
- `src/Selcuk.Istemci.Ortak`: alt uygulamaların ortak kütüphanesi. `AddSelcukSSO(configuration)` (Cookie + OIDC,
  code + PKCE, `response_mode=query`, SaveTokens, userinfo, claim eşlemeleri, `DefaultForbidScheme` = cookie),
  `UseSelcukGuvenlikBasliklari()`, `MapSelcukBackChannelLogout()` (POST `/signout-backchannel`: logout_token
  doğrulama, jti tekrar koruması, sid iptali; discovery/JWKS alınamazsa 503) ve `OturumIptalDeposu` (iptal edilen
  oturumu çerez doğrulamasında reddeder; kayıtlar `IDistributedCache`'te, varsayılan bellek içi, en az çerez süresi
  kadar). Çerez süresi `Kimlik:OturumSuresi` (varsayılan 8 saat, kayan). `GirisDonusHatasi` `/signin-oidc`
  hatalarını `/GirisHatasi`'na yönlendirir; `UcuncuTarafGiris` initiate_login isteğindeki `iss`'i doğrular (yanlışsa
  400). Yeni bir alt uygulama bu kütüphaneyle bağlanır.
- `src/Selcuk.Demo.Obis` (https://localhost:5101): rol bazlı sayfalar (Notlarım: ogrenci, Ders Yönetimi:
  akademisyen), Profilim, yalnızca Development'ta Token İnceleme, RP-initiated logout. Veriler uydurmadır.
- `src/Selcuk.Demo.Yemekhane` (https://localhost:5102): Haftalık menü (herkese açık), Rezervasyonlarım (bellek içi),
  Yemekhane Yönetimi (personel), Profilim, RP-initiated logout. Veriler uydurmadır.
- `tests/Selcuk.Identity.Tests`: xUnit + `WebApplicationFactory` entegrasyon, uçtan uca protokol, tek giriş ve tek
  çıkış, şifre sıfırlama/değiştirme, istek sınırı ve tahmin koruması, 2FA (TOTP tekrar koruması dahil), kilit
  tutarlılığı, denetim kaydı (HMAC özeti, eksik anahtarla açılmama), güvenilen proxy, Tek Kapı (public istemci,
  loopback, back-channel) ve uygulama bazlı erişim izni testleri. `OidcYardimcisi` istemci parametreli (varsayılan
  demo-obis; public istemci için `PublicTokenIsteAsync`).
  Her test kendi adlandırılmış, paylaşımlı önbellekli SQLite in-memory veritabanını kullanır (DbContext başına
  bağlantı); ortam adı `Testing`; fabrikaya `ayarlar` ile appsettings değeri verilir; her fabrika rastgele bir HMAC
  anahtarı verir (`OzetAnahtari`). Back-Channel Logout POST'ları
  sahte HTTP handler ile, e-postalar `SahteEpostaGonderici` ile yakalanır. TOTP kodlarını `Totp` yardımcısı Identity'nin
  iç `Rfc6238AuthenticationService`'ini yansımayla çağırarak üretir; `Totp.Kodlar` farklı zaman adımlarının kodlarını
  tek bir zaman ölçümünden üretir. Denetim kayıtları arka planda yazıldığı için
  `DenetimKayitlariAsync` ile beklenir.
- `tests/Selcuk.Istemci.Ortak.Tests`: Back-Channel Logout alıcısı, çerez süresi, ortak dağıtık önbellek, JWKS hatası
  (503) ve `iss` doğrulaması testleri; yerel RSA anahtarı ve sahte JWKS.
- `tests/Selcuk.Demo.Obis.Tests`, `tests/Selcuk.Demo.Yemekhane.Tests`: yönlendirme ve rol testleri; gerçek kimlik
  sunucusu yerine sabit discovery bilgisi ve sahte kimlik doğrulama şeması kullanır.
- `araclar/tek-kapi` (http://127.0.0.1:4545): Tek Kapı, kullanıcının kendi bilgisayarında çalışan Node.js portalı
  (Express + Playwright; kişisel ESSO, gerçek Selçuk sistemlerine form tabanlı otomatik giriş). Kendi kuralları
  `araclar/tek-kapi/AGENTS.md`'de. Üniversite şifreleri yalnızca yereldeki şifreli kasada (`data/`, git'e girmez).
  İsteğe bağlı SSO modu (`SSO_ISSUER`): kimlik sunucusuna `tek-kapi` public istemcisi olarak bağlanır (openid-client,
  PKCE S256), erişim iki katmanlı (SSO oturumu + kasa ana şifresi), Back-Channel Logout gelince kasayı kilitler ve
  gerçek site oturumlarını kapatır (jose ile logout_token doğrulama). TLS: `node --use-system-ca` (doğrulama
  kapatılmaz). Testleri (`npm test`: bağımsız mod 33 + SSO 13 kontrol) sahte siteler ve sahte OIDC sağlayıcıyla
  çalışır.
- `scripts/run-all.ps1`: üç uygulamayı ayrı pencerelerde başlatır; `-TekKapi` ile Tek Kapı'yı da SSO modunda başlatır.

Ayrıntılar için `README.md` dosyasına bakın.

## Değişmez kurallar

a) **Kapsam dışına çıkma.** Yalnızca istenen değişikliği yap. İstenmeyen refactor yapma, istenmeyen bağımlılık
   (NuGet paketi, araç, servis) ekleme. Gerekli gördüğün ek işleri yapmak yerine öneri olarak bildir.

b) **Şifreler ve sırlar koda, loglara ve repoya girmez.** Geliştirmede `dotnet user-secrets`, diğer ortamlarda ortam
   değişkenleri veya güvenli bir sır deposu kullanılır. Şifreleri, token'ları veya bağlantı sırlarını loglama, çıktıya
   yazdırma ve commit'leme.

c) **Kimlik sunucusu ve demo uygulamalar gerçek Selçuk sistemlerine bağlanmaz.** İstisna: `araclar/tek-kapi`,
   kullanıcının kendi hesabıyla, kendi bilgisayarında çalışan kişisel ESSO aracıdır. Kuralları
   `araclar/tek-kapi/AGENTS.md`'dedir. Hiçbir test gerçek Selçuk sistemlerine istek atmaz. Kimlik sunucusu ve demo
   uygulamaların test verileri kurgusaldır, e-posta adresleri `@test.local` uzantılıdır; gerçek öğrenci numaraları
   veya hesaplar kullanılmaz.

d) **OIDC/OAuth2 standardından sapılmaz.** PKCE zorunludur. Implicit flow yoktur. Password (ROPC) grant yoktur.
   Yeni akış veya uç nokta eklenecekse ilgili RFC ve OpenID Connect belirtimine uyulur.

e) **Kriptografi elle yazılmaz.** Şifre özetleme, token imzalama/şifreleme ve anahtar yönetimi için ASP.NET Core
   Identity ve OpenIddict'in sağladığı mekanizmalar kullanılır.

f) **Her değişiklikten sonra `dotnet build` ve `dotnet test` geçmelidir.**

g) **Her adım sonunda sır kontrolünden geçmiş tek bir commit atılır ve origin/main'e push edilir; force push ve
   geçmişi yeniden yazma yasak.**

h) **Başlarken 5001/5101/5102 portlarında çalışan uygulama varsa dur ve raporla; kullanıcının süreçlerini kapatma.**

## Sık kullanılan komutlar

```powershell
dotnet build
dotnet test
dotnet run --project src/Selcuk.Identity                                   # https://localhost:5001
dotnet run --project src/Selcuk.Demo.Obis                                  # https://localhost:5101
dotnet run --project src/Selcuk.Demo.Yemekhane                             # https://localhost:5102
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1               # üçü birlikte
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi      # + Tek Kapı (SSO modu, :4545)
cd araclar\tek-kapi; npm ci; npm test                                       # Tek Kapı testleri
dotnet tool restore
dotnet ef migrations add <Ad> --project src/Selcuk.Identity --output-dir Data/Migrations
dotnet ef database update --project src/Selcuk.Identity
```
