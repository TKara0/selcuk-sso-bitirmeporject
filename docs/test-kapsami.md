# Test kapsamı

Sayılar elle sayılmadı: .NET testleri `dotnet test <proje> --no-build --list-tests` çıktısından (her `[InlineData]`
satırı ayrı test), Tek Kapı kontrolleri `npm test` çıktısından ("35 kontrol geçti.", "14 SSO kontrolü geçti."), kod
kapsamı `dotnet test SelcukSSO.sln --collect:"XPlat Code Coverage"` (test projelerinde zaten olan
`coverlet.collector` 6.0.4) ile üretilen Cobertura raporlarından alındı. Ölçüm tarihi: 2026-10-26 (Adım 8). Test
sayıları Öğrenci Danışmanlık, İŞKUR, MÜDEK ve Sınav Başarısı Analizi adımında (2026-10-31) güncellendi; kod kapsamı tablosu Adım 8 ölçümüdür, sonrasında yeniden ölçülmedi.

## Test sayıları

| Test projesi | Test | Kategori (test sınıfı: test sayısı) |
|---|---:|---|
| `tests/Selcuk.Identity.Tests` | 236 | KalanOtomasyonIstemcileriTests (Öğrenci Danışmanlık, İŞKUR, MÜDEK, Sınav Başarısı Analizi: kayıt, access_denied, token, kartlar ve simgeler): 30 · KurulVeProjeIstemcileriTests (Etik Kurul, Bölüm Kurul Karar, Lisans Projeleri: kayıt, access_denied, token, kartlar ve simgeler): 23 · OtomasyonIstemcileriTests (İntibak, Mezuniyet, ÇAP-Yandal: kayıt, izinsiz rollerin access_denied'ı, izinli rollerin token'ı, Uygulamalarım kartları, simge): 27 · StajOtomasyonuIstemciTests (kayıt, izinsiz rollerin access_denied'ı, izinli dört rolün token'ı, Uygulamalarım kartı): 9 · RolModeliTests (rol kataloğu doğrulayıcısı, seed, role dizisi, Hesabım rolleri, 2FA'nın hiçbir rolde zorunlu olmaması): 24 · KabaKuvvetTests (istek sınırı, kullanıcı adı tahmini, ölçüm gevşetmesi): 22 · IkiAdimTests (2FA): 17 · TokenYasamDongusuTests (token, refresh, iptal, mutlak sınır): 10 · IstemciErisimiTests (uygulama bazlı erişim, Tek Kapı): 10 · SifreSifirlamaTests: 9 · OidcAkisTests (protokol): 8 · DenetimKaydiTests: 8 · TekOturumTests (tek giriş/çıkış): 6 · GirisTests (giriş, kilit): 5 · GuvenilenProxyTests: 5 · IstemciMfaTests (istemci bazlı 2FA, önbellek): 5 · DiscoveryTests: 3 · SifreDegistirmeTests: 3 · ArayuzTests (tasarım sistemi: statik dosyalar ve CSP, etiketler, boş durum, HTML e-posta, kapı SVG'leri, D1 düzeltmeleri): 8 · TokenBudamaTests: 2 · PkceTests: 1 · RolTests (katalogdaki bütün roller): 1 |
| `tests/Selcuk.Istemci.Ortak.Tests` | 21 | BackChannelLogoutTests: 8 · OturumGuvenligiTests (çerez süresi, iss, JWKS, dağıtık önbellek): 7 · TokenYenilemeTests: 4 · RolTests (çok rollü IsInRole, tam OIDC girişi): 2 |
| `tests/Selcuk.Demo.Obis.Tests` | 24 | GirisDonusHatasiTests: 8 · YetkilendirmeTests: 4 · NotlarimApiTests (API, toplam kredi ve ortalama): 4 · TokenIncelemeTests: 3 · ProfilimTests (gruplu roller): 2 · ArayuzTests (ortak düzen, `_content`): 2 · OnbellekTests: 1 |
| `tests/Selcuk.Demo.Yemekhane.Tests` | 9 | YetkilendirmeTests: 4 · GirisDonusHatasiTests: 2 · ArayuzTests (ortak düzen, `_content`): 2 · OnbellekTests: 1 |
| `tests/Selcuk.Demo.Api.Tests` | 2 | NotlarApiTests: 2 |
| `tests/Selcuk.Otomasyon.Ortak.Tests` | 36 | SablonTests (menü, kapsam, veri kaynağı ayarları ve seçimi, şablon tanım hataları, oturum bileti deposu, Api istemcisi): 28 · AcilisVeParcaTests (açılış denetimi, oturum bileti süreleri ve silinmesi, ortak parçalar): 8 |
| `tests/Selcuk.Otomasyon.Staj.Tests` | 37 | StajEkranTests (menü, yetki, kapsam, temsili işlem, düzen, güvenlik başlıkları, kontrast): 20 · VeriKaynagiTests (Mock/Api seçimi, geçersiz ayar, Api modu ve hata sayfaları, Back-Channel Logout): 17 |
| `tests/Selcuk.Otomasyon.Intibak.Tests` | 35 | IntibakTemelTestleri (ortak: menü, yetki, düzen, başlıklar, kontrast, temsili işlem, Mock/Api, Api hataları, Back-Channel Logout, çıkış): 25 · IntibakEkranTests (kapsam, bölüm/fakülte süzmesi, ortak parçalar, şartlar, form): 10 |
| `tests/Selcuk.Otomasyon.Mezuniyet.Tests` | 31 | MezuniyetTemelTestleri: 25 · MezuniyetEkranTests (eksikler, şartlar, aşamalar, ilişik kesme, kapsam): 6 |
| `tests/Selcuk.Otomasyon.CapYandal.Tests` | 32 | CapYandalTemelTestleri: 25 · CapYandalEkranTests (öğrenci, şart göstergesi, kontenjan, devam uyarısı, kapsam): 7 |
| `tests/Selcuk.Otomasyon.EtikKurul.Tests` | 33 | EtikKurulTemelTestleri: 25 · EtikKurulEkranTests (kendi başvuruları, atanan dosyalar, bölüm, şartlar, form, çok rol): 8 |
| `tests/Selcuk.Otomasyon.BolumKurul.Tests` | 31 | BolumKurulTemelTestleri: 25 · BolumKurulEkranTests (bölüm ve fakülte süzmesi, oylama, şartlar): 6 |
| `tests/Selcuk.Otomasyon.LisansProjeleri.Tests` | 31 | LisansProjeleriTemelTestleri: 25 · LisansProjeleriEkranTests (kendi kaydı, kendi projeleri, eşleştirme, şartlar): 6 |
| `tests/Selcuk.Otomasyon.Danismanlik.Tests` | 30 | DanismanlikTemelTestleri: 25 · DanismanlikEkranTests (kendi öğrencileri ve notları, kendi kaydı ve talepleri, danışman atama ve kontenjan, şartlar): 5 |
| `tests/Selcuk.Otomasyon.Iskur.Tests` | 29 | IskurTemelTestleri: 25 · IskurEkranTests (kendi devam kayıtları, bölüm süzmesi, şartlar): 4 |
| `tests/Selcuk.Otomasyon.Mudek.Tests` | 29 | MudekTemelTestleri: 25 · MudekEkranTests (kendi ders çıktıları, program çıktısı gerçekleşmesi, eksikler, arşiv, şartlar): 4 |
| `tests/Selcuk.Otomasyon.SinavAnalizi.Tests` | 31 | SinavAnaliziTemelTestleri: 25 · SinavAnaliziEkranTests (kendi dersleri, bölüm geneli, çıktı bazlı analiz, öğrenci düzeyinde veri ve satır içi stil yok, şartlar): 6 |
| `tests/Selcuk.Olcum.Tests` | 15 | YerelAgTests (localhost dışı reddi, form ayrıştırma, istatistik): 14 · OlcumAkisTests (dört uygulamayla bellek içi uçtan uca tur): 1 |
| **.NET toplam** | **692** | Adım 7 sonunda 137; Adım 8'de 36 yeni (Identity 21, Olcum 15); Tasarım D1'de 4 yeni (Identity); Tasarım D2'de 12 yeni (Identity 4, Demo OBİS 6, Demo Yemekhane 2); Rol modelinde 29 yeni (Identity 24, Istemci.Ortak 2, Demo OBİS 3); otomasyon şablonu ve Staj'da 74 yeni (Identity 9, Otomasyon.Ortak 28, Otomasyon.Staj 37); İntibak, Mezuniyet ve ÇAP-Yandal'da 133 yeni (Identity 27, Otomasyon.Ortak 8, İntibak 35, Mezuniyet 31, ÇAP-Yandal 32); Etik Kurul, Bölüm Kurul Karar ve Lisans Projeleri'nde 118 yeni (Identity 23, Etik Kurul 33, Bölüm Kurul 31, Lisans Projeleri 31); Öğrenci Danışmanlık, İŞKUR, MÜDEK ve Sınav Başarısı Analizi'nde 149 yeni (Identity 30, Danışmanlık 30, İŞKUR 29, MÜDEK 29, Sınav Analizi 31), hiçbiri silinmedi |
| `araclar/tek-kapi` `test/e2e.js` (bağımsız mod) | 35 kontrol | Kasa, otomatik/yarı otomatik girişler (sahte siteler 5600-5606), ölçüm kaydı, hesap kilitletmeme, "Hepsinden çık", CSV, Host/CSRF korumaları, CSP'de Google Fonts olmaması, `public/arayuz/` kopyalarının kaynakla aynı olması (SHA-256), günlüklerde kimlik bilgisi olmaması |
| `araclar/tek-kapi` `test/sso.js` (SSO modu) | 14 kontrol | Sahte OIDC sağlayıcıyla (5190) giriş, state/PKCE, id_token doğrulama, izinli kullanıcılar, `iss`, Back-Channel Logout doğrulama ve kilitleme, çıkış |
| **Tek Kapı toplam** | **49 kontrol** | Tasarım D2'de 2 yeni (e2e), hiçbiri silinmedi |

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
foreach ($p in Get-ChildItem tests -Directory | Where-Object { Test-Path (Join-Path $_.FullName "*.csproj") }) { dotnet test $p.FullName --no-build --list-tests }
dotnet test SelcukSSO.sln --no-build --collect:"XPlat Code Coverage" --results-directory TestResults\kapsam
cd araclar\tek-kapi; npm test
```

`TestResults/` ve `coverage*.xml` `.gitignore`'dadır. Cobertura raporlarını birleştirmek için ReportGenerator gibi bir
araç kullanılmadı; yukarıdaki tablo raporların `<line>` öğeleri kısa bir betikle (Python, standart kütüphane)
birleştirilerek hesaplandı: dosya yolları raporun `<source>` köküyle mutlak yola çevrildi, satırlar dosya + satır
numarasına göre birleştirildi.
