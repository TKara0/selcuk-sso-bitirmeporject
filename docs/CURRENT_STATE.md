# CURRENT_STATE

Son güncelleme: 2026-11-25 (R12, son arayüz düzeltmeleri, güncel kod kapsamı ve ekran görüntülerinin yenilenmesi).
Son commit: bu belgeyi içeren "Son arayüz düzeltmeleri, güncel kod kapsamı ve ekran görüntülerinin yenilenmesi" (öncesi `62636b6`);
hash için `git log -1`. Dal `main`.

## Çalışanlar (kodda var ve testle kapsanıyor)

- OIDC sunucusu: authorize, token, userinfo, end-session, revocation, discovery/JWKS; code + PKCE S256; refresh.
- Tek giriş/tek çıkış (Back-Channel Logout), `sid` takibi.
- Token yaşam döngüsü: 15 dk access, 8 sa kayan refresh, 12 sa mutlak sınır, tekrar kullanım tespiti, budama.
- Şifre sıfırlama/değiştirme, kilit (5/15 dk), hız sınırı, 2FA (TOTP, kurtarma kodu, cihaz hatırlama, step-up),
  denetim kaydı + güvenlik geçmişi, uygulama bazlı erişim izni, rol kataloğu (çok rollü kullanıcı).
- Tek komutla başlatma: `python start.py` (ön kontrol, `run-all.ps1 -ArkaPlan`, https bekleme, Ctrl+C / `--durdur`;
  `--kurulum` eksik user-secrets'ı üretir). Bkz. README > Hızlı başlangıç.
- Ortak tasarım sistemi; Demo OBİS (gerçek bir öğrenci bilgi sisteminin sayfa düzenine benzeyen temsili arayüz: mavi şerit,
  sekme çubuğu, 10 öğrenci ve 2 akademisyen sekmesi), Demo Yemekhane, Demo API; on bir bölüm otomasyonunun hepsi hazır (temsili ekran,
  Mock veri): İntibak 5301, Staj 5302, Etik Kurul 5303, Mezuniyet 5304, Bölüm Kurul Karar 5305, Öğrenci Danışmanlık
  5306, İŞKUR 5307, MÜDEK 5308, Lisans Projeleri 5309, Sınav Başarısı Analizi 5310, ÇAP-Yandal 5311.
- Tek Kapı SSO modu (bağımsız mod 35 + SSO 14 kontrol).
- Otomatik ölçüm aracı ve ölçümler: ilk ölçüm (`docs/olcumler/sso-olcumler-2026-10-26-0037.*`) ve 2026-11-24 Release/Debug
  ölçümleri (`sso-olcumler-2026-11-24-*`, `derleme-karsilastirmasi-2026-11-24.md`).

## Çalışmayan / eksik

- Yönetim paneli yok. Production sertifika/anahtar yönetimi yok (Development/Testing dışında açılış bilerek durur).
- Otomasyon API'leri yok (`VeriKaynagi:Mod=Api` yalnızca istemci tarafı + sözleşme).
- Elle ölçümler, Tek Kapı ölçüm kayıtları ve SUS yanıtları "henüz ölçülmedi".
- Eksik otomasyon yok: planlı 11 otomasyonun hepsi hazır (temsili ekran, Mock veri; `mimari.md`).

## Doğrulama durumu

- Bu adımda çalıştırıldı ve geçti: `dotnet build` (0 uyarı), `dotnet test SelcukSSO.sln` (varsayılan paralel; 18 .NET test projesi, **970** test), Tek Kapı
  `npm test` (**49** kontrol: 35 + 14). Sayılar `docs/test-kapsami.md`'de.
- Kod kapsamı 2026-11-25'te (R12) 18 test projesiyle yeniden ölçüldü: satır %92,1 (10301 / 11180), dal %78,0 (2962 / 3797);
  tablo 20 kaynak projeyi (Demo OBİS dahil) kapsar, 2026-11-24 değerleri (%91,7 / %77,4) `docs/test-kapsami.md`'de önceki ölçüm notu olarak durur.

## Bu adımın değişiklikleri (R12, son arayüz düzeltmeleri, kapsam ve ekran görüntüleri)

- **A1 (otomasyonlar):** 600 px altında üst çubuk tek satır: uygulama adı ellipsis (metin DOM'da tam), "Uygulamalarım"
  yalnızca ızgara simgesi (CSS ile çizilir; metin `font-size: 0` ile görsel olarak gizli ama ekran okuyucuya açık), hesap
  düğmesi sağda, 44x44 px hedefler. Yalnızca `otomasyon.css`; işaretleme değişmedi (mevcut testler `<span>Ad</span>` ve
  `<a class="uygulamalarim-baglantisi" …>Uygulamalarım</a>` biçimini sabitliyor). Geniş ekran aynı.
- **A2:** ana sayfadaki kısayol listesinde menü simgeleri (`MenuSimgeleri.Isaret`; `aria-hidden`, bağlantı metni ve adresi aynı).
- **A3 (kimlik sunucusu):** hesap düğmesinde görünen ad (ad soyad, gövde yazı tipi; boşsa kullanıcı adı); menüde görünen ad,
  altında kullanıcı adı (Mono, ikincil) ve kullanıcı türü. Açılır liste başlığı üst çubuktan açık renk miras alıyordu (beyaz
  zeminde okunmuyordu); mürekkep rengi verildi.
- **A4:** 11 otomasyon kartının açıklamasından "(temsili, örnek veri)" eki çıkarıldı; ilk otomasyon grubunun başlığının
  üstünde tek satırlık not ("Bölüm otomasyonları temsili ekranlardan oluşur ve örnek veri gösterir."), yalnızca otomasyon
  kartı gören kullanıcıda ve bir kez. Otomasyonların kendi "Temsili ekran" bandı değişmedi.
- **A5:** Demo OBİS "OB" (`obis`, #2A6AB0 / #E4EEF8 açık 4,72:1, #9CC7F2 / #16324D koyu 7,43:1) ve Demo Yemekhane "YM" (`nar`,
  #9C3D54 / #F7E4E9 açık 5,37:1, #E99AAE / #3B1E28 koyu 6,92:1) simge ve vurgu (istemci kaydı, `site.css`, `IndexModel.VurguRenkleri`).
- Kod kapsamı yeniden ölçüldü (yukarıda), bütün ekran görüntüleri ve tez görselleri yenilendi: 96 ekran, `docs/tasarim/d2` 276
  PNG, tez 01-44 (yeni: 42 Staj defterim, 43 aynı ekran 390 px, 44 Hesabım; d2'ye Hesabım, Staj defterim eklendi).
  Görüntülerde gerçek veri yok (kurgusal test numaraları, `@test.local`, Tek Kapı'da sahte bilgiler).
- Testler: 928 → 970 (+42: Identity +10, Staj +2, 10 otomasyonun ortak tabanında +3 × 10); hiçbiri silinmedi. Tek istisna:
  `OtomasyonIstemcileriTests.Simge_kayittan_geliyor_…` testinin "simgesiz kayıt" kurulumu (Demo OBİS artık simge taşıdığı için
  simge/vurgu özelliği silinmiş kopyası kaydedilir; assert'ler aynı).

## Önceki adım (R11, fakülte sitesinden esinlenen yerleşim)

- Kimlik sunucusu sayfalarına ve bölüm otomasyonlarının ortak düzenine fakülte sitesinin sayfa yerleşiminden dört öğe
  eklendi (yeniden tasarım değil; renkler `selcuk.css` token'ları, yazı tipi aynı; gerekçe ve neden kopyalanmadığı
  [DECISIONS.md](DECISIONS.md)): koyu lacivert üst çubuk, h1 altında kısa kalın çizgi, gezinti yolu, ikonlu sol menü
  (otomasyonlarda ayrıca beyaz yuvarlatılmış gölgesiz kartlar). Değişen yerler: `Selcuk.Otomasyon.Ortak` (`otomasyon.css`,
  `_OtomasyonDuzen`, `_OtomasyonMenuListesi`, yeni `GeziniYolu`, `MenuSimgeleri`, `_GeziniYolu`, `_MenuSimgeleri`,
  `MenuOgesi.Simge`), 11 otomasyonun menü tanımları (yalnızca `Simge`), kimlik sunucusu (`site.css`, `_GeziniYolu`, Hesabım,
  Şifre değiştir, Güvenlik geçmişi ve İki aşamalı doğrulama ayarları sayfaları). `_SelcukDuzen`, `selcuk.css`,
  `Selcuk.Arayuz/wwwroot`, Demo OBİS, Demo Yemekhane, Demo API ve Tek Kapı değişmedi.
- Gezinti yolu: otomasyonlarda "Uygulamalarım / Otomasyon adı / Ekran adı" (ilk bağlantı Authority kökü); kimlik sunucusunda
  yalnızca Hesabım ve alt sayfaları; giriş, girişin ikinci adımı (`Account/IkiAdim`), onay, çıkış ve hata sayfalarında yok.
- `start.py` tutarlılık testi (`StartPyTutarlilikTests`): start.py'deki test kullanıcıları, roller ve kullanıcı-uygulama
  eşlemesi koddakiyle karşılaştırıldı; **uyuşmazlık çıkmadı**, start.py değişmedi.
- `scripts/ekran-goruntuleri.mjs`: `--cikti <klasör>` (docs dışına yazar, docs PNG'lerine dokunmaz) ve `--ekranlar <ad,...>`
  eklendi; yeni `hesabim` ekranı. docs/ altındaki PNG'ler R11'de yenilenmedi (R12'de toplu yenilendi); önizleme
  `TestResults/onizleme/` (`.gitignore`'da).
- Testler: 818 → 928 (+110: Identity +7, Otomasyon.Ortak +17, Staj +6, 10 otomasyonun ortak tabanında +8 × 10); hiçbiri
  silinmedi, hiçbiri zayıflatılmadı ve değiştirilmedi.

## Önceki adım (R10, Demo OBİS arayüzü ve start.py)

- Demo OBİS arayüzü yeniden tasarlandı (yalnızca `src/Selcuk.Demo.Obis` ve testleri): mavi üst şerit ("Demo OBİS · Öğrenci
  Bilgi Sistemi", "Test" rozeti, hesap menüsünde yuvarlak baş harf görseli), gri sekme çubuğu (kare amblem alanında projenin
  kendi kapı işareti), sabit "Demo ekran" bandı, açık mavi sayfa başlığı şeridi, renkli bilgi kutuları. Yeni öğrenci
  ekranları: Onay Takip, Son Yıl Notları, Transkript, Harç Durumu, Sınav Tarihleri, Uzaktan Eğitim, Uzaktan Sınav, YKK
  Kararı (Not Durumu = mevcut `/Notlarim`, adres değişmedi, Demo API'den). Akademisyen: Anasayfa + Ders Yönetimi. Token
  İnceleme hesap menüsünde. Temsili düğmeler tek `/TemsiliIslem` POST ucuna (antiforgery zorunlu, veri değişmez). Örnek veri
  (`Data/OrnekOgrenciVerisi.cs`) uydurma ve 203011001-003 için ayrı ayrı. Gerçek kurum logosu, duyuru metni, adresi ve giriş
  formu yok; simgeler yerel SVG; CSP, `selcuk.css` ve `Selcuk.Arayuz/wwwroot` değişmedi. Mavi tonlar ve kontrast oranları
  `site.css` başlığında (açık ve koyu tema).
- `start.py` (depo kökü): ön kontrol (Python, .NET 10, LocalDB + veritabanı, sertifika, user-secrets anahtar adları,
  portlar), `run-all.ps1 -ArkaPlan` ile başlatma, her portun https cevabını bekleme, durum tablosu, test kullanıcıları
  (şifresiz), tarayıcı, Ctrl+C / `--durdur` ile kapatma ve doğrulama; `--kurulum [--deneme]` eksik user-secrets'ı üretir
  (var olana dokunmaz, değer yazmaz). Tek Kapı başlatılmaz.
- Testler: Demo OBİS 31 → 63 (+32); toplam 786 → 818; hiçbiri silinmedi, hiçbiri zayıflatılmadı.
  `CsrfTests.Senaryolar` listesine yeni POST işleyicisi (`TemsiliIslem`) eklendi (test, yeni işleyici için senaryo ister);
  `ObisFactory` sahte kimliğe kullanıcı adı, ad ve e-posta başlıkları aldı.
- `scripts/ekran-goruntuleri.mjs`: OBİS grubuna Transkript ve dar ekran çekimleri eklendi; yalnızca OBİS grubu çekildi
  (`--sadece obis`: 13 ekran, 6 tez görseli); OBİS PNG'leri aynı adlarla yenilendi, tez 39-41 eklendi (tez görseli toplamı 41).

## Önceki adım (R9, ölçüm, ekran görüntüleri ve belgeler)

- Performans ölçümü yayın (Release) derlemesiyle yenilendi: `scripts/olcum-calistir.ps1 -Yapilandirma Release`
  (`run-all.ps1` da `-Yapilandirma` alır). İki Release turu, bir Debug turu ve 26 Ekim kodunun (`07121a6`) bugünkü
  makinede Debug turu alındı; hepsi 100 deneme, hata yok. Sonuç: Release ile Debug arasında ölçülebilir fark yok (ilk giriş
  p50 ≈ 204-211 ms); verim 25 eşzamanlı kullanıcıda doyuyor (≈49-53 giriş/sn). 26 Ekim'deki 123 ms'lik tek kullanıcılı
  süre yeniden elde edilmedi: aynı kod bugün 207 ms verdi, fark kod veya derleme türünden değil ortamdandır (nedeni
  ölçülmedi). Tablo: `docs/olcumler/derleme-karsilastirmasi-2026-11-24.md` (`scripts/olcum_derleme_karsilastir.py`);
  `karsilastirma.md` ve grafikleri en yeni tura göre yeniden üretildi. Eski 26 Ekim dosyaları değişmedi.
- `tools/Selcuk.Olcum/Rapor.cs`: ölçüm raporundaki "Debug derleme" ifadesi sabitti, aracın derleme türünü yazıyor.
- Ekran görüntüleri toplu yenilendi: 91 ekran, 266 PNG (`docs/tasarim/d2`), 38 tez görseli. Yeni: Uygulamalarım gruplu
  (baskan.test) ve öğrenci gözüyle, otomasyonda ve Demo OBİS'te "Uygulamalarım" dönüş bağlantısı (tez 35-38). `scripts/
  ekran-goruntuleri.mjs` düzeltmeleri: giriş sayfası istek sınırına takılmamak için giriş hız ayarı, İŞKUR öğrencisi
  2FA'sız 203011002, onay zaten verilmişse geçen Staj başkan akışı. Güvenlik geçmişinde IP (`::1`) artık aralıksız.
  `docs/tez-gorselleri/README.md` eklendi.
- Belgeler son duruma göre düzeltildi (test sayısı, kapsam, ölçüm, NEXT_TASKS, KNOWN_ISSUES, mimari, roller).

## Önceki adım (R8, güvenlik ve testler)

- R21 (kararsız test) kapandı: kök neden `WebApplicationFactory`'nin `DeferredHost.StartAsync`'inin, `app.Run()` ile
  başlarken `ValidateOnStart` hatası yüzünden hemen dispose edilen host'a erişmesiydi (`ObjectDisposedException`).
  Otomasyon ayar doğrulaması `UseSelcukOtomasyon` içinde, host başlamadan fırlıyor (üretimde de aynı Türkçe hatayla
  açılış durur). `OturumBiletDeposu` ad alanı `Selcuk.Istemci.Ortak` oldu (testlere yalnızca `using` eklendi).
- CSRF (R13 kapandı): `CsrfTests` (Identity 15, Demo OBİS 2, Demo Yemekhane 4); POST işleyicisi listesi çalışan
  uygulamadan toplanır, antiforgery'siz 400 ve yan etkisizlik, token'la aynı istek 400 almaz; protokol uçları antiforgery
  beklemez.
- Rol değişikliği (R19 kapandı): `RolDegisikligiIslemi` (servis; arayüz/uç nokta yok), `BaslangicVerisi` çağırır; rol
  kümesi değişince güvenlik damgası + Back-Channel Logout + token iptali + `RolDegisti` denetim kaydı; değişmiyorsa hiçbir
  şey. `RolDegisikligiTests` (6).
- Kod kapsamı yeniden ölçüldü (11 otomasyon, Otomasyon.Ortak, Roller, Arayuz dahil): satır %91,7, dal %77,4
  (`docs/test-kapsami.md`); `scripts/kapsam_birlestir.py` eklendi.
- Testler: +27 (Identity 21, Demo OBİS 2, Demo Yemekhane 4), hiçbiri silinmedi.

## Önceki adım (R7, kullanılabilirlik)

- Uygulamalarım: kartlar istemci `grup` özelliğine göre h2 başlıklı gruplarda (Kampüs uygulamaları, Öğrenci işleri,
  Kurullar ve komisyonlar, Kalite ve analiz; grupsuz "Diğer"); kartı olmayan grup gösterilmez. Tek Kapı kaydı değişmedi.
- Demo OBİS ve Demo Yemekhane: üst çubukta "Uygulamalarım" bağlantısı (`KimlikSunucusuBilgisi`, otomasyonlarla ortak
  türetme) ve sunucu tarafı oturum (`OturumBiletDeposu` `Selcuk.Istemci.Ortak`'a taşındı, `AddSelcukSSO` seçeneği
  `SunucuTarafliOturum`, varsayılan kapalı; şablon ve iki demo açar). KNOWN_ISSUES R15 kapandı.
- Güvenlik geçmişi: IP adresi gövde yazı tipiyle ve tabular rakamla (R20 kodda kapandı; PNG'ler R9'da yenilendi).
- Uçtan uca ölçüm (run-all.ps1 -ArkaPlan, geçici betik, baskan.test, 11 uygulama sırayla): localhost Cookie başlığı
  31.022 bayt (demolar 4 parçalı çerez) → 6.740 bayt (demo başına tek 304 karakterlik çerez); hiçbir yanıtta 431 yok;
  öğrenciyle Demo OBİS Notlarım (Demo API çağrısı) ve Token İnceleme çalıştı. Token yenileme (15 dk) uçtan uca beklenmedi;
  yenileme testleri Istemci.Ortak.Tests'tedir.
- Testler: +24 (Identity 5, Istemci.Ortak 10, Demo OBİS 5, Demo Yemekhane 4), hiçbiri silinmedi.

## Önceki adım (kullanılabilirlik)

- Otomasyonların ortak düzeni (`_OtomasyonDuzen`): giriş yapmış kullanıcının üst çubuğunda hesap menüsünün solunda
  "Uygulamalarım" bağlantısı (kimlik sunucusunun kök adresi; `Kimlik:Authority`'den `OtomasyonBilgisi.KimlikSunucusuKoku`
  ile, yalnızca https veya `http://127.0.0.1`; aynı sekme; betik gerekmez). Stil `otomasyon.css`'te. Ekran görüntüleri
  (üst çubuk değişti) R9'da yenilendi.
- `scripts/run-all.ps1`: `-Sadece <adlar>` (seçili uygulamalar; kimlik sunucusu her zaman), `-ArkaPlan` (gizli pencere,
  `logs/<uygulama>.log`, PID kaydı `logs/run-all.pids`); `scripts/durdur-hepsi.ps1` yalnızca kayıttaki PID'leri ve alt
  işlemlerini durdurur. Varsayılan davranış ve `-TekKapi` değişmedi. Betik kodlaması UTF-8 BOM'lu (diğer betiklerle aynı).
- Testler: +43 (Otomasyon.Ortak 12, Staj 1, ortak `OtomasyonTemelTestleri` 3 × 10 otomasyon), hiçbiri silinmedi.

## Aşama ve son işler

Adım 1-8 (çekirdek → ölçüm), Köprü A-C (Tek Kapı), Tasarım D1/D2, rol modeli, otomasyon şablonu + Staj, ardından
İntibak/Mezuniyet/ÇAP-Yandal, belge sistemi, ardından Etik Kurul/Bölüm Kurul Karar/Lisans Projeleri, ardından Öğrenci
Danışmanlık/İŞKUR/MÜDEK/Sınav Başarısı Analizi (11 otomasyonun sonuncuları). Git geçmişi:
`git log --oneline`. Proje bitirme tezi için ölçüm ve
değerlendirme aşamasında; yeni özellik yerine ölçüm/yönetim paneli/production hazırlığı bekliyor.
