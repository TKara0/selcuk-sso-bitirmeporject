# Test kapsamı

Sayılar elle sayılmadı: .NET testleri `dotnet test <proje> --no-build --list-tests` çıktısından (her `[InlineData]`
satırı ayrı test), Tek Kapı kontrolleri `npm test` çıktısından ("35 kontrol geçti.", "14 SSO kontrolü geçti."), kod
kapsamı `dotnet test SelcukSSO.sln --collect:"XPlat Code Coverage"` (test projelerinde zaten olan
`coverlet.collector` 6.0.4) ile üretilen Cobertura raporlarından alındı. Test
sayıları CSRF testleri ve rol değişikliği adımında (2026-11-24) güncellendi (bütün çözüm varsayılan paralel ayarla çalıştırılır; önceki adımdaki rastgele düşme R21 ile kapandı); kod kapsamı 2026-11-24'te yeniden ölçüldü (aşağıda).

## Test sayıları

| Test projesi | Test | Kategori (test sınıfı: test sayısı) |
|---|---:|---|
| `tests/Selcuk.Identity.Tests` | 262 | KalanOtomasyonIstemcileriTests (Öğrenci Danışmanlık, İŞKUR, MÜDEK, Sınav Başarısı Analizi: kayıt, access_denied, token, kartlar ve simgeler): 30 · KurulVeProjeIstemcileriTests (Etik Kurul, Bölüm Kurul Karar, Lisans Projeleri: kayıt, access_denied, token, kartlar ve simgeler): 23 · OtomasyonIstemcileriTests (İntibak, Mezuniyet, ÇAP-Yandal: kayıt, izinsiz rollerin access_denied'ı, izinli rollerin token'ı, Uygulamalarım kartları, simge): 27 · StajOtomasyonuIstemciTests (kayıt, izinsiz rollerin access_denied'ı, izinli dört rolün token'ı, Uygulamalarım kartı): 9 · RolModeliTests (rol kataloğu doğrulayıcısı, seed, role dizisi, Hesabım rolleri, 2FA'nın hiçbir rolde zorunlu olmaması): 24 · KabaKuvvetTests (istek sınırı, kullanıcı adı tahmini, ölçüm gevşetmesi): 22 · IkiAdimTests (2FA): 17 · TokenYasamDongusuTests (token, refresh, iptal, mutlak sınır): 10 · IstemciErisimiTests (uygulama bazlı erişim, Tek Kapı): 10 · SifreSifirlamaTests: 9 · OidcAkisTests (protokol): 8 · DenetimKaydiTests: 8 · TekOturumTests (tek giriş/çıkış): 6 · GirisTests (giriş, kilit): 5 · GuvenilenProxyTests: 5 · IstemciMfaTests (istemci bazlı 2FA, önbellek): 5 · DiscoveryTests: 3 · SifreDegistirmeTests: 3 · ArayuzTests (tasarım sistemi: statik dosyalar ve CSP, etiketler, boş durum, HTML e-posta, kapı SVG'leri, D1 düzeltmeleri): 8 · TokenBudamaTests: 2 · PkceTests: 1 · RolTests (katalogdaki bütün roller): 1 · UygulamalarimGruplariTests (gruplar, sıra, boş grup yok, "Diğer", başlık erişilebilirliği, kayıt grupları): 5 · CsrfTests (POST işleyicisi listesi, antiforgery'siz 400 ve yan etkisizlik, protokol uçları): 15 · RolDegisikligiTests (rol eklenince/çıkarılınca oturum kapatma, Back-Channel Logout, denetim): 6 |
| `tests/Selcuk.Istemci.Ortak.Tests` | 31 | BackChannelLogoutTests: 8 · OturumGuvenligiTests (çerez süresi, iss, JWKS, dağıtık önbellek): 7 · TokenYenilemeTests: 4 · RolTests (çok rollü IsInRole, tam OIDC girişi): 2 · SunucuTarafliOturumSecenekTests (seçenek varsayılan kapalı/açık, Authority kökü): 10 |
| `tests/Selcuk.Demo.Obis.Tests` | 63 | ObisEkranTests (yeni arayüz: sekmeler ve roller, claim'lerden Anasayfa, kişiye özel örnek veri, Demo ekran bandı / Test rozeti / resmî sistem değildir, dış kaynak yok, no-store ve güvenlik başlıkları, baş harfler, örnek veri tutarlılığı, Onay Takip): 19 · TemsiliIslemTests (antiforgery'siz 400, veri değişmez, dönüş adresi): 11 · ObisArayuzTests (mavi tonların AA kontrastı, ortak CSS'e dokunulmaması): 2 · GirisDonusHatasiTests: 8 · YetkilendirmeTests: 4 · NotlarimApiTests (API, toplam kredi ve ortalama): 4 · TokenIncelemeTests: 3 · ProfilimTests (gruplu roller): 2 · ArayuzTests (ortak düzen, `_content`): 2 · OnbellekTests: 1 · SunucuTarafliOturumTests (tek küçük çerez, token'sız çerez, Notlarım/Token İnceleme, çıkışta ve Back-Channel Logout'ta silme, Uygulamalarım bağlantısı): 5 · CsrfTests: 2 |
| `tests/Selcuk.Demo.Yemekhane.Tests` | 17 | YetkilendirmeTests: 4 · GirisDonusHatasiTests: 2 · ArayuzTests (ortak düzen, `_content`): 2 · OnbellekTests: 1 · SunucuTarafliOturumTests (aynı, Notlarım/Token İnceleme hariç): 4 · CsrfTests: 4 |
| `tests/Selcuk.Demo.Api.Tests` | 2 | NotlarApiTests: 2 |
| `tests/Selcuk.Otomasyon.Ortak.Tests` | 48 | SablonTests (Uygulamalarım adresi türetme, menü, kapsam, veri kaynağı ayarları ve seçimi, şablon tanım hataları, oturum bileti deposu, Api istemcisi): 40 · AcilisVeParcaTests (açılış denetimi, oturum bileti süreleri ve silinmesi, ortak parçalar): 8 |
| `tests/Selcuk.Otomasyon.Staj.Tests` | 38 | StajEkranTests (menü, yetki, kapsam, temsili işlem, düzen, güvenlik başlıkları, kontrast, Uygulamalarım bağlantısı): 21 · VeriKaynagiTests (Mock/Api seçimi, geçersiz ayar, Api modu ve hata sayfaları, Back-Channel Logout): 17 |
| `tests/Selcuk.Otomasyon.Intibak.Tests` | 38 | IntibakTemelTestleri (ortak: menü, yetki, düzen, başlıklar, kontrast, temsili işlem, Mock/Api, Api hataları, Back-Channel Logout, çıkış): 28 · IntibakEkranTests (kapsam, bölüm/fakülte süzmesi, ortak parçalar, şartlar, form): 10 |
| `tests/Selcuk.Otomasyon.Mezuniyet.Tests` | 34 | MezuniyetTemelTestleri: 28 · MezuniyetEkranTests (eksikler, şartlar, aşamalar, ilişik kesme, kapsam): 6 |
| `tests/Selcuk.Otomasyon.CapYandal.Tests` | 35 | CapYandalTemelTestleri: 28 · CapYandalEkranTests (öğrenci, şart göstergesi, kontenjan, devam uyarısı, kapsam): 7 |
| `tests/Selcuk.Otomasyon.EtikKurul.Tests` | 36 | EtikKurulTemelTestleri: 28 · EtikKurulEkranTests (kendi başvuruları, atanan dosyalar, bölüm, şartlar, form, çok rol): 8 |
| `tests/Selcuk.Otomasyon.BolumKurul.Tests` | 34 | BolumKurulTemelTestleri: 28 · BolumKurulEkranTests (bölüm ve fakülte süzmesi, oylama, şartlar): 6 |
| `tests/Selcuk.Otomasyon.LisansProjeleri.Tests` | 34 | LisansProjeleriTemelTestleri: 28 · LisansProjeleriEkranTests (kendi kaydı, kendi projeleri, eşleştirme, şartlar): 6 |
| `tests/Selcuk.Otomasyon.Danismanlik.Tests` | 33 | DanismanlikTemelTestleri: 28 · DanismanlikEkranTests (kendi öğrencileri ve notları, kendi kaydı ve talepleri, danışman atama ve kontenjan, şartlar): 5 |
| `tests/Selcuk.Otomasyon.Iskur.Tests` | 32 | IskurTemelTestleri: 28 · IskurEkranTests (kendi devam kayıtları, bölüm süzmesi, şartlar): 4 |
| `tests/Selcuk.Otomasyon.Mudek.Tests` | 32 | MudekTemelTestleri: 28 · MudekEkranTests (kendi ders çıktıları, program çıktısı gerçekleşmesi, eksikler, arşiv, şartlar): 4 |
| `tests/Selcuk.Otomasyon.SinavAnalizi.Tests` | 34 | SinavAnaliziTemelTestleri: 28 · SinavAnaliziEkranTests (kendi dersleri, bölüm geneli, çıktı bazlı analiz, öğrenci düzeyinde veri ve satır içi stil yok, şartlar): 6 |
| `tests/Selcuk.Olcum.Tests` | 15 | YerelAgTests (localhost dışı reddi, form ayrıştırma, istatistik): 14 · OlcumAkisTests (dört uygulamayla bellek içi uçtan uca tur): 1 |
| **.NET toplam** | **818** | Adım 7 sonunda 137; Adım 8'de 36 yeni (Identity 21, Olcum 15); Tasarım D1'de 4 yeni (Identity); Tasarım D2'de 12 yeni (Identity 4, Demo OBİS 6, Demo Yemekhane 2); Rol modelinde 29 yeni (Identity 24, Istemci.Ortak 2, Demo OBİS 3); otomasyon şablonu ve Staj'da 74 yeni (Identity 9, Otomasyon.Ortak 28, Otomasyon.Staj 37); İntibak, Mezuniyet ve ÇAP-Yandal'da 133 yeni (Identity 27, Otomasyon.Ortak 8, İntibak 35, Mezuniyet 31, ÇAP-Yandal 32); Etik Kurul, Bölüm Kurul Karar ve Lisans Projeleri'nde 118 yeni (Identity 23, Etik Kurul 33, Bölüm Kurul 31, Lisans Projeleri 31); Öğrenci Danışmanlık, İŞKUR, MÜDEK ve Sınav Başarısı Analizi'nde 149 yeni (Identity 30, Danışmanlık 30, İŞKUR 29, MÜDEK 29, Sınav Analizi 31), hiçbiri silinmedi; Uygulamalarım bağlantısında 43 yeni (Otomasyon.Ortak 12, Staj 1, ortak TemelTestleri 3 × 10 otomasyon), hiçbiri silinmedi; Uygulamalarım grupları, demolarda dönüş bağlantısı ve sunucu tarafı oturumda 24 yeni (Identity 5, Istemci.Ortak 10, Demo OBİS 5, Demo Yemekhane 4), hiçbiri silinmedi; kararsız test düzeltmesi, CSRF testleri ve rol değişikliğinde 27 yeni (Identity 21, Demo OBİS 2, Demo Yemekhane 4), hiçbiri silinmedi; Demo OBİS yeni arayüzünde 32 yeni (Demo OBİS), hiçbiri silinmedi |
| `araclar/tek-kapi` `test/e2e.js` (bağımsız mod) | 35 kontrol | Kasa, otomatik/yarı otomatik girişler (sahte siteler 5600-5606), ölçüm kaydı, hesap kilitletmeme, "Hepsinden çık", CSV, Host/CSRF korumaları, CSP'de Google Fonts olmaması, `public/arayuz/` kopyalarının kaynakla aynı olması (SHA-256), günlüklerde kimlik bilgisi olmaması |
| `araclar/tek-kapi` `test/sso.js` (SSO modu) | 14 kontrol | Sahte OIDC sağlayıcıyla (5190) giriş, state/PKCE, id_token doğrulama, izinli kullanıcılar, `iss`, Back-Channel Logout doğrulama ve kilitleme, çıkış |
| **Tek Kapı toplam** | **49 kontrol** | Tasarım D2'de 2 yeni (e2e), hiçbiri silinmedi |

Her testin ne doğruladığı README > Testler tablosunda.

## Kod kapsamı

Ölçüm tarihi: 2026-11-24. Satır ve dal kapsamı kaynak projeye göre; 18 test projesinin raporları birleştirildi (bir
satır herhangi bir test projesinde çalıştıysa kapsanmış sayılır; dallarda aynı satır için raporlar arasındaki en
yüksek kapsanan dal sayısı alındı, bu yüzden dal oranı bir alt sınırdır). Bu ölçümde 11 otomasyon, `Selcuk.Otomasyon.Ortak`,
`Selcuk.Roller` ve `Selcuk.Arayuz` da tabloda; `Selcuk.Olcum.Tests`'in uçtan uca testi dört web uygulamasını da
çalıştırdığı için onların kapsamına da katkı verir. EF Core'un ürettiği `Data/Migrations` kodu (kimlik sunucusunda
2 583 satır) hariç tutuldu; Razor sayfalarının derlenmiş kodu dahildir. "Yalnızca kendi test projesi" sütunu bu ölçümde
yeniden hesaplanmadı (raporlar projeye göre ayrılmadı).

| Kaynak proje | Satır | Satır % | Dal | Dal % |
|---|---:|---:|---:|---:|
| `Selcuk.Arayuz` | 163 / 168 | 97,0 % | 106 / 118 | 89,8 % |
| `Selcuk.Demo.Api` | 69 / 72 | 95,8 % | 5 / 8 | 62,5 % |
| `Selcuk.Demo.Obis` | 346 / 370 | 93,5 % | 131 / 162 | 80,9 % |
| `Selcuk.Demo.Yemekhane` | 189 / 200 | 94,5 % | 85 / 100 | 85,0 % |
| `Selcuk.Identity` | 2638 / 3009 | 87,7 % | 904 / 1164 | 77,7 % |
| `Selcuk.Istemci.Ortak` | 512 / 563 | 90,9 % | 197 / 248 | 79,4 % |
| `Selcuk.Olcum` | 805 / 1047 | 76,9 % | 226 / 392 | 57,7 % |
| `Selcuk.Otomasyon.BolumKurul` | 341 / 355 | 96,1 % | 69 / 86 | 80,2 % |
| `Selcuk.Otomasyon.CapYandal` | 489 / 506 | 96,6 % | 105 / 142 | 73,9 % |
| `Selcuk.Otomasyon.Danismanlik` | 452 / 468 | 96,6 % | 68 / 84 | 81,0 % |
| `Selcuk.Otomasyon.EtikKurul` | 349 / 357 | 97,8 % | 77 / 97 | 79,4 % |
| `Selcuk.Otomasyon.Intibak` | 655 / 673 | 97,3 % | 137 / 163 | 84,0 % |
| `Selcuk.Otomasyon.Iskur` | 223 / 226 | 98,7 % | 28 / 38 | 73,7 % |
| `Selcuk.Otomasyon.LisansProjeleri` | 433 / 450 | 96,2 % | 85 / 104 | 81,7 % |
| `Selcuk.Otomasyon.Mezuniyet` | 436 / 465 | 93,8 % | 82 / 119 | 68,9 % |
| `Selcuk.Otomasyon.Mudek` | 325 / 334 | 97,3 % | 55 / 69 | 79,7 % |
| `Selcuk.Otomasyon.Ortak` | 537 / 559 | 96,1 % | 260 / 298 | 87,2 % |
| `Selcuk.Otomasyon.SinavAnalizi` | 305 / 306 | 99,7 % | 20 / 32 | 62,5 % |
| `Selcuk.Otomasyon.Staj` | 338 / 356 | 94,9 % | 91 / 111 | 82,0 % |
| `Selcuk.Roller` | 108 / 113 | 95,6 % | 44 / 48 | 91,7 % |
| **Toplam (Migrations hariç)** | 9713 / 10597 | **91,7 %** | 2775 / 3583 | **77,4 %** |

Önceki ölçüm (2026-10-26, Adım 8; yalnızca 6 proje): toplam 3707 / 4494 satır (%82,5), 1276 / 1831 dal (%69,7); proje
kümeleri farklı olduğu için toplamlar doğrudan karşılaştırılamaz. Aynı projelerde: `Selcuk.Identity` %85,4 → %87,7 satır,
%75,4 → %77,7 dal; `Selcuk.Istemci.Ortak` %87,7 → %90,9 / %73,5 → %79,4; `Selcuk.Demo.Obis` %64,0 → %93,5 / %55,9 → %80,9;
`Selcuk.Demo.Yemekhane` %75,9 → %94,5 / %68,0 → %85,0; `Selcuk.Demo.Api` %95,8 / %62,5 (aynı); `Selcuk.Olcum` %76,9 / %57,7 (aynı).
%60'ın altında kalan tek değer `Selcuk.Olcum` dal kapsamıdır (%57,7).

Tek Kapı (Node.js) için kod kapsamı ölçülmedi: depoda bir kapsam aracı yok ve yeni araç kurulmadı.

## Nasıl yeniden üretilir

```powershell
dotnet build SelcukSSO.sln
foreach ($p in Get-ChildItem tests -Directory | Where-Object { Test-Path (Join-Path $_.FullName "*.csproj") }) { dotnet test $p.FullName --no-build --list-tests }
dotnet test SelcukSSO.sln --no-build --collect:"XPlat Code Coverage" --results-directory TestResults\kapsam
python scripts\kapsam_birlestir.py TestResults\kapsam
cd araclar\tek-kapi; npm test
```

`TestResults/` ve `coverage*.xml` `.gitignore`'dadır. Cobertura raporlarını birleştirmek için ReportGenerator gibi bir
araç kullanılmadı; yukarıdaki tablo raporların `<line>` öğeleri `scripts/kapsam_birlestir.py` ile (Python, standart
kütüphane) birleştirilerek hesaplandı: dosya yolları raporun `<source>` köküyle mutlak yola çevrildi, satırlar dosya +
satır numarasına göre birleştirildi, Migrations hariç tutuldu.
