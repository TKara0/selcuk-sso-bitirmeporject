# Mezuniyet API sözleşmesi

**Durum: sözleşme.** Mezuniyet API'si henüz yazılmadı. Mezuniyet Otomasyonu (`src/Selcuk.Otomasyon.Mezuniyet`,
https://localhost:5304) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir:

```json
"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<mezuniyet-api-adresi>/" }
```

İstemci tarafı hazırdır: `ApiMezuniyetVeriKaynagi` aşağıdaki uç noktaları çağırır, DTO'lar
`src/Selcuk.Otomasyon.Mezuniyet/Sozlesme/MezuniyetSozlesmesi.cs`'tedir (Mock ve Api aynı türleri kullanır).

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır. Token kimlik sunucusunun (`https://localhost:5001/`)
  `mezuniyet-otomasyonu` istemcisine verdiği imzalı JWT'dir: `typ` `at+jwt`, `aud` `mezuniyet-api`, `scope` içinde
  `mezuniyet.oku` (okuma) ve `mezuniyet.yaz` (yazma). API token'ı kendisi doğrular (JWKS, `iss`, `aud`, `typ`, süre).
- Token'da `sub`, `role`, `scope`, `sid`, `client_id` vardır; ad, bölüm ve fakülte yoktur. API bunları `sub` ile kendi
  kullanıcı kaydından okur.
- **Kapsam filtresini API yapar.** Öğrenci uç noktaları yalnızca `sub`'ın kendi kaydını, `mezuniyet_komisyonu` ve
  `bolum_sekreteri` uç noktası kullanıcının bölümünü, `fakulte_sekreteri` uç noktası kullanıcının fakültesini döndürür.
  Mock veri kaynağı aynı kuralı oturumdaki claim'lerle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401. İstemci 401/403'te "Bu verilere erişim yetkin yok", bağlantı
  hatası, zaman aşımı (5 sn) veya 5xx'te "Veri kaynağına şu an ulaşılamıyor" sayfasını gösterir.

## Şartlar ve uygulamalar arası veri

- Gereken toplam AKTS ve GNO alt sınırı yönetmeliğe bağlıdır; koda gömülmez, otomasyonun `appsettings.json`'ındadır
  (`MezuniyetSartlari:GerekenAkts` örnek 240, `MezuniyetSartlari:GnoAltSiniri` örnek 2,00). API ham veriyi (AKTS, GNO,
  zorunlu dersler, staj) döndürür; eksikler listesi ve kontrol sonuçları (`MezuniyetKontrolu`) bu ayarlarla istemcide
  hesaplanır.
- **Staj durumu (`staj`) şimdilik örnek veridir; ileride Staj API'sinden (`staj-api`, [staj.md](staj.md)) alınacak.**
  Mezuniyet Otomasyonu Staj Otomasyonu'nu veya Staj API'sini çağırmaz; birleştirmeyi Mezuniyet API'si yapacaktır.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`; GNO ondalık sayı (`2.65`).

- `durum`: `basvuruYapilmadi`, `komisyonKontrolunde`, `eksikBildirildi`, `bolumOnayinda`, `dekanlikta`,
  `diplomaHazirlaniyor`, `diplomaHazir`
- `staj`: `tamamlandi`, `devamEdiyor`, `eksik`
- İlişik kesmede birim `durum`: `tamam`, `bekliyor`, `borclu`
- Aşamalar: Başvuru → Komisyon kontrolü → Bölüm onayı → Dekanlık → Diploma hazırlığı.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/mezuniyet/durumum` | `mezuniyet.oku` | `ogrenci` | `MezuniyetAdayi[]` (kendi kaydı; yoksa boş) | Mezuniyet durumum, Mezuniyet başvurusu |
| `POST /api/mezuniyet/basvurular` | `mezuniyet.yaz` | `ogrenci` | 201 + `MezuniyetAdayi` | Mezuniyet başvurusu |
| `GET /api/mezuniyet/ilisik-kesme` | `mezuniyet.oku` | `ogrenci` | `IlisikKesmeBirimi[]` | İlişik kesme |
| `GET /api/mezuniyet/adaylar?durum=…` | `mezuniyet.oku` | `mezuniyet_komisyonu`, `bolum_sekreteri` | `MezuniyetAdayi[]` (bölüm) | Mezuniyet adayları, Dekanlığa gidecek liste |
| `POST /api/mezuniyet/adaylar/{id}/karar` | `mezuniyet.yaz` | `mezuniyet_komisyonu` | 204 | Mezuniyet adayları |
| `POST /api/mezuniyet/dekanlik-listesi` | `mezuniyet.yaz` | `bolum_sekreteri` | 204 | Dekanlığa gidecek liste |
| `GET /api/mezuniyet/fakulte-listeleri` | `mezuniyet.oku` | `fakulte_sekreteri` | `MezuniyetAdayi[]` (fakülte; dekanlıkta, diploması hazırlanan ve hazır) | Gelen mezuniyet listeleri |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir (veri değişmez).

## Örnekler

`GET /api/mezuniyet/durumum` (200):

```json
[
  {
    "id": "M27-02",
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "donem": "2026-2027 Güz",
    "toplamAkts": 236,
    "gno": 2.65,
    "zorunluDersler": [
      { "kod": "BM101", "ad": "Programlamaya Giriş", "tamamlandi": true },
      { "kod": "BM401", "ad": "Bitirme Projesi", "tamamlandi": false }
    ],
    "staj": "devamEdiyor",
    "durum": "komisyonKontrolunde",
    "basvuruTarihi": "2026-10-08",
    "komisyonNotu": null
  }
]
```

`GET /api/mezuniyet/ilisik-kesme` (200):
`[{ "birim": "Kütüphane", "durum": "borclu", "aciklama": "İade edilmemiş 1 kitap." }, { "birim": "Laboratuvar", "durum": "bekliyor", "aciklama": null }]`

`POST /api/mezuniyet/basvurular` gövdesi (`MezuniyetBasvuruFormu`):
`{ "donem": "2026-2027 Güz", "iletisimAdresi": "…", "diplomaTeslimi": "sahsen" }`.
`POST /api/mezuniyet/adaylar/{id}/karar` gövdesi: `{ "karar": "onayla" }` veya `{ "karar": "eksikBildir", "not": "…" }`.

Örneklerdeki öğrenciler kimlik sunucusunun uydurma test kullanıcılarıdır.
