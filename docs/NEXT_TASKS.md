# NEXT_TASKS

Kaynak: README > Yapılacaklar, tehdit modeli, mimari.md. Öncelikler koddan/belgelerden **önerilmiştir**;
İŞ ÖNCELİĞİ DOĞRULANMALI (tez teslim tarihi ve danışman beklentisi bilinmiyor).

| Görev | Öncelik (öneri) | Durum | Bağımlılık | Risk |
|---|---|---|---|---|
| Elle giriş ölçümleri (`manuel-giris-protokolu.md`), Tek Kapı ölçüm kayıtları ve SUS anketi — **kullanıcı yapacak** (şablonlar hazır; henüz ölçülmedi) | P0 (tez için) | Bekliyor | Gerçek katılımcı/gerçek sistem erişimi | Ölçülmemiş sayı yazılmaz |
| Otomasyon API'si örneği (en az bir otomasyon için kaynak sunucusu; `VeriKaynagi:Mod=Api`) | P2 (isteğe bağlı) | Planlı | `api-sozlesmeleri/`, `<ad>.oku/.yaz` kapsamları | Gerçek veri/KVKK |
| Tek Kapı'nın kaldırılması | Karar verildi ([DECISIONS](DECISIONS.md)): yeni işlere dahil edilmez, sonra iptal edilecek | **Kullanıcı kararı bekliyor** (tarih belirlenmedi) | Toplantı kararı | Kaldırılana kadar mevcut testleri (npm test 35 + 14) geçmeli |
| Production hazırlığı (sertifika/anahtar yönetimi, gerçek `OpenIddict:Issuer`, dağıtık önbellek) | Öneri; mevcut kapsam dışı | Planlı | X.509 deposu/Key Vault; Redis/SQL | R3, R4, R8, R10; şu an Dev dışı açılmaz |
| Test kapsamını yeniden ölç | P1 | Tamamlandı (2026-11-25, R12: satır %92,1, dal %78,0; `test-kapsami.md`) | – | – |
| Performans ölçümü Release derlemesiyle | P1 | Tamamlandı (2026-11-24; `olcumler/derleme-karsilastirmasi-2026-11-24.md`) | – | Tek kullanıcılı süreler ortama duyarlı (KNOWN_ISSUES) |
| Ekran görüntülerini ve tez görsellerini yenile | P1 | Tamamlandı (2026-11-25, R12; 96 ekran, 276 d2 PNG, tez 01-44) | `scripts/ekran-goruntuleri.mjs` | – |
| `dotnet build`/`test`/`npm test` güncel yeşil durumunu doğrula | P1 | Yapıldı (R13: 978 test, 35 + 14) | – | Her adımda tekrarlanır |
| Yönetim paneli (kullanıcı, rol, istemci, erişim izni, denetim görüntüleme) | P1 | Planlı | Rol modeli (`Selcuk.Roller`) hazır | R5; kapsam büyük |
| Otomasyon API'leri (11 otomasyonun hepsi: Staj, İntibak, Etik Kurul, Mezuniyet, Bölüm Kurul Karar, Öğrenci Danışmanlık, İŞKUR, MÜDEK, Lisans Projeleri, Sınav Başarısı Analizi, ÇAP-Yandal) | P2 | Planlı | `api-sozlesmeleri/`, `<ad>.oku/.yaz` kapsamları | Gerçek veri/KVKK |
| Planlı otomasyonlar: Öğrenci Danışmanlık 5306, İŞKUR 5307, MÜDEK 5308, Sınav Başarısı Analizi 5310 | P2 | Tamamlandı (temsili ekran, Mock veri; 11 otomasyonun hepsi hazır) | `otomasyon-sablonu.md` kontrol listesi | – |
| Kalıcı denetim/e-posta kuyruğu (outbox), back-channel yeniden deneme | P2 | Açık (R2, R6) | DB şeması | Migration |
| CSRF testi (tokensiz POST=400) | P2 | Tamamlandı (R13 kapandı; `CsrfTests`) | `docs/tehdit-modeli.md` T2 | – |
| Rol değişince oturum kapatma + denetim | P2 | Tamamlandı (R19 kapandı; `RolDegisikligiIslemi`, yalnızca servis, arayüz yok) | `docs/tehdit-modeli.md` E7 | Yönetim paneli geldiğinde bu servisi çağırmalı |
| WebAuthn/passkey, rol bazlı 2FA zorunluluğu | P2 | Açık (R9) | Ürün kararı | Kullanılabilirlik |
| Introspection (RFC 7662) | P2 | Bilinçli ertelendi (R1) | – | Karmaşıklık |
