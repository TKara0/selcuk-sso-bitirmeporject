# ÇAP-Yandal API sözleşmesi

**Durum: sözleşme.** ÇAP-Yandal API'si henüz yazılmadı. ÇAP-Yandal Otomasyonu (`src/Selcuk.Otomasyon.CapYandal`,
https://localhost:5311) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir:

```json
"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<cap-yandal-api-adresi>/" }
```

İstemci tarafı hazırdır: `ApiCapYandalVeriKaynagi` aşağıdaki uç noktaları çağırır, DTO'lar
`src/Selcuk.Otomasyon.CapYandal/Sozlesme/CapYandalSozlesmesi.cs`'tedir (Mock ve Api aynı türleri kullanır).

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır. Token kimlik sunucusunun (`https://localhost:5001/`)
  `cap-yandal-otomasyonu` istemcisine verdiği imzalı JWT'dir: `typ` `at+jwt`, `aud` `cap-yandal-api`, `scope` içinde
  `cap-yandal.oku` (okuma) ve `cap-yandal.yaz` (yazma). API token'ı kendisi doğrular (JWKS, `iss`, `aud`, `typ`, süre).
- Token'da `sub`, `role`, `scope`, `sid`, `client_id` vardır; ad, bölüm ve fakülte yoktur. API bunları `sub` ile kendi
  kullanıcı kaydından okur.
- **Kapsam filtresini API yapar.** Öğrenci uç noktaları yalnızca `sub`'ın kendi kayıtlarını döndürür.
  `cap_yandal_koordinatoru`, `bolum_baskani` ve `bolum_sekreteri` uç noktaları **programı açan bölüme** göre süzülür:
  başvurunun `bolum` ve `fakulte` alanları programın birimidir, `ogrenciBolumu` öğrencinin anadalıdır. Mock veri kaynağı
  aynı kuralı oturumdaki claim'lerle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401. İstemci 401/403'te "Bu verilere erişim yetkin yok", bağlantı
  hatası, zaman aşımı (5 sn) veya 5xx'te "Veri kaynağına şu an ulaşılamıyor" sayfasını gösterir.

## Şartlar

Başvuru GNO alt sınırı, başarı sırası (sınıfın ilk yüzde kaçı), devam GNO alt sınırı ve kontenjan yönetmeliğe bağlıdır;
koda gömülmez, otomasyonun `appsettings.json`'ındadır (`CapYandalSartlari:Cap:*` ve `CapYandalSartlari:Yandal:*`:
`BasvuruGnoAltSiniri`, `BasariYuzdesiUstSiniri` (verilmezse aranmaz), `DevamGnoAltSiniri`, `Kontenjan`; değerler
örnektir). API ham veriyi döndürür; şart göstergesi, kontenjan içi/yedek ve devam uyarısı bu ayarlarla istemcide
hesaplanır.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`; GNO ondalık sayı.

- `tur`: `cap` (çift anadal), `yandal`
- `durum`: `koordinatorDegerlendirmesinde`, `baskanOnayinda`, `kesinKayitBekliyor`, `kesinKayitYapildi`, `reddedildi`
- Aşamalar: Başvuru → Koordinatör değerlendirmesi → Bölüm başkanı onayı → Kesin kayıt.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/cap-yandal/programlar` | `cap-yandal.oku` | `ogrenci` | `CapProgrami[]` | Başvuru, Gelen başvurular |
| `GET /api/cap-yandal/akademik-bilgim` | `cap-yandal.oku` | `ogrenci` | `AkademikBilgi[]` (kendi GNO'su ve başarı sırası; yoksa boş) | Başvuru |
| `POST /api/cap-yandal/basvurular` | `cap-yandal.yaz` | `ogrenci` | 201 + `CapBasvurusu` | Başvuru |
| `GET /api/cap-yandal/basvurularim` | `cap-yandal.oku` | `ogrenci` | `CapBasvurusu[]` (yalnızca kendi) | Başvurularım |
| `GET /api/cap-yandal/ilerlemem` | `cap-yandal.oku` | `ogrenci` | `ProgramIlerlemesi[]` (kesin kaydı yapılmış) | Programdaki ilerlemem |
| `GET /api/cap-yandal/basvurular?durum=…` | `cap-yandal.oku` | `cap_yandal_koordinatoru`, `bolum_baskani`, `bolum_sekreteri` | `CapBasvurusu[]` (bölümün programları) | Gelen başvurular, Başvuru onayı, Evrak |
| `POST /api/cap-yandal/basvurular/{id}/onay` | `cap-yandal.yaz` | `bolum_baskani` | 204 | Başvuru onayı |
| `GET /api/cap-yandal/programdaki-ogrenciler` | `cap-yandal.oku` | `cap_yandal_koordinatoru` | `ProgramIlerlemesi[]` (bölümün programları) | Öğrenci takibi |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir (veri değişmez).

## Örnekler

`GET /api/cap-yandal/basvurularim` (200):

```json
[
  {
    "id": "C26-10",
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "ogrenciBolumu": "Bilgisayar Mühendisliği",
    "programKodu": "YAN-EEM",
    "programAdi": "Elektrik-Elektronik Mühendisliği Yandal",
    "tur": "yandal",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Elektrik-Elektronik Mühendisliği",
    "donem": "2026-2027 Güz",
    "gno": 2.80,
    "basariSirasi": 31,
    "sinifMevcudu": 80,
    "basvuruTarihi": "2026-08-24",
    "durum": "baskanOnayinda",
    "evrak": null,
    "not": null
  }
]
```

`GET /api/cap-yandal/akademik-bilgim` (200): `[{ "ogrenciNo": "203011002", "gno": 2.80, "basariSirasi": 31, "sinifMevcudu": 80 }]`

`GET /api/cap-yandal/ilerlemem` (200):

```json
[
  {
    "basvuru": { "id": "C25-04", "programKodu": "CAP-EEM", "tur": "cap", "durum": "kesinKayitYapildi", "...": "CapBasvurusu alanları" },
    "alinanDersler": [{ "kod": "EEM101", "ad": "Devre Teorisi I", "akts": 6, "not": "BB", "donem": "2025-2026 Güz" }],
    "kalanDersler": [{ "kod": "EEM201", "ad": "Elektronik I", "akts": 6, "not": null, "donem": null }],
    "donemlikGno": [{ "donem": "2025-2026 Güz", "gno": 3.00 }, { "donem": "2025-2026 Bahar", "gno": 2.83 }]
  }
]
```

Kesin kayıt evrakı (`evrak`): `{ "dilekce": true, "transkript": false, "anadalOnayi": false }`; kesin kayda
çağrılmamış başvuruda `null`.

`POST /api/cap-yandal/basvurular` gövdesi (`CapBasvuruFormu`): `{ "tur": "cap", "programKodu": "CAP-EEM" }`.

Örneklerdeki öğrenciler kimlik sunucusunun uydurma test kullanıcılarıdır; ders kodları ve notlar örnektir.
