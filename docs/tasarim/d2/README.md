# Tasarım D2: demo uygulamalar, Tek Kapı ve ekran görüntüsü betiği

D1'de kurulan tasarım sistemi (`src/Selcuk.Arayuz`, "Taç kapı") bu adımda Demo OBİS, Demo Yemekhane ve Tek Kapı'ya
uygulandı; D1 gözden geçirmesindeki düzeltmeler yapıldı ve ekran görüntüsü betiği depoya alındı
(`scripts/ekran-goruntuleri.mjs`). Davranış, yönlendirme, API ve kimlik doğrulama ayarı değişmedi; yalnızca görünüm,
yerleşim ve metinler.

**Kural:** firuze renk ve kapı işareti yalnızca kimlikle ilgili öğelerde: "Tek Şifre ile giriş yap" düğmesi, hesap
menüsü, kimlik sunucusu ekranları, Tek Kapı'nın SSO öğeleri (ve kasa ekranları: Tek Kapı'nın kendisi bir kimlik
aracıdır). Her demo uygulamanın kendi içeriği kendi vurgu rengini kullanır: farklı uygulamalar oldukları ama aynı
kapıdan girildiği görünür.

## D1 düzeltmeleri

| | Ne yapıldı |
|---|---|
| a) Kapı örgüsü | Kemer içindeki desen klasik "yıldız ve haç" örgüsü: sekiz köşeli yıldızlar 2a aralıklı kare ızgaranın dama tahtası konumlarında, satır ve sütunlarda yıldız ile haç dönüşümlü. Yıldızlar yalnızca çapraz uçlarında birbirine değer; eksen uçları komşu haçın çentiğine oturur, haçların bütün kenarları yıldız kenarıdır (kenar kenara). Ölçek büyüdü (kare yarısı 11 → 14), ortadaki sütunda üzengi hizasında bir yıldız var. Çizgi kalınlığı (1) ve rengi (`--kapi-cizgi`) aynı; küçük işaret değişmedi. |
| b) Kapı paneli cümlesi | "Fakültedeki bütün uygulamalar için tek hesap." `max-width: 26em` ve `text-wrap: pretty`: geniş panelde tek satır, darda kelime sınırından iki satır (tek kelimelik son satır yok). |
| c) Güvenlik geçmişi | Saat dilimi satırlarda değil sütun başlığında: "Zaman (TSİ)" (`EpostaSablonlari.DilimsizBicimle`; e-postalar eki korur). Dar ekranda başlık gizli olduğu için tablonun üstünde bir kez "Zamanlar Türkiye saatiyle (TSİ) yazılır." IP Mono, `letter-spacing: 0`. |
| d) 2FA ikinci adım ve ek doğrulama | "Telefonun yanında değilse…" cümlesi kaldırıldı, "Kurtarma kodu kullan" bağlantısı kaldı. Kod alanı ortalı, yer tutucu `000 000`; kurtarma kodu modunda `XXXXX-XXXXX` (`data-yer-tutucu-kurtarma`, selcuk.js). Boş kalan ipucu gizlenir. Tutarlılık için 2FA kurulum/yenileme/kapatma kod alanlarına da aynı yer tutucu. |
| e) Hata sayfaları | Başlığın üstünde 48px "kapalı kapı" (`KapiIsareti.Kapali`): küçük kemer çizgisi, eşik ve içi 45° ince tarama (kemerle kırpılmış). Kimlik sunucusunda Erişim reddedildi, Hata ve 429; demolarda Erişim reddedildi, Hata ve Giriş tamamlanamadı; Tek Kapı'nın SSO bilgi sayfalarında. Rengi `--uygulama-vurgu` (kimlik sunucusunda firuze, demoda uygulamanın rengi). |
| f) Onay ekranı | Profil kapsamının açıklaması "Adın, fakülte ve bölüm bilgilerin". |

## Selcuk.Arayuz eklemeleri

- **Uygulama vurgu rengi:** `--uygulama-vurgu`, `--uygulama-vurgu-acik`. Varsayılan firuze (metin tonu `--baglanti`,
  çünkü `--firuze` açık temada taş zeminde 4,5:1'in altında). Her uygulama kendi `site.css`'inde açık ve koyu tema
  için verir. Kullanım: başlık şeridi (üst çubuğun üstünde 4px), sekme altı çizgisi, tablo başlığı çizgisi, özet
  kenarı, harf/durum etiketi, uygulama simgesi, bağlantılar ve ikincil düğmeler (`uygulama-icerigi`), uygulama düğmesi
  (`dugme-uygulama`), uygulama bandı (`bant-uygulama`), kapalı kapı.
- **"Tek Şifre ile giriş yap":** `_TekSifreGirisi` (model: giriş adresi): küçük kapı işareti + metin, firuze ana düğme.
  Demoların giriş yapılmamış ana sayfasında; "Giriş tamamlanamadı"daki "Tekrar giriş yap" da aynı görünümde.
- **Ortak düzen:** `_SelcukDuzen` uygulama adını (`Duzen.Ayar.UygulamaAdi`), simgesini (`Duzen.Ayar.UygulamaSimgesi`:
  baş harfler, uygulamanın renginde; verilince kapı işareti yerine simge ve başlık şeridi), alt bilgiyi
  (`Duzen.Ayar.AltBilgi`), hesap menüsünü (`HesapMenusu` bölümü) ve sekmeleri (`Sekmeler` bölümü) alır. Alt
  uygulamalar için `_IstemciHesapMenusu` (küçük kapı işareti, ad, kullanıcı adı, bağlantılar, /Cikis'e POST eden
  "Çıkış yap") ve `_IstemciProfili`.
- **Bileşenler:** sekmeler, özet sayıları (`ozetler`), tablo (`tablo`; sayılar `sayi` ile sağa hizalı ve eşit genişlikli
  rakamlarla; dar ekranda `tablo-yigin` her satırı iki satırlık öğeye çevirir), etiket içi simge, `data-duyur`
  (yüklemeden sonra canlı bölgeye yazılan işlem sonucu).
- **SVG dosyaları:** `wwwroot/img/kapi-kucuk.svg`, `kapi-buyuk.svg`, `kapi-kapali.svg` (`KapiIsareti.Dosya`: satır içi
  SVG + xmlns). Eşitliği `ArayuzTests` denetler; Tek Kapı bu dosyaları kopyalar.

## Uygulama vurgu renkleri: kontrast (WCAG 2.x)

Metin için eşik 4,5:1 (AA), metin dışı öğeler (çizgi, şerit, odak halkası) için 3:1. Değerler token'lardan WCAG göreli
parlaklık formülüyle hesaplandı; zeminler D1 token'larıdır (`--tas` #EDF0EF / #0E1B27, `--yuzey` #FFFFFF / #15273A,
`--murekkep` #12263A / #E6EEF0, `--soluk` #4A5C6A / #A9BAC2).

| Uygulama | `--uygulama-vurgu` açık / koyu | `--uygulama-vurgu-acik` açık / koyu |
|---|---|---|
| Varsayılan (kimlik sunucusu, Tek Kapı) | #0C6B6E / #5CC3C0 (`--baglanti`) | #D3EBE9 / #16393C (`--firuze-acik`) |
| Demo OBİS (lacivert) | #2F4F8F / #9DB5E8 | #E3E9F5 / #1C2A45 |
| Demo Yemekhane (nar) | #9C3D54 / #E99AAE | #F7E4E9 / #3B1E28 |

### Demo OBİS (lacivert)

| Metin / öğe | Zemin | Kullanım | Açık tema | Koyu tema | Eşik | Sonuç |
|---|---|---|---|---|---|---|
| `--uygulama-vurgu` | `--tas` | Bağlantı, sayfa zemininde | 6.95:1 (#2F4F8F / #EDF0EF) | 8.47:1 (#9DB5E8 / #0E1B27) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--yuzey` | Bağlantı, ikincil düğme metni, "Ham hâlini göster" | 7.97:1 (#2F4F8F / #FFFFFF) | 7.38:1 (#9DB5E8 / #15273A) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--uygulama-vurgu-acik` | Harf notu etiketi, uygulama simgesi, "Var" etiketi | 6.54:1 (#2F4F8F / #E3E9F5) | 6.96:1 (#9DB5E8 / #1C2A45) | 4.5:1 | ✓ |
| `--murekkep` | `--uygulama-vurgu-acik` | Bilgi bandı metni (access token) | 12.64:1 (#12263A / #E3E9F5) | 12.17:1 (#E6EEF0 / #1C2A45) | 4.5:1 | ✓ |
| `--soluk` | `--uygulama-vurgu-acik` | İkincil metin bant içinde | 5.69:1 (#4A5C6A / #E3E9F5) | 7.15:1 (#A9BAC2 / #1C2A45) | 4.5:1 | ✓ |
| `--yuzey` | `--uygulama-vurgu` | Uygulama düğmesi metni ("Ana sayfaya dön") | 7.97:1 (#FFFFFF / #2F4F8F) | 7.38:1 (#15273A / #9DB5E8) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--yuzey` | Sekme altı çizgisi, tablo başlığı çizgisi, başlık şeridi, özet kenarı, odak halkası (metin dışı) | 7.97:1 (#2F4F8F / #FFFFFF) | 7.38:1 (#9DB5E8 / #15273A) | 3:1 | ✓ |

### Demo Yemekhane (nar)

| Metin / öğe | Zemin | Kullanım | Açık tema | Koyu tema | Eşik | Sonuç |
|---|---|---|---|---|---|---|
| `--uygulama-vurgu` | `--tas` | Bağlantı, sayfa zemininde | 5.71:1 (#9C3D54 / #EDF0EF) | 8.05:1 (#E99AAE / #0E1B27) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--yuzey` | İkincil düğme metni ("İptal et") | 6.54:1 (#9C3D54 / #FFFFFF) | 7.01:1 (#E99AAE / #15273A) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--uygulama-vurgu-acik` | "Rezervasyonlu" ve "Bugün" etiketi, uygulama simgesi | 5.37:1 (#9C3D54 / #F7E4E9) | 6.92:1 (#E99AAE / #3B1E28) | 4.5:1 | ✓ |
| `--murekkep` | `--uygulama-vurgu-acik` | Bugünün menü sütunu, işlem sonucu bandı | 12.63:1 (#12263A / #F7E4E9) | 12.73:1 (#E6EEF0 / #3B1E28) | 4.5:1 | ✓ |
| `--soluk` | `--uygulama-vurgu-acik` | Bugünün sütunundaki yemek türü ve tarih | 5.69:1 (#4A5C6A / #F7E4E9) | 7.48:1 (#A9BAC2 / #3B1E28) | 4.5:1 | ✓ |
| `--yuzey` | `--uygulama-vurgu` | Uygulama düğmesi metni ("Rezervasyon yap") | 6.54:1 (#FFFFFF / #9C3D54) | 7.01:1 (#15273A / #E99AAE) | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--yuzey` | Sekme altı çizgisi, tablo başlığı çizgisi, başlık şeridi, bugünün kenarı, odak halkası (metin dışı) | 6.54:1 (#9C3D54 / #FFFFFF) | 7.01:1 (#E99AAE / #15273A) | 3:1 | ✓ |

### Varsayılan (firuze)

D1 tablosundaki `--baglanti` ve `--firuze-acik` satırlarıyla aynı: vurgu/taş 5.48 / 8.32, vurgu/yüzey 6.28 / 7.25,
vurgu/vurgu-açık 5.03 / 5.95, mürekkep/vurgu-açık 12.34 / 10.60, soluk/vurgu-açık 5.55 / 6.23, yüzey/vurgu
6.28 / 7.25 (açık / koyu). Hepsi ✓.

## Demo uygulamalar

- **Ortak:** üst çubukta uygulama simgesi (DO, DY; Uygulamalarım'daki baş harflerle aynı) ve adı, "Test" etiketi, başlık şeridi; giriş yapınca sekmeler ve
  hesap menüsü. Ortak sayfalar (Profilim, Erişim reddedildi, Giriş tamamlanamadı, Hata, Çıkış yap) iki uygulamada
  aynı. Eski CSS silindi; `site.css` yalnızca vurgu rengi ve uygulamaya özel yerleşimler. Metinler kimlik sunucusu
  gibi "sen" hitabıyla.
- **Demo OBİS:** Notlarım tablosu (ders kodu, ders adı, kredi, vize, final, harf etiketi), üstünde toplam kredi ve
  dönem ağırlıklı ortalaması. Demo API yalnızca not verir (API değişmedi); kredi OBİS'in ders kataloğundandır
  (`SahteVeri.Krediler`), ortalama `NotOzeti` ile 4'lük katsayı × kredi. Örnek veride 17 kredi, 3,03. Ders Yönetimi
  aynı tablo dilinde. Token İnceleme: id_token ve access_token bölümleri, her başlık alanı ve claim bir satır (Mono ad
  ve değer, Türkçe açıklama), access token'da "kişisel bilgi içermez" bandı, ham JSON açılır bölümde.
- **Demo Yemekhane:** haftalık menü günler sütun (dar ekranda satır), bugün vurgulu; Rezervasyonlarım gün listesi,
  durum etiketi, "Rezervasyon yap" / "İptal et", işlem sonucu bantta ve ekran okuyucuya; Yemekhane Yönetimi tablosu.

## Tek Kapı

- Tasarım kaynağı tek: `scripts/arayuz-esitle.ps1` `selcuk.css`, yazı tipleri ve kapı SVG'lerini
  `araclar/tek-kapi/public/arayuz/` altına kopyalar; `npm test` kopyaların kaynakla birebir aynı olduğunu (SHA-256) ve
  CSP'de Google Fonts olmadığını denetler. Manrope ve Google Fonts bağlantısı kaldırıldı, CSP'den
  `fonts.googleapis.com` ve `fonts.gstatic.com` çıkarıldı (başka bir şey eklenmedi).
- Kapı işaretleri SVG dosyasıyla CSS maskesi olarak çizilir (satır içi stil yok).
- Kimlik sunucusuyla aynı üst çubuk; kasa durumu kilit simgesiyle ("Kasa kilitli" / "Kasa açık"); SSO modunda kişinin
  adı (küçük kapı işaretiyle) ve "SSO'dan çık". Kilit ve kurulum ekranları kapı paneli düzeninde ("Kasanı aç",
  "Kasanı oluştur"). Sistemler ayırıcı çizgili satırlar (ad, alan adı, giriş türü etiketi, bilgi eksikse uyarı
  etiketi, "Aç"); "Hepsinden çık" ikincil tehlikeli düğme. Son işlemler ve ölçümler tablo dilinde, süreler sağa hizalı
  ve eşit genişlikli rakamlarla. Ayarlar tek sütun, en fazla 560px.
- Noktalı birleştirmeler kaldırıldı: tarayıcıdaki bilgi şeridinde "Tek Kapı · " → "Tek Kapı: " (browser.js'te yalnızca
  bu önek), ölçüm olayında "Giriş tamamlandı, 2,3 sn ölçüldü", ölçüm tablosunda "3 kez, ort. 1,2 sn".

## Ekran görüntüleri

`node --use-system-ca scripts/ekran-goruntuleri.mjs` ile üretildi (README > Ekran görüntüleri). Masaüstü 1280 × 800,
mobil 390 × 844, tam sayfa, hareket azaltma açık. Geçici LocalDB veritabanı ve rastgele test şifreleri; Tek Kapı
geçici veri klasörüyle ve `test/mock-sites.js`'teki sahte bilgilerle. QR kod, anahtar ve kurtarma kodları örnek
değerlerle (`Örnek QR`, `ABCD EFGH …`, `ORNEK-00001` …). Görüntülerdeki kişi ve hesaplar kurgusal test verisidir.

Dosya adı `<ekran>-acik.png`, `<ekran>-koyu.png`, `<ekran>-mobil.png` (menü açık ve kurtarma modu ekranlarında
mobil yok).

35 ekran, 101 dosya.

| Uygulama | Ekran | Dosya önü | Mobil |
|---|---|---|---|
| Kimlik sunucusu | Giriş (kapı paneli, yeni örgü ve cümle) | `giris` | ✓ |
| Kimlik sunucusu | Girişin ikinci adımı (2FA) | `iki-adim-giris` | ✓ |
| Kimlik sunucusu | Girişin ikinci adımı, kurtarma kodu modu | `iki-adim-giris-kurtarma` | — |
| Kimlik sunucusu | Ek doğrulama (step-up, Tek Kapı için) | `ek-dogrulama` | ✓ |
| Kimlik sunucusu | Ek doğrulama, kurtarma kodu modu | `ek-dogrulama-kurtarma` | — |
| Kimlik sunucusu | Onay (Demo OBİS) | `onay` | ✓ |
| Kimlik sunucusu | Uygulamalarım | `uygulamalarim` | ✓ |
| Kimlik sunucusu | Uygulamalarım, hesap menüsü açık | `hesap-menusu` | — |
| Kimlik sunucusu | 2FA kurulumu | `iki-adim-kurulum` | ✓ |
| Kimlik sunucusu | Kurtarma kodları | `kurtarma-kodlari` | ✓ |
| Kimlik sunucusu | Güvenlik geçmişi | `guvenlik-gecmisi` | ✓ |
| Kimlik sunucusu | Erişim reddedildi | `erisim-reddedildi` | ✓ |
| Kimlik sunucusu | Hata | `hata` | ✓ |
| Kimlik sunucusu | Çok fazla deneme (429) | `cok-fazla-istek` | ✓ |
| Demo OBİS | Giriş yapılmamış ana sayfa | `obis-karsilama` | ✓ |
| Demo OBİS | Ana sayfa (giriş yapılmış) | `obis-ana` | ✓ |
| Demo OBİS | Notlarım | `obis-notlarim` | ✓ |
| Demo OBİS | Ders Yönetimi (akademisyen) | `obis-ders-yonetimi` | ✓ |
| Demo OBİS | Token İnceleme | `obis-token-inceleme` | ✓ |
| Demo OBİS | Profilim | `obis-profilim` | ✓ |
| Demo OBİS | Profilim, hesap menüsü açık | `obis-hesap-menusu` | — |
| Demo OBİS | Erişim reddedildi | `obis-erisim-reddedildi` | ✓ |
| Demo OBİS | Giriş tamamlanamadı | `obis-giris-hatasi` | ✓ |
| Demo OBİS | Çıkış yap | `obis-cikis` | ✓ |
| Demo Yemekhane | Giriş yapılmamış ana sayfa ve haftalık menü | `yemekhane-karsilama` | ✓ |
| Demo Yemekhane | Haftalık menü (giriş yapılmış) | `yemekhane-menu` | ✓ |
| Demo Yemekhane | Rezervasyonlarım (işlem sonucu bandıyla) | `yemekhane-rezervasyonlarim` | ✓ |
| Demo Yemekhane | Yemekhane Yönetimi (personel) | `yemekhane-yonetimi` | ✓ |
| Demo Yemekhane | Profilim | `yemekhane-profilim` | ✓ |
| Tek Kapı | Kurulum (Kasanı oluştur) | `tekkapi-kurulum` | ✓ |
| Tek Kapı | Ayarlar (boş) | `tekkapi-ayarlar` | ✓ |
| Tek Kapı | Ana ekran, bilgi eksik | `tekkapi-ana-eksik` | ✓ |
| Tek Kapı | Ana ekran | `tekkapi-ana` | ✓ |
| Tek Kapı | Kilit ekranı (Kasanı aç) | `tekkapi-kilit` | ✓ |
| Tek Kapı | SSO bilgi sayfası (erişim izni yok) | `tekkapi-erisim-yok` | ✓ |

Tez görselleri (`docs/tez-gorselleri/`, açık tema, 1440px): `01-giris`, `02-onay`, `03-uygulamalarim`,
`04-obis-notlarim`, `05-obis-token-inceleme`, `06-yemekhane-menu`, `07-tekkapi-ana`, `08-tekkapi-kilit`,
`09-guvenlik-gecmisi`, `10-iki-adim-kurulum`.

## Gözden geçirmede bulunup düzeltilenler

| Sorun | Düzeltme |
|---|---|
| Sekmeler biçimlenmemiş, üst çubuğun altında alt alta düz bağlantı olarak çıkıyordu: `RenderSectionAsync` içeriği doğrudan çıktıya yazıp boş döndürdüğü için düzendeki "boş sekme listesi" denetimi hep boş görüyordu. | Gezinme kabı (`<nav class="sekmeler">`) sekme bölümünün içine alındı; düzen yalnızca bölümü işler. |
| Token İnceleme'de GUID ve anahtar kimliği gibi değerler kelime ortasından iki satıra bölünüyordu. | Değerler tek satır, küçük metin boyunda (uzunsa sunucuda kırpılır, tam hâli `title`'da); dar ekranda gerekirse kırılır. |
| Token İnceleme 390px'te yatay taşıyordu (değer sütununun masaüstü genişliği dar ekranda da geçerliydi). | Dar ekranda claim adı ve değer sütunlarının genişliği `auto`. |
| Haftalık menüde "Bugün" etiketi tarihin altına kayıyor, bugünün sütunundaki satırlar diğer sütunlardan aşağıda kalıyordu. | Gün başlığı her sütunda iki satır: gün adı (bugünse yanında etiket) ve tarih. |
| Uygulamalarım'daki baş harfler (DO, DY) demoların üst çubuğundaki simgelerle (OB, YM) uyuşmuyordu. | Demolar Uygulamalarım'la aynı baş harfleri kullanır (DO, DY). |
| Tek Kapı kilit ve kurulum ekranında dar ekranda büyük kapı işareti küçülmüyor, panel metni kırpılıyordu (ortak CSS'in mobil kuralı SVG'yi hedefliyor, Tek Kapı maske kullanıyor). | Dar ekranda maske işareti de 116px yüksekliğe iner. |
| Tek Kapı'da alan adları dar ekranda kelime ortasından kırılıyordu ("obis.selcuk.edu. / tr"). | Alan adı yalnızca noktalardan sonra kırılır (`<wbr>`). |
| Tek Kapı'nın SSO bilgi sayfasında alt bilgi yoktu (diğer ekranlarla tutarsız). | Alt bilgi eklendi. |
| Çekimde fare imleci bir düğmenin üzerinde kalıp üzerine-gelme rengi görünüyordu. | Betik her çekimden önce imleci köşeye alır. |

Denetlenenler: 390px'te hiçbir ekranda yatay kaydırma yok (betik ölçer); tarayıcı konsolunda CSP ihlali veya betik
hatası yok (yalnızca kasıtlı açılan 403 ve 429 sayfalarının durum kodu iletisi, betik bunları ayıklar); arayüz yazı
tipi demolarda ve Tek Kapı'da yerelden yüklendi; Token İnceleme'de açıklaması olmayan claim yok; görüntülerde şifre,
TOTP veya kurtarma kodu, anahtar, token ya da gerçek kişisel bilgi yok (her çekimden önce o çalıştırmanın sırları
sayfada arandı; QR, anahtar ve kurtarma kodları örnek değerli).

## Bölüm otomasyonları: İntibak, Mezuniyet, ÇAP-Yandal

Ortak otomasyon şablonuyla (`_OtomasyonDuzen`, `otomasyon.css`) ve şablonun yeni ortak parçalarıyla (durum rozeti,
başvuru aşamaları, sayı kartları, kayıt tablosu). İstenen vurgu renkleri ölçüldü; hepsi 4,5:1'in üstünde olduğu için
değiştirilmedi. Açık vurgu zeminleri ve koyu tema karşılıkları Staj'daki gibi seçildi (aynı ton ailesi; koyu temada açık
ton ve koyu zemin).

| Uygulama | `--uygulama-vurgu` açık / koyu | `--uygulama-vurgu-acik` açık / koyu | Uygulamalarım simgesi |
|---|---|---|---|
| İntibak (eflatun) | #6B4C8A / #C9B3E3 | #EFE9F5 / #2B2238 | İN |
| Mezuniyet (bakır) | #8A4B1F / #E8B48E | #F6EBE2 / #3A2618 | ME |
| ÇAP-Yandal (koyu hardal) | #735A00 / #D9C068 | #F5EED6 / #332B10 | ÇY |

| Metin / öğe | Zemin | Kullanım | İntibak açık / koyu | Mezuniyet açık / koyu | ÇAP-Yandal açık / koyu | Eşik | Sonuç |
|---|---|---|---|---|---|---|---|
| `--uygulama-vurgu` | `--tas` | Bağlantı, sayfa zemininde | 6.05 / 9.16 | 5.89 / 9.42 | 5.74 / 9.70 | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--yuzey` | Menü, tablo, ikincil düğme metni | 6.94 / 7.98 | 6.76 / 8.21 | 6.58 / 8.45 | 4.5:1 | ✓ |
| `--uygulama-vurgu` | `--uygulama-vurgu-acik` | Olumlu rozet, şu anki aşamanın durumu, uygulama simgesi | 5.83 / 7.95 | 5.76 / 7.72 | 5.67 / 7.83 | 4.5:1 | ✓ |
| `--murekkep` | `--uygulama-vurgu-acik` | Temsili işlem bandı, şu anki aşamanın adı | 12.94 / 12.86 | 13.12 / 12.14 | 13.26 / 11.95 | 4.5:1 | ✓ |
| `--soluk` | `--uygulama-vurgu-acik` | İkincil metin vurgu zemininde | 5.82 / 7.55 | 5.91 / 7.13 | 5.97 / 7.02 | 4.5:1 | ✓ |
| `--yuzey` | `--uygulama-vurgu` | Uygulama düğmesi metni ("Onayla", "Başvur") | 6.94 / 7.98 | 6.76 / 8.21 | 6.58 / 8.45 | 4.5:1 | ✓ |

Ekran görüntüleri `node --use-system-ca scripts/ekran-goruntuleri.mjs --sadece intibak,mezuniyet,cap` ile üretildi
(diğer gruplarınkiler değişmedi). Öğrenci ekranları 203011002, komisyon ekranları komisyon.test, koordinatör ekranları
koordinator.test ile.

| Uygulama | Ekran | Dosya önü | Mobil |
|---|---|---|---|
| İntibak | Ana sayfa (öğrenci kısayolları) | `intibak-ana` | ✓ |
| İntibak | Başvurum (durum rozeti ve aşamalar) | `intibak-basvurum` | ✓ |
| İntibak | İntibak başvurusu (transkript alanı) | `intibak-basvuru` | ✓ |
| İntibak | Bekleyen başvurular (komisyon) | `intibak-bekleyen-basvurular` | ✓ |
| İntibak | Ders eşleştirme (komisyon) | `intibak-ders-eslestirme` | ✓ |
| Mezuniyet | Mezuniyet durumum (sayı kartları, eksikler) | `mezuniyet-durumum` | ✓ |
| Mezuniyet | Mezuniyet başvurusu (aşamalar ve form) | `mezuniyet-basvuru` | ✓ |
| Mezuniyet | İlişik kesme | `mezuniyet-ilisik-kesme` | ✓ |
| Mezuniyet | Mezuniyet adayları (komisyon) | `mezuniyet-adaylar` | ✓ |
| Mezuniyet | Temsili işlem mesajı | `mezuniyet-temsili-islem` | — |
| ÇAP-Yandal | Başvurularım | `cap-basvurularim` | ✓ |
| ÇAP-Yandal | Başvuru (şart göstergesi) | `cap-basvuru` | ✓ |
| ÇAP-Yandal | Erişim reddedildi (öğrenci, koordinatör ekranı) | `cap-erisim-reddedildi` | ✓ |
| ÇAP-Yandal | Gelen başvurular (koordinatör, kontenjan) | `cap-gelen-basvurular` | ✓ |
| ÇAP-Yandal | Öğrenci takibi (koordinatör, GNO uyarısı) | `cap-ogrenci-takibi` | ✓ |

Tez görselleri: `15-intibak-basvurum`, `16-intibak-ders-eslestirme`, `17-mezuniyet-durumum`, `18-mezuniyet-adaylar`,
`19-cap-basvurularim`, `20-cap-gelen-basvurular`.

Gözden geçirmede bulunup düzeltilenler: kayıt tablosunun son sütunundaki görünmez metinler (`.gorunmez`, mutlak
konumlu) kaydırma alanından taşıp dar ekranda sayfayı yatay genişletiyordu; kaydırma alanına `position: relative`
verildi. Ders eşleştirmede önceki dersler ve müfredat iki sütuna sığmıyor, eşleştirme tablosu kırpılıyordu; önceki ders
ile eşdeğer ders aynı satırda kaldı, müfredat altta tam genişlikte gösterilir. Geniş tablolarda sütunlar birleştirildi (Mezuniyet adaylarında eksik ders, staj ve GNO şartı tek "Kontrol sonucu" hücresinde, durum öğrenci hücresinde; İntibak listelerinde geliş türü ve önceki kurum tek hücrede; ÇAP-Yandal gelen başvurularında anadal sütunu kaldırıldı): işlem düğmeleri 1280px'te kaydırmadan görünür. "Bekliyor" rozeti taş zeminde seçilsin diye ince çerçeve aldı; tablodaki öğrenci numaraları kırılmaz.
