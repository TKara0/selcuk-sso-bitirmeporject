# CURRENT_STATE

Son güncelleme: 2026-10-31 (Etik Kurul, Bölüm Kurul Karar ve Lisans Projeleri eklendi). Son commit: bu belgeyi içeren
"Etik Kurul, Bölüm Kurul Karar ve Lisans Projeleri otomasyonları (temsili, mock veri)" (öncesi `be03552`); hash için
`git log -1`. Dal `main`.

## Çalışanlar (kodda var ve testle kapsanıyor)

- OIDC sunucusu: authorize, token, userinfo, end-session, revocation, discovery/JWKS; code + PKCE S256; refresh.
- Tek giriş/tek çıkış (Back-Channel Logout), `sid` takibi.
- Token yaşam döngüsü: 15 dk access, 8 sa kayan refresh, 12 sa mutlak sınır, tekrar kullanım tespiti, budama.
- Şifre sıfırlama/değiştirme, kilit (5/15 dk), hız sınırı, 2FA (TOTP, kurtarma kodu, cihaz hatırlama, step-up),
  denetim kaydı + güvenlik geçmişi, uygulama bazlı erişim izni, rol kataloğu (çok rollü kullanıcı).
- Ortak tasarım sistemi; Demo OBİS, Demo Yemekhane, Demo API; yedi bölüm otomasyonu hazır (temsili ekran, Mock veri):
  İntibak 5301, Staj 5302, Etik Kurul 5303, Mezuniyet 5304, Bölüm Kurul Karar 5305, Lisans Projeleri 5309, ÇAP-Yandal 5311.
- Tek Kapı SSO modu (bağımsız mod 35 + SSO 14 kontrol).
- Otomatik ölçüm aracı ve ilk ölçüm (`docs/olcumler/sso-olcumler-2026-10-26-0037.*`).

## Çalışmayan / eksik

- Yönetim paneli yok. Production sertifika/anahtar yönetimi yok (Development/Testing dışında açılış bilerek durur).
- Otomasyon API'leri yok (`VeriKaynagi:Mod=Api` yalnızca istemci tarafı + sözleşme).
- Elle ölçümler, Tek Kapı ölçüm kayıtları ve SUS yanıtları "henüz ölçülmedi".
- Kalan planlı otomasyonlar yok: Öğrenci Danışmanlık 5306, İŞKUR 5307, MÜDEK 5308, Sınav Başarısı Analizi 5310 (`mimari.md`).

## Doğrulama durumu

- Bu adımda çalıştırıldı ve geçti: `dotnet build` (0 uyarı), `dotnet test` (14 .NET test projesi, **543** test), Tek Kapı
  `npm test` (**49** kontrol: 35 + 14). Sayılar `docs/test-kapsami.md`'de.
- Kod kapsamı satır %82,5 / dal %69,7 Adım 8 ölçümüdür; sonradan yeniden ölçülmedi (otomasyon projeleri dahil değil).

## Aşama ve son işler

Adım 1-8 (çekirdek → ölçüm), Köprü A-C (Tek Kapı), Tasarım D1/D2, rol modeli, otomasyon şablonu + Staj, ardından
İntibak/Mezuniyet/ÇAP-Yandal, belge sistemi, ardından Etik Kurul/Bölüm Kurul Karar/Lisans Projeleri. Git geçmişi:
`git log --oneline`. Proje bitirme tezi için ölçüm ve
değerlendirme aşamasında; yeni özellik yerine ölçüm/yönetim paneli/production hazırlığı bekliyor.
