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
    Authorization Code + PKCE (zorunlu) ve Refresh Token akışları. Access token'lar imzalı JWT.
  - **EF Core + SQL Server** (geliştirmede LocalDB, veritabanı `SelcukSSO`). Identity ve OpenIddict tabloları aynı
    `ApplicationDbContext` içinde.
  - **Razor Pages** (Türkçe arayüz): Giriş, Onay, Çıkış, Erişim reddedildi.
- `tests/Selcuk.Identity.Tests`: xUnit + `WebApplicationFactory` entegrasyon testleri. Her test SQLite in-memory
  üzerinde kendi veritabanını kullanır; ortam adı `Testing`.

Ayrıntılar için `README.md` dosyasına bakın.

## Değişmez kurallar

a) **Kapsam dışına çıkma.** Yalnızca istenen değişikliği yap. İstenmeyen refactor yapma, istenmeyen bağımlılık
   (NuGet paketi, araç, servis) ekleme. Gerekli gördüğün ek işleri yapmak yerine öneri olarak bildir.

b) **Şifreler ve sırlar koda, loglara ve repoya girmez.** Geliştirmede `dotnet user-secrets`, diğer ortamlarda ortam
   değişkenleri veya güvenli bir sır deposu kullanılır. Şifreleri, token'ları veya bağlantı sırlarını loglama, çıktıya
   yazdırma ve commit'leme.

c) **Gerçek Selçuk Üniversitesi sistemlerine veya gerçek hesaplara bağlanılmaz.** Test verileri kurgusaldır, e-posta
   adresleri `@test.local` uzantılıdır. Gerçek `selcuk.edu.tr` adresleri, gerçek öğrenci numaraları veya kurum
   sistemleri kullanılmaz.

d) **OIDC/OAuth2 standardından sapılmaz.** PKCE zorunludur. Implicit flow yoktur. Password (ROPC) grant yoktur.
   Yeni akış veya uç nokta eklenecekse ilgili RFC ve OpenID Connect belirtimine uyulur.

e) **Kriptografi elle yazılmaz.** Şifre özetleme, token imzalama/şifreleme ve anahtar yönetimi için ASP.NET Core
   Identity ve OpenIddict'in sağladığı mekanizmalar kullanılır.

f) **Her değişiklikten sonra `dotnet build` ve `dotnet test` geçmelidir.**

## Sık kullanılan komutlar

```powershell
dotnet build
dotnet test
dotnet run --project src/Selcuk.Identity                                   # https://localhost:5001
dotnet tool restore
dotnet ef migrations add <Ad> --project src/Selcuk.Identity --output-dir Data/Migrations
dotnet ef database update --project src/Selcuk.Identity
```
