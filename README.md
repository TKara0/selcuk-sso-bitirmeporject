# Tek Şifre Giriş Sistemi (SelcukSSO)

Bitirme projesi: fakülte ve bölüm web uygulamaları için merkezi kimlik sunucusu.

> **Proje tanımı (bölümün verdiği):** "Fakülte ve bölüm genelinde kullanılan tüm web otomasyonları için merkezi
> bir kimlik doğrulama ve yetkilendirme altyapısı sunar. Kullanıcılar (öğrenci, akademisyen, idari personel) tek bir
> kullanıcı adı ve şifre ile güvenli bir şekilde tüm alt uygulamalara giriş yapabilir. Token tabanlı (JWT vb.) oturum
> yönetimini, şifre sıfırlama vb. özelliklere sahiptir."

Bu depo şu an yalnızca **kimlik sunucusunun çekirdeğini** içerir. Demo uygulamalar, şifre sıfırlama, 2FA ve yönetim
paneli [yol haritasında](#yapılacaklar-yol-haritası) yer alıyor.

## Mimari özet

```
 Tarayıcı ──► Alt uygulama (OBİS, Yemekhane…)          [henüz yok]
    │              │  1) /connect/authorize?…&code_challenge=…  (PKCE S256)
    ▼              ▼
 ┌──────────────────────────────────────────────┐
 │ Selcuk.Identity  (ASP.NET Core 10)           │
 │  • Razor Pages: Giriş, Onay, Çıkış, Erişim   │
 │    reddedildi                                │
 │  • ASP.NET Core Identity: kullanıcı, rol,    │
 │    şifre politikası, hesap kilitleme         │
 │  • OpenIddict Server: OIDC / OAuth 2.0       │
 │  • EF Core ──► MSSQL LocalDB (SelcukSSO)     │
 └──────────────────────────────────────────────┘
                   │  2) code ──► /connect/token (code_verifier ile)
                   ▼  3) id_token + imzalı JWT access_token + refresh_token
```

| Uç nokta | Adres | Açıklama |
|---|---|---|
| Discovery | `/.well-known/openid-configuration` | OIDC meta verisi |
| JWKS | `/.well-known/jwks` | İmza doğrulama anahtarları |
| Authorize | `/connect/authorize` | Giriş ve onay ekranı (Razor Page) |
| Token | `/connect/token` | `authorization_code` ve `refresh_token` grant'leri |
| UserInfo | `/connect/userinfo` | Kapsama göre kullanıcı bilgisi |
| End-session | `/connect/logout` | Çıkış sayfası |

**Akışlar:** yalnızca Authorization Code + PKCE (sunucu genelinde zorunlu) ve Refresh Token akışları açık.
Implicit, hybrid, password ve client credentials akışları kapalı.

**Kapsamlar ve claim'ler:**

| Kapsam | Token'a giren claim'ler |
|---|---|
| `openid` | `sub` |
| `profile` | `name` (AdSoyad), `preferred_username` (öğrenci no / kullanıcı adı) |
| `email` | `email` |
| `roles` | `role` (`ogrenci`, `akademisyen`, `personel`) |

Claim'ler yalnızca kullanıcı ilgili kapsama onay verdiyse id_token ve access_token'a yazılır. Access token'lar
**imzalı ama şifresiz JWT** (`DisableAccessTokenEncryption`), içerikleri tezde gösterilebilir (ör. jwt.io).
Authorization code ve refresh token'lar OpenIddict tarafından şifreli tutulur.

**Kullanıcı modeli:** `ApplicationUser : IdentityUser` + `AdSoyad`, `Fakulte`, `Bolum`. Öğrencilerin `UserName`'i
öğrenci numarası, akademik/idari personelinki kurumsal kullanıcı adıdır.

**Güvenlik ayarları:**
- Şifre: en az 10 karakter, büyük harf, küçük harf ve rakam zorunlu.
- 5 hatalı denemede hesap 15 dakika kilitlenir. Yanlış şifre, var olmayan kullanıcı ve kilitli hesap için **aynı**
  genel mesaj gösterilir, bu yüzden mesajdan hesabın var olup olmadığı anlaşılmaz.
- Antiforgery tüm formlarda açık. HTTPS yönlendirmesi ve OpenIddict'in HTTPS zorunluluğu açık.
- HSTS Development dışındaki ortamlarda açık (ASP.NET Core varsayılanı; tarayıcının `localhost` için HSTS'yi
  önbelleğe almaması için Development'ta kapalı).
- Çerezler: `Secure`, `HttpOnly`. Oturum çerezi `SameSite=Lax` (istemciden gelen yönlendirmede gönderilebilmesi
  için), antiforgery çerezi `SameSite=Strict`.
- İlk girişte her istemci için onay ekranı gösterilir (`ConsentType = explicit`). Onay kalıcı yetkilendirme olarak
  saklanır.

### Klasör yapısı

```
SelcukSSO.sln
dotnet-tools.json                 dotnet-ef yerel aracı
src/Selcuk.Identity/
  Program.cs                      Identity, OpenIddict, çerez/HSTS ayarları
  Data/                           ApplicationUser, DbContext, Roller, BaslangicVerisi (seed), Migrations
  Oidc/                           Token/UserInfo uç noktaları, claim hedefleri
  Pages/Account/                  Login, AccessDenied
  Pages/Connect/                  Authorize (onay ekranı), Logout (end-session)
tests/Selcuk.Identity.Tests/      xUnit + WebApplicationFactory entegrasyon testleri
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
dotnet dev-certs https --trust                               # (bir kez) tarayıcı için HTTPS sertifikası
```

Test kullanıcılarının şifreleri user-secrets'ta tutulur (bkz. [Test kullanıcıları](#test-kullanıcıları)). Yeni bir
makinede bunları yeniden tanımlamak gerekir.

## Çalıştırma

```powershell
dotnet run --project src/Selcuk.Identity
```

Uygulama `https://localhost:5001` adresinde açılır (`Properties/launchSettings.json`, ortam: Development).
Kontrol: <https://localhost:5001/.well-known/openid-configuration>

Uygulama açılışta `ogrenci`, `akademisyen` ve `personel` rollerini (yoksa) oluşturur. Development ortamında test
kullanıcılarını da ekler.

## Test kullanıcıları

Yalnızca **Development** ortamında, kullanıcı yoksa oluşturulur. Şifreler koda yazılmaz; user-secrets'tan okunur.

| Rol | Kullanıcı adı | E-posta | Fakülte / Bölüm | Şifre anahtarı |
|---|---|---|---|---|
| ogrenci | `203011001` | 203011001@test.local | Teknoloji Fakültesi / Bilgisayar Mühendisliği | `TestKullanicilari:Ogrenci:Sifre` |
| akademisyen | `hoca.test` | hoca.test@test.local | Teknoloji Fakültesi / Bilgisayar Mühendisliği | `TestKullanicilari:Akademisyen:Sifre` |
| personel | `personel.test` | personel.test@test.local | Teknoloji Fakültesi | `TestKullanicilari:Personel:Sifre` |

**Şifreleri görmek için:**

```powershell
dotnet user-secrets list --project src/Selcuk.Identity
```

Dosya olarak: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` (`UserSecretsId` değeri
`src/Selcuk.Identity/Selcuk.Identity.csproj` içinde). Bu dosya repoya girmez.

**Şifre tanımlamak veya değiştirmek için** (en az 10 karakter, büyük/küçük harf ve rakam):

```powershell
dotnet user-secrets set "TestKullanicilari:Ogrenci:Sifre" "<yeni-şifre>" --project src/Selcuk.Identity
```

Kullanıcı veritabanında zaten varsa seed işlemi şifresini değiştirmez. Yeni şifrenin geçmesi için kullanıcıyı
silmek ya da veritabanını yeniden oluşturmak gerekir:
`dotnet ef database drop --project src/Selcuk.Identity`, ardından `dotnet ef database update --project src/Selcuk.Identity`.
Şifre anahtarı tanımlı değilse o kullanıcı atlanır ve logda bir uyarı görünür.

## Testler

```powershell
dotnet build
dotnet test
```

Testler LocalDB kullanmaz. Her test, **SQLite in-memory** üzerinde kendine ait bir veritabanıyla ayrı bir uygulama
örneği başlatır (`EnsureCreated`). Testler `Testing` ortamında çalışır: geçici (ephemeral) imzalama ve şifreleme
anahtarları kullanılır, user-secrets okunmaz, test kullanıcıları seed edilmez. Testlerin kullandığı kullanıcılar ve
şifreleri test içinde, çalışma anında üretilir.

| Test | Doğruladığı |
|---|---|
| `Discovery_dokumani_donuyor_ve_issuer_dogru` | Discovery 200 döner, `issuer` = `https://localhost:5001/` |
| `Discovery_grant_turleri_ve_S256_iceriyor` | `authorization_code`, `refresh_token` var, `password`/`implicit` yok, `S256` var |
| `Giris_sayfasi_aciliyor` | Giriş formu ve antiforgery token'ı geliyor |
| `Dogru_sifreyle_giris_basarili` | 302 + oturum çerezi |
| `Yanlis_sifreyle_giris_basarisiz` | Genel hata mesajı, çerez yok |
| `Bes_hatali_denemeden_sonra_hesap_kilitleniyor` | 5 denemede ~15 dk kilit; kilitliyken doğru şifre de reddedilir ve mesaj aynı kalır |
| `Uc_rol_olusturulmus` | `akademisyen`, `ogrenci`, `personel` |
| `Pkce_olmadan_authorize_istegi_reddediliyor` | Geçici istemciyle `code_challenge` olmadan istek 400 `invalid_request` alır; PKCE ile aynı istek girişe yönlenir |

## Sürümler

| Bileşen | Sürüm |
|---|---|
| .NET SDK / hedef çatı | 10.0.402 / `net10.0` (çalışma zamanı 10.0.13) |
| **OpenIddict** (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`) | **7.7.2** (NuGet'teki en güncel kararlı sürüm) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.EntityFrameworkCore.SqlServer / .Design | 10.0.13 |
| dotnet-ef (yerel araç) | 10.0.13 |
| Test: xunit / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 2.9.3 / 3.1.4 / 17.14.1 |
| Test: Microsoft.AspNetCore.Mvc.Testing / Microsoft.EntityFrameworkCore.Sqlite | 10.0.13 / 10.0.13 |
| Veritabanı | SQL Server 2022 Express LocalDB |

## Yapılacaklar (yol haritası)

- [ ] **Demo OBİS** istemcisi (Authorization Code + PKCE ile giriş, rol bazlı sayfalar)
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
