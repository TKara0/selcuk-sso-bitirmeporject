# Elle giriş ölçümü protokolü

Amaç: gerçek sistemlere (OBİS, e-posta, UZEM, Uzaktan Sınav, Yemekhane) üç yöntemle girişin süresini ve zahmetini
aynı koşullarda ölçmek ve otomatik SSO ölçümüyle (`sso-olcumler-*.md`) karşılaştırmak.

| Yöntem | Ne yapılır |
|---|---|
| `elle` | Tarayıcıda sistemin giriş sayfası açılır, kullanıcı adı ve şifre elle yazılır (bugünkü durum) |
| `Tek Kapı` | Tek Kapı portalında sistemin kartına tıklanır (kasa açık); Tek Kapı formu doldurur |
| `kendi SSO demo` | Kimlik sunucusunun Uygulamalarım sayfasından Demo OBİS veya Demo Yemekhane'ye girilir (yalnızca `OBİS` ve `Yemekhane` satırları; gerçek sistemler değil demo uygulamalar, test hesabıyla) |

## Gerçek şifre hiçbir yere yazılmaz

- **Gerçek şifreniz, kullanıcı adınız, öğrenci numaranız veya e-posta adresiniz bu CSV'ye, `not` sütununa, ekran
  görüntüsüne, ekran kaydına veya başka bir dosyaya yazılmaz.** Tuş vuruşu tahmini için şifrenin yalnızca
  **uzunluğunu** sayın, şifreyi değil.
- Ölçüm sırasında ekran kaydı almayın; kronometre ayrı bir cihazda veya uygulamada olsun.
- Doldurulmuş dosyayı commit etmeden önce açıp kimlik bilgisi olmadığını kontrol edin.
- `kendi SSO demo` için yalnızca test hesapları kullanılır (`@test.local`); gerçek hesap kullanılmaz.

## Dosya

`manuel-giris-sablonu.csv`'yi `manuel-giris.csv` (veya `manuel-giris-<tarih>.csv`) adıyla kopyalayıp doldurun;
şablonun kendisini doldurmayın. Ayırıcı virgül, ondalık ayırıcı nokta (`12.4`). Excel Türkçe ayarla `;` ayırıcı
ve virgüllü ondalık yazarsa analiz betiği bunu da okur. Her deneme bir satır.

| Sütun | Değer |
|---|---|
| `tarih` | `YYYY-AA-GG` |
| `sistem` | Tam olarak şunlardan biri: `OBİS`, `e-posta`, `UZEM`, `Uzaktan Sınav`, `Yemekhane` |
| `yontem` | Tam olarak şunlardan biri: `elle`, `Tek Kapı`, `kendi SSO demo` |
| `deneme_no` | 1, 2, 3, … (sistem + yöntem başına) |
| `sure_sn` | Kronometre süresi, saniye, bir ondalık (aşağıdaki başlangıç/bitiş kuralı) |
| `tik_sayisi` | Fare tıklaması ve dokunma sayısı (alana tıklama, düğme, bağlantı; kaydırma sayılmaz) |
| `tus_vurusu_tahmini` | Kullanıcı adı uzunluğu + şifre uzunluğu + CAPTCHA/2FA kodu uzunluğu + Tab ve Enter sayısı (otomatik tamamlama kullanıldıysa yazılmayan karakterler sayılmaz, `not`'a yazın) |
| `sifre_girildi_mi` | `evet` / `hayır`: o denemede sistemin şifresini siz yazdınız mı (Tek Kapı ve SSO'da kasa ana şifresi veya SSO şifresi bu denemede yazıldıysa `not`'a ayrıca yazın) |
| `captcha_var_mi` | `evet` / `hayır` |
| `hata_oldu_mu` | `evet` / `hayır`: yanlış yazım, yeniden deneme, hata sayfası. Hatalı denemeler süre ortalamasına girmez, sayılır |
| `not` | Kısa açıklama (ör. "2FA kodu istendi", "otomatik doldurma kullanıldı"). Kimlik bilgisi yok |

## Kronometre ne zaman başlar ve biter

- **Başlar:** sistemi açmak için yapılan ilk eylemde: `elle` için yer imine / bağlantıya tıklama veya adres
  çubuğunda Enter; `Tek Kapı` için portaldaki karta tıklama; `kendi SSO demo` için Uygulamalarım'daki karta tıklama.
- **Biter:** giriş sonrası ilk sayfanın içeriği göründüğünde (OBİS ana sayfası, e-postada gelen kutusu, UZEM /
  Uzaktan Sınav'da Panom, Yemekhane ana sayfası; demo uygulamada Notlarım / Rezervasyonlarım). Yükleme göstergesi
  dönerken durdurmayın.
- CAPTCHA, 2FA kodu ve ara sayfalar süreye dahildir (bunlar `captcha_var_mi` ve `not` ile işaretlenir).

## Koşullar

- **En az 5 deneme** her sistem + yöntem çifti için. Denemeler arasında en az birkaç saniye bekleyin.
- **Aynı ağ:** tüm denemeler aynı ağda (ör. ev Wi-Fi'ı veya kampüs ağı); ağ değişirse yeni bir ölçüm dosyası
  başlatın ve `not`'a yazın. VPN kullanılıyorsa hepsi VPN ile.
- **Aynı cihaz ve tarayıcı;** tarayıcı sürümünü ilk satırın `not`'una yazın.
- **Tarayıcı ön belleği ve oturum durumu ("soğuk giriş"):** her denemeden önce o sistemden çıkış yapın ve oturum
  çerezlerini silin. Önerilen: her deneme için yeni bir **gizli pencere** (çerez ve ön bellek boş başlar). Tüm
  yöntemlerde aynı durum kullanılmalı; `Tek Kapı` kendi tarayıcı profilini kullandığı için her denemeden önce
  "Hepsinden çık" yapılır. Ön bellek dolu ("sıcak") ölçmek isterseniz bunu ayrı bir dosyada yapın ve `not`'a
  `sicak` yazın.
- Tarayıcının şifre yöneticisi / otomatik doldurma `elle` yönteminde **kapalı** olsun (ya da kullanıldığı `not`'ta
  belirtilsin); aksi hâlde `elle` ile `Tek Kapı` karşılaştırması bozulur.
- Tek Kapı'nın kasa ana şifresini yazma süresi karta tıklama süresine dahil değildir (kasa ölçüm öncesi açılır).

## Analiz

```powershell
python scripts/olcum_analiz.py
```

`karsilastirma.md`'de sistem ve yöntem bazında ortalama/medyan süre, ortalama tık ve tuş vuruşu, şifre girilen
deneme sayısı; matplotlib kuruluysa yöntemlere göre giriş süresi kutu grafiği. Ölçülmemiş çiftler "henüz
ölçülmedi" olarak listelenir.
