# Lisans Projeleri API sözleşmesi

**Durum: sözleşme.** Lisans Projeleri API'si henüz yazılmadı. Lisans Projeleri Otomasyonu
(`src/Selcuk.Otomasyon.LisansProjeleri`, https://localhost:5309) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` =
`Mock`) çalışır. API yazıldığında yalnızca ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<lisans-projeleri-api-adresi>/" }`.
İstemci tarafı hazırdır (`ApiLisansProjeleriVeriKaynagi`); DTO'lar
`src/Selcuk.Otomasyon.LisansProjeleri/Sozlesme/LisansProjeleriSozlesmesi.cs`'te.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `lisans-projeleri-otomasyonu` istemcisine verilen JWT, `typ`
  `at+jwt`, `aud` `lisans-projeleri-api`, `scope` içinde `lisans-projeleri.oku` / `lisans-projeleri.yaz`. API token'ı
  kendisi doğrular.
- **Kapsam filtresini API yapar.** Öğrenci uç noktası yalnızca `sub`'ın kendi proje kayıtlarını döndürür. Akademisyen
  uç noktaları yalnızca **kendi önerdiği** konuları ve **hocası olduğu** projeleri döndürür: konunun ve projenin hocası
  kişiye bağlı kayıt verisidir, rol değildir. Konular ve bölüm başkanının eşleştirme listesi kullanıcının bölümüyle
  süzülür. Mock veri kaynağı aynı kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular.
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.

## Şartlar

Hoca başına bir dönemde en fazla proje ve öğrenci başına konu tercihi sayısı koda gömülmez; otomasyonun
`appsettings.json`'ında `ProjeSartlari:HocaBasinaEnFazlaProje` (örnek 3) ve `ProjeSartlari:OgrenciBasinaTercihSayisi`
(örnek 3). Değerler örnektir, gerçek yönetmelik değildir.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin.

- `asama`: `konuSecimi`, `onerge`, `araRapor`, `teslim`, `sunumDegerlendirme`, `tamamlandi`
- Aşamalar: Konu seçimi → Önerge → Ara rapor → Teslim → Sunum ve değerlendirme.
- Konu seçimindeki kayıtta `konuId`, `konuBasligi`, `hocaKullaniciAdi`, `hocaAdi` `null`'dır; `tercihler` sıralı konu
  kimlikleridir. `not` yalnızca tamamlanan projede bulunur.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/lisans-projeleri/konular` | `lisans-projeleri.oku` | `ogrenci`, `bolum_baskani` | `ProjeKonusu[]` (bölüm, içinde bulunulan dönem) | Konu seçimi, Eşleştirme |
| `GET /api/lisans-projeleri/projelerim` | `lisans-projeleri.oku` | `ogrenci` | `LisansProjesi[]` (yalnızca kendi) | Konu seçimi, Proje aşamalarım |
| `POST /api/lisans-projeleri/tercihlerim` | `lisans-projeleri.yaz` | `ogrenci` | 204 | Konu seçimi |
| `GET /api/lisans-projeleri/konularim` | `lisans-projeleri.oku` | `akademisyen` | `ProjeKonusu[]` (kendi önerdikleri) | Konu önerisi |
| `POST /api/lisans-projeleri/konular` | `lisans-projeleri.yaz` | `akademisyen` | 201 + `ProjeKonusu` | Konu önerisi |
| `GET /api/lisans-projeleri/danismanligimdaki-projeler` | `lisans-projeleri.oku` | `akademisyen` | `LisansProjesi[]` (hocası olduğu) | Değerlendirme |
| `POST /api/lisans-projeleri/projeler/{id}/degerlendirme` | `lisans-projeleri.yaz` | `akademisyen` (yalnızca kendi projesi) | 204 | Değerlendirme |
| `GET /api/lisans-projeleri/projeler` | `lisans-projeleri.oku` | `bolum_baskani` | `LisansProjesi[]` (bölüm, içinde bulunulan dönem) | Eşleştirme |
| `POST /api/lisans-projeleri/projeler/{id}/eslestirme` | `lisans-projeleri.yaz` | `bolum_baskani` | 204 | Eşleştirme |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir.

## Örnek

`GET /api/lisans-projeleri/projelerim` (200):

```json
[
  {
    "id": "P26-02",
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "donem": "2026-2027",
    "asama": "konuSecimi",
    "konuId": null,
    "konuBasligi": null,
    "hocaKullaniciAdi": null,
    "hocaAdi": null,
    "tercihler": ["L-K03", "L-K01", "L-K06"],
    "not": null
  }
]
```

`POST /api/lisans-projeleri/projeler/{id}/degerlendirme` gövdesi: `{ "rubrik": { "onerge": 80, "araRapor": 75, "sunum": 90 }, "not": 82 }`.

Örneklerdeki öğrenciler ve hocalar uydurma test kullanıcıları veya örnek veridir; konu başlıkları uydurmadır.
