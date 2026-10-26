# Tasarım D1: ortak tasarım sistemi ve kimlik sunucusu ekranları

Yön: "Taç kapı". Konya'daki Selçuklu taç kapıları (Karatay Medresesi firuze çinileri, sekiz köşeli yıldız örgüsü).
Cesur öğe tek yerde: giriş ekranındaki sivri kemerli kapı çerçevesi ve içindeki ince yıldız örgüsü. Geri kalan her
şey sakin, düzenli, okunaklı. Kurumsal logo veya resmî görünüm kullanılmaz; "(Test)" ibaresi alt bilgide ve üst
çubukta kalır.

Kaynak: `src/Selcuk.Arayuz` (Razor Class Library, dosyalar `_content/Selcuk.Arayuz/` altında). Bu adımda yalnızca
kimlik sunucusu kullanır; demolar ve Tek Kapı D2'de.

## Token'lar

| Token | Açık | Koyu | Kullanım |
|---|---|---|---|
| `--murekkep` | #12263A | #E6EEF0 | Metin |
| `--firuze` | #0F7B7E | #5CC3C0 | Ana eylem, odak halkası |
| `--firuze-acik` | #D3EBE9 | #16393C | Seçili/üzerine gelinen satır, bilgi zemini, etiket |
| `--tas` | #EDF0EF | #0E1B27 | Sayfa zemini |
| `--yuzey` | #FFFFFF | #15273A | Form ve liste zemini |
| `--kiremit` | #A8362A | #E8857A | Hata, tehlikeli işlem |
| `--soluk` | #4A5C6A | #A9BAC2 | İkincil metin (türetilmiş) |
| `--baglanti` | #0C6B6E | #5CC3C0 | Firuze metin ve bağlantı (türetilmiş; açık temada `--firuze` taş ve firuze-açık zeminde 4,5:1'i geçmediği için bir ton koyu) |
| `--firuze-koyu` | #0A6164 | #80D3D0 | Ana düğme üzerine gelince |
| `--firuze-uzerinde` | #FFFFFF | #0E1B27 | Firuze zemin üzerindeki metin |
| `--kiremit-acik` | #F7E3E0 | #3A1F22 | Uyarı ve hata bandı zemini |
| `--kiremit-koyu` | #8E2C22 | #F0A49B | Tehlikeli düğme üzerine gelince |
| `--kiremit-uzerinde` | #FFFFFF | #0E1B27 | Kiremit zemin üzerindeki metin |
| `--cizgi` | #D3DADB | #2A3E51 | Ayırıcı çizgi (dekoratif) |
| `--kontrol-cizgi` | #75858F | #6F8597 | Form kontrolü kenarı (zemine karşı en az 3:1) |
| `--kapi-zemin` | #12263A | #0A1520 | Giriş ekranının kapı paneli |
| `--kapi-cizgi` | #5CC3C0 | #5CC3C0 | Kapı çerçevesi ve örgü çizgisi |
| `--kapi-metin` / `--kapi-soluk` | #E6EEF0 / #B9C9CF | aynı | Kapı panelindeki ad ve cümle |

Kapı paneli için ayrı token: `--murekkep` koyu temada açık renk (#E6EEF0) olduğu için paneli doğrudan ona bağlamak
koyu temada açık bir panel verirdi. Panel iki temada da mürekkep (koyu) kalır; koyu temada sayfa zemininden ayrılsın
diye bir ton daha koyu.

Tipografi: gövde 17px / 1.55, küçük metin 15px, başlıklar 22 / 28 / 36px, yalnızca 400 ve 700. Aralık 4'ün katları
(`--a1` … `--a16`). Köşe: kontroller 6px, büyük yüzeyler 12px. Gölge yok. Odak halkası her etkileşimli öğede
firuze, 2px, 2px boşluk. Kendiliğinden başlayan tek animasyon giriş ekranındaki örgünün bir kez çizilmesi
(stroke-dashoffset, 1,2 sn); `prefers-reduced-motion: reduce`'da hiç yok.

## Yazı tipi

| | Atkinson Hyperlegible Next | Atkinson Hyperlegible Mono |
|---|---|---|
| Kaynak | fontsource npm paketi `@fontsource/atkinson-hyperlegible-next` (Google Fonts kaynağı, `npm pack` ile indirildi, projeye bağımlılık eklenmedi) | `@fontsource/atkinson-hyperlegible-mono` |
| Paket sürümü | 5.3.0 (Google Fonts sürümü v7, 2025-09-05) | 5.3.0 (v8, 2025-09-16) |
| Lisans | SIL Open Font License 1.1 (`fonts/OFL-AtkinsonHyperlegibleNext.txt`) | SIL OFL 1.1 (`fonts/OFL-AtkinsonHyperlegibleMono.txt`) |
| Dosyalar | latin ve latin-ext, 400 ve 700 (4 woff2) | latin ve latin-ext, 400 (2 woff2) |
| Kullanım | Arayüz | Yalnızca kodlar, kurtarma kodları, anahtar, kullanıcı adı / öğrenci numarası, IP |

Türkçe karakterler (İ ı Ğ ğ Ş ş Ç ç Ö ö Ü ü) dosyaların cmap tablosundan doğrulandı: ı, Ç ç, Ö ö, Ü ü latin
dosyasında, İ, Ğ ğ, Ş ş latin-ext dosyasında; her biri `unicode-range`'in yönlendirdiği dosyada var (altı dosyanın
hepsinde). Harici CDN yok; CSP değişmedi (`font-src` ayrıca tanımlı değil, `default-src 'self'` yeterli).

## Kontrast (WCAG 2.x)

Metin için eşik 4,5:1 (AA), metin dışı öğeler (form kenarı, odak halkası) için 3:1. Değerler `selcuk.css`'teki
token'lardan WCAG göreli parlaklık formülüyle hesaplandı.

| Metin / öğe | Zemin | Kullanım | Açık tema | Koyu tema | Eşik | Sonuç |
|---|---|---|---|---|---|---|
| `--murekkep` | `--tas` | Gövde metni, sayfa zemini | 13.42:1 (#12263A / #EDF0EF) | 14.82:1 (#E6EEF0 / #0E1B27) | 4.5:1 | ✓ |
| `--murekkep` | `--yuzey` | Gövde metni, form/liste zemini | 15.40:1 (#12263A / #FFFFFF) | 12.91:1 (#E6EEF0 / #15273A) | 4.5:1 | ✓ |
| `--murekkep` | `--firuze-acik` | Bilgi bandı metni, seçili/üzerine gelinen satır | 12.34:1 (#12263A / #D3EBE9) | 10.60:1 (#E6EEF0 / #16393C) | 4.5:1 | ✓ |
| `--murekkep` | `--kiremit-acik` | Uyarı ve hata bandı metni | 12.48:1 (#12263A / #F7E3E0) | 12.76:1 (#E6EEF0 / #3A1F22) | 4.5:1 | ✓ |
| `--soluk` | `--tas` | İkincil metin, sayfa zemini; "Test" etiketi | 6.04:1 (#4A5C6A / #EDF0EF) | 8.71:1 (#A9BAC2 / #0E1B27) | 4.5:1 | ✓ |
| `--soluk` | `--yuzey` | İkincil metin (ipucu), form/liste zemini | 6.93:1 (#4A5C6A / #FFFFFF) | 7.58:1 (#A9BAC2 / #15273A) | 4.5:1 | ✓ |
| `--soluk` | `--firuze-acik` | İkincil metin, bilgi zemini | 5.55:1 (#4A5C6A / #D3EBE9) | 6.23:1 (#A9BAC2 / #16393C) | 4.5:1 | ✓ |
| `--baglanti` | `--tas` | Bağlantı, sayfa zemini | 5.48:1 (#0C6B6E / #EDF0EF) | 8.32:1 (#5CC3C0 / #0E1B27) | 4.5:1 | ✓ |
| `--baglanti` | `--yuzey` | Bağlantı, ikincil düğme metni | 6.28:1 (#0C6B6E / #FFFFFF) | 7.25:1 (#5CC3C0 / #15273A) | 4.5:1 | ✓ |
| `--baglanti` | `--firuze-acik` | Etiket ("2FA gerekli"), uygulama simgesi, ikincil düğme üzerine gelince | 5.03:1 (#0C6B6E / #D3EBE9) | 5.95:1 (#5CC3C0 / #16393C) | 4.5:1 | ✓ |
| `--firuze-uzerinde` | `--firuze` | Ana düğme metni | 5.05:1 (#FFFFFF / #0F7B7E) | 8.32:1 (#0E1B27 / #5CC3C0) | 4.5:1 | ✓ |
| `--firuze-uzerinde` | `--firuze-koyu` | Ana düğme metni (üzerine gelince) | 7.22:1 (#FFFFFF / #0A6164) | 10.08:1 (#0E1B27 / #80D3D0) | 4.5:1 | ✓ |
| `--kiremit` | `--yuzey` | Alan hatası metni | 6.51:1 (#A8362A / #FFFFFF) | 5.83:1 (#E8857A / #15273A) | 4.5:1 | ✓ |
| `--kiremit` | `--tas` | Hata metni, sayfa zemini | 5.68:1 (#A8362A / #EDF0EF) | 6.70:1 (#E8857A / #0E1B27) | 4.5:1 | ✓ |
| `--kiremit` | `--kiremit-acik` | "Başarısız" etiketi | 5.28:1 (#A8362A / #F7E3E0) | 5.77:1 (#E8857A / #3A1F22) | 4.5:1 | ✓ |
| `--kiremit-uzerinde` | `--kiremit` | Tehlikeli düğme metni | 6.51:1 (#FFFFFF / #A8362A) | 6.70:1 (#0E1B27 / #E8857A) | 4.5:1 | ✓ |
| `--kiremit-uzerinde` | `--kiremit-koyu` | Tehlikeli düğme metni (üzerine gelince) | 8.29:1 (#FFFFFF / #8E2C22) | 8.71:1 (#0E1B27 / #F0A49B) | 4.5:1 | ✓ |
| `--kapi-metin` | `--kapi-zemin` | Kapı paneli başlığı | 13.09:1 (#E6EEF0 / #12263A) | 15.65:1 (#E6EEF0 / #0A1520) | 4.5:1 | ✓ |
| `--kapi-soluk` | `--kapi-zemin` | Kapı paneli cümlesi | 9.03:1 (#B9C9CF / #12263A) | 10.80:1 (#B9C9CF / #0A1520) | 4.5:1 | ✓ |
| `--kontrol-cizgi` | `--yuzey` | Form kontrolü kenarı (metin dışı, 1.4.11) | 3.81:1 (#75858F / #FFFFFF) | 3.96:1 (#6F8597 / #15273A) | 3:1 | ✓ |
| `--kontrol-cizgi` | `--tas` | Form kontrolü kenarı, sayfa zemini (metin dışı) | 3.32:1 (#75858F / #EDF0EF) | 4.55:1 (#6F8597 / #0E1B27) | 3:1 | ✓ |
| `--firuze` | `--tas` | Odak halkası, sayfa zemini (metin dışı) | 4.41:1 (#0F7B7E / #EDF0EF) | 8.32:1 (#5CC3C0 / #0E1B27) | 3:1 | ✓ |
| `--firuze` | `--yuzey` | Odak halkası, form zemini (metin dışı) | 5.05:1 (#0F7B7E / #FFFFFF) | 7.25:1 (#5CC3C0 / #15273A) | 3:1 | ✓ |
| `--kapi-cizgi` | `--kapi-zemin` | Kapı örgüsü çizgisi (dekoratif, bilgi taşımaz) | 7.35:1 (#5CC3C0 / #12263A) | 8.78:1 (#5CC3C0 / #0A1520) | 3:1 | ✓ |

E-postanın HTML sürümü aynı açık tema değerlerini kullanır (başlık #E6EEF0 / #12263A 13,09:1, düğme #FFFFFF / #0F7B7E
5,05:1, bağlantı #0C6B6E / #FFFFFF 6,28:1, alt satır #4A5C6A / #FFFFFF 6,93:1).

## Ekran görüntüleri

Kimlik sunucusu Development ortamında, ayrı bir geçici LocalDB veritabanıyla (`SelcukSSO_TasarimD1`, çekimden sonra
silindi) ve test kullanıcısı `203011001` (Test Öğrenci) ile çekildi; şifre ve istemci sırları çekim için üretilen
rastgele değerlerdi, hiçbir yere yazılmadı. QR kod, doğrulayıcı anahtarı ve kurtarma kodları çekimden önce örnek
veriyle değiştirildi ("Örnek QR", `ABCD EFGH …`, `ORNEK-00001` …); görüntülerde şifre, kod veya token yok.
Masaüstü 1280 × 800, mobil 390 × 844, tam sayfa; hareket azaltma açık (örgü çizilmiş hâliyle).

| Ekran | Açık | Koyu | Mobil (390px) |
|---|---|---|---|
| Giriş | `giris-acik.png` | `giris-koyu.png` | `giris-mobil.png` |
| Girişin ikinci adımı (2FA) | `iki-adim-giris-acik.png` | `iki-adim-giris-koyu.png` | `iki-adim-giris-mobil.png` |
| Onay (Demo OBİS) | `onay-acik.png` | `onay-koyu.png` | `onay-mobil.png` |
| Uygulamalarım | `uygulamalarim-acik.png` | `uygulamalarim-koyu.png` | `uygulamalarim-mobil.png` |
| 2FA kurulumu | `iki-adim-kurulum-acik.png` | `iki-adim-kurulum-koyu.png` | `iki-adim-kurulum-mobil.png` |
| Kurtarma kodları | `kurtarma-kodlari-acik.png` | `kurtarma-kodlari-koyu.png` | `kurtarma-kodlari-mobil.png` |
| Güvenlik geçmişi | `guvenlik-gecmisi-acik.png` | `guvenlik-gecmisi-koyu.png` | `guvenlik-gecmisi-mobil.png` |
| Erişim reddedildi | `erisim-reddedildi-acik.png` | `erisim-reddedildi-koyu.png` | `erisim-reddedildi-mobil.png` |

Çekim sırasında denetlenenler: tarayıcı konsolunda CSP ihlali veya betik hatası yok; 390 ve 320 px genişlikte hiçbir
ekranda yatay kaydırma yok; arayüz yazı tipi gerçekten yüklendi; klavyeyle giriş → 2FA → onay → Uygulamalarım →
hesap menüsü (Enter ile açılır, Esc ile kapanıp odağı düğmeye döndürür) → çıkış akışı tamamlandı.
