# MÜDEK API sözleşmesi

**Durum: sözleşme.** MÜDEK API'si henüz yazılmadı. MÜDEK Otomasyonu (`src/Selcuk.Otomasyon.Mudek`,
https://localhost:5308) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<mudek-api-adresi>/" }`. İstemci tarafı hazırdır
(`ApiMudekVeriKaynagi`); DTO'lar `src/Selcuk.Otomasyon.Mudek/Sozlesme/MudekSozlesmesi.cs`'te.

Program çıktısı adları örnektir ("Örnek program çıktısı N"); gerçek akreditasyon metni değildir. Otomasyon MÜDEK'in
sistemlerine bağlanmaz.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `mudek-otomasyonu` istemcisine verilen JWT, `typ` `at+jwt`,
  `aud` `mudek-api`, `scope` içinde `mudek.oku` / `mudek.yaz`. API token'ı kendisi doğrular.
- **Kapsam filtresini API yapar.** Akademisyen uç noktası yalnızca **hocası olduğu** derslerin çıktılarını döndürür:
  dersin hocası kişiye bağlı kayıt verisidir, rol değildir. Program çıktıları, ders çıktıları ve belgeler kullanıcının
  bölümüyle süzülür. Mock veri kaynağı aynı kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle
  uygular.
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.

## Şartlar

Ders çıktısı başarı eşiği ve program çıktısı hedef oranı koda gömülmez; otomasyonun `appsettings.json`'ında
`MudekSartlari:DersCiktisiBasariEsigi` (örnek 0,6) ve `MudekSartlari:ProgramCiktisiHedefOrani` (örnek 0,7). Program
çıktısının gerçekleşmesi, bağlı ve ölçülmüş ders çıktılarının ortalamasıdır (istemcide hesaplanır). Değerler örnektir,
gerçek akreditasyon ölçütü değildir.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; oranlar 0-1 arası ondalık.

- `tur` (belge): `donemRaporu`, `ozDegerlendirme`, `dersDosyasi`, `tutanak`
- `durum` (belge): `hazirlaniyor`, `tamamlandi`, `arsivlendi`
- Ölçümü girilmemiş ders çıktısında `olculenOran` `null`'dır; `programCiktilari` bağlı program çıktısı kodlarıdır.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/mudek/ders-ciktilarim` | `mudek.oku` | `akademisyen` | `DersCiktisi[]` (hocası olduğu dersler) | Ders çıktısı veri girişi |
| `POST /api/mudek/ders-ciktilari/{id}/olcum` | `mudek.yaz` | `akademisyen` (yalnızca kendi dersi) | 204 | Ders çıktısı veri girişi |
| `GET /api/mudek/program-ciktilari` | `mudek.oku` | `mudek_komisyonu`, `bolum_baskani` | `ProgramCiktisi[]` (bölüm) | Program çıktıları, Eksikler, Raporlar |
| `GET /api/mudek/ders-ciktilari` | `mudek.oku` | `mudek_komisyonu`, `bolum_baskani` | `DersCiktisi[]` (bölüm) | Program çıktıları, Eksikler, Raporlar |
| `GET /api/mudek/belgeler` | `mudek.oku` | `bolum_sekreteri` | `MudekBelgesi[]` (bölüm) | Rapor ve evrak arşivi |

`POST` uç noktası gelecekteki işlev içindir; ekranlar şu an temsilidir.

## Örnek

`GET /api/mudek/ders-ciktilarim` (200):

```json
[
  {
    "id": "BM101-1",
    "dersKodu": "BM101",
    "dersAdi": "Programlamaya Giriş",
    "hocaKullaniciAdi": "hoca.test",
    "hocaAdi": "Test Akademisyen",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "donem": "2025-2026",
    "ciktiKodu": "DÇ-1",
    "aciklama": "Örnek ders çıktısı 1",
    "programCiktilari": ["PÇ-1", "PÇ-2"],
    "olculenOran": 0.78
  }
]
```

`POST /api/mudek/ders-ciktilari/{id}/olcum` gövdesi: `{ "olculenOran": 0.72 }`.

Örneklerdeki hocalar uydurma test kullanıcıları veya örnek veridir; ders ve program çıktısı metinleri örnektir.
