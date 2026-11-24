# KNOWN_ISSUES

Kaynak: [tehdit-modeli.md](tehdit-modeli.md) "Açık kalan riskler" (R1-R14; burada özet, ayrıntı orada); R15-R20 bu
belgede eklendi. Hepsi
bilinçli, belgelenmiş sınırlamalardır; hiçbirinin "denenmiş ve başarısız çözümü" depoda kayıtlı değil
(BİLİNMİYOR / DOĞRULANMALI). "Çözüm durumu": kapananlar üstü çizili; kalanlar kabul edilmiş tasarım dengesi.

| # | Problem | Modül | Belirti / etki | Workaround | Olası çözüm |
|---|---|---|---|---|---|
| R1 | Durumsuz JWT access token | Demo API, token | İptal sonrası ≤15 dk geçerli | Kısa ömür | Introspection (RFC 7662; bilinçli yapılmadı) |
| R2 | Denetim (10 000) ve e-posta (1 000) kuyrukları bellek içi | `Denetim/`, `Eposta/` | Çökmede kayıt/e-posta kaybı | – | DB outbox |
| R3 | `OturumIptalDeposu` ve jti kaydı bellek içi | `Istemci.Ortak` | Yeniden başlayınca iptal edilmiş çerez geçerli olabilir | Çerez süresi | Redis/SQL dağıtık önbellek |
| R4 | Hız sınırı ve tek uçuş yenileme örnek başına | `HizSiniri` | Çok örnekte gevşer | – | Ağ geçidi sınırı |
| R5 | Yönetim paneli yok | Identity | Hesap kapatma/kayıt inceleme elle | DB/yapılandırma | Panel ([NEXT_TASKS](NEXT_TASKS.md)) |
| R6 | Back-Channel Logout tekrar denenmez | `BackChannelLogoutGonderici` | Ulaşılamayan istemcide oturum çerez süresine kadar açık | Denetim kaydı | Kalıcı yeniden deneme |
| R7 | Denetim kaydı imzasız/zincirsiz | `DenetimKayitlari` | Tek başına güçlü kanıt değil | – | Eklenebilir depo/özet zinciri |
| R8 | Production anahtar yönetimi yok | `Program.cs` | Development/Testing dışında açılmaz | – | X.509, rotasyon |
| R9 | 2FA isteğe bağlı, TOTP kimlik avına açık | 2FA | Çoğu kullanıcı yalnız şifreli | `mfaZorunlu` (yalnız tek-kapi) | Rol zorunluluğu, WebAuthn |
| R10 | Kimlik sunucusu tek nokta | Identity | Yeni giriş/yenileme yapılamaz | Açık oturumlar çerezle sürer | Çok örnek + paylaşılan anahtar/önbellek |
| R11 | Tek Kapı yerel kasası | `araclar/tek-kapi` | Kasa açıkken yerel zararlı yazılım erişebilir | 15 dk kilit | Gerçek sistemlerin OIDC'ye geçmesi |
| R12 | Authorize/token/userinfo'da hız sınırı yok | Identity | Dağıtık denemede yalnız hesap kilidi | – | WAF/ağ geçidi |
| R13 | ~~CSRF için doğrudan test yok~~ **Kapandı (2026-11-24):** her Razor POST işleyicisi için antiforgery'siz 400 + yan etkisizlik testi; işleyici listesi koddan toplanır, yeni/unutulan işleyici testi düşürür (`docs/tehdit-modeli.md` T2) | Identity, Demo OBİS, Demo Yemekhane testleri | – | – | – |
| R14 | Yanıt süresi eşitliği testle doğrulanmadı | `SahteSifreDogrulayici` | Zamanlama farkı hesap varlığını sızdırabilir | Kod incelemesi | İstatistiksel ölçüm |
| R15 | ~~Demo OBİS ve Demo Yemekhane büyük token çerezi kullanıyor~~ **Kapandı:** iki demo da sunucu tarafı oturum bileti kullanıyor (`SunucuTarafliOturum`); demo başına 4 parçalı ≈12,4 KB çerez yerine tek 304 karakterlik çerez | `Selcuk.Demo.Obis`, `.Yemekhane` | – | Uçtan uca ölçüldü (2026-11-24, baskan.test, 11 uygulama): localhost Cookie başlığı 31.022 bayt → 6.740 bayt, 431 yok (`docs/CURRENT_STATE.md`) | – |
| R16 | `OturumBiletDeposu` varsayılan olarak bellek içi (otomasyonlar ve iki demo) | `Selcuk.Istemci.Ortak` | Uygulama yeniden başlayınca kullanıcı sessizce yeniden giriş yapar; çok örnekte biletler paylaşılmaz | Tek girişle sessiz yeniden giriş | Paylaşımlı `IDistributedCache` (Redis/SQL) |
| R17 | `RolDogrulayici` yalnızca seed verisinde çalışır | `Selcuk.Roller`, `BaslangicVerisi` | Elle/DB ile atanan rol kurallara uymayabilir | Roller yalnızca seed ile atanıyor | Rol atama yoluna (gelecekteki panel) doğrulayıcı |
| R18 | Rol başına kapsam yok | `KullaniciKapsami` | Bütün bölüm rolleri kullanıcının tek `bolum`/`fakulte` alanına bağlı; iki birimde görevli kişi modellenemez | – | Rol atamasına birim alanı ([DECISIONS](DECISIONS.md)) |
| R19 | ~~Rol değişince Back-Channel Logout gönderilmiyor ve denetim kaydı yazılmıyor~~ **Kapandı (2026-11-24):** `RolDegisikligiIslemi` rol kümesi değişince oturumları kapatır, Back-Channel Logout gönderir, token'ları iptal eder ve `RolDegisti` denetim kaydı yazar (başlangıç verisi çağırır; arayüz/uç nokta yok). Kalan sınır: verilmiş access token'ın rol claim'i en çok 15 dk eski kalır | Identity | – | – | – |
| R20 | ~~Güvenlik geçmişinde IP adresi (`::1`) Mono yazı tipinde aralıklı görünüyor~~ **Kapandı:** IP hücresi gövde yazı tipi ve `tabular-nums` ile gösteriliyor (kimlik sunucusunun `site.css`'i, `.gecmis td.mono`); D2 ekran görüntüleri 2026-11-24'te yenilendi ve düzeltme görüntüde doğrulandı | Identity (Güvenlik geçmişi) | – | – | – |
| R21 | ~~Otomasyon test projelerindeki `Gecersiz_veri_kaynagi_ayari_acilista_duruyor` testi paralel koşuda rastgele düşüyor~~ **Kapandı (2026-11-24):** kök neden `WebApplicationFactory`'nin `DeferredHost.StartAsync`'inin, giriş noktasında `app.Run()` başlarken `ValidateOnStart` hatası yüzünden hemen dispose edilen host'un servislerine erişmesiydi (yük altında `OptionsValidationException` yerine `ObjectDisposedException`). Otomasyon ayar doğrulaması (`VeriKaynagi` ve `Ayarlar<T>`) artık `UseSelcukOtomasyon` içinde, host başlamadan fırlar; üretimde de açılış aynı Türkçe iletiyle durur | `Selcuk.Otomasyon.Ortak` | – | – | – |

Ek notlar:
- Kilit yalnızca yeni girişleri engeller (kilitli hesapta önceden açık oturum token almaya devam eder); DoS'a karşı
  bilinçli denge ([DECISIONS.md](DECISIONS.md)).
- Tek Kapı: toplantı kararıyla yeni işlere dahil edilmez ve sonra iptal edilecek ([DECISIONS](DECISIONS.md)); iptal
  tarihi belirlenmedi. O zamana kadar R11 geçerli.
- Test kapsamı 2026-11-25'te (R12) 18 test projesiyle yeniden ölçüldü: satır %92,1, dal %78,0 (`docs/test-kapsami.md`); "yalnızca kendi test projesi" sütunu hesaplanmadı. `Selcuk.Olcum` dal kapsamı %57,7 (tek %60 altı değer).
- Performans ölçümünde tek kullanıcılı süreler ölçüm anındaki ortama duyarlı: 26 Ekim'deki ilk giriş p50 123 ms, aynı kod bugün aynı makinede 207 ms verdi; Release ile Debug arasında fark yok (`docs/olcumler/derleme-karsilastirmasi-2026-11-24.md`). Nedeni ölçülmedi; tezde aynı gün alınmış ölçümler karşılaştırılmalı.
