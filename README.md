# Tek Şifre Giriş Sistemi (SelcukSSO)

Bitirme projesi: fakülte ve bölüm web uygulamaları için merkezi kimlik sunucusu.

> **Proje tanımı (bölümün verdiği):** "Fakülte ve bölüm genelinde kullanılan tüm web otomasyonları için merkezi
> bir kimlik doğrulama ve yetkilendirme altyapısı sunar. Kullanıcılar (öğrenci, akademisyen, idari personel) tek bir
> kullanıcı adı ve şifre ile güvenli bir şekilde tüm alt uygulamalara giriş yapabilir. Token tabanlı (JWT vb.) oturum
> yönetimini, şifre sıfırlama vb. özelliklere sahiptir."

Depoda şu an **kimlik sunucusu**, ona OpenID Connect ile bağlanan iki alt uygulama (**Demo OBİS**, **Demo Yemekhane**)
ve bu uygulamaların ortak istemci kütüphanesi var. **Tek giriş** (bir kez giriş, diğer uygulamalara şifresiz geçiş),
OIDC Back-Channel Logout ile **tek çıkış** (bir uygulamadan çıkınca hepsinden çıkış), e-postayla **şifre sıfırlama**
ve **şifre değiştirme** çalışıyor. Alınan önlemler [Güvenlik önlemleri](#güvenlik-önlemleri) bölümünde. Diğer
özellikler [yol haritasında](#yapılacaklar-yol-haritası).

## Mimari özet

```
                       ┌──────────────────────────────────────────┐
                       │ Selcuk.Identity  :5001                    │
                       │  Giriş · Onay · Çıkış · Uygulamalarım     │
                       │  ASP.NET Core Identity + OpenIddict (OIDC)│
                       │  EF Core ──► LocalDB (SelcukSSO)          │
                       └──────────────────────────────────────────┘
        authorize/token/userinfo ▲   │ logout_token      ▲   │ logout_token
        (code + PKCE S256)       │   ▼ (back-channel)    │   ▼ (back-channel)
            ┌────────────────────────────┐    ┌────────────────────────────┐
            │ Selcuk.Demo.Obis  :5101    │    │ Selcuk.Demo.Yemekhane :5102│
            │  Notlarım, Ders Yönetimi   │    │  Menü, Rezervasyonlarım,   │
            │                            │    │  Yemekhane Yönetimi        │
            └─────────────┬──────────────┘    └─────────────┬──────────────┘
                          └───────── Selcuk.Istemci.Ortak ──┘
                     AddSelcukSSO: Cookie + OIDC, güvenlik başlıkları,
                     /signout-backchannel alıcısı
```

| Uç nokta | Adres | Açıklama |
|---|---|---|
| Discovery | `/.well-known/openid-configuration` | OIDC meta verisi (`backchannel_logout_supported: true`) |
| JWKS | `/.well-known/jwks` | İmza doğrulama anahtarları |
| Authorize | `/connect/authorize` | Giriş ve onay ekranı (Razor Page) |
| Token | `/connect/token` | `authorization_code` ve `refresh_token` grant'leri |
| UserInfo | `/connect/userinfo` | Kapsama göre kullanıcı bilgisi |
| End-session | `/connect/logout` | Çıkış sayfası; Back-Channel Logout bildirimlerini gönderir, `post_logout_redirect_uri` ile istemciye döner |
| Uygulamalarım | `/` | Giriş yapmış kullanıcıya kayıtlı uygulamaların kartları; kart `initiate_login_uri?iss=…` adresine gider |
| Şifremi unuttum | `/Account/SifremiUnuttum` | Kullanıcı adı, öğrenci no veya e-postayla sıfırlama bağlantısı isteği |
| Şifre sıfırlama | `/Account/SifreSifirla?kullanici=…&kod=…` | E-postadaki bağlantı: yeni şifre + tekrar |
| Hesabım | `/Account/Hesabim` | Hesap bilgileri; **Şifre değiştir** (`/Account/SifreDegistir`) |

**Akışlar:** yalnızca Authorization Code + PKCE ve Refresh Token. PKCE sunucu genelinde zorunlu ve **yalnızca
`S256`** kabul edilir (`plain` kapalı). Implicit, hybrid, password ve client credentials akışları kapalı.

**Kapsamlar ve claim'ler:**

| Kapsam | Claim'ler |
|---|---|
| `openid` | `sub`; id_token'da ayrıca `sid` (kimlik sunucusu oturum kimliği) |
| `profile` | `name` (AdSoyad), `preferred_username` (öğrenci no / kullanıcı adı), `fakulte`, `bolum` |
| `email` | `email` (userinfo'da ayrıca `email_verified`) |
| `roles` | `role` (`ogrenci`, `akademisyen`, `personel`) |

Claim'ler yalnızca kullanıcı ilgili kapsama onay verdiyse tokenlara yazılır. Access token'lar **imzalı ama şifresiz
JWT** (`typ: at+jwt`, `DisableAccessTokenEncryption`), içerikleri tezde gösterilebilir. Henüz ayrı bir API (kaynak
sunucusu) tanımlı olmadığı için access token'da `aud` yoktur; tokenın verildiği istemci `client_id` claim'inde
taşınır. Discovery'deki `claims_supported` listesi: `aud exp iat iss sub name email role fakulte bolum sid`.

**Kullanıcı modeli:** `ApplicationUser : IdentityUser` + `AdSoyad`, `Fakulte`, `Bolum`. Öğrencilerin `UserName`'i
öğrenci numarası, akademik/idari personelinki kurumsal kullanıcı adıdır.

**Kayıtlı istemciler** (Development'ta açılışta oluşturulur veya güncellenir; tanımlar
`src/Selcuk.Identity/Data/Istemciler.cs`):

| | `demo-obis` | `demo-yemekhane` |
|---|---|---|
| Görünen ad | Demo OBİS | Demo Yemekhane |
| Redirect URI | `https://localhost:5101/signin-oidc` | `https://localhost:5102/signin-oidc` |
| Post-logout redirect URI | `https://localhost:5101/signout-callback-oidc` | `https://localhost:5102/signout-callback-oidc` |
| `backchannel_logout_uri` | `https://localhost:5101/signout-backchannel` | `https://localhost:5102/signout-backchannel` |
| `client_uri` / `initiate_login_uri` | `https://localhost:5101/` / `…/Giris` | `https://localhost:5102/` / `…/Giris` |

Ortak kurallar: gizli (confidential) web istemcisi, `client_secret` user-secrets'ta; izinler authorization, token,
end-session uç noktaları, `authorization_code`, `code`, `openid profile email roles`; PKCE zorunlu; onay türü
`explicit` (her uygulama için ilk girişte onay ekranı). Özel ayarlar (`backchannel_logout_uri`, `client_uri`,
`initiate_login_uri`, `aciklama`) OpenIddict istemci kaydının `Properties` alanında, OIDC istemci meta verisi
adlarıyla saklanır.

**Güvenlik ayarları:** şifre politikası, kilitleme, istek sınırı, çerezler ve başlıklar
[Güvenlik önlemleri](#güvenlik-önlemleri) bölümünde.

## Tek çıkış nasıl çalışıyor

1. Kullanıcı kimlik sunucusunda oturum açınca bu oturuma rastgele bir **oturum kimliği (`sid`)** verilir ve oturum
   çerezine yazılır.
2. Kullanıcı bir uygulamaya girdiğinde id_token'a aynı `sid` konur. Token uç noktası, o `sid` ile hangi istemciye
   token verildiğini `IstemciOturumlari` tablosuna kaydeder. Aynı oturumdan giriş yapılan her uygulama aynı `sid`'i
   alır.
3. Kullanıcı herhangi bir uygulamadan çıkınca uygulama yerel çerezini siler ve kullanıcıyı kimlik sunucusunun
   end-session uç noktasına gönderir (RP-initiated logout).
4. Kimlik sunucusu kendi oturumunu kapatır. Ardından o `sid`'e kayıtlı **her** istemcinin `backchannel_logout_uri`
   adresine tarayıcıyı kullanmadan, sunucudan sunucuya bir **logout_token** POST eder (OIDC Back-Channel Logout 1.0):
   - OpenIddict'in imzalama anahtarıyla imzalı JWT, `typ: logout+jwt`
   - Claim'ler: `iss`, `aud` (client_id), `iat`, `exp` (2 dk), `jti`, `sid`, `sub`,
     `events: {"http://schemas.openid.net/event/backchannel-logout": {}}`; `nonce` yok
   - Zaman aşımı 5 sn. Bildirimler paralel gider; bir istemci yanıt vermezse diğerleri yine bilgilendirilir.
     Hata loglanır, token içeriği loglanmaz.
   - Kayıtlar kapatılır (`KapanmaZamani`).
5. İstemcideki `/signout-backchannel` (Selcuk.Istemci.Ortak) token'ı doğrular ve `sid`'i dağıtık önbellekte
   (`IDistributedCache`) "iptal edildi" olarak işaretler. Doğrulanan kurallar: JWKS ile imza, iss, aud, exp/iat,
   typ, events, `nonce` yokluğu, `sid` veya `sub` varlığı, `jti`'nin daha önce kullanılmamış olması. Geçersiz token
   400, geçerli token 200 alır. İstemci kimlik sunucusunun discovery/JWKS adresine ulaşamazsa token doğrulanamaz;
   bu geçici durumda 503 döner.
6. O kullanıcının bu uygulamadaki çerezi bir sonraki istekte (`OnValidatePrincipal`) reddedilir. Kullanıcı
   çıkış yapmış sayılır; korumalı sayfaya gidince yeniden girişe yönlendirilir.

```mermaid
sequenceDiagram
    autonumber
    actor K as Kullanıcı (tarayıcı)
    participant Y as Demo Yemekhane :5102
    participant I as Kimlik sunucusu :5001
    participant O as Demo OBİS :5101

    Note over K,O: Önceden: aynı kimlik sunucusu oturumuyla (sid=S) OBİS'e ve Yemekhane'ye giriş yapılmış
    K->>Y: Çıkış (POST /Cikis)
    Y->>Y: Yerel çerezi sil
    Y-->>K: 302 /connect/logout?id_token_hint=…&post_logout_redirect_uri=…
    K->>I: Çıkış onayı (POST /connect/logout)
    I->>I: Kimlik sunucusu oturumunu kapat, sid=S kayıtlarını bul
    par Back-channel (sunucudan sunucuya)
        I->>O: POST /signout-backchannel  logout_token(aud=demo-obis, sid=S)
        O->>O: İmza, iss, aud, events, jti doğrula, sid=S iptal
        O-->>I: 200
    and
        I->>Y: POST /signout-backchannel  logout_token(aud=demo-yemekhane, sid=S)
        Y-->>I: 200
    end
    I->>I: IstemciOturumlari kayıtlarını kapat
    I-->>K: 302 https://localhost:5102/signout-callback-oidc
    K->>Y: signout-callback-oidc → ana sayfa
    K->>O: (OBİS sekmesini yenile) GET /Notlarim
    O->>O: Çerezdeki sid=S iptal edilmiş → oturumu reddet
    O-->>K: 302 /connect/authorize → kimlik sunucusu giriş ister
```

Aynı mekanizma şifre sıfırlamada (kullanıcının tüm oturumları) ve şifre değiştirmede (şifreyi değiştiren oturum
dışındakiler) da kullanılır.

### İptal kayıtları ve dağıtık önbellek

İptal edilen `sid`/`sub` kayıtları ve kullanılmış `jti`'ler `IDistributedCache` üzerinde tutulur. Kayıtların ömrü
en az istemci çerezinin süresi (`Kimlik:OturumSuresi`, varsayılan 8 saat) kadardır; iptal edilen bir oturumun çerezi
bu süreden önce geçerliliğini yitirmez. `AddSelcukSSO` varsayılan olarak **bellek içi** önbelleği kaydeder
(`AddDistributedMemoryCache`): tek örnekli demo için yeterlidir, ama uygulama yeniden başlatılırsa kayıtlar silinir
ve aynı uygulamanın birden çok örneği kayıtları paylaşmaz.

Üretimde, aynı uygulamanın her örneğinin aynı kayıtları görmesi için dağıtık önbelleği `AddSelcukSSO`'dan **önce**
kaydedin (`AddSelcukSSO` önceden kaydedilmiş bir `IDistributedCache` varsa onu kullanır):

- **Redis:** `Microsoft.Extensions.Caching.StackExchangeRedis` paketi,
  ```csharp
  builder.Services.AddStackExchangeRedisCache(o =>
  {
      o.Configuration = builder.Configuration.GetConnectionString("Redis"); // ortam değişkeni / sır deposu
      o.InstanceName = "SelcukSSO:demo-obis:";
  });
  builder.Services.AddSelcukSSO(builder.Configuration);
  ```
- **SQL Server:** `Microsoft.Extensions.Caching.SqlServer` paketi; tablo bir kez
  `dotnet sql-cache create "<bağlantı>" dbo IstemciOnbellegi` ile oluşturulur,
  ```csharp
  builder.Services.AddDistributedSqlServerCache(o =>
  {
      o.ConnectionString = builder.Configuration.GetConnectionString("Onbellek");
      o.SchemaName = "dbo";
      o.TableName = "IstemciOnbellegi";
  });
  builder.Services.AddSelcukSSO(builder.Configuration);
  ```

Bu paketler depoya eklenmedi; bağlantı dizeleri koda değil ortam değişkenine veya sır deposuna yazılır.
`IDistributedCache`'te atomik "yoksa ekle" olmadığından birden çok örnekte aynı logout_token'ın iki kez kabul
edilmesi mümkündür; etkisi aynı oturumun ikinci kez iptal edilmesidir.

## Şifre sıfırlama ve şifre değiştirme

**Şifremi unuttum** (`/Account/SifremiUnuttum`): kullanıcı adı, öğrenci numarası veya e-posta girilir. Hesap var
olsun ya da olmasın aynı mesaj gösterilir: "Bilgiler bir hesapla eşleşiyorsa şifre sıfırlama bağlantısı e-posta
adresine gönderildi." Hesap varsa ve e-postası doğrulanmışsa bağlantı e-postayla gider.

- Token, Identity'nin yerleşik `DataProtectorTokenProvider`'ıdır; ayrı bir veri koruma amacıyla
  (`SifreSifirlama`) kaydedilir ve **30 dakika** geçerlidir. Token kullanıcının güvenlik damgasını içerir; şifre
  değişince (sıfırlamanın kendisi dahil) damga yenilenir, token **tek kullanımlıktır**.
- Bağlantı istekteki `Host` başlığından değil yapılandırılmış issuer adresinden (`OpenIddict:Issuer`) üretilir.
- Sıfırlama sayfası yeni şifre ve tekrarını ister, şifre politikasını gösterir. Başarıda: güvenlik damgası
  yenilenir, hesap kilidi ve hatalı deneme sayacı sıfırlanır, kullanıcının kimlik sunucusundaki **tüm oturumları**
  kapanır ve bu oturumlardan giriş yapılmış her istemciye Back-Channel Logout gider, "Şifreniz değiştirildi"
  bilgilendirme e-postası gönderilir.

**Şifre değiştirme** (Uygulamalarım → **Hesabım** → **Şifre değiştir**): mevcut şifre + yeni şifre + tekrar. Başarıda
güvenlik damgası yenilenir; bu oturum yeni damgayla yeniden imzalanır ve aynı `sid` ile açık kalır. Kullanıcının
**diğer** oturumları kapanır, onların istemcilerine Back-Channel Logout gider, bilgilendirme e-postası gönderilir.

Kimlik sunucusu oturum çerezindeki güvenlik damgası **her istekte** veritabanıyla karşılaştırılır (Identity'nin
varsayılanı 30 dakikada bir). Bu yüzden şifre değişince diğer tarayıcılardaki oturumlar bir sonraki istekte kapanır.
Çerez bu kontrolde yeniden yazılmaz, böylece `max_age` için kullanılan oturum başlangıç zamanı korunur.

### E-posta

| Ortam | Gönderici | Davranış |
|---|---|---|
| Development | `DosyaEpostaGonderici` | E-posta gönderilmez; `src/Selcuk.Identity/App_Data/eposta/` klasörüne `.eml` dosyası olarak yazılır (klasör `.gitignore`'da) |
| Diğer | `SmtpEpostaGonderici` | `Eposta:Smtp` ayarlarıyla (host, port, kullanıcı, SSL) SMTP; şifre `Eposta:Smtp:Sifre` (user-secrets / ortam değişkeni `Eposta__Smtp__Sifre`) |
| Testing | Sahte gönderici | Testler e-postaları bellekte yakalar |

E-postalar bir kuyruğa bırakılır ve arka planda gönderilir; sayfanın yanıt süresi e-posta gönderiminden etkilenmez.
Şablonlar Türkçe, düz metindir, başlıkları "Selçuk Üniversitesi (Test) — Tek Şifre Giriş Sistemi"dir. Gönderen adresi
`Eposta:Gonderen` (varsayılan `tek-sifre@test.local`). Alıcı adresi ve e-posta gövdesi (sıfırlama bağlantısı)
loglanmaz.

## Güvenlik önlemleri

Şu ana kadar sisteme eklenen önlemler ve her birinin kısa gerekçesi:

1. **Authorization Code + PKCE, yalnızca `S256`.** PKCE sunucu genelinde zorunlu, `plain` kapalı; implicit, hybrid,
   password (ROPC) ve client credentials akışları kapalı. *Gerekçe:* PKCE, ele geçirilen bir yetkilendirme kodunun
   başka biri tarafından token'a çevrilmesini önler (RFC 7636). Implicit akış token'ı URL'de taşır, ROPC kullanıcının
   şifresini istemci uygulamaya verir; ikisi de OAuth 2.0 güvenlik önerilerinde (RFC 9700) önerilmez.
2. **İmzalı JWT token'lar.** id_token ve access_token kimlik sunucusunun anahtarıyla imzalıdır; istemciler imzayı
   JWKS ile, ayrıca `iss`, `aud`, `exp` ve `nonce` değerlerini doğrular. Access token şifresizdir (içerik gizli değil,
   değiştirilemez). *Gerekçe:* token'ın kimin tarafından, kime verildiği ve değiştirilmediği doğrulanabilir; `nonce`
   tekrar oynatmayı önler.
3. **Şifre politikası ve özetleme.** En az 10 karakter, büyük harf, küçük harf ve rakam. Şifreler Identity'nin
   `PasswordHasher`'ıyla (PBKDF2) özetlenir, açık hâliyle saklanmaz. *Gerekçe:* tahmin edilebilir şifreleri azaltır;
   veritabanı sızsa bile şifreler doğrudan okunamaz. Kriptografi elle yazılmadı.
4. **Hesap kilitleme.** 5 hatalı denemede hesap 15 dakika kilitlenir. *Gerekçe:* tek bir hesaba karşı şifre
   denemesini yavaşlatır.
5. **İstek sınırı (rate limit).** Giriş, şifremi unuttum ve şifre sıfırlama formlarının POST'u IP başına dakikada 10
   istekle sınırlıdır (`HizSiniri:DakikadaIstek`); aşılınca 429, `Retry-After` ve Türkçe hata sayfası. *Gerekçe:*
   kilitleme tek hesabı korur; birçok hesaba yayılan denemeleri (password spraying), kullanıcı adı taramasını ve
   e-posta bombardımanını yavaşlatmak için IP başına sınır gerekir.
6. **Kullanıcı adı tahmini koruması.** Yanlış şifre, var olmayan kullanıcı ve kilitli hesap için aynı genel mesaj;
   şifremi unuttumda hesap olsun olmasın aynı mesaj. Yanıt süresi de bu durumlar arasında ayırt edilemeyecek şekilde
   eşitlenmiştir (ayrıntı aşağıda). *Gerekçe:* saldırgan, mesajdan veya yanıt süresinden hangi kullanıcı adlarının
   ve e-posta adreslerinin kayıtlı olduğunu öğrenmemeli.
7. **Güvenli şifre sıfırlama.** 30 dakika geçerli, tek kullanımlık, Identity'nin yerleşik token'ı; bağlantı
   yapılandırılmış issuer'dan üretilir; e-posta yalnızca doğrulanmış adrese gider. *Gerekçe:* kısa ömür ve tek
   kullanım, ele geçirilen bir bağlantının kullanılabileceği süreyi daraltır. Host başlığından üretilen bir bağlantı
   saldırganın alan adına yönlendirilebilirdi (Host header injection).
8. **Şifre değişince oturumların kapatılması.** Sıfırlamada tüm oturumlar, değiştirmede şifreyi değiştiren oturum
   dışındakiler kapanır; kimlik sunucusunda güvenlik damgası her istekte doğrulanır, istemcilere Back-Channel Logout
   gider; kullanıcıya bilgilendirme e-postası gönderilir. *Gerekçe:* şifresi çalınan kullanıcı şifresini değiştirdiğinde
   saldırganın açık oturumları da sonlanmalıdır. Bilgilendirme e-postası, kullanıcının haberi olmadan yapılan
   değişikliği fark etmesini sağlar.
9. **Güvenlik başlıkları.** Tüm yanıtlarda `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`,
   `X-Frame-Options: DENY`, `Content-Security-Policy: default-src 'self'; img-src 'self' data:; object-src 'none';
   base-uri 'self'; frame-ancestors 'none'`. *Gerekçe:* MIME türü tahminini, sayfanın başka sitede çerçeve içinde
   açılmasını (clickjacking) ve dış betik yüklenmesini engeller. `no-referrer` sorgu dizesindeki bilgilerin (ör.
   sıfırlama kodu) başka sitelere `Referer` ile sızmasını önler. `form-action` bilerek kısıtlanmadı, çünkü giriş,
   onay ve çıkış formları başka bir adrese yönlendirmeyle biter; istemciler bu yüzden `response_mode=query` kullanır.
10. **Çerezler ve oturum süresi.** Tüm çerezler `Secure`, `HttpOnly`, `SameSite=Lax` (antiforgery `Strict`); her
    uygulamanın oturum çerezi ayrı adlı (`SelcukSSO.Oturum`, `DemoObis.Oturum`, `DemoYemekhane.Oturum`). Kimlik sunucusu oturumu (`Oturum:Sure`) ve istemci çerezleri
    (`Kimlik:OturumSuresi`) aynı süreyi kullanır: **8 saat, kayan**. *Gerekçe:* çerezler betikle okunamaz, HTTP
    üzerinden gönderilmez, siteler arası isteklerde sınırlı gider. İstemci çerezleri önceden ASP.NET Core
    varsayılanı olan 14 gün geçerliydi; kimlik sunucusu oturumundan uzun yaşayan istemci oturumu açığı kapatıldı.
11. **Tek çıkış (OIDC Back-Channel Logout 1.0).** Çıkışta, şifre sıfırlamada ve şifre değiştirmede ilgili oturumların
    istemcilerine imzalı `logout+jwt` gider; istemci imza, iss, aud, iat, events, `nonce` yokluğu ve `jti` tekrarını
    doğrular. İptal kayıtları dağıtık önbellekte, en az çerez süresi kadar tutulur; JWKS'e ulaşılamazsa 503.
    *Gerekçe:* kullanıcı bir yerden çıktığında tarayıcı açık kalmasa da tüm uygulamalardaki oturumları kapanır;
    tekrar oynatılan veya sahte bildirimler reddedilir.
12. **Giriş dönüşü hata yönetimi.** Geri tuşu, süresi dolmuş giriş veya reddedilen onay nedeniyle `/signin-oidc`
    başarısız olursa ham hata sayfası yerine kullanıcı ana sayfaya (giriş yapmışsa; yalnızca yerel dönüş adresi) veya
    `/GirisHatasi?neden=…` sayfasına yönlendirilir; URL'ye hata ayrıntısı konmaz, loglara code/state/token yazılmaz.
    Correlation, nonce ve token doğrulamaları değiştirilmedi. *Gerekçe:* geliştirici hata sayfası iç bilgi
    sızdırabilir; açık yönlendirme (open redirect) engellenir.
13. **Üçüncü taraf giriş başlatmada `iss` doğrulaması.** Uygulamalarım kartları `initiate_login_uri`'ye `iss`
    parametresi ekler; istemci bunu kendi kimlik sunucusunun issuer değeriyle karşılaştırır, eşleşmezse 400 döner
    (OIDC Core 1.0 bölüm 4). Uygulamanın kendi "Giriş yap" düğmesi `iss` göndermez. *Gerekçe:* istemcinin başka bir
    sunucu adına giriş başlatmaya yönlendirilmesini önler.
14. **Antiforgery, HTTPS ve HSTS.** Tüm formlarda antiforgery token; HTTPS yönlendirmesi ve OpenIddict'in HTTPS
    zorunluluğu; HSTS Development dışında açık (tarayıcı `localhost` için HSTS'yi önbelleğe almasın diye Development'ta
    kapalı). *Gerekçe:* siteler arası istek sahteciliğini (CSRF) ve şifrelenmemiş
    bağlantıda çerez/token çalınmasını önler.
15. **Sırların yönetimi.** Hiçbir şifre, client secret veya SMTP şifresi repoda yoktur: Development'ta
    `dotnet user-secrets`, diğer ortamlarda ortam değişkeni veya sır deposu. Şifreler, token'lar, sıfırlama
    bağlantıları ve çerez değerleri loglanmaz; Development e-postalarının yazıldığı klasör `.gitignore`'dadır. Test
    verileri kurgusaldır (`@test.local`). *Gerekçe:* repo veya log sızıntısı hesapları ele geçirmeye yetmemeli.

### Yanıt süresi eşitleme (kod incelemesi)

Zamanlama farkı birim testle ölçülmedi, çünkü ölçüm makineye ve yüke bağlı olarak kırılgan olurdu. Bunun yerine iki
yolun aynı işi yaptığı kodda doğrulandı:

- **Giriş** (`Pages/Account/Login.cshtml.cs`): her durumda önce `FindByNameAsync` (1 sorgu) çalışır.
  - Kullanıcı var, şifre yanlış: Identity PBKDF2 doğrulaması yapar, ardından hatalı deneme sayacını artırır.
  - Kullanıcı yok: `SahteSifreDogrulayici` aynı `IPasswordHasher` ve aynı PBKDF2 ayarlarıyla, önceden üretilmiş
    sahte bir özete karşı doğrulama yapar.
  - Hesap kilitli: Identity kilitli hesapta özet hesaplamadığı için yine sahte doğrulama yapılır.
  - Üç durumda da mesaj aynıdır. En pahalı adım olan PBKDF2 doğrulaması her yolda bir kez çalışır. Kalan fark,
    yanlış şifrede hatalı deneme sayacının yazılmasıdır (tek bir `UPDATE`); bu, PBKDF2 süresinin ve ağ
    gecikmesindeki dalgalanmanın yanında küçüktür.
- **Şifremi unuttum** (`Pages/Account/SifremiUnuttum.cshtml.cs`): her durumda hem `FindByNameAsync` hem
  `FindByEmailAsync` çalışır (hangisi eşleşirse eşleşsin 2 sorgu). Hesap yoksa bellekteki sahte bir kullanıcı için
  aynı token üretimi (veri koruma), bağlantı ve e-posta metni hazırlanır; veritabanına yazılmaz. Tek fark, var olan
  hesapta e-postanın bellek içi kuyruğa eklenmesidir. Gönderim (dosya veya SMTP) arka plan hizmetinde yapıldığı için
  yanıt süresine yansımaz. E-postası doğrulanmamış hesap, var olmayan hesap gibi işlenir.


## Demo uygulamalar

### Demo OBİS (`src/Selcuk.Demo.Obis`, https://localhost:5101)

| Sayfa | Erişim |
|---|---|
| Ana sayfa (`/`) | Herkese açık, "Giriş yap" butonu |
| Notlarım (`/Notlarim`) | Yalnızca `ogrenci` |
| Ders Yönetimi (`/DersYonetimi`) | Yalnızca `akademisyen` |
| Profilim (`/Profilim`) | Giriş yapmış herkes: ad soyad, rol, fakülte, bölüm |
| Token İnceleme (`/TokenInceleme`) | Yalnızca Development: id_token ve access_token'ın header/payload'u (imza hariç), son kullanma zamanı |
| Erişim reddedildi (`/ErisimReddedildi`) | Yetkisiz rol buraya yönlendirilir |
| Çıkış (`/Cikis`) | Yerel çerezi siler, kimlik sunucusunun end-session uç noktasına gider, dönüşte ana sayfa |

### Demo Yemekhane (`src/Selcuk.Demo.Yemekhane`, https://localhost:5102)

| Sayfa | Erişim |
|---|---|
| Haftalık menü (`/`) | Herkese açık, bu haftanın uydurma menüsü |
| Rezervasyonlarım (`/Rezervasyonlarim`) | Giriş yapmış herkes: sonraki 5 iş gününden seçip rezervasyon ekleme ve iptal (bellek içi) |
| Yemekhane Yönetimi (`/YemekhaneYonetimi`) | Yalnızca `personel`: günlük rezervasyon sayıları |
| Profilim, Erişim reddedildi, Çıkış | OBİS'teki gibi |

## Yeni bir uygulamayı sisteme bağlamak

1. **İstemciyi kaydedin.** `src/Selcuk.Identity/Data/Istemciler.cs` dosyasına `DemoObis`/`DemoYemekhane` gibi bir
   tanım ekleyin (client_id, görünen ad, kök adres, açıklama). Redirect, post-logout, `backchannel_logout_uri`,
   `client_uri` ve `initiate_login_uri` adresleri kök adresten türetilir. `BaslangicVerisi.StartAsync` içinde
   `IstemciKaydetAsync(..., "Istemciler:<Ad>:ClientSecret", Istemciler.<Ad>)` satırını ekleyin.
2. **Secret üretin** ve iki projenin user-secrets'ına aynı değeri yazın (bkz. [Sırlar](#sırlar-user-secrets)):
   kimlik sunucusunda `Istemciler:<Ad>:ClientSecret`, yeni uygulamada `Kimlik:ClientSecret`.
3. **Uygulamayı bağlayın.** Projeye `src/Selcuk.Istemci.Ortak` referansı ekleyin ve `Program.cs`'de:
   ```csharp
   builder.Services.AddSelcukSSO(builder.Configuration);   // Cookie + OIDC (code + PKCE), claim eşlemeleri
   // ...
   var app = builder.Build();
   app.UseSelcukGuvenlikBasliklari();                      // nosniff, no-referrer, DENY, CSP
   // UseHttpsRedirection, UseStaticFiles, UseRouting, UseAuthentication, UseAuthorization ...
   app.MapRazorPages();
   app.MapSelcukBackChannelLogout();                       // POST /signout-backchannel
   ```
4. **Ayarları yazın** (`appsettings.json`):
   ```json
   "Kimlik": {
     "Authority": "https://localhost:5001",
     "ClientId": "<client_id>",
     "CerezAdi": "<Uygulama>.Oturum",
     "GirisHatasiYolu": "/GirisHatasi",
     "OturumSuresi": "08:00:00"
   }
   ```
   `OturumSuresi` isteğe bağlıdır (varsayılan 8 saat, kayan); kimlik sunucusundaki `Oturum:Sure` ile aynı tutulmalıdır.
   `GirisHatasiYolu` isteğe bağlıdır (varsayılan `/GirisHatasi`): `/signin-oidc` dönüşü başarısız olduğunda (geri
   tuşu, süresi dolmuş giriş, reddedilen onay) giriş yapmamış kullanıcı bu sayfaya `?neden=oturum-suresi` veya
   `?neden=erisim-reddedildi` ile yönlendirilir.
5. **Sayfaları ekleyin:** `/Giris` (OIDC challenge; Uygulamalarım kartından gelen `iss` parametresini
   `AddSelcukSSO` doğrular), `/Cikis` (çerez + OIDC SignOut), `/ErisimReddedildi`, `/GirisHatasi`.
   Rol korumalı sayfalarda `[Authorize(Roles = "...")]` kullanın; rol claim'i `role`, ad claim'i `name`.
6. Kimlik sunucusunu yeniden başlatın: istemci kaydedilir ve "Uygulamalarım" sayfasında görünür.

## Klasör yapısı

```
SelcukSSO.sln
dotnet-tools.json                 dotnet-ef yerel aracı
.gitattributes                    Satır sonu kuralları
scripts/run-all.ps1               Üç uygulamayı ayrı pencerelerde başlatır
src/Selcuk.Identity/
  Program.cs                      Identity, OpenIddict, çerez/HSTS ayarları, e-posta, istek sınırı, discovery ek alanları
  GuvenlikBasliklari.cs           Güvenlik başlıkları ara katmanı
  Data/                           ApplicationUser, DbContext, IstemciOturumu, Roller, Istemciler, BaslangicVerisi, Migrations
  Eposta/                         IEpostaGonderici, DosyaEpostaGonderici, SmtpEpostaGonderici, kuyruk, şablonlar
  Hesap/                          Sıfırlama token sağlayıcısı, sahte şifre doğrulama, istek sınırı, oturum damgası,
                                  şifre değişikliği sonrası işler, Türkçe Identity hataları
  Oidc/                           Token/UserInfo uç noktaları, claim hedefleri, OturumKimligi (sid),
                                  BackChannelLogoutGonderici
  Pages/                          Uygulamalarım (Index), Account/Login, SifremiUnuttum, SifreSifirla, Hesabim,
                                  SifreDegistir, AccessDenied, Connect/Authorize, Logout
src/Selcuk.Istemci.Ortak/         AddSelcukSSO, güvenlik başlıkları, Back-Channel Logout alıcısı, OturumIptalDeposu
                                  (IDistributedCache), giriş dönüşü hataları, iss doğrulaması
src/Selcuk.Demo.Obis/             Notlarım, Ders Yönetimi, Profilim, Token İnceleme …
src/Selcuk.Demo.Yemekhane/        Haftalık menü, Rezervasyonlarım, Yemekhane Yönetimi …
tests/Selcuk.Identity.Tests/      Kimlik sunucusu entegrasyon, protokol, tek giriş/çıkış testleri
tests/Selcuk.Istemci.Ortak.Tests/ Back-Channel Logout alıcısı testleri (yerel anahtar + sahte JWKS)
tests/Selcuk.Demo.Obis.Tests/     Demo OBİS yönlendirme ve rol testleri
tests/Selcuk.Demo.Yemekhane.Tests/ Demo Yemekhane yönlendirme ve rol testleri
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
dotnet ef database update --project src/Selcuk.Identity      # veritabanını oluşturur / son migration'a getirir
dotnet dev-certs https --trust                               # (bir kez) HTTPS geliştirme sertifikasına güven
```

Migration'lar: `Baslangic` (Identity + OpenIddict tabloları), `IstemciOturumlari` (tek çıkış için oturum–istemci
kayıtları). Mevcut bir veritabanında yeni migration'ı uygulamak için yine `dotnet ef database update` yeterlidir.
Şifre sıfırlama yeni tablo gerektirmez (token'lar veritabanında tutulmaz).

`dotnet dev-certs https --trust` istemciler için gereklidir: istemciler kimlik sunucusunun discovery ve JWKS
adreslerine HTTPS ile bağlanır. Komut Windows'ta bir onay penceresi açar.

### Sırlar (user-secrets)

Hiçbir şifre veya secret repoda yoktur. Development'ta `dotnet user-secrets` kullanılır:

| Proje | Anahtar | İçerik |
|---|---|---|
| `src/Selcuk.Identity` | `TestKullanicilari:Ogrenci:Sifre`, `…:Akademisyen:Sifre`, `…:Personel:Sifre` | Test kullanıcılarının şifreleri |
| `src/Selcuk.Identity` | `Istemciler:DemoObis:ClientSecret` | demo-obis secret'ı |
| `src/Selcuk.Identity` | `Istemciler:DemoYemekhane:ClientSecret` | demo-yemekhane secret'ı |
| `src/Selcuk.Demo.Obis` | `Kimlik:ClientSecret` | demo-obis secret'ı (kimlik sunucusundakiyle aynı) |
| `src/Selcuk.Demo.Yemekhane` | `Kimlik:ClientSecret` | demo-yemekhane secret'ı (kimlik sunucusundakiyle aynı) |
| `src/Selcuk.Identity` (yalnızca Development dışı) | `Eposta:Smtp:Sifre` | SMTP şifresi; ortam değişkeni olarak `Eposta__Smtp__Sifre`. Development'ta SMTP kullanılmaz |

Görmek için:

```powershell
dotnet user-secrets list --project src/Selcuk.Identity
dotnet user-secrets list --project src/Selcuk.Demo.Obis
dotnet user-secrets list --project src/Selcuk.Demo.Yemekhane
```

Yeni bir makinede tanımlamak için (her istemci için ayrı secret üretin ve iki projeye de aynı değeri yazın):

```powershell
$b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
$s = [Convert]::ToBase64String($b)
dotnet user-secrets set "Istemciler:DemoYemekhane:ClientSecret" $s --project src/Selcuk.Identity
dotnet user-secrets set "Kimlik:ClientSecret" $s --project src/Selcuk.Demo.Yemekhane
```

Kimlik sunucusu her açılışta istemci tanımlarını, secret dahil, user-secrets'taki değerle eşitler.

## Çalıştırma

Üçünü birlikte başlatmak için:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1
```

Betik kimlik sunucusunu (5001), Demo OBİS'i (5101) ve Demo Yemekhane'yi (5102) ayrı PowerShell pencerelerinde
başlatır, hepsi hazır olunca tarayıcıda https://localhost:5001 adresini açar. Durdurmak için pencerelerde `Ctrl+C`.

Tek tek:

```powershell
dotnet run --project src/Selcuk.Identity          # https://localhost:5001
dotnet run --project src/Selcuk.Demo.Obis         # https://localhost:5101
dotnet run --project src/Selcuk.Demo.Yemekhane    # https://localhost:5102
```

Kimlik sunucusu açılışta rolleri (yoksa) oluşturur. Development'ta test kullanıcılarını ve iki demo istemciyi de
ekler.

## Elle deneme

### Tek giriş ve tek çıkış

1. `powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1`. Tarayıcıda https://localhost:5001 açılır ve giriş
   sayfasına yönlendirilirsiniz.
2. **`203011001`** ile giriş yapın. Şifre, `dotnet user-secrets list --project src/Selcuk.Identity` çıktısındaki
   `TestKullanicilari:Ogrenci:Sifre` değeri. **Uygulamalarım** sayfası açılır; Demo OBİS ve Demo Yemekhane kartları
   görünür.
3. **Demo OBİS** kartına tıklayın. Şifre sorulmaz. İlk seferde onay ekranı çıkar; **İzin ver** deyin. OBİS'e
   girmiş olursunuz. Bu sekmeyi açık bırakın.
4. Yeni bir sekmede https://localhost:5001 adresine dönüp **Demo Yemekhane** kartına tıklayın. Şifre sorulmaz; ilk
   seferde onay verin. **Rezervasyonlarım** açılır; bir gün seçip rezervasyon yapabilirsiniz.
5. Yemekhane'de **Çıkış** → kimlik sunucusunun çıkış sayfasında **Çıkış yap**. Yemekhane ana sayfasına dönersiniz.
6. OBİS sekmesini yenileyin, ya da https://localhost:5101/Notlarim adresine gidin. **OBİS de çıkış yapmış
   olmalıdır**: sayfa açılmaz, kimlik sunucusu yeniden giriş ister. Kimlik sunucusunun penceresindeki logda iki
   istemci için "Back-channel logout gönderildi" satırları görünür.

### Uygulama içi senaryolar

1. Demo OBİS'te **Notlarım**: 5 dersin vize, final ve harf notu görünür.
2. **Token İnceleme** (yalnızca Development): id_token ve access_token'ın header ve payload'u, son kullanma
   zamanlarıyla görünür. `sid`, `role`, `fakulte`, `bolum` ve `name` claim'lerini kontrol edin (tez ekran görüntüsü
   için).
3. Çıkış yapıp **`hoca.test`** (şifresi `TestKullanicilari:Akademisyen:Sifre`) ile girin. OBİS'te **Ders Yönetimi**
   açılır; `/Notlarim` adresine gidince **Erişim reddedildi** sayfası görünür.
4. **`personel.test`** (şifresi `TestKullanicilari:Personel:Sifre`) ile Yemekhane'de **Yemekhane Yönetimi** açılır
   ve günlük rezervasyon sayılarını gösterir. Öğrenci hesabıyla aynı sayfa **Erişim reddedildi** verir.
5. Demo OBİS'e giriş yaptıktan sonra tarayıcıda iki kez **geri** tuşuna basın. Uygulamanın ana sayfasına
   dönülmeli; "Correlation failed" veya başka bir hata sayfası görünmemelidir. Giriş yapmadan
   https://localhost:5101/signin-oidc?code=x&state=y adresine gidilirse **Giriş tamamlanamadı** sayfası ve
   **Tekrar giriş yap** düğmesi görünür.

### Şifre sıfırlama

1. Üç uygulamayı başlatın, **`203011001`** ile giriş yapıp **Demo OBİS** kartından OBİS'e girin. Bu sekmeyi açık
   bırakın.
2. Gizli (private) bir tarayıcı penceresinde https://localhost:5001/Account/Login → **Şifremi unuttum**. `203011001`
   (veya `203011001@test.local`) yazıp **Bağlantı gönder**. "Bilgiler bir hesapla eşleşiyorsa…" mesajı görünür.
   Olmayan bir kullanıcı adıyla da aynı mesaj görünür ve e-posta oluşmaz.
3. `src/Selcuk.Identity/App_Data/eposta/` klasöründeki en yeni `.eml` dosyasını bir e-posta istemcisiyle veya Not
   Defteri'yle açın. "Şifre sıfırlama" konulu e-postadaki bağlantıyı aynı gizli pencerede açın.
4. Yeni şifreyi iki kez yazıp **Şifreyi değiştir**. "Şifreniz değiştirildi" mesajı görünür. Klasörde "Şifreniz
   değiştirildi" konulu ikinci bir `.eml` oluşur.
5. İlk penceredeki **OBİS sekmesini yenileyin**: OBİS çıkış yapmış olmalıdır (kimlik sunucusu logunda "Back-channel
   logout gönderildi"). https://localhost:5001 adresi de yeniden giriş ister. Eski şifre artık geçmez, yeni şifre
   geçer. Aynı e-postadaki bağlantı ikinci kez açılırsa "Bağlantı geçersiz…" mesajı görünür.
6. Test kullanıcısının şifresini user-secrets'taki değere döndürmek için **Şifre değiştir** sayfasını kullanın
   (Uygulamalarım → **Hesabım** → **Şifre değiştir**). Bu sırada başka bir tarayıcıdaki oturum kapanır, şifreyi
   değiştirdiğiniz oturum açık kalır.

## Testler

```powershell
dotnet build
dotnet test
```

Testler LocalDB'ye, çalışan bir kimlik sunucusuna ve ağa ihtiyaç duymaz. E-postalar sahte bir göndericiyle bellekte
yakalanır.
- **Selcuk.Identity.Tests:** her test, SQLite in-memory üzerinde kendi veritabanıyla ayrı bir uygulama örneği
  başlatır (`EnsureCreated`, ortam `Testing`, geçici imzalama anahtarları, user-secrets okunmaz). Test kullanıcıları
  ve istemci secret'ları test içinde, çalışma anında üretilir. İstemciler uygulamanın kendi tanımlarıyla
  (`Istemciler.*`) kaydedilir. Back-Channel Logout POST'ları sahte bir HTTP handler ile yakalanır.
- **Selcuk.Istemci.Ortak.Tests:** kimlik sunucusu yerine yerel bir RSA anahtar çifti ile, bu anahtarı yayınlayan
  sahte discovery ve JWKS uç noktaları kullanılır.
- **Selcuk.Demo.Obis.Tests / Selcuk.Demo.Yemekhane.Tests:** discovery bilgisi sabit verilir. Rol testlerinde gerçek
  kimlik sunucusu yerine sahte bir kimlik doğrulama şeması kullanılır.

| Test | Doğruladığı |
|---|---|
| **Selcuk.Identity.Tests** | |
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
| `Oturum_aciksa_ikinci_istemci_sifre_sormadan_kod_aliyor` | OBİS'ten sonra Yemekhane: giriş formu yok; onaydan sonra doğrudan kod |
| `Iki_istemcinin_id_tokeninda_ayni_sid_var` | İki id_token'da aynı `sid`; `IstemciOturumlari`'nda iki açık kayıt |
| `Cikista_her_istemciye_dogrulanabilir_logout_token_gidiyor` | İki `backchannel_logout_uri`'ye form POST; JWKS ile imza, `typ logout+jwt`, aud, sid, sub, jti, events, nonce yok; kayıtlar kapanır |
| `Bir_istemciye_ulasilamasa_da_digerine_gonderiliyor` | OBİS'e bağlanılamazken Yemekhane yine bildirilir; çıkış tamamlanır |
| `Discovery_back_channel_logout_ve_claims_supported_iceriyor` | `backchannel_logout(_session)_supported: true`; claims_supported ⊇ sub, name, email, role, fakulte, bolum, sid |
| `Uygulamalarim_sayfasi_istemci_kartlarini_gosteriyor` | Girişsiz `/` → giriş sayfası; girişli: iki kart ve `iss` parametreli `initiate_login_uri` bağlantıları |
| `Giris_sayfasinda_sifremi_unuttum_baglantisi_var` | Giriş sayfasında bağlantı; Şifremi unuttum sayfası 200 |
| `Var_olan_ve_olmayan_hesap_icin_sifremi_unuttum_yaniti_ayni` | Kullanıcı adı, e-posta, olmayan kullanıcı ve olmayan e-posta için durum kodu ve HTML birebir aynı |
| `Var_olan_hesaba_baglantili_eposta_gidiyor_olmayana_gitmiyor` | Yalnızca var olan hesaba başlıklı, "30 dakika" bilgili, issuer adresli bağlantı; olmayan hesaplara e-posta yok |
| `Gecerli_tokenla_sifirlama_calisiyor_eski_sifre_gecmiyor_yeni_geciyor` | Sıfırlama başarılı, güvenlik damgası değişti, eski şifre reddedilir, yeni şifre geçer, bilgilendirme e-postası |
| `Ayni_token_ikinci_kez_kullanilamiyor` | İkinci kullanımda "Bağlantı geçersiz…"; şifre ilk sıfırlamadaki kalır |
| `Sifirlama_tokeni_30_dakika_gecerli_ve_ayri_saglayiciyla_uretiliyor` | `PasswordResetTokenProvider` = `SifreSifirlama`, ömür 30 dk |
| `Suresi_dolmus_token_reddediliyor` | Ömrü kısaltılmış (1 ms) token süre geçince reddedilir, şifre değişmez |
| `Sifirlamadan_sonra_acik_oturumlara_back_channel_gidiyor_ve_kimlik_oturumu_kapaniyor` | Kullanıcının açık her oturum–istemci çiftine logout_token; kapalı kayıt ve başka kullanıcı etkilenmez; kimlik sunucusu oturumu kapanır |
| `Sifirlama_kilitli_hesabin_kilidini_aciyor` | Kilit ve hatalı deneme sayacı sıfırlanır, yeni şifreyle giriş |
| `Yanlis_mevcut_sifre_reddediliyor_dogru_sifreyle_degisiyor_diger_oturumlar_kapaniyor` | Yanlış mevcut şifre: değişiklik ve bildirim yok. Doğru şifre: aynı sid'le oturum açık, diğer oturum kapanır, yalnızca onun istemcilerine logout_token, e-posta |
| `Sifre_degistirme_ve_hesabim_giris_gerektiriyor` | Girişsiz → giriş sayfası |
| `Giris_POST_siniri_asilinca_429` | 10 POST'tan sonra 429, `Retry-After`, Türkçe sayfa; GET sınırlanmaz |
| `Olmayan_kullanici_ve_yanlis_sifre_ayni_mesaji_donuyor` | Durum kodu ve sayfa (yazılan kullanıcı adı ve antiforgery token'ı dışında) aynı |
| **Selcuk.Istemci.Ortak.Tests** | |
| `Gecerli_logout_token_oturumu_sonlandiriyor` | 200 (`no-store`); o sid'li oturum sonraki istekte authorize'a yönlenir |
| `Imzasi_gecersiz_token_reddediliyor` | 400, oturum etkilenmez |
| `Yanlis_aud_reddediliyor` | 400, oturum etkilenmez |
| `Events_eksik_token_reddediliyor` | 400, oturum etkilenmez |
| `Nonce_iceren_token_reddediliyor` | 400, oturum etkilenmez |
| `Ayni_jti_ikinci_kez_reddediliyor` | İlki 200, ikincisi 400 |
| `Farkli_sidli_oturum_etkilenmiyor` | sid-1 kapanır, sid-2 açık kalır |
| `Guvenlik_basliklari_istemci_yanitlarinda_mevcut` | İstemci yanıtlarında nosniff, no-referrer, DENY, CSP |
| `Cerez_suresi_yapilandirmadan_geliyor_ve_kayan` | Varsayılan 8 saat; `Kimlik:OturumSuresi` = 02:30 → 150 dk; ikisinde de kayan süre |
| `Iptal_kaydi_ortak_dagitik_onbellekten_okunuyor` | Aynı `IDistributedCache`'i paylaşan iki örnek: logout_token birinciye gelir, iki örnekteki oturum da kapanır |
| `Jwks_ulasilamazsa_503` | JWKS'e bağlanılamazken 503; oturum etkilenmez |
| `Yanlis_iss_ile_baslatilan_giris_reddediliyor` | Yabancı, eğik çizgisiz veya boş `iss`: 400, yönlendirme yok |
| `Dogru_iss_ile_veya_iss_olmadan_giris_kimlik_sunucusuna_gidiyor` | Doğru `iss` veya `iss` yok: authorize'a yönlendirme |
| **Selcuk.Demo.Obis.Tests** | |
| `Girissiz_kullanici_Notlarim_icin_kimlik_sunucusuna_yonlendiriliyor` | 302 → `https://localhost:5001/connect/authorize`, `code_challenge` + `S256` |
| `Ogrenci_Notlarim_acilir_Ders_Yonetimi_reddedilir` | ogrenci: Notlarım 200, Ders Yönetimi → Erişim reddedildi |
| `Akademisyen_Ders_Yonetimi_acilir_Notlarim_reddedilir` | akademisyen: Ders Yönetimi 200, Notlarım → Erişim reddedildi |
| `Token_Inceleme_Development_disinda_kapali` | Development dışında 404 |
| **Selcuk.Demo.Yemekhane.Tests** | |
| `Haftalik_menu_herkese_acik` | Girişsiz 200, başlıkta "Demo Yemekhane — test uygulaması" |
| `Girissiz_kullanici_Rezervasyonlarim_icin_kimlik_sunucusuna_yonlendiriliyor` | 302 → authorize, `client_id=demo-yemekhane`, `code_challenge` + `S256` |
| `Ogrenci_Yemekhane_Yonetimi_erisim_reddedildi` | ogrenci: Yönetim → Erişim reddedildi; Rezervasyonlarım 200 |
| `Personel_Yemekhane_Yonetimi_aciliyor` | personel: Yönetim 200 |

## Sürümler

| Bileşen | Sürüm |
|---|---|
| .NET SDK / hedef çatı | 10.0.402 / `net10.0` (çalışma zamanı 10.0.13) |
| **OpenIddict** (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`) | **7.7.2** (NuGet'teki en güncel kararlı sürüm) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.EntityFrameworkCore.SqlServer / .Design | 10.0.13 |
| Microsoft.AspNetCore.Authentication.OpenIdConnect (Selcuk.Istemci.Ortak) | 10.0.13 |
| System.Security.Cryptography.Pkcs (açık kapatmak için sabitlendi; Identity + Identity.Tests) | 10.0.13 |
| System.Security.Cryptography.Xml (açık kapatmak için sabitlendi; Identity.Tests) | 10.0.13 |
| dotnet-ef (yerel araç) | 10.0.13 |
| Test: xunit / xunit.runner.visualstudio / Microsoft.NET.Test.Sdk | 2.9.3 / 3.1.4 / 17.14.1 |
| Test: Microsoft.AspNetCore.Mvc.Testing / Microsoft.EntityFrameworkCore.Sqlite | 10.0.13 / 10.0.13 |
| Veritabanı | SQL Server 2022 Express LocalDB |

## Yapılacaklar (yol haritası)

- [x] **Demo OBİS** istemcisi (Authorization Code + PKCE ile giriş, rol bazlı sayfalar)
- [x] **Demo Yemekhane** istemcisi (ikinci istemci; tek girişle iki uygulamaya şifresiz geçiş)
- [x] **Tek çıkış** (single logout): OIDC Back-Channel Logout 1.0 ile bir uygulamadan çıkınca diğerlerinden de
      çıkış. Front-channel logout eklenmedi.
- [x] **Şifre sıfırlama** (e-posta ile 30 dakikalık, tek kullanımlık bağlantı), **şifre değiştirme**, istek sınırı ve
      kullanıcı adı tahmini koruması
- [ ] **2FA** (TOTP doğrulayıcı uygulama)
- [ ] **Denetim kaydı** (giriş, başarısız deneme, kilitleme, onay, çıkış olayları)
- [ ] **Yönetim paneli** (kullanıcı, rol ve istemci uygulama yönetimi)
- [ ] **Token iptali** (revocation uç noktası, oturum/yetkilendirme iptali)
- [ ] **Production sertifika yönetimi**: şu an yalnızca Development sertifikaları (`AddDevelopmentSigningCertificate`,
      `AddDevelopmentEncryptionCertificate`) kullanılıyor. Production'da kalıcı X.509 imzalama/şifreleme
      sertifikaları (sertifika deposu veya Key Vault), anahtar rotasyonu ve `OpenIddict:Issuer`'ın gerçek alan adına
      ayarlanması gerekiyor. Development ve Testing dışındaki ortamlarda uygulama bilerek açılmaz.
