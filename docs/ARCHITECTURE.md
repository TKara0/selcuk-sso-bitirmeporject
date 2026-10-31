# ARCHITECTURE

Diyagram ve port/istemci tablosu: [mimari.md](mimari.md). Rol modeli: [roller.md](roller.md). Burada yalnızca yol
haritası var.

## Çözüm yapısı

```
src/Selcuk.Identity            kimlik sunucusu (Program.cs, Data/, Oidc/, Hesap/, Denetim/, Eposta/, Pages/)
src/Selcuk.Roller              rol kataloğu (Roller.Katalog, RolDogrulayici) – tek kaynak
src/Selcuk.Arayuz              RCL: tasarım sistemi (selcuk.css/js, fontlar, kapı SVG, _SelcukDuzen)
src/Selcuk.Istemci.Ortak       AddSelcukSSO, Back-Channel alıcısı, OturumIptalDeposu, TokenYenileyici
src/Selcuk.Otomasyon.Ortak     AddSelcukOtomasyon/UseSelcukOtomasyon, menü→yetki, VeriKaynagi, OturumBiletDeposu
src/Selcuk.Otomasyon.*         Intibak/Staj/EtikKurul/Mezuniyet/BolumKurul/LisansProjeleri/CapYandal: Sozlesme/ (DTO + arayüz), Veri/ (Mock + Api)
src/Selcuk.Demo.Obis|Yemekhane|Api
araclar/tek-kapi               Node.js portal (ayrı AGENTS.md)
tools/Selcuk.Olcum             otomatik ölçüm (yalnızca localhost+https)
tests/                         her src projesi için *.Tests; Otomasyon.TestOrtak ortak kaynak (bağlantıyla derlenir)
scripts/                       run-all, olcum-calistir, arayuz-esitle, ekran-goruntuleri, olcum_analiz
docs/                          bu sistem + konu belgeleri
```

Bağımlılık yönü: Identity → Roller; Istemci.Ortak → (OIDC); Otomasyon.Ortak → Istemci.Ortak, Arayuz, Roller;
Otomasyon.X → Otomasyon.Ortak; Demolar → Istemci.Ortak, Arayuz, Roller. Tek Kapı yalnızca OIDC/HTTP ile bağlanır.

## Kimlik sunucusu – kritik kod yolları

| Konu | Yer |
|---|---|
| Uç nokta URI'leri, akışlar, sertifikalar | `Program.cs` (`SetAuthorizationEndpointUris` vb.) |
| İstemci tanımları, adres doğrulama, otomasyon kapsamları | `Data/Istemciler.cs` |
| Seed (roller, 13 test kullanıcısı, ölçüm kullanıcıları) | `Data/BaslangicVerisi.cs` |
| `sid`, `amr`, claim hedefleri (id_token/access/userinfo) | `Oidc/OturumKimligi.cs`, `Oidc/OidcKimlik.cs` |
| Authorize/token/logout işleyicileri | `Oidc/OidcUcNoktalari.cs`, `Pages/Connect/` |
| Uygulama bazlı erişim izni | `Oidc/IstemciErisimi.cs` |
| Refresh mutlak sınırı, tekrar kullanım tespiti, budama | `Oidc/YenilemeUstSiniri.cs`, `RefreshTokenTekrarDenetimi.cs`, `TokenBudamaHizmeti.cs` |
| Ad-hoc yetkilendirme/iptal | `Oidc/TokenIptali.cs` |
| Tek çıkış gönderimi | `Oidc/BackChannelLogoutGonderici.cs` (+ `IstemciOturumlari` tablosu) |
| Şifre/2FA/kilit/hız sınırı | `Hesap/` (`IkiAdimIslemleri`, `TekKullanimlikTotpSaglayici`, `HizSiniri`, `SahteSifreDogrulayici`) |
| Denetim | `Denetim/` (kuyruk → arka plan yazma/temizleme; HMAC `AnahtarliOzet`) |
| E-posta | `Eposta/` (kuyruk + arka plan gönderim; Dev'de `.eml` dosyası) |
| Güvenlik başlıkları, proxy | `GuvenlikBasliklari.cs`, `GuvenilenProxyler.cs` |
| Sayfalar | `Pages/` (Account/*, Index = Uygulamalarım) |

Tüm tablolar (Identity, OpenIddict, `IstemciOturumlari`, `DenetimKayitlari`) tek `ApplicationDbContext`'te;
migration'lar `Data/Migrations` (`Baslangic`, `IstemciOturumlari`, `DenetimKayitlari`, sonrakiler için klasöre bak).
Arka plan hizmetleri: denetim yazma/temizleme, e-posta gönderim, token budama.

## Alt uygulama akışı

`AddSelcukSSO` (Cookie + OIDC) → `MapSelcukBackChannelLogout` (`POST /signout-backchannel`) → `TokenYenileyici`
(çerez doğrulamasında refresh) → `OturumIptalDeposu` (iptal edilen sid reddedilir; `IDistributedCache`, varsayılan
bellek içi). Otomasyonlarda çerez yalnızca anahtar taşır (`OturumBiletDeposu`, Data Protection ile şifreli önbellek).
Yeni otomasyon kontrol listesi: [otomasyon-sablonu.md](otomasyon-sablonu.md).

## Veri kaynakları

- Kimlik: SQL Server. Demo/otomasyon verileri: bellek içi/örnek (`SahteVeri`, `OrnekStajVerisi` vb.).
- Otomasyon `VeriKaynagi:Mod` = `Mock` (varsayılan örnek veri) | `Api` (`ApiAdresi` https, Bearer access token;
  karşı API'ler **henüz yok**, sözleşmeler [api-sozlesmeleri/](api-sozlesmeleri/)).
- Tek Kapı: şifreli yerel kasa `araclar/tek-kapi/data/` (git'e girmez).

## Test mimarisi

`WebApplicationFactory` + SQLite in-memory (test başına izole), ortam `Testing`; sahte HTTP handler/e-posta;
istemci testlerinde sahte discovery + yerel RSA JWKS. Sayılar: [test-kapsami.md](test-kapsami.md).
