# Tek Şifre Giriş Sistemi (SelcukSSO)

Bitirme projesi: fakülte ve bölüm web uygulamaları için merkezi kimlik sunucusu.

> **Proje tanımı (bölümün verdiği):** "Fakülte ve bölüm genelinde kullanılan tüm web otomasyonları için merkezi
> bir kimlik doğrulama ve yetkilendirme altyapısı sunar. Kullanıcılar (öğrenci, akademisyen, idari personel) tek bir
> kullanıcı adı ve şifre ile güvenli bir şekilde tüm alt uygulamalara giriş yapabilir. Token tabanlı (JWT vb.) oturum
> yönetimini, şifre sıfırlama vb. özelliklere sahiptir."

Depoda şu an **kimlik sunucusu**, ona OpenID Connect ile bağlanan iki alt uygulama (**Demo OBİS**, **Demo Yemekhane**),
bu uygulamaların ortak istemci kütüphanesi ve access token'ı doğrulayan bir kaynak sunucusu (**Demo API**) var. **Tek giriş** (bir kez giriş, diğer uygulamalara şifresiz geçiş),
OIDC Back-Channel Logout ile **tek çıkış** (bir uygulamadan çıkınca hepsinden çıkış), e-postayla **şifre sıfırlama**,
**şifre değiştirme**, TOTP tabanlı **iki aşamalı doğrulama** (2FA), güvenlik olaylarının **denetim kaydı** ve
**uygulama bazlı erişim izni** ve **token yaşam döngüsü** (kısa ömürlü access token, kayan refresh token, iptal)
çalışıyor. Ayrıca yerelde çalışan **Tek Kapı** köprüsü (`araclar/tek-kapi`) public bir
OIDC istemcisi olarak bağlı ([Tek Kapı köprüsü](#tek-kapı-köprüsü)): SSO ile giriş, Back-Channel Logout ile tek çıkış.
Alınan önlemler [Güvenlik önlemleri](#güvenlik-önlemleri) bölümünde. Diğer özellikler
[yol haritasında](#yapılacaklar-yol-haritası).

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
                     /signout-backchannel alıcısı, token yenileme/iptal

     Selcuk.Demo.Obis ── Authorization: Bearer <access token> ──► Selcuk.Demo.Api :5201
                                                                   GET /api/notlar (JWKS ile doğrulama)
```

| Uç nokta | Adres | Açıklama |
|---|---|---|
| Discovery | `/.well-known/openid-configuration` | OIDC meta verisi (`backchannel_logout_supported: true`) |
| JWKS | `/.well-known/jwks` | İmza doğrulama anahtarları |
| Authorize | `/connect/authorize` | Giriş ve onay ekranı (Razor Page) |
| Token | `/connect/token` | `authorization_code` ve `refresh_token` grant'leri |
| Revocation | `/connect/revoke` | RFC 7009: gizli istemciler refresh token'ı iptal eder (çıkışta) |
| UserInfo | `/connect/userinfo` | Kapsama göre kullanıcı bilgisi |
| End-session | `/connect/logout` | Çıkış sayfası; Back-Channel Logout bildirimlerini gönderir, `post_logout_redirect_uri` ile istemciye döner |
| Uygulamalarım | `/` | Giriş yapmış kullanıcıya kayıtlı uygulamaların kartları; kart `initiate_login_uri?iss=…` adresine gider |
| Şifremi unuttum | `/Account/SifremiUnuttum` | Kullanıcı adı, öğrenci no veya e-postayla sıfırlama bağlantısı isteği |
| Şifre sıfırlama | `/Account/SifreSifirla?kullanici=…&kod=…` | E-postadaki bağlantı: yeni şifre + tekrar |
| Hesabım | `/Account/Hesabim` | Hesap bilgileri; **Şifre değiştir** (`/Account/SifreDegistir`) |
| Girişin ikinci adımı | `/Account/IkiAdim` | 2FA açık kullanıcıda şifreden sonra: TOTP kodu veya kurtarma kodu, "Bu cihazı hatırla" |
| İki aşamalı doğrulama | `/Account/IkiAdimDogrulama` | Hesabım → 2FA kurulumu (anahtar + QR), kurtarma kodlarını yenileme, kapatma |
| Ek doğrulama | `/Account/EkDogrulama` | 2FA isteyen bir uygulamaya şifreyle açılmış oturumdan gidilince ikinci adım (step-up) |
| Güvenlik geçmişi | `/Account/GuvenlikGecmisi` | Hesabım → kullanıcının kendi son 20 güvenlik olayı |

**Akışlar:** yalnızca Authorization Code + PKCE ve Refresh Token. PKCE sunucu genelinde zorunlu ve **yalnızca
`S256`** kabul edilir (`plain` kapalı). Implicit, hybrid, password ve client credentials akışları kapalı.

**Kapsamlar ve claim'ler:**

| Kapsam | Claim'ler |
|---|---|
| `openid` | `sub`; id_token'da ayrıca `sid` (kimlik sunucusu oturum kimliği) ve `amr` (`["pwd"]` veya `["pwd","mfa"]`) |
| `profile` | `name` (AdSoyad), `preferred_username` (öğrenci no / kullanıcı adı), `fakulte`, `bolum` |
| `email` | `email` (userinfo'da ayrıca `email_verified`) |
| `roles` | `role` (`ogrenci`, `akademisyen`, `personel`) |
| `offline_access` | Claim yok; refresh token verilir (yalnızca `demo-obis`, `demo-yemekhane`) |
| `notlar.oku` | Claim yok; access token'ın `aud`'u `demo-api` olur (Demo API kaynağı, yalnızca `demo-obis`) |

Claim'ler yalnızca kullanıcı ilgili kapsama onay verdiyse tokenlara yazılır. Profil ve e-posta bilgileri yalnızca
id_token'da ve userinfo'dadır; access token küçültülmüştür ([Token yaşam döngüsü](#token-yaşam-döngüsü)). Access
token'lar **imzalı ama şifresiz JWT** (`typ: at+jwt`, `DisableAccessTokenEncryption`), içerikleri tezde gösterilebilir.
Kaynak sunucusu kapsamı (`notlar.oku`) istenmişse `aud` = `demo-api`, istenmemişse `aud` yoktur; token'ın verildiği
istemci her durumda `client_id` claim'inde taşınır. Discovery'deki `claims_supported` listesi: `aud exp iat iss sub name email role fakulte bolum sid amr`.

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

Demo istemcilerin ortak kuralları: gizli (confidential) web istemcisi, `client_secret` user-secrets'ta; izinler authorization, token,
end-session ve revocation uç noktaları, `authorization_code` ve `refresh_token` grant'leri, `code`,
`openid profile email roles offline_access` (`demo-obis` ayrıca `notlar.oku`); PKCE zorunlu; onay türü
`explicit` (her uygulama için ilk girişte onay ekranı). Özel ayarlar (`backchannel_logout_uri`, `client_uri`,
`initiate_login_uri`, `aciklama`, ayrıca erişim izni için `izinliRoller` ve `izinliKullaniciAdlari`) OpenIddict istemci
kaydının `Properties` alanında saklanır. Demo istemcilerde izin listeleri boştur (herkes girebilir). Üçüncü kayıtlı
istemci **`tek-kapi`** (public, loopback adresli) [Tek Kapı köprüsü](#tek-kapı-köprüsü) bölümünde.

Tüm istemci adresleri https olmalıdır; tek istisna yerel (native) uygulamalar için `http://127.0.0.1` loopback
adresleridir (RFC 8252). Kural iki yerde uygulanır: kayıtta (`Istemciler.AdresleriDogrula`, açılışta) ve protokol
düzeyinde (authorize isteğindeki `redirect_uri` ile end-session isteğindeki `post_logout_redirect_uri` için OpenIddict
doğrulama işleyicisi; ihlalde istemciye yönlendirmeden 400 `invalid_request`). OpenIddict'in kendisi http adreslerini
sunucu adına bakmadan kabul ettiği için bu kural ayrıca eklendi.

## Uygulama bazlı erişim izni

Her istemcinin kaydında iki isteğe bağlı liste vardır (OpenIddict `Properties`, JSON dizisi):

| Ayar | İçerik |
|---|---|
| `izinliRoller` | Rol adları (ör. `["akademisyen"]`) |
| `izinliKullaniciAdlari` | Kullanıcı adları / öğrenci numaraları (ör. `["203011001"]`) |

İkisi de boş veya yoksa herkes girebilir (önceki davranış; `demo-obis` ve `demo-yemekhane` böyle). Biri doluysa
kullanıcının rollerinden biri `izinliRoller`'de **veya** kullanıcı adı `izinliKullaniciAdlari`'nda olmalıdır
(büyük/küçük harf duyarsız). Hatalı biçimli (dizi olmayan) bir ayar istemciyi herkese açmaz, kimse uymaz sayılır.
Kural `Oidc/IstemciErisimi` sınıfındadır ve üç yerde uygulanır:

- **Authorize** (`/connect/authorize`, GET ve onay POST'u): izinsiz kullanıcı onay ekranı veya 2FA kurulumu görmeden
  istemcinin `redirect_uri`'sine OIDC standardındaki `error=access_denied` (ve `state`) ile döner; kod verilmez, onay
  kaydı oluşmaz.
- **Token** (`/connect/token`): yetkilendirme kodu ve refresh token için yeniden kontrol edilir. Kod alındıktan sonra
  izin kaldırıldıysa `invalid_grant` döner.
- **Uygulamalarım**: kullanıcının girmeye izni olmayan uygulamaların kartı gösterilmez.

İzinsiz her deneme denetim kaydına **"İstemciye erişim reddedildi"** (`IstemciErisimiReddedildi`) olarak, `client_id`
ve `uc=authorize` / `uc=token` ayrıntısıyla yazılır. İzinler şimdilik koddaki istemci tanımında ve (Tek Kapı için)
ayarlardadır; yönetim paneli yol haritasındadır.

### İstemci bazlı 2FA şartı (`mfaZorunlu`)

İstemci kaydındaki isteğe bağlı `mfaZorunlu` (bool) ayarı `true` ise o uygulamaya yalnızca **iki aşamalı doğrulanmış**
bir kimlik sunucusu oturumundan (id_token `amr` içinde `mfa`) kod ve token verilir. `tek-kapi` için `true`, demo
istemciler için `false`tur (davranışları değişmedi). Uygulamalarım'da böyle bir uygulamanın kartında **"2FA gerekli"**
etiketi görünür.

Authorize isteğinde oturumun `amr`'si `mfa` içermiyorsa (yalnızca şifreyle girildiyse veya "Bu cihazı hatırla" ile
ikinci adım atlandıysa; o oturum `["pwd"]` sayılır):

- **2FA'sı açık kullanıcı → ek doğrulama (step-up):** `/Account/EkDogrulama?returnUrl=<authorize isteği>`. Mevcut
  oturumda TOTP veya kurtarma kodu istenir ("Bu uygulama iki aşamalı doğrulama gerektiriyor"). Başarılıysa oturum aynı
  `sid`'le `["pwd","mfa"]` olarak yeniden imzalanır ve authorize akışı kaldığı yerden devam eder. Hatalı kodlar hatalı
  deneme sayacına eklenir (5'te kilit), kilitliyken kabul edilmez; TOTP tekrar oynatma koruması ve istek sınırı
  girişin ikinci adımıyla aynıdır. Kurtarma kodu kullanılırsa e-posta gider.
- **2FA'sı kapalı kullanıcı → kurulum:** `/Account/IkiAdimDogrulama?returnUrl=…`, "Bu uygulama iki aşamalı doğrulama
  gerektiriyor" mesajıyla. Kurulumda doğrulanan kod oturumu çok faktörlü yapar; kurtarma kodlarının altındaki
  "Kodları kaydettim, devam et" authorize isteğine döner.
- `prompt=none` ise istemciye `interaction_required` döner; onay formu doğrudan gönderilirse `access_denied`.

Token uç noktası da denetler: `mfaZorunlu` istemciye, `amr`'si `mfa` içermeyen bir yetkilendirmeden (ör. kod alındıktan
sonra istemci 2FA ister hâle geldiyse) token verilmez (`invalid_grant`). Denetim kaydı: yönlendirme ve retler
**"Uygulama iki aşamalı doğrulama istedi"** (`IstemciMfaGerekli`, `client_id`, `uc=authorize; yonlendirme=ek-dogrulama|kurulum`,
`uc=authorize; neden=onay-formu` veya `uc=token`), ek doğrulama sonucu `EkDogrulama` (başarılı/başarısız, `yontem=totp|kurtarma-kodu`).

## Tek Kapı köprüsü

**Tek Kapı**, kullanıcının **kendi bilgisayarında** çalışan ayrı bir Node.js uygulamasıdır (`araclar/tek-kapi`;
kendi README ve AGENTS.md dosyaları orada). Amaç,
henüz OIDC desteklemeyen gerçek üniversite sistemlerine (OBİS, UZEM, e-posta) bu bilgisayardan otomatik giriş yapan
yerel ve kişisel bir **geçiş dönemi aracı** (ESSO, enterprise single sign-on) sunmaktır. Kullanıcı Tek Kapı'ya Tek
Şifre Giriş Sistemi ile (OIDC) girer; üniversite sistemlerinin şifrelerini Tek Kapı kendi bilgisayarında, şifreli
olarak saklar ve kullanır.

- **Kimlik sunucusu üniversite şifrelerini hiçbir zaman görmez ve saklamaz.** Kimlik sunucusu Tek Kapı'ya yalnızca
  standart OIDC token'ları (id_token, access_token) verir. Gerçek sistemlere bağlantı ve şifre saklama tamamen Tek
  Kapı'nın, kullanıcının bilgisayarındadır. Kimlik sunucusu ve demo uygulamalar gerçek Selçuk sistemlerine
  bağlanmaz; tek istisna, kullanıcının kendi hesabıyla kendi bilgisayarında çalıştırdığı `araclar/tek-kapi`'dir.
  Hiçbir test gerçek Selçuk sistemlerine istek atmaz.
- **Kalıcı çözüm bu değildir.** Ekran/form doldurmaya dayanan ESSO kırılgandır ve şifrelerin bir yerde tutulmasını
  gerektirir. Gerçek sistemlerin kalıcı çözümü, Demo OBİS ve Demo Yemekhane gibi **OIDC ile doğrudan** kimlik
  sunucusuna bağlanmalarıdır ([Yeni bir uygulamayı sisteme bağlamak](#yeni-bir-uygulamayı-sisteme-bağlamak)).

**İstemci kaydı** (`Istemciler.TekKapi`, Development'ta açılışta oluşturulur veya güncellenir):

| | `tek-kapi` |
|---|---|
| Tür | **Public** (`client_secret` yok), uygulama türü native; PKCE (`S256`) zorunlu |
| Akış / yanıt türü | `authorization_code` / `code` |
| Kapsamlar | `openid profile roles` (e-posta yok) |
| Onay türü | `explicit` |
| Redirect URI | `http://127.0.0.1:4545/auth/callback` |
| Post-logout redirect URI | `http://127.0.0.1:4545/` |
| `backchannel_logout_uri` | `http://127.0.0.1:4545/auth/backchannel-logout` |
| `client_uri` / `initiate_login_uri` | `http://127.0.0.1:4545/` / `http://127.0.0.1:4545/auth/login` |
| Görünen ad | Tek Kapı — gerçek Selçuk sistemleri (yerel) |
| `izinliKullaniciAdlari` | `Istemciler:TekKapi:IzinliKullaniciAdlari` ayarından (`appsettings.Development.json`, varsayılan `["203011001"]`) |
| `mfaZorunlu` | `true`: yalnızca iki aşamalı doğrulanmış oturumdan kod/token (bkz. Uygulama bazlı erişim izni > İstemci bazlı 2FA şartı). Tek Kapı da callback'te `amr`'yi denetler (`SSO_MFA_ZORUNLU`, varsayılan açık) |

İzin listesi ayarda yoksa veya boşsa Tek Kapı **herkese açılmaz**; varsayılan `["203011001"]` kullanılır. Listeye
kullanıcı eklemek için `appsettings.Development.json`'daki diziyi değiştirip kimlik sunucusunu yeniden başlatın.

**Neden public ve neden http?** Tek Kapı kullanıcının bilgisayarında çalışan yerel (native) bir uygulamadır; içine
konan bir `client_secret` o bilgisayardaki herkes tarafından okunabilir, yani gizli tutulamaz (RFC 8252 bölüm 8.5).
Bu yüzden secret yerine PKCE kullanılır: kodu ele geçiren, `code_verifier`'ı bilmeden token alamaz. Yönlendirme
adresi `http://127.0.0.1` loopback'tir (RFC 8252 bölüm 7.3): istek bilgisayardan dışarı çıkmaz, TLS sertifikası
gerekmez; `localhost` adı yerine IP kullanılır (ad çözümlemesi değiştirilemesin, bölüm 8.3). OpenIddict http loopback
adreslerini **hiçbir güvenlik ayarı gevşetilmeden** kabul eder (adresler birebir karşılaştırılır).

**Back-Channel Logout:** çıkışta Tek Kapı'nın `http://127.0.0.1:4545/auth/backchannel-logout` adresine de imzalı
`logout_token` gider (gönderici https dışında yalnızca `http://127.0.0.1`'e gönderir). OIDC Back-Channel Logout 1.0
bölüm 2.2 bu adresin https kullanmasını önerir (SHOULD); http'yi açıkça yalnızca gizli istemciler için anar. Burada
istisna bilinçlidir: adres loopback olduğu için token makineden çıkmaz, `logout_token` bir erişim yetkisi taşımaz
(yalnızca oturumu kapattırır) ve alıcının imzayı, `aud`'u ve `jti`'yi doğrulaması gerekir. Tek Kapı çalışmıyorsa
gönderim hatası loglanır ve denetim kaydına "çıkış bildirimi ulaştırılamadı" yazılır; diğer istemcilere bildirim
gider, çıkış tamamlanır.

**Tek Kapı tarafı (SSO modu):** `SSO_ISSUER` tanımlıysa Tek Kapı portala erişimi iki katmanlı yapar: önce kimlik
sunucusunda SSO oturumu (openid-client; PKCE `S256`, state ve nonce sunucuda tek kullanımlık; id_token imzası, `iss`,
`aud`, `nonce`, `exp` doğrulanır; yalnızca izinli kullanıcılar), sonra kasa ana şifresi (kasa anahtarı kimlik
sunucusundan türetilmez). `logout_token` gelince (jose ile imza, `iss`, `aud`, `iat`, `typ`, `events`, `sid`/`sub`,
`nonce` yokluğu, `jti` tekrarı doğrulanır) kasa kilitlenir, yönetilen penceredeki gerçek site sekmeleri kapanır ve
çerezleri silinir. Node, kimlik sunucusunun .NET geliştirme sertifikasına `--use-system-ca` ile güvenir; TLS
doğrulaması kapatılmaz. Ayrıntılar: `araclar/tek-kapi/README.md` > SSO modu.

**Güvenlik notu:** Tek Kapı public istemcidir (RFC 8252): secret yok, PKCE zorunlu, adresler yalnızca 127.0.0.1.
Üniversite şifreleri yalnızca o bilgisayardaki şifreli kasada durur; kimlik sunucusu onları hiç görmez.

**Çalıştırma:** üç uygulama ve Tek Kapı (SSO modunda, ayrı pencerede) birlikte:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1 -TekKapi
```

Ardından https://localhost:5001 → **203011001** ile giriş → Uygulamalarım → **Tek Kapı** kartı → onay → kasa ana şifresi.
Kimlik sunucusundan veya Demo OBİS'ten çıkış yapınca Tek Kapı kilitlenir ve gerçek site sekmeleri kapanır. Taşıma
sonrası Tek Kapı kasası yeni konumda yeniden kurulur (`data/` taşınmadı).

**Güvenlik ayarları:** şifre politikası, kilitleme, istek sınırı, çerezler ve başlıklar
[Güvenlik önlemleri](#güvenlik-önlemleri) bölümünde.

## Token yaşam döngüsü

| Token | Ne işe yarar | Kime gider | Ömür | Biçim |
|---|---|---|---|---|
| **id_token** | Kullanıcının kim olduğunu ve nasıl doğrulandığını istemciye bildirir (giriş kanıtı); profil (`name`, `preferred_username`, `fakulte`, `bolum`, `email`), `sid`, `amr` | İstemci uygulama (OBİS, Yemekhane, Tek Kapı) | OpenIddict varsayılanı (20 dk); yalnızca girişte doğrulanır, oturum çerezi ayrıca tutulur | İmzalı JWT |
| **access token** | Bir API'yi (kaynak sunucusu) çağırma yetkisi. Veri minimizasyonu (KVKK): yalnızca `iss`, `sub`, `aud`, `exp`, `iat`, `jti`, `client_id`, `scope`, `role`, `sid` ve OpenIddict'in kişisel veri içermeyen iç tanımlayıcıları (`oi_prst`, `oi_au_id`, `oi_tkn_id`); ad, e-posta, fakülte, bölüm **yok** | Kaynak sunucusu (Demo API: `aud` = `demo-api`) ve userinfo | **15 dakika** (`Tokenlar:AccessTokenSuresi`) | İmzalı, şifresiz JWT (`typ: at+jwt`) |
| **refresh token** | Kullanıcı tekrar giriş yapmadan yeni access token almak | Yalnızca gizli istemci (sunucu tarafında, çerezde); `offline_access` kapsamıyla | **8 saat**, kayan (`Tokenlar:RefreshTokenSuresi`, kimlik sunucusu oturumuyla aynı) | Kimlik sunucusunca şifreli; istemci içeriğini okuyamaz |

`offline_access` yalnızca `demo-obis` ve `demo-yemekhane`'ye izinlidir (`refresh_token` grant izni); `tek-kapi`
isteyemez (OpenIddict isteği 400 ile reddeder), çünkü yalnızca id_token ile kendi oturumunu kurar. Onay ekranında
açıklaması: "Uygulama, oturumun açıkken erişimini sen tekrar giriş yapmadan yenileyebilir."

**Kayan (rolling) refresh token ve tekrar kullanım tespiti.** OpenIddict'te kayan refresh token varsayılan olarak
açıktır: her kullanımda yeni bir refresh token verilir, kullanılan "redeemed" işaretlenir. Kullanılmış bir refresh
token tekrar gelirse OpenIddict isteği `invalid_grant` ile reddeder ve **aynı yetkilendirmeye bağlı tüm token'ları
iptal eder** (yeni refresh token dahil). OpenIddict'in varsayılan **30 saniyelik tekrar kullanım payı**
(`RefreshTokenReuseLeeway`, eşzamanlı istekler için) kapatıldı (`SetRefreshTokenReuseLeeway(null)`): kullanılmış bir
refresh token hiçbir zaman kabul edilmez. Eşzamanlı istekler istemci tarafında tek uçuşla çözülür. Tekrar kullanım
denetim kaydına **"Refresh token tekrar kullanıldı"** (`RefreshTokenTekrarKullanildi`, kullanıcı ve `client_id` ile)
olarak yazılır (`Oidc/RefreshTokenTekrarDenetimi`, OpenIddict'in reddeden işleyicisinden hemen önce).

**Oturuma bağlı yetkilendirme.** Onay ekranının tekrar sorulmaması için kalıcı (permanent) yetkilendirme korunur;
ama authorize her kod verişinde o kimlik sunucusu oturumuna (`sid`) ait ayrı bir **ad-hoc yetkilendirme** açar
(`Oidc/TokenIptali`) ve kod ile ondan türeyen tüm token'lar ona bağlanır. Böylece iptal oturum bazında yapılabilir,
tekrar kullanılan bir refresh token da yalnızca o oturumun zincirini düşürür.

**İstemci tarafında otomatik yenileme** (`Selcuk.Istemci.Ortak/TokenYenileyici`): çerezdeki access token'ın süresi
**1 dakikadan az** kaldıysa çerez doğrulaması sırasında refresh token ile sessizce yenilenir ve çerez güncellenir.
Aynı refresh token için eşzamanlı gelen istekler **tek bir** token isteği yapar (tek uçuş; sonuç 1 dakika boyunca, yeni
çerezi henüz almamış istekler için de paylaşılır). Yenileme `invalid_grant` ile reddedilirse (iptal edilmiş, süresi
dolmuş veya tekrar kullanılmış) yerel oturum kapanır ve korumalı sayfa kullanıcıyı yeniden kimlik doğrulamaya
gönderir. Ağ hatasında oturum korunur. Token değerleri loglanmaz. Tek uçuş kaydı uygulama örneğinin belleğindedir;
birden çok örnekte yapışkan oturum veya dağıtık kilit gerekir.

**İptal anları** (refresh token'lar dahil, oturuma bağlı yetkilendirme ve token'ları):

| Olay | Kapsam |
|---|---|
| İstemciden çıkış (RP-initiated logout) | İstemci önce refresh token'ını revocation uç noktasıyla iptal eder (RFC 7009), sonra end-session'a gider |
| Kimlik sunucusundan çıkış | O `sid`'e bağlı yetkilendirmeler (+ Back-Channel Logout) |
| Şifre değiştirme | Mevcut oturum hariç tüm oturumlar |
| Şifre sıfırlama | Tüm oturumlar |
| 2FA açma, kapatma, kurtarma kodu yenileme, kurulumu başlatma | Mevcut oturum hariç tüm oturumlar |
| Kullanılmış refresh token'ın tekrar gelmesi | O yetkilendirmenin (oturum + istemci) bütün token'ları (OpenIddict) |

Revocation uç noktası `/connect/revoke` (discovery'de `revocation_endpoint`); gizli istemciler `client_secret` ile
kullanır.

```mermaid
sequenceDiagram
    autonumber
    actor K as Kullanıcı (tarayıcı)
    participant O as Demo OBİS
    participant I as Kimlik sunucusu
    participant A as Demo API
    K->>O: /Notlarim
    O->>I: authorize (code + PKCE, offline_access notlar.oku)
    I-->>K: giriş + onay
    I->>O: kod
    O->>I: token isteği (kod + code_verifier + client_secret)
    I-->>O: id_token, access token (15 dk, aud demo-api), refresh token (8 sa)
    O->>A: GET /api/notlar (Authorization: Bearer access token)
    A->>A: imza (JWKS), iss, aud, typ, süre, scope notlar.oku, rol ogrenci
    A-->>O: notlar (yalnızca token'daki sub'ınkiler)
    Note over O: access token süresine 1 dk'dan az kaldı
    O->>I: refresh_token grant (tek uçuş)
    I-->>O: yeni access token + yeni refresh token (eskisi redeemed)
    K->>O: Çıkış
    O->>I: revoke (refresh token, RFC 7009)
    O->>I: end-session (id_token_hint)
    I->>I: o sid'in yetkilendirmeleri ve token'ları iptal
    I-->>O: logout_token (Back-Channel Logout)
```

**Bilinçli sınırlama: JWT access token durumsuzdur.** Kaynak sunucusu (Demo API) access token'ı kendi başına, imza ve
süre üzerinden doğrular; kimlik sunucusuna sormaz. Bu yüzden iptal edilmiş bir oturumun access token'ı **süresi
dolana kadar (en çok 15 dakika)** API tarafından kabul edilebilir. Bu pencereyi daraltmak için access token ömrü kısa
seçildi; refresh token iptal edildiği için yeni access token alınamaz. Alternatif, **token introspection (RFC 7662)**:
API her istekte (veya kısa önbellekle) token'ı kimlik sunucusuna sorar. *Artısı:* iptal anında geçerli olur, token
opak (referans) olabilir ve içeriği istemcide görünmez. *Eksisi:* her API isteği kimlik sunucusuna bir ağ isteği
ekler (gecikme, yük), kimlik sunucusu erişilemezse API de çalışamaz ve API'nin kendi istemci kimlik bilgisi
gerekir. Bu projede introspection uygulanmadı; yalnızca belgelendi.

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

Aynı mekanizma şifre sıfırlamada (kullanıcının tüm oturumları), şifre değiştirmede ve 2FA'nın açılması, kapatılması
veya kurtarma kodlarının yenilenmesinde (işlemi yapan oturum dışındakiler) da kullanılır.

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

## İki aşamalı doğrulama (2FA)

TOTP (RFC 6238, 6 hane, 30 saniye) tabanlıdır; Google Authenticator, Microsoft Authenticator gibi uygulamalarla
çalışır. Anahtar üretimi ve kod doğrulama Identity'nin yerleşik `AuthenticatorTokenProvider`'ı ve `UserManager`
API'siyle yapılır; kriptografi elle yazılmadı.

**Kurulum** (Uygulamalarım → Hesabım → **İki aşamalı doğrulama**):

1. **Kurulumu başlat**: Identity yeni bir paylaşılan anahtar üretir (`ResetAuthenticatorKeyAsync`). Bu, güvenlik
   damgasını da yeniler: bu oturum yeniden imzalanıp açık kalır, kullanıcının diğer oturumları kapanır ve onlardan
   giriş yapılmış istemcilere Back-Channel Logout gider (şifre değiştirmedeki rutin). Önceden diğer oturumlar damga
   yüzünden kapanıyor ama istemcilere bildirim gitmiyordu. Denetim kaydına "İki aşamalı doğrulama kurulumu başlatıldı
   (yeni anahtar)" yazılır ve kullanıcıya bilgilendirme e-postası gider (anahtar e-postada yoktur).
2. Anahtar hem metin olarak (4'lü gruplar) hem QR kod olarak gösterilir. QR kod **QRCoder** ile sunucu tarafında SVG
   olarak üretilir ve `<img src="data:image/svg+xml;base64,…">` ile gösterilir: harici CDN veya JavaScript yok, CSP
   değişmedi (`img-src 'self' data:` zaten vardı). otpauth URI'si:
   `otpauth://totp/Selcuk%20SSO%20%28Test%29:<kullanıcı adı>?secret=<anahtar>&issuer=Selcuk%20SSO%20%28Test%29&digits=6`.
3. Uygulamadaki 6 haneli kod doğrulanmadan 2FA **açılmaz**. Bu kod kullanılmış sayılır: 3 dakika içinde girişte veya
   onaylarda tekrar kabul edilmez.
4. Açılınca 10 kurtarma kodu üretilir ve **yalnızca o yanıtta bir kez** gösterilir (TempData veya çerezle taşınmaz).
   Anahtar ve kod içeren yanıtlar `Cache-Control: no-store` ile döner.
5. **Kurtarma kodlarını yenile** ve **2FA'yı kapat**: ikisi de mevcut şifre + geçerli TOTP kodu ister. Biri yanlışsa
   hatalı deneme sayacı artar (5'te hesap kilitlenir). Kapatınca anahtar yenilenir, kalan kurtarma kodları silinir.
6. Açma, kapatma ve kurtarma kodu yenilemede güvenlik damgası yenilenir: bu oturum yeniden imzalanıp açık kalır,
   kullanıcının diğer oturumları kapanır ve onların istemcilerine Back-Channel Logout gider (şifre değiştirmedeki
   mekanizma), hatırlanan cihazlar geçersizleşir ve bilgilendirme e-postası gönderilir.

**Giriş:** şifre doğru ve 2FA açıksa kullanıcı `/Account/IkiAdim` sayfasına gider (Identity'nin 5 dakikalık
`TwoFactorUserId` çereziyle; kimlik sunucusu oturumu henüz açılmaz, authorize kod vermez). Tek alana TOTP kodu veya
kurtarma kodu yazılır (6 rakamsa TOTP, değilse kurtarma kodu; boşluk ve tire yok sayılır).

- Hatalı TOTP'yi Identity hatalı deneme sayacına ekler; hatalı kurtarma kodunu Identity saymadığı için sayfa ekler.
  5 denemede hesap 15 dakika kilitlenir. Identity kurtarma koduyla girişte kilidi kontrol etmediği için kilit iki yol
  için de sayfada kontrol edilir.
- POST istek sınırına tabidir (dakikada 10).
- **Tekrar oynatma koruması:** başarıyla kullanılan bir TOTP kodu aynı kullanıcı için **3 dakika** boyunca tekrar
  kabul edilmez (kurulumdaki doğrulama, girişin ikinci adımı, 2FA kapatma ve kurtarma kodu yenileme onayları). Tekrar kullanılan kod hatalı
  kod gibi hatalı deneme sayacına eklenir ve denetim kaydına "2FA kodu tekrar kullanıldı" olarak yazılır. Ayrıntı
  aşağıda.
- Kullanılan kurtarma kodu silinir (tek kullanımlık) ve kullanıcıya kalan kod sayısıyla bilgilendirme e-postası gider.
- **Bu cihazı 30 gün hatırla**: Identity'nin `TwoFactorRememberMe` çerezi; süresi `IkiAdimCihazHatirlamaGun`
  (varsayılan 30). Çerez güvenlik damgasını içerir; şifre veya 2FA ayarı değişince geçersiz olur.

**TOTP tekrar oynatma koruması** (`Hesap/TekKullanimlikTotpSaglayici`): Identity'nin `AuthenticatorTokenProvider`'ı
geçerlilik penceresi (±2 zaman adımı, yaklaşık ±1 dakika) içinde aynı kodu defalarca kabul eder; omuz üstünden
görülen veya kaydedilen bir kod bu sürede şifreyle birlikte yeniden kullanılabilir. Sağlayıcı Identity'ninkinden
türetildi ve `TokenOptions.DefaultAuthenticatorProvider` olarak kaydedildi: kod doğrulaması değişmeden Identity'de
kalır, geçerli kod ayrıca kullanıcının son 3 dakikada kabul edilmiş kodlarıyla karşılaştırılır.

- **Saklanan:** kullanıcı başına, son 3 dakikada kabul edilen her kod için `kabul zamanı (Unix saniye)|h1:<HMAC>`;
  HMAC, kullanıcı kimliği ve kodun anahtarlı özetidir (`AnahtarliOzet`, amaç etiketi `totp-tekrar`; denetim kaydındaki
  kullanıcı adı özetiyle aynı anahtar, farklı etiket). Kod düz metin saklanmaz; 6 haneli kodun yalnızca 10⁶ olası
  değeri olduğu için anahtarsız bir özet tümü denenerek geri çevrilebilirdi. Süresi geçen kayıtlar her yazımda atılır.
  Yalnızca "son kod" değil son 3 dakikanın tüm kodları tutulur; aksi hâlde araya başka bir kod girdiğinde ilk kod
  pencere içinde yeniden kullanılabilirdi.
- **Saklama yeri: `AspNetUserTokens`** (sağlayıcı `[SelcukSSO]`, ad `KullanilanTotpKodlari`; kurtarma kodu özetleriyle
  aynı yer), `IDistributedCache` değil. Gerekçe: (1) Yazım `UserManager.SetAuthenticationTokenAsync` üzerinden
  kullanıcı kaydının `ConcurrencyStamp` denetiminden geçer; aynı kodla eşzamanlı iki istekten yalnızca biri kabul
  edilir. `IDistributedCache`'te karşılaştır-ve-yaz işlemi yoktur, iki istek de "kullanılmamış" görüp kabul
  edilebilirdi. (2) Kayıt veritabanında olduğu için yeniden başlatmada kaybolmaz ve birden fazla sunucu örneği aynı
  kaydı görür; varsayılan bellek içi önbellekte ikisi de sağlanmazdı. (3) Yeni bir altyapı veya paket gerekmez.
  Bedeli, başarılı her TOTP doğrulamasında tek bir `UPDATE`'tir.
- **Kapsam:** kurulumdaki "Doğrula ve aç", girişin ikinci adımı (SignInManager → sağlayıcı) ile 2FA kapatma ve kurtarma
  kodu yenileme onayları. Onaylarda kod yalnızca şifre de doğruysa kullanılmış sayılır (şifresini yanlış yazan
  kullanıcı aynı kodla tekrar deneyebilir; `TuketmedenDogrulaAsync`).

**Kurtarma kodlarının saklanması:** Identity varsayılan olarak kurtarma kodlarını `AspNetUserTokens` tablosunda düz
metin (`;` ile birleşik) tutar. `Hesap/KurtarmaKoduOzetDeposu`, Identity'nin EF Core kullanıcı deposunu
(`UserStore`) genişletir ve yalnızca üç metodu değiştirir: `ReplaceCodesAsync` her kodu Identity'nin
`IPasswordHasher<ApplicationUser>`'ıyla (kullanıcı şifreleriyle aynı PBKDF2 biçimi ve ayarları, kod başına rastgele
tuz) özetler ve yalnızca özetleri saklar (`[SelcukSSO]` / `KurtarmaKoduOzetleri`); `RedeemCodeAsync` girilen kodu
özetlerle `VerifyHashedPassword` ile karşılaştırır ve eşleşen özeti siler; `CountCodesAsync` özetleri sayar.
`UserManager` ve `SignInManager`'ın kurtarma kodu API'si değişmeden bu depoyu kullanır. Kod büyük/küçük harf, boşluk ve
tireden bağımsız (normalize edilerek) özetlenir.

**`amr` claim'i (RFC 8176):** id_token'a yazılır ve her zaman JSON dizisidir: yalnızca şifreyle girişte `["pwd"]`,
şifre + TOTP veya kurtarma koduyla girişte `["pwd","mfa"]`. "Bu cihazı hatırla" ile ikinci adım atlandıysa `["pwd"]`
(Identity bu girişi şifre girişi sayar). 2FA kurulumunu bu oturumda tamamlayan kullanıcının oturumu da `mfa` sayılır
(şifre ve geçerli bir TOTP kodu doğrulandı). `otp` değeri eklenmedi: Identity oturum yenilemede yalnızca tek bir `amr`
değeri taşıyor, TOTP ile kurtarma kodunu ayırmak için ek durum gerekirdi. `claims_supported`'a `amr` eklendi. Demo
uygulamalar `amr`'yi göstermez ve zorunlu kılmaz (step-up yok).

**Rol bazlı zorunluluk:** `IkiAdimZorunluRoller` (varsayılan boş liste) içindeki bir role sahip ve 2FA'sı kapalı
kullanıcı şifreyle girince kurulum sayfasına yönlendirilir. Kurulum bitmeden authorize kod vermez (kurulum sayfasına
dönüş adresiyle yönlendirir; `prompt=none`'da `interaction_required`), onay formu doğrudan gönderilse de
`access_denied` döner, token uç noktası da (kod veya refresh token zorunluluktan önce alınmış olsa bile)
`invalid_grant` döner. Zorunlu roldeki kullanıcı 2FA'yı kapatamaz.

| Ayar (`appsettings.json` kökü) | Varsayılan | Açıklama |
|---|---|---|
| `IkiAdimZorunluRoller` | `[]` | 2FA'nın zorunlu olduğu roller, ör. `["akademisyen", "personel"]` |
| `IkiAdimCihazHatirlamaGun` | `30` | "Bu cihazı hatırla" çerezinin süresi (gün) |

## Denetim kaydı

Güvenlik olayları `DenetimKayitlari` tablosuna yazılır (migration `DenetimKayitlari`): `Id`, `ZamanUtc`, `OlayTuru`,
`KullaniciId`, `IstemciId`, `IpAdresi`, `KisaUserAgent` (en fazla 200 karakter), `Sonuc` (`Basarili`/`Basarisiz`),
`Ayrinti` (kısa serbest metin, en fazla 500 karakter, ör. `neden=sifre-yanlis`).

**Kaydedilen olaylar:** giriş başarılı/başarısız, hesap kilitlendi, 2FA başarılı/başarısız, 2FA kodu tekrar
kullanıldı, kurtarma kodu kullanıldı, 2FA kurulumu başlatıldı,
2FA açıldı/kapatıldı (başarısız denemeler dahil), kurtarma kodları yenilendi, çıkış, şifre sıfırlama istendi, şifre
sıfırlandı (geçersiz bağlantı dahil), şifre değişti (yanlış mevcut şifre dahil), onay verildi/reddedildi, istemciye
token verildi (`client_id` ile), istemciye erişim reddedildi (`client_id` ile), uygulama iki aşamalı doğrulama
istedi (`client_id` ile), ek doğrulama (başarılı/başarısız), back-channel logout
gönderildi/başarısız, hız sınırı aşıldı.

**Asla kaydedilmeyenler:** şifre, TOTP veya kurtarma kodu, paylaşılan anahtar, sıfırlama token'ı, access/id/refresh
token, yetkilendirme kodu, çerez, logout_token. Var olmayan bir kullanıcı adıyla başarısız girişte (ve şifremi
unuttumda) yazılan metin saklanmaz, çünkü kullanıcılar bazen şifrelerini bu alana yazar. Bunun yerine normalize
edilmiş (Identity'nin `NormalizeName`'i, büyük harf) hâlinin anahtarlı özeti `Ayrinti`'ye
`kullanici_adi_ozeti=h1:<hex>` olarak yazılır; aynı hedefe yapılan denemeler böylece gruplanabilir.

**Anahtarlı özet (`Denetim/AnahtarliOzet`):** `h1:` + HMAC-SHA256(anahtar, amaç etiketi ‖ 0x00 ‖ değer)
(`System.Security.Cryptography.HMACSHA256`, elle yazılmadı). Anahtar `Denetim:KullaniciAdiOzetAnahtari` ayarından
gelir (base64, en az 32 bayt): Development'ta user-secrets'ta (bkz. [Sırlar](#sırlar-user-secrets)), diğer
ortamlarda ortam değişkeni (`Denetim__KullaniciAdiOzetAnahtari`) veya sır deposunda. Development dışında anahtar
yoksa veya geçersizse kimlik sunucusu **açılmaz** (açılışta açıklayıcı hata); Development'ta yoksa her açılışta
geçici bir anahtar üretilir ve uyarı loglanır. *Neden anahtarlı:* düz SHA-256 anahtarsızdır; kayıtları okuyan biri
olası kullanıcı adlarını (öğrenci numaraları kısa ve tahmin edilebilir) özetleyip karşılaştırarak yazılan adı geri
bulabilirdi. Anahtarı bilmeden bu yapılamaz. Aynı anahtar TOTP tekrar korumasında başka bir amaç etiketiyle kullanılır.

**Eski kayıtlar:** bu değişiklikten önce yazılmış kayıtlarda alan `kullanici_adi_sha256=<hex>` (anahtarsız SHA-256)
olarak **olduğu gibi kalır**; dönüştürülmez (asıl ad bilinmediği için dönüştürülemez de) ve yeni `h1:` özetleriyle
karşılaştırılamaz. Saklama süresi (180 gün) dolunca temizlik işiyle silinirler. `h1` öneki sürümdür: anahtar veya
yöntem değişirse yeni önek kullanılır, eski kayıtlar ayırt edilebilir kalır.

**Yazım:** sayfalar kaydı bellek içi bir kuyruğa bırakır (`Denetim/DenetimKaydedici`), arka plan hizmeti
(`DenetimYazmaHizmeti`) toplu olarak yazar. İstek yavaşlamaz; yazım hatası ana işlemi bozmaz, yalnızca loglanır.
Kapanışta kuyrukta kalanlar da yazılır.

**Saklama süresi (KVKK):** `DenetimKaydiSaklamaGun` (varsayılan **180**). `DenetimTemizlemeHizmeti` açılışta ve
ardından günde bir kez bu süreden eski kayıtları siler.

**Güvenlik geçmişi** (Hesabım → **Güvenlik geçmişi**): kullanıcı yalnızca kendi son 20 olayını görür (zaman TSİ,
Türkçe olay adı, uygulamanın görünen adı, IP). Yönetici ekranı yol haritasında (6. adım).

### Proxy arkasında çalışma (`GuvenilenProxyler`)

İstek sınırı ve denetim kaydı istemci IP'sini kullanır. `GuvenilenProxyler` (varsayılan boş liste) **boşsa**
`X-Forwarded-For` başlığı hiç işlenmez; ASP.NET Core'un loopback'ten gelen başlığa güvenen varsayılanı da kapatılır.
Böylece bir istemci başlığı kendisi yazarak istek sınırını atlatamaz veya kayıtlara sahte IP yazdıramaz. Uygulama bir
ters proxy (IIS ARR, nginx, YARP vb.) arkasındaysa proxy'nin adresi listeye yazılır; başlık **yalnızca** bu
adreslerden gelen isteklerde kabul edilir ve `RemoteIpAddress` gerçek istemci IP'si olur:

```json
"GuvenilenProxyler": [ "10.0.0.5", "10.1.0.0/24" ]
```

Tek adres veya CIDR ağı yazılabilir; geçersiz bir değerde uygulama açıklayıcı bir hatayla açılmaz.
Liste doluyken `X-Forwarded-For` ve `X-Forwarded-Proto` işlenir (`ForwardLimit` 1: en sağdaki, proxy'nin eklediği
değer); `X-Forwarded-Host` işlenmez. TLS'i proxy sonlandırıyor ve uygulamaya HTTP ile iletiyorsa proxy
`X-Forwarded-Proto: https` göndermelidir: istek HTTPS sayılır, OpenIddict'in HTTPS zorunluluğu karşılanır,
`UseHttpsRedirection` döngüye girmez ve yönlendirmeler (ör. girişe yönlendirme) `https://` ile üretilir. Issuer ve
sıfırlama bağlantıları zaten istekten değil `OpenIddict:Issuer` ayarından üretilir; çerezler `Secure` bayrağını her
durumda alır (`CookieSecurePolicy.Always`). Liste **boşken** `X-Forwarded-Proto` da yok sayılır: istemci kendi
yazdığı bir başlıkla HTTP isteğini HTTPS gibi gösteremez.

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
4. **Hesap kilitleme.** 5 hatalı denemede hesap 15 dakika kilitlenir. Yanlış şifre, yanlış TOTP veya kurtarma kodu,
   şifre değiştirmede yanlış mevcut şifre ve 2FA kapatma/kod yenilemede yanlış şifre veya kod aynı sayaca eklenir;
   kilitliyken şifre değiştirilemez. *Gerekçe:* tek bir hesaba karşı şifre ve kod denemesini yavaşlatır; ele
   geçirilmiş bir oturumdan mevcut şifre tahmin edilemez.

   **Kilit açık oturumları düşürmez (karar).** Hatalı denemelerden kaynaklanan bu geçici kilit **yeni girişleri**
   (şifre adımı, girişin ikinci adımı) engeller; kilitten önce açılmış kimlik sunucusu oturumunu ve o oturumdan token
   alınmasını engellemez. Authorize ve token uç noktaları bu kurala göre tutarlıdır: ikisi de kilidi kontrol etmez
   (önceden token uç noktası kontrol ediyor, authorize etmiyordu; oturumu açık kullanıcı kilit süresince authorize'dan
   kod alıp token alamıyordu). Şifre değiştirme, 2FA kapatma ve kurtarma kodu yenileme gibi yeniden kimlik doğrulama
   isteyen işlemler kilitliyken yine reddedilir. *Gerekçe:* kilidi herkes tetikleyebilir; yalnızca kullanıcı adını
   bilen biri 5 yanlış şifre deneyerek gerçek kullanıcının açık oturumlarını 15 dakika boyunca kullanılamaz hâle
   getirebilirdi (hizmet engelleme). Kilidin amacı tahmini yavaşlatmaktır; zaten doğrulanmış bir oturum tahminle
   ilgili değildir. Saldırgan oturumu ele geçirdiyse çözüm şifre sıfırlama/değiştirmedir (oturumları kapatır).
   Yöneticinin hesabı kapatması ayrı bir durumdur ve yol haritasında (6. adım) ele alınacaktır.
5. **İstek sınırı (rate limit).** Giriş, girişin ikinci adımı, 2FA kurulum/yönetim, şifremi unuttum, şifre sıfırlama ve
   şifre değiştirme formlarının POST'u IP başına dakikada 10 istekle sınırlıdır (`HizSiniri:DakikadaIstek`); aşılınca
   429, `Retry-After`, Türkçe hata sayfası ve denetim kaydı. IP, yalnızca `GuvenilenProxyler`'den gelen
   `X-Forwarded-For` ile değişir. *Gerekçe:* kilitleme tek hesabı korur; birçok hesaba yayılan denemeleri (password
   spraying), kullanıcı adı taramasını ve e-posta bombardımanını yavaşlatmak için IP başına sınır gerekir. Herkesin
   yazabildiği bir başlığa güvenilseydi sınır atlatılabilirdi.
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
16. **İki aşamalı doğrulama (TOTP).** Identity'nin yerleşik authenticator altyapısı; kod doğrulanmadan açılmaz,
    açma/kapatma/kod yenilemede güvenlik damgası yenilenir, diğer oturumlar ve istemcileri kapanır, e-posta gider;
    kapatma ve kod yenileme şifre + TOTP ister; `IkiAdimZorunluRoller` ile rol bazlı zorunluluk (kurulum bitmeden
    authorize ve token uç noktası reddeder). *Gerekçe:* şifre çalınsa bile ikinci faktör olmadan giriş yapılamaz;
    ayar değişikliğini yalnızca hem şifreyi hem telefonu olan kişi yapabilir.
17. **`amr` claim'i.** id_token'da `["pwd"]` veya `["pwd","mfa"]` (RFC 8176), discovery'de ilan edilir. *Gerekçe:*
    istemci uygulama kullanıcının nasıl doğrulandığını standart yolla öğrenir; ileride hassas sayfalar için çok faktör
    şartı (step-up) koyabilir.
18. **Kurtarma kodları özetlenerek saklanır.** Identity'nin düz metin varsayılanı yerine her kod
    `IPasswordHasher` (PBKDF2, tuzlu) ile özetlenir; tek kullanımlıktır, yalnızca bir kez gösterilir, kullanımı
    e-postayla bildirilir. *Gerekçe:* veritabanı sızsa bile kurtarma kodları 2FA'yı atlatmak için kullanılamaz.
19. **Denetim kaydı.** Giriş, 2FA, şifre, onay, token, çıkış, back-channel logout ve istek sınırı olayları kim (kullanıcı
    kimliği), ne zaman, hangi uygulama, hangi IP ile kaydedilir; şifre, kod, token, çerez kaydedilmez, var olmayan
    kullanıcı adı yalnızca anahtarlı özetiyle (`h1:` HMAC-SHA256) tutulur; yazım arka planda, isteği yavaşlatmaz ve hata ana işlemi bozmaz.
    Kullanıcı kendi son 20 olayını görebilir. *Gerekçe:* saldırı ve kötüye kullanım sonradan incelenebilir, kullanıcı
    tanımadığı bir girişi fark edebilir; kaydın kendisi yeni bir sızıntı kaynağı olmamalıdır.
20. **KVKK saklama süresi.** Denetim kayıtları `DenetimKaydiSaklamaGun` (varsayılan 180) gün sonra günlük arka plan
    işiyle silinir. *Gerekçe:* kişisel veri (IP, kullanıcı kimliği) amaç için gereken süreden uzun tutulmaz (veri
    minimizasyonu).
21. **TOTP tekrar oynatma koruması.** Kabul edilen bir TOTP kodu aynı kullanıcı için 3 dakika boyunca tekrar kabul
    edilmez; tekrar hatalı deneme sayılır ve denetim kaydına yazılır. Kodlar anahtarlı özetle `AspNetUserTokens`'ta
    tutulur, eşzamanlı kullanım `ConcurrencyStamp` ile engellenir. *Gerekçe:* görülen veya yakalanan bir kod,
    geçerlilik penceresi içinde ikinci kez kullanılamamalı (RFC 6238 bölüm 5.2).
22. **Anahtarlı özet anahtarı zorunlu.** Var olmayan kullanıcı adı ve kullanılmış TOTP kodları HMAC-SHA256 ile
    özetlenir; anahtar Development dışında yoksa uygulama açılmaz. *Gerekçe:* anahtarsız özet, küçük bir değer
    uzayında (kullanıcı adları, 6 haneli kodlar) deneme yoluyla geri çevrilebilir; eksik yapılandırma sessizce zayıf
    bir moda düşmek yerine açılışta fark edilmeli.
23. **Public istemci + PKCE + loopback (RFC 8252).** Tek Kapı gibi kullanıcının bilgisayarında çalışan uygulamalar
    `client_secret`'sız public istemcidir; PKCE (`S256`) zorunludur, adresler yalnızca `http://127.0.0.1`. http başka
    hiçbir sunucu adı için kabul edilmez (kayıtta ve authorize/end-session isteğinde denetlenir; `localhost` adı da
    reddedilir). *Gerekçe:* yerel uygulamaya konan secret gizli kalamaz, sahte güvenlik verir; PKCE ele geçirilen kodu
    işe yaramaz kılar. Loopback trafiği makineden çıkmaz; IP literal'i ad çözümlemesiyle başka arayüze
    yönlendirilemez. Diğer adreslerde https zorunluluğu korunur.
24. **Uygulama bazlı erişim izni.** İstemci kaydındaki `izinliRoller` / `izinliKullaniciAdlari` ile bir uygulamaya
    yalnızca belirli rol veya kullanıcılar girebilir; authorize (`access_denied`), token uç noktası (`invalid_grant`)
    ve Uygulamalarım aynı kuralı uygular, her ret denetim kaydına yazılır. Hatalı biçimli ayar kimseye izin vermez
    (fail-closed). *Gerekçe:* kimlik doğrulama herkese açık olsa da her uygulama herkese açık olmamalıdır (en az
    yetki); kontrolün token anında tekrarlanması, kod veya refresh token verildikten sonra kaldırılan iznin de
    uygulanmasını sağlar.
25. **İstemci bazlı 2FA şartı (`mfaZorunlu`).** Tek Kapı gibi hassas bir uygulamaya yalnızca iki aşamalı doğrulanmış
    oturumla girilir: şifreyle veya "Bu cihazı hatırla" ile açılmış oturumda ek doğrulama (step-up), 2FA'sı kapalı
    kullanıcıda kurulum; token uç noktası da `amr`'yi denetler; Tek Kapı callback'te ayrıca denetler (derinlemesine
    savunma). *Gerekçe:* Tek Kapı gerçek üniversite sistemlerinin şifrelerinin bulunduğu kasaya kapı açar; yalnızca
    şifrenin çalınması buna yetmemeli. Uygulama bazında istemek, diğer uygulamalarda kullanıcıyı gereksiz yere zorlamaz.
26. **Giriş gerektiren yanıtlar önbelleğe alınmaz.** Kimlik sunucusunda ve ortak istemci kütüphanesinde (demo
    uygulamalar) `[Authorize]` uç noktaların tüm yanıtları (girişe yönlendirme dahil) `Cache-Control: no-store` ile
    döner; Tek Kapı tüm yanıtlarında zaten `no-store` kullanır. Herkese açık sayfalar (menü, ana sayfa) ve statik
    dosyalar normal önbelleklenir. Onay ekranı (`/connect/authorize`) da `no-store` döner. *Gerekçe:* çıkıştan sonra
    geri tuşu veya ortak bilgisayardaki tarayıcı geçmişi, kişisel bir sayfayı (hesap bilgileri, güvenlik geçmişi,
    notlar) önbellekten göstermemeli.
27. **Access token'da veri minimizasyonu.** Access token'da yalnızca yetkilendirme için gerekenler (`sub`, `role`,
    `scope`, `sid`, `client_id`, `aud`, zaman damgaları) bulunur; ad, kullanıcı adı, e-posta, fakülte ve bölüm yalnızca
    id_token'da ve userinfo'dadır. Ömür 15 dakika. *Gerekçe (KVKK):* access token istemciden API'lere taşınır ve
    loglanabilir; amaç için gerekmeyen kişisel veri taşımamalıdır.
28. **Kayan refresh token ve tekrar kullanım tespiti.** Her yenilemede yeni refresh token; kullanılmış biri tekrar
    gelirse o oturum zincirinin bütün token'ları iptal edilir ve denetim kaydına yazılır (tekrar kullanım payı
    kapalı). İstemci eşzamanlı istekleri tek yenilemeyle karşılar. *Gerekçe (RFC 9700 §4.14):* çalınan bir refresh
    token'ı saldırgan ya da gerçek kullanıcı ikinci kez kullanınca hırsızlık ortaya çıkar ve zincir düşer.
29. **Token iptali.** Revocation uç noktası (RFC 7009); istemci çıkışında refresh token iptali; kimlik sunucusundan
    çıkışta, şifre ve 2FA değişikliklerinde oturum bazında yetkilendirme ve token iptali. *Gerekçe:* çıkış veya
    hesap güvenliği değişikliği, uzun ömürlü refresh token'larla sessizce devam eden erişim bırakmamalı.
30. **Audience ve kapsam denetimi (Demo API).** API yalnızca kendisi için verilmiş (`aud` = `demo-api`), `at+jwt`
    türünde, JWKS ile imzası doğrulanan ve süresi dolmamış token'ı kabul eder; ayrıca `notlar.oku` kapsamı ve `ogrenci`
    rolü ister (eksikse 401/403, hata ayrıntısı dönmez). Öğrenci kimliği yalnızca token'daki `sub`'dan alınır.
    *Gerekçe:* başka bir istemciye veya API'ye verilmiş bir token (ör. `aud` = `demo-obis`) bu API'de kullanılamamalı
    (confused deputy); kullanıcı parametreyle başkasının verisini isteyememeli.
31. **Loglarda PKCE doğrulayıcısı yok.** OpenIddict token isteğini Information düzeyinde loglarken `code`,
    `client_secret` ve `refresh_token`'ı maskeler ama `code_verifier`'ı maskelemez; bu yüzden
    `OpenIddict.Server.OpenIddictServerDispatcher` kategorisi Warning düzeyine çekildi (önemli olaylar denetim
    kaydında). Test, loglarda ve denetim kaydında hiçbir token, kod, doğrulayıcı veya secret olmadığını denetler.

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
| Notlarım (`/Notlarim`) | Yalnızca `ogrenci`. Notlar sunucu tarafında **Demo API'den** (`GET /api/notlar`, oturumdaki access token ile) çekilir; API'ye ulaşılamazsa "Not bilgileri şu an alınamıyor" |
| Ders Yönetimi (`/DersYonetimi`) | Yalnızca `akademisyen` |
| Profilim (`/Profilim`) | Giriş yapmış herkes: ad soyad, rol, fakülte, bölüm |
| Token İnceleme (`/TokenInceleme`) | Yalnızca Development: id_token ve access_token'ın header/payload'u (imza hariç), son kullanma zamanı; refresh token'ın yalnızca var olduğu ve (yaklaşık) son kullanma zamanı, değeri gösterilmez |
| Erişim reddedildi (`/ErisimReddedildi`) | Yetkisiz rol buraya yönlendirilir |
| Çıkış (`/Cikis`) | Yerel çerezi siler, kimlik sunucusunun end-session uç noktasına gider, dönüşte ana sayfa |

### Demo Yemekhane (`src/Selcuk.Demo.Yemekhane`, https://localhost:5102)

| Sayfa | Erişim |
|---|---|
| Haftalık menü (`/`) | Herkese açık, bu haftanın uydurma menüsü |
| Rezervasyonlarım (`/Rezervasyonlarim`) | Giriş yapmış herkes: sonraki 5 iş gününden seçip rezervasyon ekleme ve iptal (bellek içi) |
| Yemekhane Yönetimi (`/YemekhaneYonetimi`) | Yalnızca `personel`: günlük rezervasyon sayıları |
| Profilim, Erişim reddedildi, Çıkış | OBİS'teki gibi |

### Demo API (`src/Selcuk.Demo.Api`, https://localhost:5201)

Yalnızca kaynak sunucusu (ASP.NET Core minimal API, kendi istemci kaydı yok). Kimlik sunucusunda `demo-api` kaynağı ve
`notlar.oku` kapsamı tanımlıdır (açılışta, her ortamda).

| Uç nokta | Kural |
|---|---|
| `GET /api/notlar` | JWT Bearer: imza kimlik sunucusunun JWKS'iyle, `iss`, `aud` = `demo-api`, `typ` = `at+jwt`, süre (30 sn tolerans). Yetki: `scope` içinde `notlar.oku` ve rol `ogrenci`. Token yok/geçersiz → 401, kapsam veya rol eksik → 403. Yanıt: token'daki `sub`'ın notları (uydurma, OBİS'teki önceki sabit veri); sorgu parametresiyle başka öğrenci istenemez |

Tüm yanıtlar `Cache-Control: no-store`, `nosniff`, `no-referrer`, `X-Frame-Options: DENY`,
`Content-Security-Policy: default-src 'none'`. 401/403 gövdesiz ve `WWW-Authenticate`'te hata ayrıntısı yok
(`IncludeErrorDetails = false`); beklenmeyen hata genel bir 500 yanıtı döner.

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
7. İsteğe bağlı: uygulamaya yalnızca belirli rol veya kullanıcılar girebilecekse tanımın `Properties`'ine
   `izinliRoller` / `izinliKullaniciAdlari` ekleyin ([Uygulama bazlı erişim izni](#uygulama-bazlı-erişim-izni)).
   Kullanıcının bilgisayarında çalışan bir uygulama için `Istemciler.TekKapi` gibi public istemci ve
   `http://127.0.0.1` adresleri kullanın.

## Klasör yapısı

```
SelcukSSO.sln
dotnet-tools.json                 dotnet-ef yerel aracı
.gitattributes                    Satır sonu kuralları
scripts/run-all.ps1               Üç uygulamayı ayrı pencerelerde başlatır (-TekKapi: Tek Kapı da, SSO modunda)
araclar/tek-kapi/                 Tek Kapı: yerel kişisel ESSO portalı (Node.js), SSO modu; kendi README ve AGENTS.md'si
src/Selcuk.Identity/
  Program.cs                      Identity, OpenIddict, çerez/HSTS ayarları, e-posta, istek sınırı, denetim kaydı,
                                  ForwardedHeaders, discovery ek alanları
  GuvenlikBasliklari.cs           Güvenlik başlıkları ara katmanı
  GuvenilenProxyler.cs            X-Forwarded-For/Proto'ya yalnızca listedeki proxy'lerden güvenilmesi
  Data/                           ApplicationUser, DbContext, IstemciOturumu, DenetimKaydi, Roller, Istemciler
                                  (demo-obis, demo-yemekhane, tek-kapi; adres kuralı),
                                  BaslangicVerisi, Migrations
  Denetim/                        DenetimKaydedici (kuyruk) + yazma hizmeti, saklama süresi temizliği, Türkçe olay adları,
                                  AnahtarliOzet (HMAC-SHA256)
  Eposta/                         IEpostaGonderici, DosyaEpostaGonderici, SmtpEpostaGonderici, kuyruk, şablonlar
  Hesap/                          Sıfırlama token sağlayıcısı, sahte şifre doğrulama, istek sınırı, oturum damgası,
                                  şifre değişikliği sonrası işler, Türkçe Identity hataları, IkiAdimIslemleri (2FA),
                                  KurtarmaKoduOzetDeposu, TekKullanimlikTotpSaglayici (TOTP tekrar koruması)
  Oidc/                           Token/UserInfo uç noktaları, claim hedefleri (amr dahil), OturumKimligi (sid),
                                  BackChannelLogoutGonderici, IstemciErisimi (uygulama bazlı erişim izni)
  Pages/                          Uygulamalarım (Index), Account/Login, IkiAdim, IkiAdimDogrulama, GuvenlikGecmisi,
                                  SifremiUnuttum, SifreSifirla, Hesabim, SifreDegistir, AccessDenied,
                                  Connect/Authorize, Logout
src/Selcuk.Istemci.Ortak/         AddSelcukSSO, güvenlik başlıkları, Back-Channel Logout alıcısı, OturumIptalDeposu
                                  (IDistributedCache), giriş dönüşü hataları, iss doğrulaması, TokenYenileyici
                                  (access token yenileme, çıkışta refresh token iptali)
src/Selcuk.Demo.Obis/             Notlarım, Ders Yönetimi, Profilim, Token İnceleme …
src/Selcuk.Demo.Yemekhane/        Haftalık menü, Rezervasyonlarım, Yemekhane Yönetimi …
src/Selcuk.Demo.Api/              Demo API (kaynak sunucusu): GET /api/notlar, JWT Bearer
tests/Selcuk.Identity.Tests/      Kimlik sunucusu entegrasyon, protokol, tek giriş/çıkış testleri
tests/Selcuk.Istemci.Ortak.Tests/ Back-Channel Logout alıcısı testleri (yerel anahtar + sahte JWKS)
tests/Selcuk.Demo.Obis.Tests/     Demo OBİS yönlendirme ve rol testleri
tests/Selcuk.Demo.Yemekhane.Tests/ Demo Yemekhane yönlendirme ve rol testleri
tests/Selcuk.Demo.Api.Tests/      Demo API token doğrulama ve yetkilendirme testleri (yerel anahtar)
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
kayıtları), `DenetimKayitlari` (denetim kaydı). Mevcut bir veritabanında yeni migration'ı uygulamak için yine
`dotnet ef database update` yeterlidir. Şifre sıfırlama yeni tablo gerektirmez (token'lar veritabanında tutulmaz);
2FA anahtarı ve kurtarma kodu özetleri Identity'nin mevcut `AspNetUserTokens` tablosundadır.

`dotnet dev-certs https --trust` istemciler için gereklidir: istemciler kimlik sunucusunun discovery ve JWKS
adreslerine HTTPS ile bağlanır. Komut Windows'ta bir onay penceresi açar.

### Sırlar (user-secrets)

Hiçbir şifre veya secret repoda yoktur. Development'ta `dotnet user-secrets` kullanılır:

| Proje | Anahtar | İçerik |
|---|---|---|
| `src/Selcuk.Identity` | `TestKullanicilari:Ogrenci:Sifre`, `…:Akademisyen:Sifre`, `…:Personel:Sifre` | Test kullanıcılarının şifreleri |
| `src/Selcuk.Identity` | `Istemciler:DemoObis:ClientSecret` | demo-obis secret'ı |
| `src/Selcuk.Identity` | `Istemciler:DemoYemekhane:ClientSecret` | demo-yemekhane secret'ı |
| `src/Selcuk.Identity` | `Denetim:KullaniciAdiOzetAnahtari` | Anahtarlı özet (HMAC) anahtarı, base64, 32 bayt. Development dışında zorunlu (ortam değişkeni `Denetim__KullaniciAdiOzetAnahtari`) |
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

Anahtarlı özet anahtarını üretmek için (değer ekrana yazılmaz):

```powershell
$b = New-Object byte[] 32; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
dotnet user-secrets set "Denetim:KullaniciAdiOzetAnahtari" ([Convert]::ToBase64String($b)) --project src/Selcuk.Identity | Out-Null
```

Anahtar değişirse eski özetler yenileriyle eşleşmez (denetim kayıtlarında gruplama ve son 3 dakikanın TOTP tekrar
kayıtları); başka bir etkisi yoktur.

## Çalıştırma

Hepsini birlikte başlatmak için:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\run-all.ps1
```

Betik önce çözümü bir kez derler (`dotnet build SelcukSSO.sln`; başarısızsa durur), sonra kimlik sunucusunu
(5001), Demo OBİS'i (5101), Demo Yemekhane'yi (5102) ve Demo API'yi (5201) ayrı PowerShell pencerelerinde
`dotnet run --no-build` ile başlatır, hepsi hazır olunca tarayıcıda https://localhost:5001 adresini açar
(`-TarayiciAcma` verilirse açmaz). Durdurmak için pencerelerde `Ctrl+C`.
Önceden her pencere kendisi derlediği için ortak projelerin (`Selcuk.Istemci.Ortak`) dosyalarında kilit hatası
oluşabiliyordu.
`-TekKapi` verilirse Tek Kapı da (`araclar/tek-kapi`, http://127.0.0.1:4545) SSO modunda ayrı bir pencerede başlar
(bkz. [Tek Kapı köprüsü](#tek-kapı-köprüsü)).

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

### İki aşamalı doğrulama ve güvenlik geçmişi

1. Üç uygulamayı başlatın (`dotnet ef database update` ile `DenetimKayitlari` migration'ının uygulandığından emin
   olun). **`203011001`** ile giriş yapın → Uygulamalarım → **Hesabım** → **İki aşamalı doğrulama** → **Kurulumu
   başlat**.
2. Telefonda **Google Authenticator** veya **Microsoft Authenticator**'ı açıp yeni hesap ekleyin ve sayfadaki **QR
   kodu okutun** (veya 4'lü gruplar hâlindeki anahtarı elle girin). Uygulamada "Selcuk SSO (Test)" adıyla bir hesap
   görünür.
3. Uygulamadaki 6 haneli kodu yazıp **Doğrula ve aç**. 10 kurtarma kodu **bir kez** gösterilir; bir yere not edin.
   `App_Data/eposta/` klasöründe "İki aşamalı doğrulama açıldı" e-postası oluşur. Sayfayı yenileyince kodlar artık
   görünmez.
4. Uygulamalarım → **Çıkış yap**. Tekrar **`203011001`** ve şifresiyle giriş yapın: şifreden sonra **kod istenir**.
   Telefondaki güncel kodu girin. ("Bu cihazı 30 gün hatırla" seçilirse bu tarayıcıda bir sonraki girişte kod
   sorulmaz.)
5. Demo OBİS'e girip **Token İnceleme**'de id_token'daki `amr` claim'ini kontrol edin: `["pwd","mfa"]`. 2FA'sı kapalı
   bir kullanıcıyla (ör. `hoca.test`) `["pwd"]` görünür.
6. Çıkış yapıp tekrar girin, bu kez kod yerine bir **kurtarma kodu** yazın. Giriş yapılır; "kurtarma koduyla giriş"
   e-postası oluşur. Aynı kod ikinci kez geçmez.
7. Hesabım → **Güvenlik geçmişi**: giriş, iki aşamalı doğrulama, kurtarma kodu, onay, uygulamaya giriş ve çıkış
   olayları zaman, uygulama ve IP ile listelenir.
8. Tekrar oynatma koruması: çıkış yapıp girişte kullandığınız kodu (uygulamada henüz değişmeden) başka bir tarayıcıda
   tekrar girin: reddedilir; Güvenlik geçmişinde "2FA kodu tekrar kullanıldı" görünür.
9. Test kullanıcısını eski hâline döndürmek için Hesabım → İki aşamalı doğrulama → **Kapat** (mevcut şifre + güncel
   kod; girişte kullandığınız kod 3 dakika içinde tekrar kabul edilmez, uygulamada yeni kodun çıkmasını bekleyin).

### Uygulama bazlı erişim izni ve Tek Kapı kartı

1. Kimlik sunucusunu başlatın (açılışta `tek-kapi` kaydedilir; logda "İstemci kaydedildi/güncellendi: tek-kapi").
2. **`203011001`** ile giriş yapın: Uygulamalarım'da **Tek Kapı — gerçek Selçuk sistemleri (yerel)** kartı görünür.
   Kart `http://127.0.0.1:4545/auth/login?iss=…` adresine gider; Tek Kapı çalışmıyorsa tarayıcı bağlanamaz (beklenen).
3. Çıkış yapıp **`hoca.test`** ile girin: Tek Kapı kartı görünmez, Demo OBİS ve Demo Yemekhane görünür.
4. `hoca.test` oturumu açıkken tarayıcıya Tek Kapı'nın authorize adresini (client_id `tek-kapi`, PKCE ile) elle
   yazarsanız istemciye `error=access_denied` ile dönülür; Güvenlik geçmişinde "İstemciye erişim reddedildi" görünür.
5. **2FA şartı:** Tek Kapı kartında "2FA gerekli" etiketi vardır. `203011001`'in 2FA'sı kapalıysa karta tıklayınca
   kurulum sayfası "Bu uygulama iki aşamalı doğrulama gerektiriyor" der; kurulumdan sonra "devam et" Tek Kapı'ya
   götürür. 2FA'sı açık ve "Bu cihazı hatırla" ile girilmişse karta tıklayınca **Ek doğrulama** sayfası kodu ister.

Rol bazlı zorunluluğu denemek için `src/Selcuk.Identity/appsettings.Development.json`'a geçici olarak
`"IkiAdimZorunluRoller": ["akademisyen"]` ekleyip kimlik sunucusunu yeniden başlatın: `hoca.test` girişten sonra
kurulum sayfasına yönlendirilir ve kurulumu bitirmeden Demo OBİS'e giremez.

## Testler

```powershell
dotnet build
dotnet test
```

Testler LocalDB'ye, çalışan bir kimlik sunucusuna ve ağa ihtiyaç duymaz. E-postalar sahte bir göndericiyle bellekte
yakalanır.
- **Selcuk.Identity.Tests:** her test, SQLite in-memory üzerinde kendi veritabanıyla ayrı bir uygulama örneği
  başlatır (`EnsureCreated`, ortam `Testing`, geçici imzalama anahtarları, user-secrets okunmaz). Veritabanı
  adlandırılmış, paylaşımlı önbellekli bir in-memory veritabanıdır; her DbContext kendi bağlantısını açar, çünkü denetim
  kaydı arka planda isteklerle eşzamanlı yazılır. Test kullanıcıları ve istemci secret'ları test içinde, çalışma anında
  üretilir. İstemciler uygulamanın kendi tanımlarıyla (`Istemciler.*`) kaydedilir. Back-Channel Logout POST'ları sahte
  bir HTTP handler ile yakalanır. TOTP kodları `Totp` yardımcısıyla üretilir: Identity'nin `AuthenticatorTokenProvider`'ının
  kullandığı `Base32.FromBase32` ve `Rfc6238AuthenticationService.ComputeTotp` fonksiyonlarını çağırır (ikisi de
  Identity'de `internal` olduğundan yansımayla; HMAC elle yazılmadı); `Totp.Kodlar` tek bir zaman ölçümünden farklı
  adımların kodlarını üretir (tekrar korumalı testlerde iki kod aynı adıma düşmesin). Her fabrika kendi rastgele HMAC
  anahtarını verir (`OzetAnahtari`). Uygulamaya ayar vermek için fabrikanın `ayarlar`
  parametresi (ör. `IkiAdimZorunluRoller:0`), proxy testlerinde istemci adresini belirlemek için bir `IStartupFilter`
  kullanılır.
- **Selcuk.Istemci.Ortak.Tests:** kimlik sunucusu yerine yerel bir RSA anahtar çifti ile, bu anahtarı yayınlayan
  sahte discovery ve JWKS uç noktaları kullanılır; token yenileme testlerinde sahte token (gecikme ve invalid_grant
  ayarlı) ve revocation uç noktaları.
- **Selcuk.Demo.Api.Tests:** yerel RSA anahtarı ve sabit discovery bilgisi; token'lar kimlik sunucusunun access
  token'ı gibi üretilir.
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
| `Kilitli_hesapta_yeni_giris_reddediliyor_kilitten_once_acilan_oturum_kod_ve_token_alabiliyor` | Oturum açıkken başka biri 5 yanlış şifreyle hesabı kilitler: doğru şifreyle yeni giriş reddedilir, yeni tarayıcı authorize'dan girişe yönlenir; kilitten önce açılmış oturum authorize'dan kod, token uç noktasından token alır (iki kez), kilit sürer |
| `Uc_rol_olusturulmus` | `akademisyen`, `ogrenci`, `personel` |
| `Pkce_olmadan_authorize_istegi_reddediliyor` | `code_challenge` olmadan istek 400 `invalid_request`; PKCE ile aynı istek girişe yönlenir |
| `Kod_akisi_PKCE_ile_imzali_JWT_tokenlar_uretiyor` | Giriş → authorize (S256) → onay → kod → token; JWKS ile imza, iss/aud/exp/nonce; iki token'da sub, role; id_token'da name, fakulte, bolum, access token'da yok (15 dk); userinfo |
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
| `Iki_adim_ve_sifre_degistirme_POST_siniri_asilinca_429_ve_denetim_kaydi` (×3) | `/Account/IkiAdim`, `/Account/IkiAdimDogrulama`, `/Account/SifreDegistir`: 11. POST 429, denetim kaydında `HizSiniriAsildi` |
| `Yanlis_mevcut_sifre_hatali_deneme_sayacini_artiriyor_kilitliyken_degisiklik_yapilmiyor` | Yanlış mevcut şifre sayacı 1 artırır; 5'te kilit, kilitliyken doğru şifreyle de değişmez; denetim kayıtları |
| `Discovery_claims_supported_amr_iceriyor` | `claims_supported` içinde `amr` |
| `Totp_yardimcisi_Identity_dogrulayicisinin_kabul_ettigi_kodu_uretiyor` | Test yardımcısının kodu Identity'nin `VerifyTwoFactorTokenAsync`'i ile geçer, "geçersiz" kod geçmez |
| `Kurulum_gecerli_kod_olmadan_acilmiyor_gecerli_kodla_aciliyor_10_kurtarma_kodu_donuyor` | "Kurulumu başlat" e-postası; anahtar 4'lü gruplar + SVG QR (data URI, script yok, CSP aynı), otpauth issuer `Selcuk SSO (Test)`; boş/yanlış kodla açılmaz; doğru kodla açılır, 10 farklı kod, `no-store`, e-posta; kodlar ikinci kez gösterilmez |
| `Kurtarma_kodlari_veritabaninda_duz_metin_olarak_bulunmuyor` | `AspNetUserTokens`/`AspNetUsers`'ta hiçbir kod (tireli/tiresiz) yok; Identity'nin `RecoveryCodes` kaydı yok, 10 PBKDF2 özeti var; küçük harf/tiresiz kod kullanılabilir ve tek kullanımlık |
| `IkiAdim_acik_kullanici_sifreyle_girince_ikinci_adima_yonleniyor_kod_olmadan_token_alamiyor` | Şifreden sonra `/Account/IkiAdim`, oturum çerezi yok; authorize girişe yönlendirir, kod yok |
| `Dogru_TOTP_ile_giris_tamamlaniyor_amr_mfa_iceriyor_sadece_sifreyle_amr_pwd` | TOTP ile id_token `amr` = `["pwd","mfa"]`, yalnızca şifreyle `["pwd"]` (ikisi de JSON dizisi); access token'da `amr` yok; `IkiAdimBasarili` ve `client_id`'li `TokenVerildi` kayıtları |
| `Kurtarma_koduyla_giris_calisiyor_ayni_kod_ikinci_kez_reddediliyor` | Kurtarma koduyla giriş, kalan 9 kodlu e-posta; aynı kod reddedilir ve sayaç 1 artar; başka kod küçük harf/boşlukla geçer |
| `Bes_hatali_TOTP_denemesinde_hesap_kilitleniyor` | 5 yanlış TOTP → kilit; kilitliyken doğru kod da reddedilir; `IkiAdimBasarisiz` ×6 ve `HesapKilitlendi` kayıtları |
| `Zorunlu_rol_kullanicisi_kuruluma_zorlaniyor_kurulumdan_once_token_alamiyor` | `IkiAdimZorunluRoller` = akademisyen: girişten sonra kurulum sayfası; authorize kurulum sayfasına yönlendirir; onay formu doğrudan gönderilse de `access_denied`, kod yok; öğrenci zorlanmaz; kurulumdan sonra token (`amr` ⊇ `mfa`); zorunlu rolde 2FA kapatılamaz |
| `Kurulum_zorunlulugundan_once_alinan_kod_token_ucunda_reddediliyor` | Kod alındıktan sonra 2FA zorunlu rol verilen kullanıcı: token uç noktası `invalid_grant` |
| `IkiAdim_kapatma_yanlis_sifre_veya_yanlis_kodla_reddediliyor` | Yanlış şifre, yanlış kod veya girişte kullanılmış kodla kapatma/kod yenileme reddedilir, sayaç 4, bir `IkiAdimKoduTekrarKullanildi` kaydı; şifresi yanlış denemedeki kod kullanılmış sayılmaz, doğru şifreyle kapatır; kodlar silinir, anahtar değişir, e-posta, giriş tek adımlı |
| `Kurtarma_kodlari_yenilenince_eskiler_gecersiz_diger_oturum_kapaniyor` | Yeni 10 kod, eskilerle kesişmez; e-posta; bu oturum açık, diğer oturum kapalı; eski kod geçmez, yeni geçer (iki giriş ve onay farklı zaman adımlarının kodlarıyla) |
| `Ayni_TOTP_koduyla_ikinci_giris_reddediliyor_hatali_deneme_sayaci_artiyor` | İlk giriş başarılı; aynı kodla ikinci tarayıcıda giriş reddedilir, çerez yok, sayaç 0 → 1; ilk oturum açık; `IkiAdimKoduTekrarKullanildi` ("2FA kodu tekrar kullanıldı") ve `IkiAdimBasarisiz` kayıtları; `AspNetUserTokens`'ta yalnızca `zaman\|h1:<64 hex>` |
| `Farkli_ve_gecerli_kodla_ikinci_giris_kabul_ediliyor` | Şu anki ve sonraki zaman adımının kodlarıyla iki giriş de başarılı; sayaç 0; iki `IkiAdimBasarili`, tekrar kaydı yok |
| `Kurulumu_baslatinca_diger_oturumlarin_istemcilerine_back_channel_logout_gidiyor_mevcut_oturum_acik` | "Kurulumu başlat": bu oturum aynı sid'le açık ve anahtarı görüyor; diğer oturum kapanır, yalnızca onun iki istemcisine o sid'li logout_token gider; kayıtlar kapanır |
| `Kurulumdaki_dogrulama_kodu_ikinci_kez_kullanilamiyor` | Kurulumda kullanılan kod başka tarayıcıda girişte reddedilir (sayaç 1, çerez yok) ve kurtarma kodu yenileme onayında da reddedilir; başka zaman adımının kodu kabul edilir; iki `IkiAdimKoduTekrarKullanildi` kaydı |
| `Kurulumu_baslatma_denetim_kaydi_ve_eposta_olusturuyor` | "Kurulumu başlat": kullanıcıya "İki aşamalı doğrulama kurulumu başlatıldı" e-postası (anahtar içermez), `IkiAdimKurulumuBaslatildi` (başarılı) kaydı, Güvenlik geçmişinde görünür |
| `Iki_adim_sayfalari_giris_gerektiriyor_ikinci_adim_sifre_adimi_olmadan_acilmiyor` | Kurulum ve güvenlik geçmişi girişsiz → giriş sayfası; ikinci adım şifre adımı olmadan → giriş |
| `Basarili_ve_basarisiz_giriste_kayit_olusuyor_olmayan_kullanici_adi_yalnizca_ozetle_saklaniyor` | Başarılı/başarısız giriş kayıtları (kullanıcı, sonuç, ayrıntı, user agent, 200 karaktere kısaltma); olmayan kullanıcı adı hiçbir sütunda yok, `h1:` HMAC özeti var |
| `Olmayan_kullanici_adi_h1_onekli_HMAC_ile_yaziliyor_ayni_ad_ayni_ozet_farkli_anahtar_farkli_ozet` | Bilinen anahtarla: `kullanici_adi_ozeti=h1:<64 hex>`, bağımsız hesaplanan HMAC-SHA256'ya eşit, düz SHA-256 değil; aynı ad (farklı yazılış) aynı özet; başka anahtarla farklı özet |
| `Development_disinda_HMAC_anahtari_yoksa_veya_gecersizse_uygulama_baslamiyor` (×3) | `Testing` ortamında anahtar boş, 32 bayttan kısa veya base64 değil: uygulama açılmaz, hata ayar adını içerir |
| `Denetim_tablosunun_hicbir_sutununda_sifre_kod_veya_token_bulunmuyor` | Kaydedilen olay türleri beklenen kümenin içinde; uçtan uca senaryo (kullanıcı adı alanına şifre, 2FA açma (kurulum ve giriş farklı kodlarla), TOTP ve kurtarma koduyla giriş, OIDC, çıkış, şifre sıfırlama) sonrası tablonun tüm sütunlarında şifreler, anahtar, TOTP/kurtarma kodları, kod, verifier, secret, token'lar, logout_token, çerez ve sıfırlama kodu yok |
| `Saklama_suresini_asan_kayitlar_temizlik_isiyle_siliniyor` | Varsayılan 180 gün; `DenetimKaydiSaklamaGun` = 7 iken 30 ve 8 günlük kayıtlar silinir, 6 günlük ve yeni kalır |
| `Guvenlik_gecmisi_yalnizca_kullanicinin_kendi_son_20_kaydini_gosteriyor` | Başka kullanıcının uygulama ve IP'si görünmez; 25 kayıttan 20'si; uygulamanın görünen adı ve IP |
| `GuvenilenProxyler_bosken_X_Forwarded_For_dikkate_alinmiyor` | Liste boş: kayıtta proxy IP'si; farklı `X-Forwarded-For`'lu iki istemci aynı sınıra takılır |
| `GuvenilenProxyler_doluyken_listedeki_proxyden_gelen_baslik_dikkate_aliniyor` | Liste = proxy: kayıtta gerçek istemci IP'si; iki istemcinin sınırı ayrı |
| `Listede_olmayan_adresten_gelen_baslik_dikkate_alinmiyor` | Liste başka ağ (CIDR) ve adres: başlık yok sayılır |
| `GuvenilenProxyler_doluyken_X_Forwarded_Proto_https_dikkate_aliniyor` | HTTP isteği + `X-Forwarded-Proto: https`, liste = proxy: discovery 200 (OpenIddict HTTPS şartı), girişe yönlendirme `https://` |
| `GuvenilenProxyler_bosken_X_Forwarded_Proto_dikkate_alinmiyor` | Aynı istek, liste boş: discovery reddedilir, yönlendirme `http://` |
| `TekKapi_public_istemci_secret_olmadan_PKCE_ile_kod_ve_token_aliyor` | (Tek Kapı 2FA istediği için bu ve aşağıdaki Tek Kapı token testlerinde kullanıcı şifre + TOTP ile girer.) Kayıt: public, explicit, PKCE gereksinimi, tek redirect `http://127.0.0.1:4545/auth/callback`, secret doğrulanmaz; izinli kullanıcı secret'sız token isteğiyle token alır; id_token `aud` = `tek-kapi`, `role` var, `email` yok |
| `TekKapi_PKCE_olmadan_authorize_reddediliyor` | `code_challenge` yok: 400 `invalid_request`, yönlendirme yok; `plain` ile de kod yok |
| `Izinsiz_kullanici_TekKapi_authorize_isteginde_access_denied_ile_donuyor_ve_denetim_kaydi_olusuyor` | Akademisyen (listede değil): onay ekranı olmadan `redirect_uri?error=access_denied&state=…`, kod yok; onay formu doğrudan gönderilince de; iki `IstemciErisimiReddedildi` kaydı (`client_id`, `uc=authorize`); onay kaydı yok |
| `Izinsiz_kullanici_Uygulamalarimda_TekKapi_kartini_gormuyor_izinli_goruyor` | İzinli: Tek Kapı kartı, açıklaması ve `iss`'li `initiate_login_uri`; izinsiz: kart ve `127.0.0.1` yok; ikisi de demo kartlarını görür |
| `IzinliRoller_ile_kisitli_istemcide_rolu_uymayan_reddediliyor_uyan_kabul_ediliyor` | `izinliRoller` = `["AKADEMISYEN"]`: öğrenci `access_denied` + kayıt; akademisyen (harf duyarsız) kod ve token alır |
| `Token_ucu_da_izin_kontrol_ediyor_kod_alindiktan_sonra_izin_kaldirilinca_token_reddediliyor` | Kod alındıktan sonra izin listesi değişir: token `invalid_grant`, `uc=token` kaydı, `TokenVerildi` yok |
| `Cikista_TekKapinin_backchannel_logout_uri_adresine_de_logout_token_gidiyor` | Tek Kapı + Demo OBİS token'ı, RP-initiated logout `http://127.0.0.1:4545/?state=cikis`'e döner; iki `backchannel_logout_uri`'ye form POST; Tek Kapı'nınkinde `aud` = `tek-kapi`, `sub` |
| `TekKapi_calismiyorsa_cikis_tamamlaniyor_diger_istemciler_yine_bildiriliyor` | Tek Kapı'ya bağlanılamaz: çıkış tamamlanır, Demo OBİS bildirilir, Tek Kapı için `BackChannelLogoutBasarisiz` (`hata=HttpRequestException`), kayıtlar kapanır |
| `Kayit_kurali_disinda_eklenmis_http_redirect_adresi_authorize_ve_cikista_reddediliyor` | Doğrudan eklenmiş `http://istemci.test.local/…`: authorize ve end-session 400 `invalid_request`, yönlendirme yok |
| `MfaZorunlu_istemci_sifreyle_acilmis_oturumda_ek_dogrulama_istiyor_TOTP_sonrasi_kod_ve_amr_mfa` | Şifreyle açılmış oturum (2FA sonradan, damga değişmeden açılır): authorize `/Account/EkDogrulama?returnUrl=<authorize>`'a yönlenir, sayfa mesajı; yanlış kod reddedilir ve sayaç 1; doğru TOTP aynı sid'le authorize'a döndürür, sayaç 0; token `amr` = `["pwd","mfa"]`; `IstemciMfaGerekli` ve `EkDogrulama` (başarısız/başarılı) kayıtları |
| `IkiAdimi_kapali_kullanici_MfaZorunlu_istemci_icin_kuruluma_yonleniyor_kurulumdan_once_token_alamiyor` | Uygulamalarım'da yalnızca Tek Kapı kartında "2FA gerekli"; authorize kurulum sayfasına (uygulama mesajıyla); onay formu doğrudan gönderilse de `access_denied`, kod yok; kurulumdan sonra "devam et" authorize'a döner, token `amr` ⊇ `mfa`; `yonlendirme=kurulum` ve `neden=onay-formu` kayıtları |
| `Bu_cihazi_hatirla_ile_acilmis_oturumda_ek_dogrulama_isteniyor` | "Bu cihazı hatırla" sonrası yalnızca şifreyle giriş (`amr=pwd; cihaz-hatirlandi`): authorize ek doğrulamaya; kurtarma koduyla ek doğrulama, token `amr` = `["pwd","mfa"]` |
| `Token_ucu_amr_mfa_icermeyen_yetkilendirmeyi_MfaZorunlu_istemci_icin_reddediyor` | Kod istemci 2FA istemezken şifreli oturumdan alınır, sonra `mfaZorunlu` açılır: token `invalid_grant`, `uc=token` kaydı, `TokenVerildi` yok |
| `Giris_gerektiren_hesap_sayfalarinda_no_store_var_herkese_acik_sayfa_ve_statik_dosyada_yok` | Girişsiz `/Account/Hesabim` yönlendirmesi ve girişli Uygulamalarım, Hesabım, 2FA, şifre değiştir, güvenlik geçmişi, ek doğrulama `no-store`; Erişim reddedildi sayfası ve CSS'te yok |
| `Access_token_kucultuldu_profil_bilgileri_yalnizca_id_tokenda_aud_demo_api` | Access token: sub, role, scope (⊇ notlar.oku), sid, client_id, jti, aud = `demo-api`, 15 dk; name, email, fakulte, bolum, preferred_username, amr yok; yalnızca izinli claim'ler (+ OpenIddict `oi_*`); id_token'da profil bilgileri; userinfo bu token'la 200 |
| `Offline_access_ile_refresh_token_aliniyor_kayan_yenileme_eski_token_ikinci_kez_reddediliyor` | offline_access yoksa refresh token yok; varsa yenileme yeni access + yeni refresh token verir; eski refresh token ikinci kez `invalid_grant` |
| `Kullanilmis_refresh_token_tekrar_gelince_yetkilendirmenin_tum_tokenlari_iptal_ve_denetim_kaydi` | Kullanılmış refresh token reddedilir, aynı zincirin yeni refresh token'ı da geçmez; başka oturumun zinciri çalışır; `RefreshTokenTekrarKullanildi` kaydı (kullanıcı, `client_id`) |
| `TekKapi_offline_access_isteyemiyor` | tek-kapi'nin offline_access isteği 400, kod yok |
| `Revocation_ucu_refresh_tokeni_iptal_ediyor_discovery_ve_onay_ekrani` | Discovery'de `revocation_endpoint`; onay ekranı `no-store` ve offline_access/notlar.oku Türkçe açıklamaları; revoke 200, sonra refresh `invalid_grant` |
| `Sifre_degistirmede_diger_oturumlarin_refresh_tokenlari_iptal_mevcut_calisiyor_sifirlamada_hepsi` | Şifre değiştirmede diğer oturumun refresh token'ı reddedilir, mevcut oturumunki çalışır; sıfırlamadan sonra o da reddedilir |
| `Kimlik_sunucusundan_cikista_o_side_bagli_refresh_tokenlar_iptal` | Çıkan oturumun refresh token'ı reddedilir, diğer oturumunki çalışır |
| `Loglarda_ve_denetim_kaydinda_token_degeri_yok` | Giriş, yenileme, tekrar kullanım, revoke ve çıkıştan sonra hiçbir log iletisinde ve denetim tablosunda access/id/refresh token, kod, code_verifier, secret veya şifre yok |
| `Istemci_adresleri_https_veya_yalnizca_http_127_0_0_1_olabiliyor` | Üç tanım kurala uyar; `http://localhost`, `http://192.168.1.10`, `http://127.0.0.1.evil.example`, uzak http redirect ve `http://localhost` back-channel reddedilir; boş izin listesi varsayılana döner |
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
| `Suresi_dolmak_uzere_olan_access_token_istek_sirasinda_yenileniyor_uzun_sureli_yenilenmiyor` | 10 dk kalan token yenilenmez; 30 sn kalan istek sırasında yenilenir (grant_type, refresh_token, client kimlik bilgileri), çerezde yeni access/refresh token ve ~15 dk son kullanma; sonraki istek yenilemez; loglarda token değeri yok |
| `Invalid_grant_gelince_yerel_oturum_kapaniyor_ve_kimlik_dogrulamaya_gidiliyor` | invalid_grant: korumalı sayfa authorize'a yönlenir, çerez silinir, refresh token bir daha gönderilmez |
| `Eszamanli_istekler_ayni_refresh_tokeni_bir_kez_kullaniyor` | Aynı çerezle 5 eşzamanlı istek (sunucu yanıtı gecikmeli): hepsi 200, tek token isteği |
| `Cikista_once_refresh_token_iptal_ediliyor_sonra_end_sessiona_gidiliyor` | Çıkış: revocation isteği (token, `token_type_hint`, client kimlik bilgileri), sonra end-session'a yönlendirme |
| **Selcuk.Demo.Obis.Tests** | |
| `Girissiz_kullanici_Notlarim_icin_kimlik_sunucusuna_yonlendiriliyor` | 302 → `https://localhost:5001/connect/authorize`, `code_challenge` + `S256`, kapsamlar `openid profile email roles offline_access notlar.oku` |
| `Notlarim_Demo_APIden_gelen_veriyi_access_token_ile_gosteriyor` | Sahte Demo API: `GET https://localhost:5201/api/notlar`, `Authorization: Bearer <oturumdaki access token>`, sorgu parametresi yok; sayfada API'den gelen satır |
| `Demo_API_kapaliyken_Turkce_hata_mesaji_gosteriliyor_500_donmuyor` | API'ye bağlanılamaz: 200, "Not bilgileri şu an alınamıyor", tablo yok |
| `Ogrenci_Notlarim_acilir_Ders_Yonetimi_reddedilir` | ogrenci: Notlarım 200, Ders Yönetimi → Erişim reddedildi |
| `Akademisyen_Ders_Yonetimi_acilir_Notlarim_reddedilir` | akademisyen: Ders Yönetimi 200, Notlarım → Erişim reddedildi |
| `Token_Inceleme_Development_disinda_kapali` | Development dışında 404 |
| `Giris_yapmis_kullanici_eslestirme_cerezi_olmadan_donuste_ana_sayfaya_yonlendiriliyor` | Geri tuşu (`/signin-oidc`, eşleştirme çerezi yok), giriş yapmış: 302 → `/` |
| `Giris_yapmis_kullanici_gecerli_state_ile_correlation_hatasinda_donus_adresine_yonlendiriliyor` | Geçerli state, eşleştirme çerezi yok, giriş yapmış: state'teki yerel dönüş adresine (`/Notlarim`) |
| `Girissiz_kullanici_eslestirme_cerezi_olmadan_donuste_GirisHatasi_sayfasina_yonlendiriliyor` | Giriş yapmamış: `/GirisHatasi?neden=oturum-suresi`, yalnızca `neden` parametresi; sayfada "Tekrar giriş yap" |
| `Onay_reddedilince_GirisHatasi_sayfasina_erisim_reddedildi_nedeniyle_yonlendiriliyor` | `error=access_denied`: `/GirisHatasi?neden=erisim-reddedildi` |
| `Onay_reddedilince_gecerli_state_ile_de_erisim_reddedildi_nedeniyle_yonlendiriliyor` | Geçerli state ile `access_denied` de aynı sayfaya |
| `Dis_donus_adresine_yonlendirilmiyor` (×3) | State'te `https://evil.example`, `//evil.example`, `/\evil.example`: yönlendirme `/`'a |
| `Korumali_sayfalarda_no_store_var_ana_sayfa_ve_statik_dosyada_yok` | Profilim, Notlarım, Ders Yönetimi ve girişsiz korumalı sayfanın yönlendirmesi `no-store`; ana sayfa ve CSS'te yok |
| **Selcuk.Demo.Yemekhane.Tests** | |
| `Haftalik_menu_herkese_acik` | Girişsiz 200, başlıkta "Demo Yemekhane — test uygulaması" |
| `Girissiz_kullanici_Rezervasyonlarim_icin_kimlik_sunucusuna_yonlendiriliyor` | 302 → authorize, `client_id=demo-yemekhane`, `code_challenge` + `S256` |
| `Ogrenci_Yemekhane_Yonetimi_erisim_reddedildi` | ogrenci: Yönetim → Erişim reddedildi; Rezervasyonlarım 200 |
| `Personel_Yemekhane_Yonetimi_aciliyor` | personel: Yönetim 200 |
| `Korumali_sayfalarda_no_store_var_haftalik_menude_yok` | Rezervasyonlarım, Profilim, Yemekhane Yönetimi ve girişsiz yönlendirme `no-store`; haftalık menü ve CSS'te yok |
| **Selcuk.Demo.Api.Tests** | |
| `Gecerli_token_200_eksik_veya_hatali_token_401_yetkisiz_kapsam_ve_rol_403` | Geçerli token 200 (`no-store`, `nosniff`); token yok, yanlış imza, `aud` demo-obis, süresi dolmuş, `typ` JWT → 401; `notlar.oku` yok, rol akademisyen → 403; hata gövdesi ve `WWW-Authenticate` ayrıntısı yok |
| `Yalnizca_tokendaki_subin_notlari_donuyor_parametreyle_baskasi_istenemiyor` | `?sub=` / `?ogrenci=` yok sayılır; A'nın token'ı her zaman A'nın notlarını, B'ninki B'ninkini döndürür |
| `Giris_yapmis_kullanici_eslestirme_cerezi_olmadan_donuste_ana_sayfaya_yonlendiriliyor` | Yemekhane'de geri tuşu, giriş yapmış: 302 → `/` |
| `Girissiz_kullanici_eslestirme_cerezi_olmadan_donuste_GirisHatasi_sayfasina_yonlendiriliyor` | Yemekhane'de giriş yapmamış: `/GirisHatasi?neden=oturum-suresi` |

## Sürümler

| Bileşen | Sürüm |
|---|---|
| .NET SDK / hedef çatı | 10.0.402 / `net10.0` (çalışma zamanı 10.0.13) |
| **OpenIddict** (`OpenIddict.AspNetCore`, `OpenIddict.EntityFrameworkCore`) | **7.7.2** (NuGet'teki en güncel kararlı sürüm) |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.13 |
| Microsoft.EntityFrameworkCore.SqlServer / .Design | 10.0.13 |
| QRCoder (2FA QR kodu, SVG; MIT lisanslı) | 1.8.1 |
| Microsoft.AspNetCore.Authentication.OpenIdConnect (Selcuk.Istemci.Ortak) | 10.0.13 |
| Microsoft.AspNetCore.Authentication.JwtBearer (Selcuk.Demo.Api) | 10.0.13 |
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
- [x] **2FA** (TOTP doğrulayıcı uygulama): QR ile kurulum, özetlenerek saklanan kurtarma kodları, "bu cihazı
      hatırla", rol bazlı zorunluluk, id_token'da `amr`
- [x] **Denetim kaydı** (giriş, başarısız deneme, kilitleme, 2FA, şifre, onay, token, çıkış, back-channel logout ve
      istek sınırı olayları), kullanıcının güvenlik geçmişi sayfası, KVKK saklama süresi
- [x] **Uygulama bazlı erişim izni** (`izinliRoller`, `izinliKullaniciAdlari`) ve **Tek Kapı** public istemci kaydı
      (loopback, PKCE)
- [ ] **Yönetim paneli** (kullanıcı, rol ve istemci uygulama yönetimi; erişim izinlerinin arayüzden yönetimi)
- [x] **Token yönetimi**: küçültülmüş 15 dk access token, kayan refresh token (offline_access, tekrar kullanım
      tespiti), istemcide otomatik yenileme, revocation uç noktası, çıkış/şifre/2FA değişikliğinde oturum bazında
      iptal; access token'ı kullanan Demo API (audience ve kapsam denetimi). Introspection (RFC 7662) bilinçli olarak
      yalnızca belgelendi.
- [ ] **Production sertifika yönetimi**: şu an yalnızca Development sertifikaları (`AddDevelopmentSigningCertificate`,
      `AddDevelopmentEncryptionCertificate`) kullanılıyor. Production'da kalıcı X.509 imzalama/şifreleme
      sertifikaları (sertifika deposu veya Key Vault), anahtar rotasyonu ve `OpenIddict:Issuer`'ın gerçek alan adına
      ayarlanması gerekiyor. Development ve Testing dışındaki ortamlarda uygulama bilerek açılmaz.
