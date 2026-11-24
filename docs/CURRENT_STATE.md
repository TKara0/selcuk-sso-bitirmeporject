# CURRENT_STATE

Son güncelleme: 2026-11-24 (Uygulamalarım grupları, demolarda dönüş bağlantısı ve sunucu tarafı oturum). Son commit: bu
belgeyi içeren "Uygulamalarım grupları, demolarda dönüş bağlantısı ve sunucu tarafı oturum" (öncesi `fa06e55`); hash için
`git log -1`. Dal `main`.

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
- Otomatik ölçüm aracı ve ilk ölçüm (`docs/olcumler/sso-olcumler-2026-10-26-0037.*`).

## Çalışmayan / eksik

- Yönetim paneli yok. Production sertifika/anahtar yönetimi yok (Development/Testing dışında açılış bilerek durur).
- Otomasyon API'leri yok (`VeriKaynagi:Mod=Api` yalnızca istemci tarafı + sözleşme).
- Elle ölçümler, Tek Kapı ölçüm kayıtları ve SUS yanıtları "henüz ölçülmedi".
- Kalan planlı otomasyonlar yok: Öğrenci Danışmanlık 5306, İŞKUR 5307, MÜDEK 5308, Sınav Başarısı Analizi 5310 (`mimari.md`).

## Doğrulama durumu

- Bu adımda çalıştırıldı ve geçti: `dotnet build` (0 uyarı), `dotnet test SelcukSSO.sln -m:1` (18 .NET test projesi, **759** test; paralel koşuda R21), Tek Kapı
  `npm test` (**49** kontrol: 35 + 14). Sayılar `docs/test-kapsami.md`'de.
- Kod kapsamı satır %82,5 / dal %69,7 Adım 8 ölçümüdür; sonradan yeniden ölçülmedi (otomasyon projeleri dahil değil).

## Bu adımın değişiklikleri (R7, kullanılabilirlik)

- Uygulamalarım: kartlar istemci `grup` özelliğine göre h2 başlıklı gruplarda (Kampüs uygulamaları, Öğrenci işleri,
  Kurullar ve komisyonlar, Kalite ve analiz; grupsuz "Diğer"); kartı olmayan grup gösterilmez. Tek Kapı kaydı değişmedi.
- Demo OBİS ve Demo Yemekhane: üst çubukta "Uygulamalarım" bağlantısı (`KimlikSunucusuBilgisi`, otomasyonlarla ortak
  türetme) ve sunucu tarafı oturum (`OturumBiletDeposu` `Selcuk.Istemci.Ortak`'a taşındı, `AddSelcukSSO` seçeneği
  `SunucuTarafliOturum`, varsayılan kapalı; şablon ve iki demo açar). KNOWN_ISSUES R15 kapandı.
- Güvenlik geçmişi: IP adresi gövde yazı tipiyle ve tabular rakamla (R20 kodda kapandı; PNG'ler yenilenmedi).
- Uçtan uca ölçüm (run-all.ps1 -ArkaPlan, geçici betik, baskan.test, 11 uygulama sırayla): localhost Cookie başlığı
  31.022 bayt (demolar 4 parçalı çerez) → 6.740 bayt (demo başına tek 304 karakterlik çerez); hiçbir yanıtta 431 yok;
  öğrenciyle Demo OBİS Notlarım (Demo API çağrısı) ve Token İnceleme çalıştı. Token yenileme (15 dk) uçtan uca beklenmedi;
  yenileme testleri Istemci.Ortak.Tests'tedir.
- Testler: +24 (Identity 5, Istemci.Ortak 10, Demo OBİS 5, Demo Yemekhane 4), hiçbiri silinmedi.

## Önceki adım (kullanılabilirlik)

- Otomasyonların ortak düzeni (`_OtomasyonDuzen`): giriş yapmış kullanıcının üst çubuğunda hesap menüsünün solunda
  "Uygulamalarım" bağlantısı (kimlik sunucusunun kök adresi; `Kimlik:Authority`'den `OtomasyonBilgisi.KimlikSunucusuKoku`
  ile, yalnızca https veya `http://127.0.0.1`; aynı sekme; betik gerekmez). Stil `otomasyon.css`'te. Ekran görüntüleri
  (üst çubuk değişti) bu adımda yenilenmedi.
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
