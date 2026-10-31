# NEXT_TASKS

Kaynak: README > Yapılacaklar, tehdit modeli, mimari.md. Öncelikler koddan/belgelerden **önerilmiştir**;
İŞ ÖNCELİĞİ DOĞRULANMALI (tez teslim tarihi ve danışman beklentisi bilinmiyor).

| Görev | Öncelik (öneri) | Durum | Bağımlılık | Risk |
|---|---|---|---|---|
| Elle giriş ölçümleri, Tek Kapı ölçüm kayıtları, SUS anketi (şablonlar hazır) | P0 (tez için) | Bekliyor | Gerçek katılımcı/gerçek sistem erişimi; `docs/olcumler/manuel-giris-protokolu.md` | Ölçülmemiş sayı yazılmaz |
| Test kapsamını yeniden ölç (Adım 8'den beri değişti; otomasyon projeleri dahil değil) | P1 | Bekliyor | `dotnet test --collect:"XPlat Code Coverage"` | Belge sayıları eski |
| `dotnet build`/`test`/`npm test` güncel yeşil durumunu doğrula | P1 | Bekliyor | LocalDB, port boşluğu | Bu belge sistemi sonrası doğrulanmadı |
| Yönetim paneli (kullanıcı, rol, istemci, erişim izni, denetim görüntüleme) | P1 | Planlı | Rol modeli (`Selcuk.Roller`) hazır | R5; kapsam büyük |
| Production sertifika/anahtar yönetimi, gerçek `OpenIddict:Issuer` | P1 (production hedefleniyorsa) | Planlı | X.509 deposu/Key Vault | R8; şu an Dev dışı açılmaz |
| Otomasyon API'leri (Staj, İntibak, Mezuniyet, ÇAP-Yandal) | P2 | Planlı | `api-sozlesmeleri/`, `<ad>.oku/.yaz` kapsamları | Gerçek veri/KVKK |
| Planlı otomasyonlar 5303, 5305-5310 | P2 | Plan | `otomasyon-sablonu.md` kontrol listesi | – |
| Kalıcı denetim/e-posta kuyruğu (outbox), back-channel yeniden deneme | P2 | Açık (R2, R6) | DB şeması | Migration |
| Dağıtık önbellek (Redis/SQL) ve dağıtık hız sınırı | P2 | Açık (R3, R4, R10) | Çok örnek dağıtımı hedefi | Altyapı |
| CSRF testi (tokensiz POST=400) | P2 | Açık (R13) | – | Düşük |
| WebAuthn/passkey, rol bazlı 2FA zorunluluğu | P2 | Açık (R9) | Ürün kararı | Kullanılabilirlik |
| Introspection (RFC 7662) | P2 | Bilinçli ertelendi (R1) | – | Karmaşıklık |
| Tek Kapı'nın kaldırılması | BİLİNMİYOR / DOĞRULANMALI | `mimari.md`'de "planlanıyor" | Gerçek sistemlerin OIDC'ye geçmesi | Kararın sahibi belli değil |
