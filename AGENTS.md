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
  - **OpenIddict Server**: authorize, token, userinfo, end-session, revocation (`/connect/revoke`, RFC 7009) ve
    discovery uç noktaları. Authorization Code + PKCE (zorunlu, yalnızca S256) ve Refresh Token akışları.
    `profile` kapsamıyla `name`, `fakulte`, `bolum`; `roles` kapsamıyla `role` claim'leri verilir.
  - **Token yaşam döngüsü**: access token imzalı JWT, 15 dk (`Tokenlar:AccessTokenSuresi`), küçültülmüş: profil/e-posta
    yok, yalnızca sub, role, scope, sid, client_id, aud (+ OpenIddict `oi_*`); profil id_token'da ve userinfo'da
    (`OidcKimlik.Hedefler`). Refresh token yalnızca `offline_access` ile (demo-obis, demo-yemekhane; tek-kapi değil),
    8 saat (`Tokenlar:RefreshTokenSuresi`), kayan; tekrar kullanım payı kapalı (`SetRefreshTokenReuseLeeway(null)`):
    kullanılmış refresh token gelirse OpenIddict zincirin tüm token'larını iptal eder, `RefreshTokenTekrarDenetimi`
    denetim kaydı yazar. Mutlak üst sınır `Tokenlar:MutlakOturumSuresi` (12 saat, ilk girişten = ad-hoc yetkilendirmenin
    oluşturulma zamanı): OpenIddict'te kayan + mutlak ayarı olmadığı için token uç noktası `Oidc/YenilemeUstSiniri` ile
    kalan süreyi hesaplar; dolduysa `invalid_grant` + `MutlakOturumSuresiDoldu` denetim kaydı, dolmadıysa refresh ve
    access token ömrü kalan süreyle kısaltılır (`SetRefreshTokenLifetime`/`SetAccessTokenLifetime`). Budama:
    `Oidc/TokenBudamaHizmeti` (BackgroundService, Quartz yok) açılışta ve `TokenBudama:Aralik` (1 gün) aralıkla
    OpenIddict `PruneAsync` (önce token, sonra yetkilendirme); yalnızca `TokenBudama:EnAzYas` (14 gün, en az mutlak
    oturum süresi) öncesi kayıtlar. Authorize her kodda kalıcı onaydan ayrı, `sid` özellikli bir ad-hoc yetkilendirme açar
    (`Oidc/TokenIptali`); çıkışta (o sid), şifre ve 2FA değişikliklerinde (mevcut sid hariç / sıfırlamada hepsi) bu
    yetkilendirmeler ve token'ları iptal edilir. `notlar.oku` kapsamı (`demo-api` kaynağı) açılışta her ortamda
    oluşturulur. `OpenIddict.Server.OpenIddictServerDispatcher` logu Warning (OpenIddict `code_verifier`'ı maskelemiyor).
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
  - **İstemci bazlı 2FA şartı**: istemci `Properties`'inde `mfaZorunlu` (bool; tek-kapi true, demolar false).
    Authorize'da oturum `amr`'si `mfa` içermiyorsa (şifre veya "Bu cihazı hatırla") 2FA'sı açık kullanıcı
    `Account/EkDogrulama`'ya (step-up: TOTP/kurtarma kodu, oturum aynı sid'le `["pwd","mfa"]`, `returnUrl` ile devam),
    kapalı kullanıcı 2FA kurulumuna (uygulama mesajıyla) gider; token uç noktası da `amr`'yi denetler (`invalid_grant`).
    Denetim: `IstemciMfaGerekli`, `EkDogrulama`. Uygulamalarım kartında "2FA gerekli".
  - **Önbellek**: `GuvenlikBasliklari` (kimlik sunucusu) ve `UseSelcukGuvenlikBasliklari` (ortak kütüphane)
    `[Authorize]` uç noktaların yanıtlarına `Cache-Control: no-store` ekler (yanıt başlarken uç nokta okunur);
    herkese açık sayfalar ve statik dosyalar etkilenmez.
  - **Uygulama bazlı erişim izni** (`Oidc/IstemciErisimi`): `izinliRoller` ve `izinliKullaniciAdlari` boşsa herkes,
    doluysa en az birine uyan kullanıcı (harf duyarsız; dizi olmayan ayar kimseye izin vermez). Authorize'da izinsiz
    kullanıcı istemciye `error=access_denied` ile döner, token uç noktası `invalid_grant` verir, Uygulamalarım kartı
    gizler; her ret `IstemciErisimiReddedildi` denetim kaydı (`client_id`, `uc=authorize|token`).
  - **EF Core + SQL Server** (geliştirmede LocalDB, veritabanı `SelcukSSO`). Identity, OpenIddict,
    `IstemciOturumlari` ve `DenetimKayitlari` tabloları aynı `ApplicationDbContext` içinde.
  - **Razor Pages** (Türkçe arayüz): Uygulamalarım (ana sayfa; kartlar `initiate_login_uri?iss=…`), Giriş, Girişin
    ikinci adımı (2FA), Şifremi unuttum, Şifre sıfırlama, Hesabım, Şifre değiştir, İki aşamalı doğrulama, Ek doğrulama
    (step-up), Güvenlik geçmişi, Onay, Çıkış, Erişim reddedildi. Tüm yanıtlarda güvenlik
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
    (`TurkceIdentityHatalari`). Ölçüm istisnası (`Hesap/OlcumKullanicilari`, `HizSiniri.UseOlcumGevsetmesi`): yalnızca
    Development + `Olcum:HizSiniriGevset=true` (varsayılan yok) iken giriş sayfasına `olcum.kullanici.01`…`50` ile gelen
    POST'lar sayılmaz; diğer kullanıcılar, sayfalar ve ortamlar aynı. Ölçüm kullanıcıları Development'ta
    `OlcumKullanicilari:Sifre` (user-secrets) varsa seed edilir (ogrenci, 2FA kapalı).
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
  400). `TokenYenileyici`: çerezdeki access token'ın süresine 1 dk'dan az kaldıysa çerez doğrulamasında refresh token
  ile yeniler (aynı refresh token için tek uçuş, sonuç 1 dk paylaşılır; invalid_grant'te yerel oturum kapanır);
  çıkışta (`OnRedirectToIdentityProviderForSignOut`) önce refresh token'ı iptal eder. Ek kapsamlar
  `Kimlik:EkKapsamlar`. Yeni bir alt uygulama bu kütüphaneyle bağlanır.
- `src/Selcuk.Demo.Obis` (https://localhost:5101): rol bazlı sayfalar (Notlarım: ogrenci, Ders Yönetimi:
  akademisyen), Profilim, yalnızca Development'ta Token İnceleme (refresh token yalnızca var/son kullanma),
  RP-initiated logout. Notlarım notları Demo API'den (`NotlarApiIstemcisi`, `DemoApi:Adres`, oturumdaki access token)
  çeker; API'ye ulaşılamazsa Türkçe hata. Veriler uydurmadır.
- `src/Selcuk.Demo.Api` (https://localhost:5201): yalnızca kaynak sunucusu, minimal API. JWT Bearer (Authority
  kimlik sunucusu, JWKS; `aud` = `demo-api`, `typ` = `at+jwt`, `IncludeErrorDetails = false`). `GET /api/notlar`:
  `scope` ⊇ `notlar.oku` ve rol `ogrenci` (yoksa 401/403); yalnızca token'daki `sub`'ın notları (`INotDeposu`).
  Güvenlik başlıkları ve `no-store`.
- `src/Selcuk.Demo.Yemekhane` (https://localhost:5102): Haftalık menü (herkese açık), Rezervasyonlarım (bellek içi),
  Yemekhane Yönetimi (personel), Profilim, RP-initiated logout. Veriler uydurmadır.
- `tests/Selcuk.Identity.Tests`: xUnit + `WebApplicationFactory` entegrasyon, uçtan uca protokol, tek giriş ve tek
  çıkış, şifre sıfırlama/değiştirme, istek sınırı ve tahmin koruması, 2FA (TOTP tekrar koruması dahil), kilit
  tutarlılığı, denetim kaydı (HMAC özeti, eksik anahtarla açılmama), güvenilen proxy, Tek Kapı (public istemci,
  loopback, back-channel), uygulama bazlı erişim izni, istemci bazlı 2FA şartı (step-up), önbellek, token budama,
  refresh mutlak sınırı ve ölçüm hız sınırı gevşetmesi testleri.
  `IstemciMfaTests` "yalnızca şifreyle açılmış oturum + 2FA açık" durumunu, 2FA'yı depoya güvenlik damgasını
  değiştirmeden yazarak kurar. `OidcYardimcisi` istemci parametreli (varsayılan
  demo-obis; public istemci için `PublicTokenIsteAsync`).
  Her test kendi adlandırılmış, paylaşımlı önbellekli SQLite in-memory veritabanını kullanır (DbContext başına
  bağlantı); ortam adı `Testing`; fabrikaya `ayarlar` ile appsettings değeri verilir; her fabrika rastgele bir HMAC
  anahtarı verir (`OzetAnahtari`). Back-Channel Logout POST'ları
  sahte HTTP handler ile, e-postalar `SahteEpostaGonderici` ile yakalanır. TOTP kodlarını `Totp` yardımcısı Identity'nin
  iç `Rfc6238AuthenticationService`'ini yansımayla çağırarak üretir; `Totp.Kodlar` farklı zaman adımlarının kodlarını
  tek bir zaman ölçümünden üretir. Denetim kayıtları arka planda yazıldığı için
  `DenetimKayitlariAsync` ile beklenir.
- `tests/Selcuk.Istemci.Ortak.Tests`: Back-Channel Logout alıcısı, çerez süresi, ortak dağıtık önbellek, JWKS hatası
  (503), `iss` doğrulaması ve token yenileme/iptal testleri; yerel RSA anahtarı, sahte JWKS ve sahte token/revocation
  uç noktaları (`TestUygulamasi.TokenUcu`).
- `tests/Selcuk.Demo.Api.Tests`: Demo API token doğrulama ve yetkilendirme; yerel RSA anahtarı, sabit discovery.
- `tests/Selcuk.Demo.Obis.Tests`, `tests/Selcuk.Demo.Yemekhane.Tests`: yönlendirme, rol ve önbellek (`no-store`) testleri; gerçek kimlik
  sunucusu yerine sabit discovery bilgisi ve sahte kimlik doğrulama şeması kullanır.
- `araclar/tek-kapi` (http://127.0.0.1:4545): Tek Kapı, kullanıcının kendi bilgisayarında çalışan Node.js portalı
  (Express + Playwright; kişisel ESSO, gerçek Selçuk sistemlerine form tabanlı otomatik giriş). Kendi kuralları
  `araclar/tek-kapi/AGENTS.md`'de. Üniversite şifreleri yalnızca yereldeki şifreli kasada (`data/`, git'e girmez).
  İsteğe bağlı SSO modu (`SSO_ISSUER`): kimlik sunucusuna `tek-kapi` public istemcisi olarak bağlanır (openid-client,
  PKCE S256), erişim iki katmanlı (SSO oturumu + kasa ana şifresi), Back-Channel Logout gelince kasayı kilitler ve
  gerçek site oturumlarını kapatır (jose ile logout_token doğrulama). TLS: `node --use-system-ca` (doğrulama
  kapatılmaz). `SSO_MFA_ZORUNLU` (varsayılan açık): id_token `amr`'si `mfa` içermezse oturum açılmaz. Testleri
  (`npm test`: bağımsız mod 33 + SSO 14 kontrol) sahte siteler (5600-5606) ve sahte OIDC sağlayıcıyla (5190) çalışır.
  Node 22.15+ (`engines`), express 4.22.3 (tam sürüm).
- `tools/Selcuk.Olcum`: otomatik ölçüm aracı (konsol, `FrameworkReference Microsoft.AspNetCore.App`, yeni NuGet yok).
  Yalnızca localhost/127.0.0.1/::1 + https hedefler (`YerelAg.Dogrula`; her istek `YalnizcaYerelHandler`'dan geçer,
  proxy yok). HttpClient + `CookieContainer` ile OIDC akışı (`TarayiciOturumu`: yönlendirme, giriş/onay/çıkış
  formları, ilk formun gizli alanları). Senaryolar (a) soğuk giriş, (b) tek giriş, (c) token yenileme ve (e) Demo API
  (aracın kendi demo-obis token'ıyla), (d) tek çıkış, (f) 1/10/25/50 eşzamanlı soğuk giriş; önce hazırlık (her ölçüm
  kullanıcısı bir kez girip çıkar), ısınma turları özete girmez. Şifre ve demo-obis secret'ı kimlik sunucusunun
  user-secrets'ından (aynı `UserSecretsId`). Çıktı `docs/olcumler/sso-olcumler-<tarih>.csv/.json/.md` (kullanıcı adı,
  şifre, token yok).
- `tests/Selcuk.Olcum.Tests`: localhost dışı reddi, form ayrıştırma, istatistik; uçtan uca test dört web uygulamasını
  `WebApplicationFactory` ile aynı süreçte çalıştırır (`extern alias` kimlik/obis/yemekhane/api; porta göre TestServer
  yönlendirici, ağ yok).
- `docs/`: `mimari.md` (Mermaid), `guvenlik-karsilastirma.md`, `tehdit-modeli.md` (STRIDE, gerçek test adları, açık
  riskler), `test-kapsami.md` (`--list-tests`, `npm test`, coverlet), `olcumler/` (ölçüm çıktıları, elle ölçüm şablonu
  ve protokolü, Tek Kapı CSV yönergesi, `karsilastirma.md` + PNG), `kullanilabilirlik/sus-anketi.md`. `ornek-` önekli
  dosyalar gerçek veri değildir, analiz okumaz. Belgelere ölçülmemiş sayı yazılmaz ("henüz ölçülmedi").
- `scripts/olcum-calistir.ps1`: portlar doluysa durur → build → `run-all.ps1 -TarayiciAcma` (`Olcum__HizSiniriGevset`
  yalnızca başlattığı süreçlere) → ölçüm → yalnızca kendi açtığı pencereleri kapatır. `scripts/olcum_analiz.py`:
  Python 3 standart kütüphane (matplotlib varsa PNG, yoksa uyarı; pip yok), `--self-test`.
- `scripts/run-all.ps1`: önce `dotnet build SelcukSSO.sln` (başarısızsa durur), sonra kimlik sunucusu, iki demo ve
  Demo API'yi ayrı pencerelerde `dotnet run --no-build` ile başlatır; `-TekKapi` ile Tek Kapı'yı da SSO modunda
  başlatır; `-TarayiciAcma` ile sonda tarayıcı açılmaz.

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
dotnet run --project src/Selcuk.Demo.Api                                   # https://localhost:5201
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1               # hepsi birlikte
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi      # + Tek Kapı (SSO modu, :4545)
cd araclar\tek-kapi; npm ci; npm test                                       # Tek Kapı testleri
powershell -ExecutionPolicy Bypass -File scripts\olcum-calistir.ps1         # otomatik ölçüm (docs/olcumler/)
python scripts/olcum_analiz.py                                              # karsilastirma.md (--self-test)
dotnet tool restore
dotnet ef migrations add <Ad> --project src/Selcuk.Identity --output-dir Data/Migrations
dotnet ef database update --project src/Selcuk.Identity
```
