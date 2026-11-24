# KNOWN_ISSUES

Kaynak: [tehdit-modeli.md](tehdit-modeli.md) "Açık kalan riskler" (R1-R14; burada özet, ayrıntı orada); R15-R20 bu
belgede eklendi. Hepsi
bilinçli, belgelenmiş sınırlamalardır; hiçbirinin "denenmiş ve başarısız çözümü" depoda kayıtlı değil
(BİLİNMİYOR / DOĞRULANMALI). "Çözüm durumu": hepsi **açık**, yalnızca R13 dışında kabul edilmiş tasarım dengesi.

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
| R13 | CSRF için doğrudan test yok | Identity testleri | Antiforgery yanlışlıkla kapanırsa yakalanmaz | – | Tokensiz POST=400 testi |
| R14 | Yanıt süresi eşitliği testle doğrulanmadı | `SahteSifreDogrulayici` | Zamanlama farkı hesap varlığını sızdırabilir | Kod incelemesi | İstatistiksel ölçüm |
| R15 | ~~Demo OBİS ve Demo Yemekhane büyük token çerezi kullanıyor~~ **Kapandı:** iki demo da sunucu tarafı oturum bileti kullanıyor (`SunucuTarafliOturum`); demo başına 4 parçalı ≈12,4 KB çerez yerine tek 304 karakterlik çerez | `Selcuk.Demo.Obis`, `.Yemekhane` | – | Uçtan uca ölçüldü (2026-11-24, baskan.test, 11 uygulama): localhost Cookie başlığı 31.022 bayt → 6.740 bayt, 431 yok (`docs/CURRENT_STATE.md`) | – |
| R16 | `OturumBiletDeposu` varsayılan olarak bellek içi (otomasyonlar ve iki demo) | `Selcuk.Istemci.Ortak` | Uygulama yeniden başlayınca kullanıcı sessizce yeniden giriş yapar; çok örnekte biletler paylaşılmaz | Tek girişle sessiz yeniden giriş | Paylaşımlı `IDistributedCache` (Redis/SQL) |
| R17 | `RolDogrulayici` yalnızca seed verisinde çalışır | `Selcuk.Roller`, `BaslangicVerisi` | Elle/DB ile atanan rol kurallara uymayabilir | Roller yalnızca seed ile atanıyor | Rol atama yoluna (gelecekteki panel) doğrulayıcı |
| R18 | Rol başına kapsam yok | `KullaniciKapsami` | Bütün bölüm rolleri kullanıcının tek `bolum`/`fakulte` alanına bağlı; iki birimde görevli kişi modellenemez | – | Rol atamasına birim alanı ([DECISIONS](DECISIONS.md)) |
| R19 | Rol değişince Back-Channel Logout gönderilmiyor ve denetim kaydı yazılmıyor | Identity | Rolü alınan kullanıcının açık oturumları çerez/token süresince eski rolle kalır; değişiklik iz bırakmaz | Kısa access token, çerez süresi | Rol değişikliğinde oturum iptali + denetim olayı |
| R20 | ~~Güvenlik geçmişinde IP adresi (`::1`) Mono yazı tipinde aralıklı görünüyor~~ **Kapandı (kod):** IP hücresi gövde yazı tipi ve `tabular-nums` ile gösteriliyor (kimlik sunucusunun `site.css`'i, `.gecmis td.mono`). Mevcut D2 PNG'leri yeniden çekilmedi; toplu ekran görüntüsü yenilemesinde güncellenir | Identity (Güvenlik geçmişi) | Eski PNG'lerde aralıklı görünüm kalır | – | Toplu ekran görüntüsü yenilemesi |
| R21 | Otomasyon test projelerindeki `Gecersiz_veri_kaynagi_ayari_acilista_duruyor` testi, `dotnet test SelcukSSO.sln` ile projeler paralel koşarken rastgele düşüyor (`Assert.Throws<OptionsValidationException>` "exact match" hatası); tek proje veya `-m:1` ile geçiyor. Değişmemiş `fa06e55`'te de görüldü | `tests/Selcuk.Otomasyon.TestOrtak/OtomasyonTemelTestleri.cs` | Tam çözüm koşusunda sahte kırmızı | Projeyi tek başına veya `-m:1` ile koşmak | Hatanın gerçek istisna türünü yakalayıp kök nedeni bulmak |

Ek notlar:
- Kilit yalnızca yeni girişleri engeller (kilitli hesapta önceden açık oturum token almaya devam eder); DoS'a karşı
  bilinçli denge ([DECISIONS.md](DECISIONS.md)).
- Tek Kapı: toplantı kararıyla yeni işlere dahil edilmez ve sonra iptal edilecek ([DECISIONS](DECISIONS.md)); iptal
  tarihi belirlenmedi. O zamana kadar R11 geçerli.
- Test kapsamı tablosu Adım 8'den beri yeniden ölçülmedi; bölüm otomasyonları ve Otomasyon.Ortak kapsamda yok.
