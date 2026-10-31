# Otomasyon şablonu

Bilgisayar Mühendisliği Bölümü'nün 11 otomasyonu ayrı web uygulamalarıdır ve aynı şablonla kurulur:
`src/Selcuk.Otomasyon.Ortak` (Razor Class Library). İlk örnek Staj Otomasyonu'dur (`src/Selcuk.Otomasyon.Staj`,
https://localhost:5302); İntibak (5301), Etik Kurul (5303), Mezuniyet (5304), Bölüm Kurul Karar (5305), Lisans Projeleri
(5309) ve ÇAP-Yandal (5311) aşağıdaki ortak parçaları da kullanır. Portlar ve `client_id`'ler: [mimari.md > Port ve istemci düzeni](mimari.md#port-ve-istemci-düzeni).
Roller ve plan matrisi: [roller.md](roller.md).

## Şablon ne verir

`Program.cs` iki satırdır:

```csharp
builder.Services.AddSelcukOtomasyon(builder.Configuration, StajOtomasyonu.Ayarla);
var app = builder.Build();
app.UseSelcukOtomasyon();
```

- **`AddSelcukOtomasyon`** (servisler, tek çağrı): `AddSelcukSSO` (çerez + OIDC, code + PKCE, userinfo, token yenileme,
  Back-Channel Logout alıcısının servisleri), oturum biletinin sunucu tarafında saklanması (`OturumBiletDeposu`:
  bilet ve token'lar Data Protection ile şifreli olarak `IDistributedCache`'te, çerezde yalnızca anahtar. Geliştirmede
  bütün uygulamalar localhost'ta olduğundan tarayıcı her isteğe hepsinin çerezlerini gönderir; token'lı çerezler
  birkaç uygulamadan sonra Kestrel'in 32 KB başlık sınırını aşıyordu, HTTP 431. Sınır yükseltilmedi. Kaydın süresi
  çerezin bitişiyle aynıdır (kayan sürede yenilenir), bitişi geçmiş kayıt okununca silinir; çıkışta ve Back-Channel
  Logout'ta (`Selcuk.Istemci.Ortak`'ın `IOturumIptalDinleyicisi`'yle, sid veya sub dizininden) hemen silinir.), rol
  tabanlı menü ve menüden türetilen sunucu tarafı yetki, Razor Pages,
  veri kaynağı seçimi (`VeriKaynagi:Mod`, açılışta doğrulanır), Api modunun HTTP istemcisi ve otomasyona özgü ayarlar
  (`secenekler.Ayarlar<T>("Bolum")`: yönetmelik şartları gibi değerler `appsettings.json`'dan `IOptions<T>`'ye bağlanır,
  `T : IDogrulanabilirAyar`, geçersizse açılış durur; koda gömülmez, ekranda "Şart değerleri örnektir."). Tanım
  hatalıysa (ad/simge yok, menüde katalog dışı rol, aynı sayfa iki kez, veri kaynağı verilmemiş) açılış Türkçe bir
  hatayla durur.
- **`UseSelcukOtomasyon`** (istek hattı ve uç noktalar): güvenlik başlıkları (giriş isteyen sayfalarda `no-store`), her
  ortamda `/Hata` (stack trace yok; veri kaynağı hatasında 503 "Veri kaynağına şu an ulaşılamıyor", 401/403'te 403 "Bu
  verilere erişim yetkin yok"), durum kodu sayfaları (404, 400), HTTPS, statik dosyalar, kimlik doğrulama,
  yetkilendirme, Razor Pages ve `/signout-backchannel`. ASP.NET Core'da ara katman ve uç noktalar servis kaydında
  eklenemediği için ikinci bir çağrıdır. Önce **açılış denetimi** (`SayfaYetkiDenetimi`) çalışır: `Pages/` altında
  menüde olmayan ve kendi `[Authorize(Roles = …)]`'ını taşımayan (yalnızca "giriş yapmış olmak"la korunan veya
  `[AllowAnonymous]`) bir sayfa varsa uygulama "Menüde olmayan ve rol istemeyen sayfa var: …" hatasıyla açılmaz.
  İstisnalar açıkça `SayfaYetkiDenetimi.Istisnalar`'dadır: `/Index`, `/Giris`, `/GirisHatasi`, `/Hata`,
  `/ErisimReddedildi`, `/Cikis`, `/Profilim`.
- **Ortak sayfalar** (uygulama aynı yolda kendi sayfasını yazarsa onunki kullanılır): `/` (girişsiz tanıtım ve "Tek
  Şifre ile giriş yap"; girişte rollere göre karşılama ve kısayol kartları), `/Giris`, `/Cikis`, `/GirisHatasi`,
  `/ErisimReddedildi` ("Bu ekrana erişim yetkin yok", 403), `/Hata`, `/Profilim` (gruplu roller).
- **Düzen** `_OtomasyonDuzen` (`_SelcukDuzen`'in üstünde): üst çubukta uygulama adı ve simgesi (uygulamanın vurgu
  rengi), içeriğin üstünde sabit "Temsili ekran: gösterilen veriler örnektir, yapılan işlemler kaydedilmez." bandı,
  solda rol başlıklı menü (dar ekranda "Menü" düğmesi; betik kapalıysa açık), temsili işlem mesajı ve alt bilgide veri
  kaynağı rozeti ("Veri: örnek veri" / "Veri: API"). Stiller `otomasyon.css`'te (bu kütüphane); `selcuk.css`
  değiştirilmez. Firuze ve kapı işareti yalnızca kimlik öğelerinde (hesap menüsü, "Tek Şifre ile giriş yap").
- **Menü ve yetki tek tanımdan:** her `MenuOgesi` başlık, sayfa yolu, kısa açıklama ve rolleri taşır. Kullanıcı
  rollerinden en az birine açık öğeleri, rol başlıkları altında (katalog sırasıyla) görür; birden çok rolü olan
  birleşimi görür. Aynı roller sayfanın yetki ilkesidir: menüde görünmeyen ekrana adresle gidilince
  `/ErisimReddedildi`. Menüde olmayan diğer sayfalar en az giriş ister; role bağlı bir sayfa menüde değilse kendi
  `[Authorize(Roles = …)]`'ını taşımalıdır; taşımazsa açılış denetimi uygulamayı durdurur (ortak sayfalar hariç).
- **Ortak parçalar** (`Pages/Shared`, modeller `OrtakParcalar.cs`): `_DurumRozeti` (`DurumRozeti(ad, DurumTuru.Bekliyor |
  Olumlu | Olumsuz | Bilgi)`; anlam metindedir, renk yalnızca destekler, tür `data-durum`'da), `_BasvuruAdimlari`
  (`BasvuruAdimlari.Olustur(aşamalar, şuAnkiSıra)`; `<ol>`, her aşamada "Tamamlandı / Şu an / Bekliyor" metni, şu anki
  aşama `aria-current="step"`), `_SayiKartlari` (`SayiKarti(başlık, sayı, anahtar)`; `data-ozet`), `_KayitTablosu`
  (`KayitTablosu.Olustur(başlık, kayıtlar, kimlik, veriAdı, sütunlar…)`: başlık h2/h3, kayıt yoksa "Kayıt yok.", dar
  ekranda yatay kaydırılan ve klavyeyle odaklanan bölge, satırlarda `data-<veriAdı>`; hücre metin, `DurumRozeti` veya
  Razor şablonu `@<text>…</text>` olabilir). Stilleri `otomasyon.css`'te.
- **Temsili işlemler:** formlar antiforgery korumalı gerçek POST yapar; işleyici `return this.TemsiliIslemSonucu();`
  ile veriyi değiştirmeden aynı sayfaya döner, "Bu ekran temsilidir; işlem kaydedilmedi." mesajı TempData ile gösterilir.
  JavaScript gerekmez.
- **Kapsam:** `KullaniciKapsami.Oku(User)` (`preferred_username`, `bolum`, `fakulte`) ve
  `Kapsar(KayitKapsami.Kendi | Bolum | Fakulte, ogrenciNo, bolum, fakulte)`. Mock kaynaklar bununla süzer: öğrenci
  yalnızca kendi kaydını, bölüm başkanı/sekreteri ve bölüm komisyonları kendi bölümünü, fakülte sekreteri kendi
  fakültesini görür. **Api modunda süzmeyi API yapar** (access token'daki `sub` ve `role`'e göre); istemci kullanıcı
  bilgisi göndermez.
- **Veri kaynağı:** `"VeriKaynagi": { "Mod": "Mock" | "Api", "ApiAdresi": "https://…", "ZamanAsimi": "00:00:05" }`.
  Varsayılan `Mock`. Geçersiz mod, Api modunda https olmayan adres veya sıfır zaman aşımı açılışı durdurur. Api
  uygulaması `OtomasyonApiIstemcisi.GetAsync<T>(yol)` kullanır: oturumdaki access token'ı (TokenYenileyici süresi
  dolmak üzereyken yeniler) `Authorization: Bearer` ile gönderir; token tarayıcıya, loga veya adrese yazılmaz.

## Yeni otomasyon nasıl eklenir (kontrol listesi)

Örnek: 5303 Etik Kurul. Her adımda Staj'daki (ortak parçalar için İntibak, Mezuniyet veya ÇAP-Yandal'daki)
karşılığına bakın.

1. **Proje.** `src/Selcuk.Otomasyon.<Ad>` (Web SDK, `net10.0`), yalnızca `Selcuk.Otomasyon.Ortak` referansı, yeni bir
   `UserSecretsId`. Çözüme ekle (`dotnet sln add --solution-folder src …`). `Pages/_ViewImports.cshtml` (Staj'daki
   gibi; `_ViewStart` gerekmez, ortak düzen gelir).
2. **Port.** mimari.md tablosundaki port: `Properties/launchSettings.json` (`https://localhost:530x`) ve
   `scripts/run-all.ps1` listesi; `scripts/olcum-calistir.ps1` ve `scripts/ekran-goruntuleri.mjs` port listeleri.
3. **Vurgu rengi.** `wwwroot/css/site.css`'te `--uygulama-vurgu` ve `--uygulama-vurgu-acik` (açık ve koyu). Beyaz, taş
   (#EDF0EF) ve açık vurgu zemininde metin kontrastı en az 4,5:1; koyu temada yüzey (#15273A) zemininde de. Değerleri
   site.css başlığına ve testine yaz. Uygulamalarım simgesi için kimlik sunucusunun site.css'ine
   `.uygulama-simge-<ad>` ve `IndexModel.VurguRenkleri`'ne adı ekle. 4,5:1'in altındaki bir ton en yakın uygun tonla
   değiştirilir.
4. **`appsettings.json`.** `Kimlik` (`Authority`, `ClientId`, `CerezAdi`, `EkKapsamlar`: `offline_access`, `<ad>.oku`,
   `<ad>.yaz`) ve `VeriKaynagi` (`Mod: "Mock"`).
5. **İstemci kaydı.** `src/Selcuk.Identity/Data/Istemciler.cs`'te `Tanim(…)` ile gizli web istemcisi (adresler
   portdan türer), `<ad>.oku`/`<ad>.yaz` izinleri, `izinliRoller`, `vurgu` ve `simge` (iki harf, üst çubuktakiyle aynı;
   Uygulamalarım kartı bunu gösterir, yoksa addan üretir) özellikleri (`Istemciler.OtomasyonTanimi`); kapsam tanımları
   (`Resources = { "<ad>-api" }`) ve Program.cs `RegisterScopes`. `BaslangicVerisi`'nde kapsamlar (her ortam) ve
   `IstemciKaydetAsync(…, "Istemciler:<Ad>:ClientSecret", …)` (Development). Sır user-secrets'ta: kimlik sunucusunda
   `Istemciler:<Ad>:ClientSecret`, otomasyonda `Kimlik:ClientSecret` (aynı rastgele değer, ekrana yazdırmadan).
6. **izinliRoller.** roller.md plan matrisindeki satırlar: otomasyona ekranı olan her rol. Listede olmayan rol
   authorize'da `access_denied` alır ve Uygulamalarım'da kartı görmez.
7. **Menü.** `<Ad>Otomasyonu.Menu`: her temsili ekran bir `MenuOgesi` (başlık, `/Rol/Ekran` yolu, kısa açıklama,
   rol). Sayfalar `Pages/<Rol>/<Ekran>.cshtml`; tek `<h1>`, tablolarda `tablo tablo-yigin`, kayıt satırlarında
   `data-<ad>="<kimlik>"` (testler kapsamı bununla denetler).
8. **Veri kaynağı arayüzü.** `Sozlesme/` klasöründe DTO'lar (record) ve `I<Ad>VeriKaynagi` (yalnızca okuma; her yöntemin
   XML yorumunda API yolu). `Mock<Ad>VeriKaynagi` (kapsamı `KullaniciKapsami` ile süzer) ve `Api<Ad>VeriKaynagi`
   (`OtomasyonApiIstemcisi`); `Ayarla`'da `secenekler.VeriKaynagi<I…, Mock…, Api…>()`. Yönetmelik şartları (GNO alt
   sınırı, AKTS, kontenjan…) `appsettings.json`'da bir bölümdür, `secenekler.Ayarlar<<Ad>Sartlari>("<Ad>Sartlari")`.
   Başka bir otomasyonun verisi (ör. Mezuniyet'te staj durumu) şimdilik örnek veridir; diğer uygulama çağrılmaz,
   sözleşmeye "ileride <ad>-api'den alınacak" notu düşülür.
9. **Mock veri.** `Veri/Ornek<Ad>Verisi.cs`: son 3 yıla yayılan 20-30 kayıt, her durum en az bir kez; öğrenciler test
   öğrencileri (203011001-003) ve yalnızca mock'ta bulunan `ORN-…` numaralı uydurma öğrenciler; en az bir kayıt başka
   bir bölümden (fakülte sekreteri ekranı varsa en az bir kayıt da başka bir fakülteden). Gerçek firma, kişi veya
   öğrenci numarası yok; adlar açıkça uydurma ("Örnek …", "Deneme …").
10. **API sözleşmesi.** `docs/api-sozlesmeleri/<ad>.md`: uç noktalar (yöntem, yol, kapsam, rol, dönen tür, ekran),
    JSON biçimi ve örnekleri, `aud` `<ad>-api`, süzmeyi API'nin token'daki claim'lere göre yapacağı.
11. **Testler.** `tests/Selcuk.Otomasyon.<Ad>.Tests`. Ortak testler hazırdır: projeye
    `tests/Selcuk.Otomasyon.TestOrtak/*.cs` bağlantıyla eklenir (csproj'da `<Compile Include=… Link=…>`; İntibak'taki
    gibi), `OtomasyonTemelTestleri<Program>`'dan türeyen bir sınıf otomasyonun değerlerini verir (ad, simge, client_id,
    çerez, aud, menü, veri kaynağı türleri, rol başına test kullanıcısı, Api örneği, temsili işlemler; öğrenci rolü
    yoksa `AnaRol`, ör. Etik Kurul'da akademisyen) ve aşağıdakilerin
    çoğu kendiliğinden gelir; kapsam ve şart testleri otomasyonun kendi sınıfında:
    menü (her rol yalnızca kendi öğeleri, çok rollü birleşim), adresle yetki reddi, kapsam (öğrenci kendi kaydı, başka
    bölüm görünmez), temsili işlem (antiforgery'siz 400; mesaj ve değişmeyen veri), Mock/Api seçimi, Api modu (Bearer,
    hata sayfaları), güvenlik başlıkları ve `no-store`, Back-Channel Logout, vurgu kontrastı. Kimlik sunucusunda:
    kayıt, izinsiz rollerin `access_denied`'ı, izinli rollerin token'ı (`aud` `<ad>-api`), Uygulamalarım kartı.
12. **Belgeler.** README (adres ve hangi test kullanıcısının hangi ekranları gördüğü), mimari.md tablosunda durum
    "var", AGENTS.md mimari bölümü, test-kapsami.md sayıları, `scripts/ekran-goruntuleri.mjs`'e temsili ekranlar.

Adımların çoğu Staj'daki dosyanın kopyalanıp adlarının değiştirilmesidir; otomasyona özgü iş 7-10. adımlardadır
(ekranlar, sözleşme, örnek veri).
