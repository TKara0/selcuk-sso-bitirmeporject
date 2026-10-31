# İŞKUR API sözleşmesi

**Durum: sözleşme.** İŞKUR API'si henüz yazılmadı. İŞKUR Otomasyonu (`src/Selcuk.Otomasyon.Iskur`,
https://localhost:5307) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<iskur-api-adresi>/" }`. İstemci tarafı hazırdır
(`ApiIskurVeriKaynagi`); DTO'lar `src/Selcuk.Otomasyon.Iskur/Sozlesme/IskurSozlesmesi.cs`'te.

Bu otomasyon üniversitenin kendi devam takibini temsil eder; İŞKUR'un veya başka bir kurumun sistemine bağlanmaz.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `iskur-otomasyonu` istemcisine verilen JWT, `typ` `at+jwt`,
  `aud` `iskur-api`, `scope` içinde `iskur.oku` / `iskur.yaz`. API token'ı kendisi doğrular.
- **Kapsam filtresini API yapar.** Öğrenci uç noktası yalnızca `sub`'ın kendi devam kayıtlarını döndürür. İŞKUR
  sorumlusunun uç noktaları kullanıcının **bölümüyle** süzülür (`iskur_sorumlusu` bölüm kapsamlı roldür; Development'taki
  `iskur.test` Teknoloji Fakültesi / Bilgisayar Mühendisliği'ndedir). Bölümü olmayan sorumlu boş liste alır. Mock veri
  kaynağı aynı kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular.
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.

## Şartlar

Aylık en fazla devam saati ve devamsızlık uyarı eşiği koda gömülmez; otomasyonun `appsettings.json`'ında
`IskurSartlari:AylikEnFazlaDevamSaati` (örnek 60) ve `IskurSartlari:DevamsizlikUyariEsigi` (örnek 0,8: devam oranı bunun
altındaysa uyarı). Değerler örnektir, gerçek mevzuat değildir.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; ay `yyyy-MM`, tarih `yyyy-MM-dd`.

- `durum`: `giriliyor`, `onaylandi`, `odendi`
- Aşamalar: Devam girişi → Sorumlu onayı → Ödeme.
- Devam oranı istemcide hesaplanır: `devamSaati / planlananSaat`.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/iskur/devamim` | `iskur.oku` | `ogrenci` | `AylikDevam[]` (yalnızca kendi) | Devam durumum |
| `GET /api/iskur/devam` | `iskur.oku` | `iskur_sorumlusu` | `AylikDevam[]` (bölüm) | Devam girişi, Takip |
| `POST /api/iskur/devam` | `iskur.yaz` | `iskur_sorumlusu` (yalnızca kendi bölümü) | 204 | Devam girişi |

`POST` uç noktası gelecekteki işlev içindir; ekranlar şu an temsilidir.

## Örnek

`GET /api/iskur/devamim` (200):

```json
[
  {
    "id": "I26-10",
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "ay": "2026-10",
    "planlananSaat": 60,
    "devamSaati": 52,
    "durum": "giriliyor"
  }
]
```

`POST /api/iskur/devam` gövdesi: `{ "ogrenciNo": "203011002", "tarih": "2026-10-15", "saat": 4 }`.

Örneklerdeki öğrenciler uydurma test kullanıcıları veya örnek veridir.
