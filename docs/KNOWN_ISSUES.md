# KNOWN_ISSUES

Kaynak: [tehdit-modeli.md](tehdit-modeli.md) "Açık kalan riskler" (R1-R14; burada özet, ayrıntı orada). Hepsi
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

Ek notlar:
- Kilit yalnızca yeni girişleri engeller (kilitli hesapta önceden açık oturum token almaya devam eder); DoS'a karşı
  bilinçli denge ([DECISIONS.md](DECISIONS.md)).
- `docs/mimari.md` Tek Kapı için "kaldırılması planlanıyor" diyor; `AGENTS.md`/README ile çelişmez ama kararın
  kesinliği BİLİNMİYOR / DOĞRULANMALI.
- Test kapsamı tablosu Adım 8'den beri yeniden ölçülmedi; İntibak/Mezuniyet/ÇAP-Yandal/Staj/Otomasyon.Ortak kapsamda yok.
