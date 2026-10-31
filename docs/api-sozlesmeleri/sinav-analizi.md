# Sınav Başarısı Analizi API sözleşmesi

**Durum: sözleşme.** Sınav Başarısı Analizi API'si henüz yazılmadı. Sınav Başarısı Analizi Otomasyonu
(`src/Selcuk.Otomasyon.SinavAnalizi`, https://localhost:5310) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` =
`Mock`) çalışır. API yazıldığında yalnızca ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<sinav-analizi-api-adresi>/" }`.
İstemci tarafı hazırdır (`ApiSinavAnaliziVeriKaynagi`); DTO'lar
`src/Selcuk.Otomasyon.SinavAnalizi/Sozlesme/SinavAnaliziSozlesmesi.cs`'te.

## Yalnızca toplu veri

API **öğrenci düzeyinde veri döndürmez**: yanıtlarda öğrenci adı, numarası veya tek bir öğrencinin notu yoktur. Bir
dersin bir dönemdeki sonucu yalnızca harf notu başına öğrenci sayısıdır (`notDagilimi`). Ortalama, geçme oranı ve
program çıktısına göre özet istemcide bu dağılımdan hesaplanır.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `sinav-analizi-otomasyonu` istemcisine verilen JWT, `typ`
  `at+jwt`, `aud` `sinav-analizi-api`, `scope` içinde `sinav-analizi.oku` / `sinav-analizi.yaz`. API token'ı kendisi
  doğrular.
- **Kapsam filtresini API yapar.** Akademisyen uç noktası yalnızca **hocası olduğu** derslerin sonuçlarını döndürür:
  dersin hocası kişiye bağlı kayıt verisidir, rol değildir. Bölüm uç noktası kullanıcının bölümüyle süzülür. Mock veri
  kaynağı aynı kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular.
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.

## Şartlar

Geçme notu ve başarı hedefi koda gömülmez; otomasyonun `appsettings.json`'ında `SinavSartlari:GecmeNotu` (örnek `DD`;
bu harf ve üstü geçer) ve `SinavSartlari:BasariHedefiOrani` (örnek 0,75: geçme oranı bunun altındaysa "Hedef altında").
Değerler örnektir, gerçek yönetmelik değildir.

## JSON biçimi

camelCase alanlar.

- `notDagilimi`: `{ "harf", "sayi" }` dizisi; harfler `AA`, `BA`, `BB`, `CB`, `CC`, `DC`, `DD`, `FD`, `FF`.
- `programCiktilari`: dersin bağlı olduğu program çıktısı kodları (MÜDEK otomasyonundaki kodlarla aynı biçim; iki
  otomasyon birbirini çağırmaz).

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/sinav-analizi/derslerim` | `sinav-analizi.oku` | `akademisyen` | `DersSonucu[]` (hocası olduğu dersler) | Kendi derslerim |
| `GET /api/sinav-analizi/bolum-dersleri` | `sinav-analizi.oku` | `bolum_baskani`, `mudek_komisyonu` | `DersSonucu[]` (bölüm) | Bölüm geneli, Çıktı bazlı analiz |

`sinav-analizi.yaz` kapsamı gelecekteki işlevler (ör. rapor isteği) için ayrılmıştır; ekranlar şu an temsilidir.

## Örnek

`GET /api/sinav-analizi/derslerim` (200):

```json
[
  {
    "id": "S02",
    "dersKodu": "BM101",
    "dersAdi": "Programlamaya Giriş",
    "hocaKullaniciAdi": "hoca.test",
    "hocaAdi": "Test Akademisyen",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "donem": "2025-2026 Güz",
    "notDagilimi": [
      { "harf": "AA", "sayi": 8 }, { "harf": "BA", "sayi": 9 }, { "harf": "BB", "sayi": 11 },
      { "harf": "CB", "sayi": 10 }, { "harf": "CC", "sayi": 7 }, { "harf": "DC", "sayi": 4 },
      { "harf": "DD", "sayi": 3 }, { "harf": "FD", "sayi": 2 }, { "harf": "FF", "sayi": 4 }
    ],
    "programCiktilari": ["PÇ-1", "PÇ-2"]
  }
]
```

Örneklerdeki hocalar uydurma test kullanıcıları veya örnek veridir; sayılar uydurmadır.
