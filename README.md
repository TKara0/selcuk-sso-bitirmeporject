# Tek Şifre Giriş Sistemi (SelcukSSO)

Bitirme projesi: fakülte ve bölüm web uygulamaları için merkezi kimlik sunucusu.

> **Proje tanımı (bölümün verdiği):** "Fakülte ve bölüm genelinde kullanılan tüm web otomasyonları için merkezi
> bir kimlik doğrulama ve yetkilendirme altyapısı sunar. Kullanıcılar (öğrenci, akademisyen, idari personel) tek bir
> kullanıcı adı ve şifre ile güvenli bir şekilde tüm alt uygulamalara giriş yapabilir. Token tabanlı (JWT vb.) oturum
> yönetimini, şifre sıfırlama vb. özelliklere sahiptir."

Depoda şu an **kimlik sunucusu** ve ona OpenID Connect ile bağlanan ilk alt uygulama **Demo OBİS** var. Diğer
özellikler [yol haritasında](#yapılacaklar-yol-haritası).

## Mimari özet

```
 Tarayıcı
    │  1) Notlarım'a git (giriş yok)
    ▼
 ┌────────────────────────────┐ 2) 302 /connect/authorize?client_id=demo-obis   ┌──────────────────────────────┐
 │ Selcuk.Demo.Obis  :5101    │    &code_challenge=…&code_challenge_method=S256 │ Selcuk.Identity  :5001       │
 │  Razor Pages, Cookie +     │ ───────────────────────────────────────────────►│  Giriş → Onay ekranı          │
 │  OpenID Connect istemcisi  │ 3) 302 /signin-oidc?code=…&state=…&iss=…        │  ASP.NET Core Identity        │
 │  (gizli istemci)           │ ◄───────────────────────────────────────────────│  OpenIddict Server (OIDC)     │
 │                            │ 4) POST /connect/token                          │  EF Core ──► LocalDB          │
 │                            │    code + code_verifier + client_secret ───────►│                              │
 │                            │ 5) id_token + access_token (imzalı JWT)  ◄──────│                              │
 │                            │ 6) GET /connect/userinfo (Bearer)  ────────────►│                              │
 └────────────────────────────┘                                                 └──────────────────────────────┘
```

| Uç nokta | Adres | Açıklama |
|---|---|---|
| Discovery | `/.well-known/openid-configuration` | OIDC meta verisi |
| JWKS | `/.well-known/jwks` | İmza doğrulama anahtarları |
| Authorize | `/connect/authorize` | Giriş ve onay ekranı (Razor Page) |
| Token | `/connect/token` | `authorization_code` ve `refresh_token` grant'leri |
| UserInfo | `/connect/userinfo` | Kapsama göre kullanıcı bilgisi |
| End-session | `/connect/logout` | Çıkış sayfası, `post_logout_redirect_uri` ile istemciye döner |

**Akışlar:** yalnızca Authorization Code + PKCE ve Refresh Token. PKCE sunucu genelinde zorunlu ve **yalnızca
`S256`** kabul edilir (`plain` kapalı). Implicit, hybrid, password ve client credentials akışları kapalı.

**Kapsamlar ve claim'ler:**

| Kapsam | id_token, access_token ve userinfo'ya giren claim'ler |
|---|---|
| `openid` | `sub` |
| `profile` | `name` (AdSoyad), `preferred_username` (öğrenci no / kullanıcı adı), `fakulte`, `bolum` |
| `email` | `email` (userinfo'da ayrıca `email_verified`) |
| `roles` | `role` (`ogrenci`, `akademisyen`, `personel`) |

Claim'ler yalnızca kullanıcı ilgili kapsama onay verdiyse tokenlara yazılır. Access token'lar **imzalı ama şifresiz
JWT** (`typ: at+jwt`, `DisableAccessTokenEncryption`), içerikleri tezde gösterilebilir. Henüz ayrı bir API (kaynak
sunucusu) tanımlı olmadığı için access token'da `aud` yoktur; tokenın verildiği istemci `client_id` claim'inde
taşınır. Authorization code ve refresh token'lar OpenIddict tarafından şifreli tutulur.

**Kullanıcı modeli:** `ApplicationUser : IdentityUser` + `AdSoyad`, `Fakulte`, `Bolum`. Öğrencilerin `UserName`'i
öğrenci numarası, akademik/idari personelinki kurumsal kullanıcı adıdır.

**Kayıtlı istemciler:** `demo-obis` (Development'ta açılışta oluşturulur veya güncellenir; tanım
`src/Selcuk.Identity/Data/Istemciler.cs`):

| Alan | Değer |
|---|---|
| Tür | Gizli (confidential) web istemcisi, `client_secret` user-secrets'ta |
| Redirect URI | `https://localhost:5101/signin-oidc` |
| Post-logout redirect URI | `https://localhost:5101/signout-callback-oidc` |
| İzinler | authorization, token, end-session uç noktaları; `authorization_code`; `code`; `openid profile email roles` |
| Gereksinim | PKCE zorunlu |
| Onay | `explicit`: ilk girişte onay ekranı, onay kalıcı yetkilendirme olarak saklanır |

**Güvenlik ayarları:**
- Şifre: en az 10 karakter, büyük harf, küçük harf ve rakam zorunlu.
- 5 hatalı denemede hesap 15 dakika kilitlenir. Yanlış şifre, var olmayan kullanıcı ve kilitli hesap için **aynı**
  genel mesaj gösterilir, bu yüzden mesajdan hesabın var olup olmadığı anlaşılmaz.
- Antiforgery tüm formlarda açık. HTTPS yönlendirmesi ve OpenIddict'in HTTPS zorunluluğu açık.
- HSTS Development dışındaki ortamlarda açık (ASP.NET Core varsayılanı; tarayıcının `localhost` için HSTS'yi
  önbelleğe almaması için Development'ta kapalı).
- Kimlik sunucusunun tüm yanıtlarında güvenlik başlıkları: `X-Content-Type-Options: nosniff`,
  `Referrer-Policy: no-referrer`, `X-Frame-Options: DENY`,
  `Content-Security-Policy: default-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'`.
  `form-action` bilerek kısıtlanmadı, çünkü giriş, onay ve çıkış formları istemcinin adresine yönlendirmeyle biter.
  Demo OBİS `response_mode=query` kullanır; `form_post` yanıtının satır içi betiği bu CSP'ye takılırdı.
- Çerezler: `Secure`, `HttpOnly`. Oturum çerezi `SameSite=Lax` (istemciden gelen yönlendirmede gönderilebilmesi
  için), antiforgery çerezi `SameSite=Strict`.

### Demo OBİS

`src/Selcuk.Demo.Obis`, https://localhost:5101. Cookie + `Microsoft.AspNetCore.Authentication.OpenIdConnect`
(`response_type=code`, PKCE, `SaveTokens`, userinfo'dan claim alma, `name`/`role` claim eşlemesi). Veriler
uydurmadır ve bellekte tutulur.

| Sayfa | Erişim |
|---|---|
| Ana sayfa (`/`) | Herkese açık, "Giriş yap" butonu |
| Notlarım (`/Notlarim`) | Yalnızca `ogrenci` |
| Ders Yönetimi (`/DersYonetimi`) | Yalnızca `akademisyen` |
| Profilim (`/Profilim`) | Giriş yapmış herkes: ad soyad, rol, fakülte, bölüm |
| Token İnceleme (`/TokenInceleme`) | Yalnızca Development: id_token ve access_token'ın header/payload'u (imza hariç), son kullanma zamanı |
| Erişim reddedildi (`/ErisimReddedildi`) | Yetkisiz rol buraya yönlendirilir |
| Çıkış (`/Cikis`) | Yerel çerezi siler, kimlik sunucusunun end-session uç noktasına gider, dönüşte ana sayfa |

### Klasör yapısı

```
SelcukSSO.sln
dotnet-tools.json                 dotnet-ef yerel aracı
scripts/run-all.ps1               İki uygulamayı ayrı pencerelerde başlatır
src/Selcuk.Identity/
  Program.cs                      Identity, OpenIddict, çerez/HSTS ayarları
  GuvenlikBasliklari.cs           Güvenlik başlıkları ara katmanı
  Data/                           ApplicationUser, DbContext, Roller, Istemciler, BaslangicVerisi (seed), Migrations
  Oidc/                           Token/UserInfo uç noktaları, claim hedefleri
  Pages/Account/                  Login, AccessDenied
  Pages/Connect/                  Authorize (onay ekranı), Logout (end-session)
src/Selcuk.Demo.Obis/
  Program.cs                      Cookie + OpenID Connect istemcisi
  Data/SahteVeri.cs               Uydurma notlar ve dersler
  Pages/                          Notlarım, Ders Yönetimi, Profilim, Token İnceleme, Erişim reddedildi, Çıkış
tests/Selcuk.Identity.Tests/      Kimlik sunucusu entegrasyon ve uçtan uca protokol testleri
tests/Selcuk.Demo.Obis.Tests/     Demo OBİS yönlendirme ve rol testleri
```

## Kurulum

Gereksinimler (Windows):
- .NET 10 SDK: `winget install --id Microsoft.DotNet.SDK.10 --exact`
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`). winget'te ayrı paket yok; SQL Server 2022 Express indiricisiyle
  yalnızca LocalDB MSI'ı indirilip kurulur:
  ```powershell
  winget install --id Microsoft.SQLServer.2022.Express --exact --override "/ACTION=Download /MEDIATYPE=LocalDB /MEDIAPATH=C:\Temp\sqlmedia /QUIET"
  msiexec /i "C:\Temp\sqlmedia\en-US\SqlLocalDB.msi" /qn IACCEPTSQLLOCALDBLICENSETERMS=YES
  ```

Proje kökünde:

```powershell
dotnet tool restore                                          # dotnet-ef yerel aracı
dotnet ef database update --project src/Selcuk.Identity      # SelcukSSO veritabanını oluşturur
dotnet dev-certs https --trust                               # (bir kez) HTTPS geliştirme sertifikasına güven
```

`dotnet dev-certs https --trust` Demo OBİS için **zorunludur**. Demo OBİS kimlik sunucusunun discovery ve JWKS
adreslerine HTTPS ile bağlanır ve güvenilmeyen sertifikayı reddeder. Komut Windows'ta bir onay penceresi açar.

### Sırlar (user-secrets)

Hiçbir şifre veya secret repoda yoktur. Development'ta `dotnet user-secrets` kullanılır:

| Proje | Anahtar | İçerik |
|---|---|---|
| `src/Selcuk.Identity` | `TestKullanicilari:Ogrenci:Sifre`, `…:Akademisyen:Sifre`, `…:Personel:Sifre` | Test kullanıcılarının şifreleri |
| `src/Selcuk.Identity` | `Istemciler:DemoObis:ClientSecret` | demo-obis istemcisinin secret'ı |
| `src/Selcuk.Demo.Obis` | `Kimlik:ClientSecret` | Aynı secret (iki tarafta aynı olmalı) |

Görmek için:

```powershell
dotnet user-secrets list --project src/Selcuk.Identity
dotnet user-secrets list --project src/Selcuk.Demo.Obis
```

Yeni bir makinede tanımlamak için (secret'ı kendiniz üretin ve iki projeye de aynı değeri yazın):

```powershell
$b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
$s = [Convert]::ToBase64String($b)
dotnet user-secrets set "Istemciler:DemoObis:ClientSecret" $s --project src/Selcuk.Identity
dotnet user-secrets set "Kimlik:ClientSecret" $s --project src/Selcuk.Demo.Obis
```

Kimlik sunucusu her açılışta demo-obis tanımını, secret dahil, user-secrets'taki değerle eşitler.

## Çalıştırma

İkisini birlikte başlatmak için:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1
```

Betik kimlik sunucusunu (https://localhost:5001) ve Demo OBİS'i (https://localhost:5101) ayrı PowerShell
pencerelerinde başlatır, ikisi de hazır olunca tarayıcıda Demo OBİS'i açar. Durdurmak için pencerelerde `Ctrl+C`.

Tek tek:

```powershell
dotnet run --project src/Selcuk.Identity       # https://localhost:5001
dotnet run --project src/Selcuk.Demo.Obis      # https://localhost:5101
```

Kimlik sunucusu açılışta rolleri (yoksa) oluşturur. Development'ta test kullanıcılarını ve demo-obis istemcisini de
ekler.

## Elle deneme

1. `powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1` ile iki uygulamayı başlatın. Tarayıcıda
   https://localhost:5101 açılır.
2. **Demo OBİS** ana sayfasında **Giriş yap**'a tıklayın. Kimlik sunucusunun giriş sayfasına
   (`https://localhost:5001/Account/Login`) yönlendirilirsiniz.
3. Kullanıcı adı **`203011001`**, şifre `dotnet user-secrets list --project src/Selcuk.Identity` çıktısındaki
   `TestKullanicilari:Ogrenci:Sifre` değeri.
4. İlk girişte **onay ekranı** açılır: "Demo OBİS uygulaması hesabınıza erişmek istiyor" başlığı altında Kimlik,
   Profil, E-posta ve Rol bilgileri Türkçe açıklamalarla listelenir. **İzin ver**'e tıklayın. Demo OBİS'e
   dönersiniz.
5. **Notlarım**: 5 dersin vize, final ve harf notu görünür.
6. **Token İnceleme**: id_token ve access_token'ın header ve payload'u, son kullanma zamanlarıyla görünür. `role`,
   `fakulte`, `bolum` ve `name` claim'lerini kontrol edin (tez ekran görüntüsü için).
7. **Çıkış**: önce kimlik sunucusunun "Çıkış" sayfası açılır. **Çıkış yap**'a tıklayınca Demo OBİS ana sayfasına
   dönersiniz.
8. Tekrar https://localhost:5101/Notlarim adresine gidin. Kimlik sunucusu **yeniden giriş istemelidir**; iki
   oturum da kapandığı için sayfa doğrudan açılmaz. Onay ekranı ikinci girişte tekrar çıkmaz, onay saklanmıştır.
9. Çıkış yapıp **`hoca.test`** (şifresi `TestKullanicilari:Akademisyen:Sifre`) ile girin. **Ders Yönetimi** açılır.
   Adres çubuğundan `/Notlarim`'e gidince **Erişim reddedildi** sayfası görünür.

## Testler

```powershell
dotnet build
dotnet test
```

Testler LocalDB'ye ve çalışan bir kimlik sunucusuna ihtiyaç duymaz.
- **Selcuk.Identity.Tests:** her test, SQLite in-memory üzerinde kendi veritabanıyla ayrı bir uygulama örneği
  başlatır (`EnsureCreated`, ortam `Testing`, geçici imzalama anahtarları, user-secrets okunmaz). Test kullanıcıları
  ve demo-obis secret'ı test içinde, çalışma anında üretilir. demo-obis, uygulamanın kendi tanımıyla
  (`Istemciler.DemoObis`) kaydedilir.
- **Selcuk.Demo.Obis.Tests:** discovery bilgisi sabit verilir. Rol testlerinde gerçek kimlik sunucusu yerine sahte
  bir kimlik doğrulama şeması kullanılır.

| Test | Doğruladığı |
|---|---|
| `Discovery_dokumani_donuyor_ve_issuer_dogru` | Discovery 200 döner, `issuer` = `https://localhost:5001/` |
| `Discovery_grant_turleri_ve_S256_iceriyor` | `authorization_code`, `refresh_token` var, `password`/`implicit` yok, `S256` var |
| `Giris_sayfasi_aciliyor` | Giriş formu ve antiforgery token'ı geliyor |
| `Dogru_sifreyle_giris_basarili` | 302 + oturum çerezi |
| `Yanlis_sifreyle_giris_basarisiz` | Genel hata mesajı, çerez yok |
| `Bes_hatali_denemeden_sonra_hesap_kilitleniyor` | 5 denemede ~15 dk kilit; kilitliyken doğru şifre de reddedilir ve mesaj aynı kalır |
| `Uc_rol_olusturulmus` | `akademisyen`, `ogrenci`, `personel` |
| `Pkce_olmadan_authorize_istegi_reddediliyor` | `code_challenge` olmadan istek 400 `invalid_request`; PKCE ile aynı istek girişe yönlenir |
| `Kod_akisi_PKCE_ile_imzali_JWT_tokenlar_uretiyor` | Giriş → authorize (S256) → onay → kod → token; JWKS ile imza, iss/aud/exp/nonce; sub, name, role, fakulte, bolum; userinfo |
| `Ayni_kod_ikinci_kez_kullanilinca_reddediliyor` | İkinci kullanımda `invalid_grant` |
| `Yanlis_code_verifier_reddediliyor` | `invalid_grant` |
| `Kayitli_olmayan_redirect_uri_reddediliyor` | 400 `invalid_request`, yönlendirme yok |
| `Yanlis_client_secret_reddediliyor` | `invalid_client` |
| `Plain_code_challenge_method_reddediliyor` | Discovery yalnızca `S256` gösterir; `plain` 400 `invalid_request` |
| `Guvenlik_basliklari_giris_sayfasinda_mevcut` | nosniff, no-referrer, DENY, CSP (`frame-ancestors 'none'`) |
| `Cikis_post_logout_redirect_uri_ile_istemciye_donuyor` | End-session demo-obis'e `state` ile döner, sonra authorize yeniden giriş ister |
| `Girissiz_kullanici_Notlarim_icin_kimlik_sunucusuna_yonlendiriliyor` | 302 → `https://localhost:5001/connect/authorize`, `code_challenge` + `S256` |
| `Ogrenci_Notlarim_acilir_Ders_Yonetimi_reddedilir` | ogrenci: Notlarım 200, Ders Yönetimi → Erişim reddedildi |
| `Akademisyen_Ders_Yonetimi_acilir_Notlarim_reddedilir` | akademisyen: Ders Yönetimi 200, Notlarım → Erişim reddedildi |
| `Token_Inceleme_Development_disinda_kapali` | Development dışında 404 |

## Sürümler

| Bileşen | Sürüm |
|---|---|
| .NET SDK / hedef çatı | 10.0.402 / `net10.0` (çalışma zamanı 10.0.13) |
| **OpenIddict** (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`) | **7.7.2** (NuGet'teki en güncel kararlı sürüm) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.EntityFrameworkCore.SqlServer / .Design | 10.0.13 |
| Microsoft.AspNetCore.Authentication.OpenIdConnect (Demo OBİS) | 10.0.13 |
| System.Security.Cryptography.Pkcs (açık kapatmak için sabitlendi; Identity + Identity.Tests) | 10.0.13 |
| System.Security.Cryptography.Xml (açık kapatmak için sabitlendi; Identity.Tests) | 10.0.13 |
| dotnet-ef (yerel araç) | 10.0.13 |
| Test: xunit / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 2.9.3 / 3.1.4 / 17.14.1 |
| Test: Microsoft.AspNetCore.Mvc.Testing / Microsoft.EntityFrameworkCore.Sqlite | 10.0.13 / 10.0.13 |
| Veritabanı | SQL Server 2022 Express LocalDB |

## Yapılacaklar (yol haritası)

- [x] **Demo OBİS** istemcisi (Authorization Code + PKCE ile giriş, rol bazlı sayfalar)
- [ ] **Demo Yemekhane** istemcisi (ikinci istemci; tek girişle iki uygulamaya erişimi göstermek için)
- [ ] **Tek çıkış** (single logout): bir uygulamadan çıkınca diğer istemcilerin de oturumunu kapatma
      (front-channel / back-channel logout)
- [ ] **Şifre sıfırlama** (e-posta ile tek kullanımlık bağlantı)
- [ ] **2FA** (TOTP doğrulayıcı uygulama)
- [ ] **Denetim kaydı** (giriş, başarısız deneme, kilitleme, onay, çıkış olayları)
- [ ] **Yönetim paneli** (kullanıcı, rol ve istemci uygulama yönetimi)
- [ ] **Token iptali** (revocation uç noktası, oturum/yetkilendirme iptali)
- [ ] **Production sertifika yönetimi**: şu an yalnızca Development sertifikaları (`AddDevelopmentSigningCertificate`,
      `AddDevelopmentEncryptionCertificate`) kullanılıyor. Production'da kalıcı X.509 imzalama/şifreleme
      sertifikaları (sertifika deposu veya Key Vault), anahtar rotasyonu ve `OpenIddict:Issuer`'ın gerçek alan adına
      ayarlanması gerekiyor. Development ve Testing dışındaki ortamlarda uygulama bilerek açılmaz.
