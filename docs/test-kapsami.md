# Test kapsamı

Sayılar elle sayılmadı: .NET testleri `dotnet test <proje> --no-build --list-tests` çıktısından (her `[InlineData]`
satırı ayrı test), Tek Kapı kontrolleri `npm test` çıktısından ("33 kontrol geçti.", "14 SSO kontrolü geçti."), kod
kapsamı `dotnet test SelcukSSO.sln --collect:"XPlat Code Coverage"` (test projelerinde zaten olan
`coverlet.collector` 6.0.4) ile üretilen Cobertura raporlarından alındı. Ölçüm tarihi: 2026-10-26 (Adım 8). Test
sayıları Tasarım D1'de güncellendi; kod kapsamı tablosu Adım 8 ölçümüdür, D1'den sonra yeniden ölçülmedi.

## Test sayıları

| Test projesi | Test | Kategori (test sınıfı: test sayısı) |
|---|---:|---|
| `tests/Selcuk.Identity.Tests` | 119 | KabaKuvvetTests (istek sınırı, kullanıcı adı tahmini, ölçüm gevşetmesi): 22 · IkiAdimTests (2FA): 17 · TokenYasamDongusuTests (token, refresh, iptal, mutlak sınır): 10 · IstemciErisimiTests (uygulama bazlı erişim, Tek Kapı): 10 · SifreSifirlamaTests: 9 · OidcAkisTests (protokol): 8 · DenetimKaydiTests: 8 · TekOturumTests (tek giriş/çıkış): 6 · GirisTests (giriş, kilit): 5 · GuvenilenProxyTests: 5 · IstemciMfaTests (istemci bazlı 2FA, önbellek): 5 · DiscoveryTests: 3 · SifreDegistirmeTests: 3 · ArayuzTests (tasarım sistemi: statik dosyalar ve CSP, etiketler, boş durum, HTML e-posta): 4 · TokenBudamaTests: 2 · PkceTests: 1 · RolTests: 1 |
| `tests/Selcuk.Istemci.Ortak.Tests` | 19 | BackChannelLogoutTests: 8 · OturumGuvenligiTests (çerez süresi, iss, JWKS, dağıtık önbellek): 7 · TokenYenilemeTests: 4 |
| `tests/Selcuk.Demo.Obis.Tests` | 15 | GirisDonusHatasiTests: 8 · YetkilendirmeTests: 4 · NotlarimApiTests: 2 · OnbellekTests: 1 |
| `tests/Selcuk.Demo.Yemekhane.Tests` | 7 | YetkilendirmeTests: 4 · GirisDonusHatasiTests: 2 · OnbellekTests: 1 |
| `tests/Selcuk.Demo.Api.Tests` | 2 | NotlarApiTests: 2 |
| `tests/Selcuk.Olcum.Tests` | 15 | YerelAgTests (localhost dışı reddi, form ayrıştırma, istatistik): 14 · OlcumAkisTests (dört uygulamayla bellek içi uçtan uca tur): 1 |
| **.NET toplam** | **177** | Adım 7 sonunda 137; Adım 8'de 36 yeni (Identity 21, Olcum 15); Tasarım D1'de 4 yeni (Identity), hiçbiri silinmedi |
| `araclar/tek-kapi` `test/e2e.js` (bağımsız mod) | 33 kontrol | Kasa, otomatik/yarı otomatik girişler (sahte siteler 5600-5606), ölçüm kaydı, hesap kilitletmeme, "Hepsinden çık", CSV, Host/CSRF korumaları, günlüklerde kimlik bilgisi olmaması |
| `araclar/tek-kapi` `test/sso.js` (SSO modu) | 14 kontrol | Sahte OIDC sağlayıcıyla (5190) giriş, state/PKCE, id_token doğrulama, izinli kullanıcılar, `iss`, Back-Channel Logout doğrulama ve kilitleme, çıkış |
| **Tek Kapı toplam** | **47 kontrol** | |

Her testin ne doğruladığı README > Testler tablosunda.

## Kod kapsamı

Satır ve dal kapsamı kaynak projeye göre. "Tüm testler": altı test projesinin raporları birleştirildi (bir satır
herhangi bir test projesinde çalıştıysa kapsanmış sayılır; dallarda aynı satır için raporlar arasındaki en yüksek
kapsanan dal sayısı alındı, bu yüzden dal oranı bir alt sınırdır). `Selcuk.Olcum.Tests`'in uçtan uca testi dört web
uygulamasını da çalıştırdığı için onların kapsamına da katkı verir. EF Core'un ürettiği `Data/Migrations` kodu
(kimlik sunucusunda 2 583 satır) satır oranından ayrıca hariç tutuldu; Razor sayfalarının derlenmiş kodu dahildir.

| Kaynak proje | Satır (tüm testler) | Satır % | Dal (tüm testler) | Dal % | Yalnızca kendi test projesi: satır % / dal % |
|---|---:|---:|---:|---:|---|
| `Selcuk.Identity` (Migrations hariç) | 2180 / 2554 | 85,4 % | 764 / 1013 | 75,4 % | 85,3 % / 75,4 % (`Selcuk.Identity.Tests`) |
| `Selcuk.Identity` (Migrations dahil) | 2180 / 5137 | 42,4 % | 764 / 1013 | 75,4 % | 42,4 % / 75,4 % |
| `Selcuk.Istemci.Ortak` | 400 / 456 | 87,7 % | 147 / 200 | 73,5 % | 79,4 % / 60,0 % (`Selcuk.Istemci.Ortak.Tests`) |
| `Selcuk.Demo.Obis` | 130 / 203 | 64,0 % | 66 / 118 | 55,9 % | 61,6 % / 52,5 % (`Selcuk.Demo.Obis.Tests`) |
| `Selcuk.Demo.Yemekhane` | 123 / 162 | 75,9 % | 68 / 100 | 68,0 % | 75,9 % / 68,0 % (`Selcuk.Demo.Yemekhane.Tests`) |
| `Selcuk.Demo.Api` | 69 / 72 | 95,8 % | 5 / 8 | 62,5 % | 95,8 % / 62,5 % (`Selcuk.Demo.Api.Tests`) |
| `Selcuk.Olcum` | 805 / 1047 | 76,9 % | 226 / 392 | 57,7 % | 76,9 % / 57,7 % (`Selcuk.Olcum.Tests`) |
| **Toplam (Migrations hariç)** | 3707 / 4494 | **82,5 %** | 1276 / 1831 | **69,7 %** | |

Tek Kapı (Node.js) için kod kapsamı ölçülmedi: depoda bir kapsam aracı yok ve yeni araç kurulmadı.

## Nasıl yeniden üretilir

```powershell
dotnet build SelcukSSO.sln
foreach ($p in Get-ChildItem tests -Directory) { dotnet test $p.FullName --no-build --list-tests }
dotnet test SelcukSSO.sln --no-build --collect:"XPlat Code Coverage" --results-directory TestResults\kapsam
cd araclar\tek-kapi; npm test
```

`TestResults/` ve `coverage*.xml` `.gitignore`'dadır. Cobertura raporlarını birleştirmek için ReportGenerator gibi bir
araç kullanılmadı; yukarıdaki tablo raporların `<line>` öğeleri kısa bir betikle (Python, standart kütüphane)
birleştirilerek hesaplandı: dosya yolları raporun `<source>` köküyle mutlak yola çevrildi, satırlar dosya + satır
numarasına göre birleştirildi.
