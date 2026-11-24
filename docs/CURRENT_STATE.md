# CURRENT_STATE

Son güncelleme: 2026-11-24 (Release ölçümü, ekran görüntülerinin yenilenmesi, belge güncellemesi).
Son commit: bu belgeyi içeren "Release ölçümü, ekran görüntülerinin yenilenmesi ve belge güncellemesi" (öncesi `b7f4a93`);
hash için `git log -1`. Dal `main`.

## Çalışanlar (kodda var ve testle kapsanıyor)

- OIDC sunucusu: authorize, token, userinfo, end-session, revocation, discovery/JWKS; code + PKCE S256; refresh.
- Tek giriş/tek çıkış (Back-Channel Logout), `sid` takibi.
- Token yaşam döngüsü: 15 dk access, 8 sa kayan refresh, 12 sa mutlak sınır, tekrar kullanım tespiti, budama.
- Şifre sıfırlama/değiştirme, kilit (5/15 dk), hız sınırı, 2FA (TOTP, kurtarma kodu, cihaz hatırlama, step-up),
  denetim kaydı + güvenlik geçmişi, uygulama bazlı erişim izni, rol kataloğu (çok rollü kullanıcı).
- Ortak tasarım sistemi; Demo OBİS, Demo Yemekhane, Demo API; on bir bölüm otomasyonunun hepsi hazır (temsili ekran,
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

- Bu adımda çalıştırıldı ve geçti: `dotnet build` (0 uyarı), `dotnet test SelcukSSO.sln` (varsayılan paralel; 18 .NET test projesi, **786** test), Tek Kapı
  `npm test` (**49** kontrol: 35 + 14). Sayılar `docs/test-kapsami.md`'de.
- Kod kapsamı 2026-11-24'te 18 proje için yeniden ölçüldü: satır %91,7, dal %77,4 (`docs/test-kapsami.md`).

## Bu adımın değişiklikleri (R9, ölçüm, ekran görüntüleri ve belgeler)

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
