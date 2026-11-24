# ARCHITECTURE

Diyagram ve port/istemci tablosu: [mimari.md](mimari.md). Rol modeli: [roller.md](roller.md). Burada yalnızca yol
haritası var.

## Çözüm yapısı

```
src/Selcuk.Identity            kimlik sunucusu (Program.cs, Data/, Oidc/, Hesap/, Denetim/, Eposta/, Pages/)
src/Selcuk.Roller              rol kataloğu (Roller.Katalog, RolDogrulayici) – tek kaynak
src/Selcuk.Arayuz              RCL: tasarım sistemi (selcuk.css/js, fontlar, kapı SVG, _SelcukDuzen)
src/Selcuk.Istemci.Ortak       AddSelcukSSO, Back-Channel alıcısı, OturumIptalDeposu, TokenYenileyici, OturumBiletDeposu
                               (isteğe bağlı sunucu tarafı oturum), KimlikSunucusuBilgisi (Uygulamalarım adresi)
src/Selcuk.Otomasyon.Ortak     AddSelcukOtomasyon/UseSelcukOtomasyon, menü→yetki, VeriKaynagi
src/Selcuk.Otomasyon.*         Intibak/Staj/EtikKurul/Mezuniyet/BolumKurul/Danismanlik/Iskur/Mudek/LisansProjeleri/
                               SinavAnalizi/CapYandal (5301-5311): Sozlesme/ (DTO + arayüz), Veri/ (Mock + Api)
src/Selcuk.Demo.Obis|Yemekhane|Api   Demo OBİS: Pages/Shared (_Layout, _ObisHesapMenusu, _SayfaBasligi, _TemsiliForm), Data/
                               (ObisMenusu, ObisSimgeleri, OrnekOgrenciVerisi), wwwroot/css/site.css (--obis-* mavi tonlar)
araclar/tek-kapi               Node.js portal (ayrı AGENTS.md)
tools/Selcuk.Olcum             otomatik ölçüm (yalnızca localhost+https)
tests/                         her src projesi için *.Tests; Otomasyon.TestOrtak ortak kaynak (bağlantıyla derlenir)
start.py                       tek komutla başlatma (ön kontrol, run-all -ArkaPlan, durdur, --kurulum)
scripts/                       run-all, durdur-hepsi, olcum-calistir, arayuz-esitle, ekran-goruntuleri, olcum_analiz
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
| Rol değişikliği | `Hesap/RolDegisikligiIslemi`: rol kümesini değiştiren tek yol; küme değişirse güvenlik damgası, Back-Channel Logout, token iptali ve `RolDegisti` denetimi (`SifreDegisikligi`'ndeki mekanizmayla aynı); `BaslangicVerisi` çağırır, arayüz yok |
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
bellek içi). `AddSelcukSSO(configuration, sso => sso.SunucuTarafliOturum = true)` ile (varsayılan kapalı) çerez yalnızca
anahtar taşır: `OturumBiletDeposu` (`Selcuk.Istemci.Ortak`, Data Protection ile şifreli `IDistributedCache`, süre çerez
ömrüyle aynı, çıkışta ve Back-Channel Logout'ta hemen silinir). Otomasyon şablonu, Demo OBİS ve Demo Yemekhane açar. Depo
sınıfının ad alanı `Selcuk.Istemci.Ortak`'tır. Alt uygulamaların
"Uygulamalarım" bağlantısının adresi `KimlikSunucusuBilgisi` servisindendir (`Kimlik:Authority` kökü; yalnızca https veya
`http://127.0.0.1`). Kimlik sunucusunun Uygulamalarım sayfası kartları istemci `grup` özelliğine göre gruplar
(`Istemciler.Gruplar`, `KartSirasi`; grupsuz → "Diğer"); iki demo ve on bir otomasyonun kartında istemci kaydından gelen
simge ve vurgu rengi (`simge`, `vurgu`; Demo OBİS "OB" mavi, Demo Yemekhane "YM" nar) bulunur. Kart açıklamaları ek not
taşımaz; "Bölüm otomasyonları temsili ekranlardan oluşur ve örnek veri gösterir." notu yalnızca otomasyon kartı gören
kullanıcıda, ilk otomasyon grubunun başlığının üstünde bir kez görünür (`Istemciler.OtomasyonMu`, `IndexModel.OtomasyonNotuGrupSirasi`).
Yeni otomasyon kontrol listesi: [otomasyon-sablonu.md](otomasyon-sablonu.md).

## Yerleşim (R11: fakülte sitesinden esinlenen öğeler)

Kimlik sunucusu (`wwwroot/css/site.css`, `Pages/Shared/_GeziniYolu.cshtml`) ve otomasyonların ortak düzeni
(`Selcuk.Otomasyon.Ortak`: `otomasyon.css`, `_OtomasyonDuzen`, `GeziniYolu`, `MenuSimgeleri`, `Pages/Shared/_GeziniYolu`,
`_MenuSimgeleri`) şunları ekler: koyu lacivert üst çubuk (`--ust-zemin`, kapı paneli token'ları), h1 altında kısa kalın
CSS çizgisi, gezinti yolu (otomasyonda `GeziniYolu.Olustur` + `GeziniYolu.BasligaEkle`: ekran sayfaları değişmeden düzen,
sayfa gövdesindeki ilk `</h1>`'in altına ekler; kimlik sunucusunda Hesabım sayfaları partial'ı doğrudan çağırır), ikonlu
sol menü (`MenuOgesi.Simge`, tek gizli SVG sprite, simge `<li>` içinde ve `aria-hidden`) ve yuvarlatılmış gölgesiz kartlar.
`selcuk.css`, `Selcuk.Arayuz/wwwroot` (Tek Kapı kopyası SHA-256 ile denetlenir) ve `_SelcukDuzen` değişmedi; Demo OBİS
(kendi düzeni), Yemekhane, Demo API ve Tek Kapı etkilenmez. Gerekçe: [DECISIONS.md](DECISIONS.md). Ayrıntı:
[otomasyon-sablonu.md](otomasyon-sablonu.md).

R12 düzeltmeleri (yalnızca CSS, işaretleme ve istemci kaydı; yeni ekran yok): otomasyonlarda dar ekranda (600 px altı) tek
satırlık üst çubuk (uygulama adı ellipsis, "Uygulamalarım" yalnızca simge, hesap düğmesi sağda, 44x44 px hedefler) ve ana
sayfa kısayol listesinde menü simgeleri (`MenuSimgeleri.Isaret`); kimlik sunucusunda hesap düğmesi ve menü başlığında
görünen ad (`_HesapMenusu`: `UserManager` ile `AdSoyad`, boşsa kullanıcı adı; kullanıcı adı menüde ikincil Mono satırı) ve
Uygulamalarım'da yukarıdaki not ile Demo OBİS/Yemekhane simgeleri.

## Veri kaynakları

- Kimlik: SQL Server. Demo/otomasyon verileri: bellek içi/örnek (`SahteVeri`, `OrnekStajVerisi` vb.).
- Otomasyon `VeriKaynagi:Mod` = `Mock` (varsayılan örnek veri) | `Api` (`ApiAdresi` https, Bearer access token;
  karşı API'ler **henüz yok**, sözleşmeler [api-sozlesmeleri/](api-sozlesmeleri/)).
- Tek Kapı: şifreli yerel kasa `araclar/tek-kapi/data/` (git'e girmez).

## Tek komutla başlatma (`start.py`)

Depo kökündeki `start.py` yalnızca Python standart kütüphanesini kullanır (Windows). Uygulama listesini yinelemez:
`scripts/run-all.ps1`'deki `@{ Ad; Baslik; Proje; Port }` satırlarını okur (`tek-kapi` satırı hariç) ve uygulamaları
`run-all.ps1 -ArkaPlan` ile başlatır (çıktı `logs/<ad>.log`, PID'ler `logs/run-all.pids`); kapatma `durdur-hepsi.ps1`.
Akış: ön kontrol (✓/✗; eksikse komutu söyler ve durur) → run-all → her portta https cevabı bekleme (cevap vermeyen
uygulamanın log sonu, gizli sözcük içeren satırlar maskelenerek) → tablo ve test kullanıcıları (şifresiz) → tarayıcı →
bekleme; `Ctrl+C` veya başka oturumdan `--durdur` ile kapatılır ve portlarda dinleyen işlem kalmadığı doğrulanır.
`--kurulum`, yalnızca eksik user-secrets anahtarlarını `dotnet user-secrets set` (JSON stdin'den) ile yazar: istemci
sırrı kimlik sunucusunda ve uygulamada aynı değerdir (32 bayt, base64); var olan değer değiştirilmez, değer yazdırılmaz;
`--deneme` hiçbir şey yazmaz. Kimlik sunucusundaki test kullanıcı ve istemci listesi `start.py`'de ayrıca durur
(`BaslangicVerisi.cs` ve `Istemciler.cs` ile aynı olmalı; yeni istemci/kullanıcı eklenince burası da güncellenir).

## Test mimarisi

`WebApplicationFactory` + SQLite in-memory (test başına izole), ortam `Testing`; sahte HTTP handler/e-posta;
istemci testlerinde sahte discovery + yerel RSA JWKS. Yerleşim denetimleri (gezinti yolu yapısı, menü simgeleri, üst çubuk
kontrastı) `tests/Selcuk.Otomasyon.TestOrtak/YerlesimDenetimi*.cs` içinde saf işlevlerdir (ad alanı `Selcuk.TestYardimcilari`);
10 otomasyon, Staj (kendi fabrikasıyla) ve Identity test projeleri bağlantıyla derler. `StartPyTutarlilikTests`, `start.py`'deki
elle kopyayı koda karşı doğrular. Sayılar: [test-kapsami.md](test-kapsami.md).
