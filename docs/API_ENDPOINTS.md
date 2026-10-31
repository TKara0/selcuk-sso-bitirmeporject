# API_ENDPOINTS

Kimlik sunucusu `https://localhost:5001` (geliştirme). Ayrıntı: README > Mimari özet / Token yaşam döngüsü;
otomasyon API sözleşmeleri (henüz uygulanmamış): [api-sozlesmeleri/](api-sozlesmeleri/).

## Kimlik sunucusu (OpenIddict)

| Uç nokta | Method | Amaç / kimlik doğrulama | Notlar |
|---|---|---|---|
| `/.well-known/openid-configuration` | GET | Discovery; herkese açık | `backchannel_logout_supported: true`; yalnızca `S256` |
| `/.well-known/jwks` | GET | İmza anahtarları; herkese açık | |
| `/connect/authorize` | GET | Giriş + onay; kullanıcı oturumu | `code_challenge` (S256) zorunlu; `prompt=login` "hesabı değiştir"; `error=access_denied` izinsiz kullanıcı |
| `/connect/token` | POST | `authorization_code`, `refresh_token`; gizli istemci secret, `tek-kapi` public | Refresh yalnızca `offline_access` (demo-obis, demo-yemekhane); hata `invalid_grant` (izin, `amr`, mutlak süre, kullanılmış refresh) |
| `/connect/userinfo` | GET/POST | Bearer access token | `profile`: name/fakulte/bolum; `roles`: role |
| `/connect/logout` | GET/POST | End-session; `post_logout_redirect_uri` | Back-Channel Logout gönderir, o `sid` token'larını iptal eder |
| `/connect/revoke` | POST | RFC 7009; gizli istemci | Çıkışta refresh iptali |

Kapsamlar (`Program.cs` `RegisterScopes`): `openid profile email roles offline_access`; ayrıca `notlar.oku` (aud
`demo-api`), `<otomasyon>.oku/.yaz` (aud `<otomasyon>-api`; ör. `cap-yandal.*`, `etik-kurul.*`, `bolum-kurul.*`, `lisans-projeleri.*`, `danismanlik.*`, `iskur.*`, `mudek.*`, `sinav-analizi.*`). Hangi istemcinin hangi
kapsamı alabildiği `Data/Istemciler.cs`'te. Token ömürleri: access 15 dk, refresh 8 sa, mutlak 12 sa (`Tokenlar:*`).

### Kimlik sunucusu sayfaları (Razor)

`/` Uygulamalarım · `/Account/Login` · `/Account/IkiAdim` · `/Account/EkDogrulama` · `/Account/SifremiUnuttum` ·
`/Account/SifreSifirla` · `/Account/Hesabim` · `/Account/SifreDegistir` · `/Account/IkiAdimDogrulama` ·
`/Account/GuvenlikGecmisi` · `/Account/AccessDenied` · `/Hata`. POST'lar antiforgery'lidir; giriş, 2FA, şifre
sayfaları IP + sayfa başına dakikada 10 POST (aşılırsa 429).

## Demo API (`https://localhost:5201`)

| Endpoint | Method | Auth | Yanıt |
|---|---|---|---|
| `/api/notlar` | GET | Bearer JWT: `aud=demo-api`, `typ=at+jwt`, scope ⊇ `notlar.oku`, rol `ogrenci` | 200 `{ ogrenci, donem, notlar[] }` (yalnızca token `sub`'ının notları); 401 token yok/hatalı; 403 kapsam/rol yok |

Parametreyle başka kullanıcı istenemez; `no-store` ve güvenlik başlıkları eklenir; hata ayrıntısı döndürülmez.

## Alt uygulama uç noktaları (`Selcuk.Istemci.Ortak` / Otomasyon.Ortak)

| Yol | Method | Amaç |
|---|---|---|
| `/signout-backchannel` | POST | Back-Channel Logout alıcısı: `logout_token` doğrular (imza, iss, aud, iat, typ, events, nonce yok, sid/sub, jti tekrarı); JWKS alınamazsa 503; 400 geçersiz |
| `/signin-oidc`, `/signout-callback-oidc` | GET | OIDC dönüşleri; hata → `/GirisHatasi` |
| `/Giris` | GET | Giriş başlatma (`iss` doğrulanır, yanlışsa 400) |
| `/Cikis` | POST | RP-initiated logout |
| `/Profilim`, `/Hata`, `/ErisimReddedildi` | GET | Ortak sayfalar |

## Otomasyon API'leri

Hiçbiri **yok** (yalnızca sözleşme belgeleri). Otomasyonlar `VeriKaynagi:Mod=Api` ile Bearer access token kullanacak
şekilde hazırlanmıştır.

| Otomasyon (port) | `aud` | Kapsamlar | Sözleşme |
|---|---|---|---|
| İntibak (5301) | `intibak-api` | `intibak.oku`, `intibak.yaz` | [intibak.md](api-sozlesmeleri/intibak.md) |
| Staj (5302) | `staj-api` | `staj.oku`, `staj.yaz` | [staj.md](api-sozlesmeleri/staj.md) |
| Etik Kurul (5303) | `etik-kurul-api` | `etik-kurul.oku`, `etik-kurul.yaz` | [etik-kurul.md](api-sozlesmeleri/etik-kurul.md) |
| Mezuniyet (5304) | `mezuniyet-api` | `mezuniyet.oku`, `mezuniyet.yaz` | [mezuniyet.md](api-sozlesmeleri/mezuniyet.md) |
| Bölüm Kurul Karar (5305) | `bolum-kurul-api` | `bolum-kurul.oku`, `bolum-kurul.yaz` | [bolum-kurul.md](api-sozlesmeleri/bolum-kurul.md) |
| Öğrenci Danışmanlık (5306) | `danismanlik-api` | `danismanlik.oku`, `danismanlik.yaz` | [danismanlik.md](api-sozlesmeleri/danismanlik.md) |
| İŞKUR (5307) | `iskur-api` | `iskur.oku`, `iskur.yaz` | [iskur.md](api-sozlesmeleri/iskur.md) |
| MÜDEK (5308) | `mudek-api` | `mudek.oku`, `mudek.yaz` | [mudek.md](api-sozlesmeleri/mudek.md) |
| Lisans Projeleri (5309) | `lisans-projeleri-api` | `lisans-projeleri.oku`, `lisans-projeleri.yaz` | [lisans-projeleri.md](api-sozlesmeleri/lisans-projeleri.md) |
| Sınav Başarısı Analizi (5310) | `sinav-analizi-api` | `sinav-analizi.oku`, `sinav-analizi.yaz` | [sinav-analizi.md](api-sozlesmeleri/sinav-analizi.md) |
| ÇAP-Yandal (5311) | `cap-yandal-api` | `cap-yandal.oku`, `cap-yandal.yaz` | [cap-yandal.md](api-sozlesmeleri/cap-yandal.md) |
