# PROJECT_CONTEXT

## Amaç ve problem

Fakülte/bölüm uygulamalarında her sistem için ayrı kullanıcı adı/şifre gerekiyor. Proje, öğrenci, akademisyen ve
idari personelin tek hesapla tüm alt uygulamalara OIDC üzerinden girmesini sağlayan merkezi kimlik doğrulama ve
yetkilendirme sunucusunu (ve onu kullanan örnek uygulamaları) gerçekler. Bitirme projesi olduğu için ölçüm,
güvenlik karşılaştırması, tehdit modeli ve tez görselleri de deliverable'dır (`docs/olcumler`, `tehdit-modeli.md`,
`tez-gorselleri/`).

Gerçek Selçuk sistemleri OIDC desteklemez; geçiş aracı olarak `araclar/tek-kapi` (kişisel ESSO: form doldurarak
giriş, şifreler yalnızca yerel şifreli kasada) SSO'ya `tek-kapi` public istemcisi olarak bağlanır.

## Teknoloji

| Alan | Seçim |
|---|---|
| Çatı | .NET SDK 10.0.402, `net10.0`, ASP.NET Core Razor Pages |
| Kimlik | ASP.NET Core Identity 10.0.13, OpenIddict 7.7.2 (Server + EF Core) |
| Veri | EF Core 10.0.13 + SQL Server LocalDB (`SelcukSSO`); testlerde SQLite in-memory |
| İstemci | Microsoft.AspNetCore.Authentication.OpenIdConnect, JwtBearer (Demo API) |
| Diğer | QRCoder 1.8.1 (2FA QR, SVG), Atkinson Hyperlegible fontları (yerel woff2) |
| Test | xUnit 2.9.3, Mvc.Testing, coverlet; Tek Kapı için Node testleri |
| Tek Kapı | Node 22.15+, Express 4.22.3, Playwright, openid-client, jose |
| Araçlar | PowerShell betikleri (`scripts/`), Python 3 standart kütüphane (`olcum_analiz.py`), `dotnet-ef` yerel aracı |

Docker, CI (`.github`) ve deploy yapılandırması **yok** (depoda bulunmadı).

## Genel akış

1. Alt uygulama kullanıcıyı `/connect/authorize`'a yönlendirir (code + PKCE S256).
2. Kimlik sunucusu şifre (+ gerekirse TOTP) ister, onay verir, `sid` özellikli oturum açar.
3. Uygulama kodu `/connect/token`'da değiştirir; id_token (profil, `role`, `amr`, `sid`), 15 dk access token,
   isteğe bağlı 8 sa refresh token alır.
4. İkinci uygulamada kimlik sunucusu çerezi geçerli olduğundan şifresiz geçiş (tek giriş).
5. Çıkışta kimlik sunucusu o `sid`'deki her istemcinin `backchannel_logout_uri`'sine imzalı `logout+jwt` yollar
   (tek çıkış); token'lar iptal edilir.
6. Demo API access token'ı JWKS ile kendisi doğrular.

Konfigürasyon: `appsettings*.json` + `dotnet user-secrets` (sır listesi README > Sırlar). Ortam: Development
(test kullanıcıları + istemciler seed), Testing; diğer ortamlarda uygulama bilerek açılmaz (production sertifikası yok).
