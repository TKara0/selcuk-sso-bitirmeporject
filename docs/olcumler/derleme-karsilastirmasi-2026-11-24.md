# Release ve Debug ölçümü (2026-11-24)

`scripts/olcum_derleme_karsilastir.py` ile ölçüm dosyalarından üretildi; elle düzenlemeyin. Süreler milisaniye (p50 / p95), her tur 100 ölçülen deneme ve 5 ısınma, hata yok. Ölçülmemiş değer yazılmaz.

## Ölçüm ortamı

| Alan | Değer |
|---|---|
| İşletim sistemi | Microsoft Windows 10.0.26300 (X64) |
| .NET | .NET 10.0.13 |
| İşlemci | AMD Ryzen 7 7840HS w/ Radeon 780M Graphics (16 mantıksal çekirdek) |
| RAM | 31.3 GB |
| Derleme türü | Debug ve Release (sütun başlıklarında) |
| Ortam | Development, LocalDB; kimlik sunucusu, iki demo, Demo API, 11 otomasyon ve ölçüm aracı aynı makinede; ölçüm kullanıcıları ve ölçüm hız sınırı gevşetmesi `docs/olcumler/sso-olcumler-*.md`'deki kurallarla |

## Sıralı senaryolar (p50 / p95, ms)

| Senaryo | Eski ölçüm<br>26 Ekim, Debug, o günkü kod | Eski kod bugün<br>Debug, `07121a6` | Güncel kod<br>Debug | Güncel kod<br>Release, tur 1 | Güncel kod<br>Release, tur 2 |
|---|---:|---:|---:|---:|---:|
| İlk giriş (OBİS) | 123,2 / 222,1 | 207,4 / 235,8 | 211,2 / 229,4 | 207,1 / 226,6 | 203,6 / 228,4 |
| Tek giriş (Yemekhane) | 54,2 / 91,5 | 81,0 / 89,3 | 86,3 / 93,7 | 85,4 / 95,4 | 83,8 / 100,5 |
| Belirteç yenileme | 27,8 / 46,7 | 43,7 / 49,6 | 45,7 / 49,5 | 44,6 / 49,0 | 43,8 / 50,6 |
| API çağrısı | 7,0 / 11,8 | 10,3 / 12,6 | 10,3 / 11,4 | 10,0 / 11,5 | 10,1 / 11,6 |
| Tek çıkış | 38,9 / 61,2 | 60,3 / 74,2 | 60,8 / 72,7 | 57,5 / 65,1 | 59,8 / 73,1 |

## Eşzamanlı soğuk giriş (saniyede başarılı giriş; p50 / p95, ms)

| Eşzamanlı kullanıcı | Eski ölçüm<br>26 Ekim, Debug, o günkü kod | Eski kod bugün<br>Debug, `07121a6` | Güncel kod<br>Debug | Güncel kod<br>Release, tur 1 | Güncel kod<br>Release, tur 2 |
|---:|---:|---:|---:|---:|---:|
| 1 | **7,1/sn**<br>118,9 / 227,4 | **5,1/sn**<br>191,2 / 227,2 | **4,9/sn**<br>203,6 / 221,4 | **4,8/sn**<br>208,0 / 219,2 | **5,2/sn**<br>190,0 / 212,2 |
| 10 | **35,7/sn**<br>272,5 / 338,8 | **41,5/sn**<br>237,5 / 272,4 | **42,0/sn**<br>234,3 / 259,1 | **42,4/sn**<br>232,8 / 261,2 | **42,0/sn**<br>231,0 / 268,9 |
| 25 | **39,3/sn**<br>588,2 / 880,2 | **52,6/sn**<br>439,7 / 603,9 | **49,7/sn**<br>450,8 / 630,5 | **48,7/sn**<br>471,5 / 614,1 | **53,1/sn**<br>434,7 / 653,8 |
| 50 | **42,6/sn**<br>1085,4 / 1897,1 | **50,5/sn**<br>897,9 / 1424,6 | **53,2/sn**<br>810,2 / 1330,4 | **51,6/sn**<br>849,4 / 1356,0 | **51,8/sn**<br>863,4 / 1330,9 |

## Doyum noktası

- Eski ölçüm 26 Ekim, Debug, o günkü kod: verim artışı 1→10: %+403, 10→25: %+10, 25→50: %+8; doyum 10 eşzamanlı kullanıcıda; en yüksek 42,6 giriş/sn.
- Eski kod bugün Debug, `07121a6`: verim artışı 1→10: %+718, 10→25: %+27, 25→50: %-4; doyum 25 eşzamanlı kullanıcıda; en yüksek 52,6 giriş/sn.
- Güncel kod Debug: verim artışı 1→10: %+754, 10→25: %+18, 25→50: %+7; doyum 25 eşzamanlı kullanıcıda; en yüksek 53,2 giriş/sn.
- Güncel kod Release, tur 1: verim artışı 1→10: %+785, 10→25: %+15, 25→50: %+6; doyum 25 eşzamanlı kullanıcıda; en yüksek 51,6 giriş/sn.
- Güncel kod Release, tur 2: verim artışı 1→10: %+711, 10→25: %+27, 25→50: %-3; doyum 25 eşzamanlı kullanıcıda; en yüksek 53,1 giriş/sn.

Doyum ölçütü: bir üst eşzamanlılık düzeyine geçerken saniyede başarılı giriş %10'dan az artıyorsa, alttaki düzey doyum noktasıdır (ölçülen düzeyler 1, 10, 25, 50).

## Bulgular

- **Release ile Debug arasında ölçülebilir bir fark yok.** İlk giriş p50: Debug 211,2 ms, Release 207,1 ve 203,6 ms; iki Release turu arasındaki fark (3,5 ms) Debug ile Release arasındaki farkla aynı büyüklükte. Diğer senaryolarda da aynı; eşzamanlı verim turlar arasında ±%5 dalgalanıyor.
- **26 Ekim'deki düşük tek kullanıcı süresi (123,2 ms) bugün yeniden elde edilmedi ve kodla açıklanmıyor.** 26 Ekim'deki kod (`07121a6`) bugünkü makinede 207,4 ms verdi, güncel kod 211,2 ms. Fark kod veya derleme türünden değil, ölçüm anındaki ortamdan geliyor; nedeni (işlemci frekansı veya güç planı, arka plan yükü) ölçülmedi. Eşzamanlı verim ise bugün daha yüksek; tek kullanıcılı süre ile eşzamanlı verim aynı yönde değişmedi.
- Bu yüzden 26 Ekim ile bugünkü sayılar birbirinin yerine geçmez: aynı gün, aynı makinede alınan sütunlar ("Eski kod bugün", "Güncel kod") karşılaştırma için esas alınmalıdır.
