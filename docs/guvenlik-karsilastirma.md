# Güvenlik karşılaştırması: üç yaklaşım

| | **Bugünkü durum** | **Tek Kapı (ESSO)** | **Kendi SSO sistemi** |
|---|---|---|---|
| Ne | Her sistem (OBİS, e-posta, UZEM, Uzaktan Sınav, Yemekhane) ayrı giriş formu ve kendi şifre kontrolüyle | Kullanıcının bilgisayarında çalışan yerel portal (`araclar/tek-kapi`): gerçek sistemlerin giriş formlarını şifreli yerel kasadan doldurur | Merkezi kimlik sunucusu (`src/Selcuk.Identity`, OpenID Connect); uygulamalar şifreyi hiç görmez |
| Kapsam | Gerçek sistemler | Gerçek sistemler, yalnızca o bilgisayardan | Bu depodaki demo uygulamalar (gerçek sistemler henüz bağlı değil) |

Hücrelerdeki "bugünkü durum" bilgisi kullanıcının gözlemine ve Tek Kapı'nın site tanımlarına (`araclar/tek-kapi/src/sites.js`)
dayanır; gerçek sistemlerin iç yapısı (sunucu tarafı oturum süreleri, kayıtlar) bilinmediği için **bilinmiyor** diye
işaretlendi. Ölçülmüş süreler yalnızca kendi SSO sistemi için var (`docs/olcumler/`); elle ve Tek Kapı ölçümleri
**henüz ölçülmedi**.

| Ölçüt | Bugünkü durum | Tek Kapı (ESSO) | Kendi SSO sistemi |
|---|---|---|---|
| **Şifrenin kaç sisteme gittiği** | Her sisteme kendi şifresi: 5 sistem → 5 ayrı doğrulama noktası; kullanıcı her girişte yazar. | Gerçek sistemlerin şifreleri yine 5 sisteme gider (Tek Kapı doldurur) ve ayrıca kullanıcının bilgisayarındaki şifreli kasada durur (6. konum). Kullanıcı yalnızca kasa ana şifresini (ve SSO modunda kimlik sunucusu şifresini) yazar. Kimlik sunucusu bu şifreleri görmez. | Tek şifre yalnızca kimlik sunucusuna gider; uygulamalar şifre değil token alır. Özet PBKDF2 (Identity), düz metin saklanmaz. |
| **Merkezi MFA** | Yok. Sisteme göre değişir (Tek Kapı site tanımlarında Yemekhane girişinde 2FA adımı, OBİS'te CAPTCHA var; diğerleri bilinmiyor). | Kısmi. Tek Kapı'ya girmek için SSO'da 2FA zorunlu (`mfaZorunlu`, `amr` denetimi) + kasa ana şifresi. Ama gerçek sistemlerin kendisi değişmez: şifreyi bilen, Tek Kapı'yı atlayıp doğrudan girebilir. | Var. TOTP (RFC 6238), tekrar oynatma koruması, kurtarma kodları özetli; rol bazlı zorunluluk (`IkiAdimZorunluRoller`) ve uygulama bazlı şart (`mfaZorunlu`, step-up); `amr` id_token'da. |
| **Tek çıkış** | Yok. Her sistemden ayrı çıkılır; çıkılmayan oturum o sistemin kendi süresine kadar açık kalır. | Yerel. "Hepsinden çık" yönetilen penceredeki site sekmelerini kapatır ve çerezlerini siler; sunucu tarafındaki oturumlar kendi süreleri dolana kadar açık kalabilir. SSO modunda kimlik sunucusundan gelen `logout_token` kasayı kilitler ve sekmeleri kapatır. | Var, sunucu tarafında. OIDC Back-Channel Logout 1.0: çıkışta o `sid`'deki her istemciye imzalı `logout_token`; istemci oturumu sonraki istekte reddeder; refresh token'lar iptal. İlk ölçümde (d) p50 38,9 ms (localhost). |
| **Merkezi yetkilendirme** | Yok; her sistem kendi rollerini ve izinlerini tutar. | Yok. Gerçek sistemlerin yetkileri değişmez; yalnızca Tek Kapı'yı kimin kullanabileceği (`izinliKullaniciAdlari`) merkezden belirlenir. | Var. Roller (`ogrenci`, `akademisyen`, `personel`) token'da; uygulama bazlı erişim izni (`izinliRoller`, `izinliKullaniciAdlari`, authorize + token uç noktasında); API için `aud` + kapsam. Yönetim paneli yok: ayarlar kod/yapılandırmada. |
| **Denetim kaydı** | Bilinmiyor; kullanıcının görebildiği ortak bir kayıt yok. | Yerel: girişlerin süresi ve sonucu kullanıcının bilgisayarında (`metrics.csv`, kimlik bilgisi yok); merkezi değil, gerçek sistemlerin kaydı değişmez. | Var, merkezi: giriş, 2FA, şifre, onay, token, çıkış, back-channel, istek sınırı, mutlak süre olayları (kim, ne zaman, hangi uygulama, IP); sır yazılmaz; 180 gün saklama; kullanıcı kendi son 20 olayını görür. Bütünlük koruması yok (bkz. tehdit modeli). |
| **Token tabanlı oturum** | Her sistemin kendi oturum çerezi (biçimi bilinmiyor). | Gerçek sistemlerde değişiklik yok (kendi çerezleri). Tek Kapı'nın kendi oturumu SSO modunda id_token ile kurulur. | Var. İmzalı JWT access token (15 dk, küçültülmüş), kayan refresh token (8 sa, tekrar kullanım tespiti, mutlak sınır 12 sa), revocation (RFC 7009), kaynak sunucusunda JWKS ile doğrulama. Durumsuz JWT iptalden sonra en çok 15 dk geçerli kalabilir. |
| **Şifre sıfırlamada oturum kapatma** | Sisteme göre; bilinmiyor. | Yok. Gerçek sistemde şifre değişirse kasadaki şifre elle güncellenir; Tek Kapı başka cihazlardaki oturumları kapatamaz. | Var. Sıfırlamada tüm oturumlar, değiştirmede işlemi yapan dışındakiler: güvenlik damgası, istemcilere Back-Channel Logout, yetkilendirme ve token iptali, bilgilendirme e-postası. |
| **Kurum desteği gereksinimi** | Yok (mevcut durum). | Yok. Kullanıcı kendi bilgisayarında, kendi hesabıyla çalıştırır; gerçek sistemlerde değişiklik gerekmez. Kişisel bir geçiş aracıdır. | Yüksek. Her sistemin OIDC istemcisi olarak bağlanması (kod değişikliği, istemci kaydı), kimlik sunucusunun kurumca işletilmesi (production sertifikaları, kullanıcı aktarımı, yedekleme) gerekir. |
| **Form değişikliğine dayanıklılık** | Etkilenmez (kullanıcı yeni forma uyum sağlar). | Düşük. Giriş formu, alan adı veya akış (ör. CAPTCHA, ara sayfa) değişirse site tanımı güncellenene kadar otomatik giriş bozulur; CAPTCHA her zaman kullanıcıyı ister. | Yüksek. Standart protokol (OIDC/OAuth 2.0); uygulamaların arayüzü değişse de giriş etkilenmez. Kimlik sunucusunun giriş sayfası tek yerde. |

## Özet

- **Tek Kapı** kullanıcı açısından tıklama ve şifre yazmayı azaltır, kurum desteği gerektirmez; ama güvenlik
  modelini değiştirmez: gerçek şifreler hâlâ her sisteme gider ve bir yerde (yerel kasada) saklanır, çıkış yereldir,
  yetkilendirme ve denetim merkezi değildir, form değişikliklerine kırılgandır. Bu yüzden geçiş dönemi aracıdır.
- **Kendi SSO sistemi** şifreyi tek noktada toplar ve token tabanlı oturum, merkezi MFA, sunucu tarafı tek çıkış,
  merkezi yetkilendirme ve denetim sağlar; bunun bedeli, her sistemin OIDC ile bağlanması için kurum desteğidir.
- Ölçülebilir karşılaştırma (süre, adım, şifre girme sayısı) için elle ve Tek Kapı ölçümleri
  `docs/olcumler/manuel-giris-protokolu.md` ile toplanacak; sonuçlar `docs/olcumler/karsilastirma.md`'de.
