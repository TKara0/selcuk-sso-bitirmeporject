# CURRENT_STATE

Son güncelleme: 2026-10-31 (belge sistemi kuruldu; kod değişmedi). Son commit: `900d1c0` (İntibak, Mezuniyet,
ÇAP-Yandal otomasyonları). Çalışma ağacı temiz, dal `main`.

## Çalışanlar (kodda var ve testle kapsanıyor)

- OIDC sunucusu: authorize, token, userinfo, end-session, revocation, discovery/JWKS; code + PKCE S256; refresh.
- Tek giriş/tek çıkış (Back-Channel Logout), `sid` takibi.
- Token yaşam döngüsü: 15 dk access, 8 sa kayan refresh, 12 sa mutlak sınır, tekrar kullanım tespiti, budama.
- Şifre sıfırlama/değiştirme, kilit (5/15 dk), hız sınırı, 2FA (TOTP, kurtarma kodu, cihaz hatırlama, step-up),
  denetim kaydı + güvenlik geçmişi, uygulama bazlı erişim izni, rol kataloğu (çok rollü kullanıcı).
- Ortak tasarım sistemi; Demo OBİS, Demo Yemekhane, Demo API; dört bölüm otomasyonu (temsili ekran, Mock veri).
- Tek Kapı SSO modu (bağımsız mod 35 + SSO 14 kontrol).
- Otomatik ölçüm aracı ve ilk ölçüm (`docs/olcumler/sso-olcumler-2026-10-26-0037.*`).

## Çalışmayan / eksik

- Yönetim paneli yok. Production sertifika/anahtar yönetimi yok (Development/Testing dışında açılış bilerek durur).
- Otomasyon API'leri yok (`VeriKaynagi:Mod=Api` yalnızca istemci tarafı + sözleşme).
- Elle ölçümler, Tek Kapı ölçüm kayıtları ve SUS yanıtları "henüz ölçülmedi".
- Planlı otomasyonlar (5303, 5305-5310) yok (`mimari.md`).

## Doğrulama durumu

- `docs/test-kapsami.md` (2026-10-26 ölçümü, sonrasında güncellendi): .NET **425** test, Tek Kapı **49** kontrol;
  kod kapsamı satır %82,5 / dal %69,7 (Adım 8 ölçümü, sonradan yeniden ölçülmedi).
- Bu belge sistemi oluşturulurken `dotnet build` / `dotnet test` / `npm test` **çalıştırılmadı** (yalnızca belge
  değişikliği). Güncel yeşil durum: BİLİNMİYOR / DOĞRULANMALI.
- Çalışan süreç/port durumu (5001/5101/5102) bu oturumda kontrol edilmedi.

## Aşama ve son işler

Adım 1-8 (çekirdek → ölçüm), Köprü A-C (Tek Kapı), Tasarım D1/D2, rol modeli, otomasyon şablonu + Staj, ardından
İntibak/Mezuniyet/ÇAP-Yandal. Git geçmişi: `git log --oneline` (18 commit). Proje bitirme tezi için ölçüm ve
değerlendirme aşamasında; yeni özellik yerine ölçüm/yönetim paneli/production hazırlığı bekliyor.
