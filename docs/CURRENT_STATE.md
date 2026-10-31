# CURRENT_STATE

Son güncelleme: 2026-11-01 (otomasyonlara "Uygulamalarım" bağlantısı; `run-all.ps1` için `-Sadece`, `-ArkaPlan` ve
`durdur-hepsi.ps1`). Son commit: bu belgeyi içeren "Otomasyonlara Uygulamalarım bağlantısı; run-all için -Sadece,
-ArkaPlan ve durdurma betiği" (öncesi `bfee031`); hash için `git log -1`. Dal `main`.

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

- Bu adımda çalıştırıldı ve geçti: `dotnet build` (0 uyarı), `dotnet test` (18 .NET test projesi, **735** test), Tek Kapı
  `npm test` (**49** kontrol: 35 + 14). Sayılar `docs/test-kapsami.md`'de.
- Kod kapsamı satır %82,5 / dal %69,7 Adım 8 ölçümüdür; sonradan yeniden ölçülmedi (otomasyon projeleri dahil değil).

## Bu adımın değişiklikleri (kullanılabilirlik)

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
