# Kullanılabilirlik: System Usability Scale (SUS)

SUS (Brooke, 1996), bir sistemin algılanan kullanılabilirliğini 10 maddeyle ölçen, 0-100 arası tek bir puan veren
standart bir ankettir. Bu çalışmada Tek Şifre Giriş Sistemi'nin (kimlik sunucusu + Demo OBİS + Demo Yemekhane)
uçtan uca kullanımını değerlendirmek için kullanılır.

## Maddeler

Her madde için 5'li ölçek: **1** Kesinlikle katılmıyorum · **2** Katılmıyorum · **3** Kararsızım ·
**4** Katılıyorum · **5** Kesinlikle katılıyorum. "Sistem" = görevlerde kullanılan Tek Şifre Giriş Sistemi.

| No | Madde |
|---:|---|
| 1 | Bu sistemi sık sık kullanmak isterim. |
| 2 | Sistemi gereksiz yere karmaşık buldum. |
| 3 | Sistemin kullanımının kolay olduğunu düşündüm. |
| 4 | Bu sistemi kullanabilmek için teknik bilgisi olan birinin desteğine ihtiyaç duyacağımı düşünüyorum. |
| 5 | Bu sistemdeki çeşitli işlevlerin iyi bütünleştirildiğini düşündüm. |
| 6 | Bu sistemde çok fazla tutarsızlık olduğunu düşündüm. |
| 7 | Çoğu insanın bu sistemi kullanmayı çok çabuk öğreneceğini düşünüyorum. |
| 8 | Sistemi kullanmayı çok zahmetli buldum. |
| 9 | Sistemi kullanırken kendimden emin hissettim. |
| 10 | Bu sistemi kullanmaya başlamadan önce birçok şey öğrenmem gerekti. |

Tek numaralı maddeler olumlu, çift numaralı maddeler olumsuz ifadedir; sıra ve ifadeler değiştirilmez (yalnızca
"sistem" yerine gerekirse "Tek Şifre Giriş Sistemi" yazılabilir).

## Uygulama yönergesi

- **Katılımcı sayısı:** en az 12 (SUS, Tullis ve Stetson'ın 2004 karşılaştırmasında 12-14 katılımcıyla tutarlı
  sonuç verdi); mümkünse 15-20. Öğrenci, akademisyen ve personel rollerinden karışık olması tercih edilir.
- **Hesaplar:** katılımcılar kendi gerçek hesaplarını değil, önceden hazırlanmış test hesaplarını (`@test.local`)
  kullanır. Şifre katılımcıya kâğıtta verilir, ankete yazılmaz.
- **Görevler** (sırayla, yönlendirmesiz; gözlemci yalnızca takıldığı yeri not eder):
  1. https://localhost:5001 adresinde giriş yapın ve **Uygulamalarım** sayfasını açın.
  2. **Demo OBİS** kartından OBİS'e geçin (ilk kez ise onay ekranında izin verin) ve **Notlarım** sayfasını açın.
  3. Şifre girmeden **Demo Yemekhane**'ye geçin ve **Rezervasyonlarım** sayfasında bir gün için rezervasyon yapın.
  4. Demo OBİS'ten **çıkış** yapın; ardından Yemekhane sekmesini yenileyip oturumun kapandığını görün.
- Görevlerden **hemen sonra**, sistemi yeniden düşünmeden anketi doldurun. Her maddeye yanıt verilmeli; emin
  değilseniz 3'ü işaretleyin.
- **Kişisel veri toplanmaz:** katılımcının adı, öğrenci numarası, e-postası, IP'si veya imzası alınmaz.
  Katılımcılar yalnızca sıra numarasıyla (1, 2, 3, …) kaydedilir. İsteniyorsa yalnızca rol kategorisi (`ogrenci`,
  `akademisyen`, `personel`) ayrı bir sütunda tutulabilir. Katılım gönüllüdür; katılımcıya yanıtların yalnızca
  toplu olarak raporlanacağı söylenir.

## Yanıtların kaydı

`sus-yanitlar-sablonu.csv`'yi `sus-yanitlar.csv` adıyla kopyalayıp her katılımcıya bir satır yazın:

```csv
katilimci,m1,m2,m3,m4,m5,m6,m7,m8,m9,m10
1,4,2,5,1,4,2,5,1,4,2
```

Eksik veya 1-5 dışında yanıt olan satır analizde atlanır ve uyarı verilir.

## Puanlama

Her katılımcı için:

- Tek numaralı maddeler (1, 3, 5, 7, 9): **katkı = yanıt − 1**
- Çift numaralı maddeler (2, 4, 6, 8, 10): **katkı = 5 − yanıt**
- **SUS puanı = (katkıların toplamı) × 2,5** → 0-100

Örnek (yukarıdaki satır): tek maddeler 3+4+3+4+3 = 17, çift maddeler 3+4+3+4+3 = 17, (17 + 17) × 2,5 = **85**.
SUS puanı yüzde değildir; yaygın kabule göre 68 ortalama kullanılabilirliktir.

`python scripts/olcum_analiz.py` `sus-yanitlar.csv` varsa katılımcı sayısını ve ortalama, medyan, en düşük ve en
yüksek SUS puanını `docs/olcumler/karsilastirma.md`'ye yazar. Henüz anket uygulanmadı.

`ornek-sus-yanitlar.csv` gerçek yanıt değildir; yalnızca `--self-test` kullanır.
