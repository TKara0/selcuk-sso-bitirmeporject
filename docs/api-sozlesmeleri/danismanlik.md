# Öğrenci Danışmanlık API sözleşmesi

**Durum: sözleşme.** Öğrenci Danışmanlık API'si henüz yazılmadı. Öğrenci Danışmanlık Otomasyonu
(`src/Selcuk.Otomasyon.Danismanlik`, https://localhost:5306) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` =
`Mock`) çalışır. API yazıldığında yalnızca ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<danismanlik-api-adresi>/" }`.
İstemci tarafı hazırdır (`ApiDanismanlikVeriKaynagi`); DTO'lar
`src/Selcuk.Otomasyon.Danismanlik/Sozlesme/DanismanlikSozlesmesi.cs`'te.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `danismanlik-otomasyonu` istemcisine verilen JWT, `typ`
  `at+jwt`, `aud` `danismanlik-api`, `scope` içinde `danismanlik.oku` / `danismanlik.yaz`. API token'ı kendisi doğrular.
- **Kapsam filtresini API yapar.** Öğrenci uç noktaları yalnızca `sub`'ın kendi kaydını ve taleplerini döndürür.
  Danışman uç noktaları yalnızca **danışmanı olduğu** öğrencileri ve **kendi yazdığı** görüşme notlarını döndürür:
  danışman-öğrenci ilişkisi kişiye bağlı kayıt verisidir, rol değildir. Bölüm başkanının listesi kullanıcının bölümüyle
  süzülür. Mock veri kaynağı aynı kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular.
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.
- Görüşme notları genel ve akademiktir (ders seçimi, akademik ilerleme, staj ve kariyer planı); sağlık, aile veya
  ekonomik durum gibi kişisel konular bu sisteme yazılmaz.

## Şartlar

Danışman başına en fazla öğrenci ve görüşme aralığı hedefi koda gömülmez; otomasyonun `appsettings.json`'ında
`DanismanlikSartlari:DanismanBasinaEnFazlaOgrenci` (örnek 5) ve `DanismanlikSartlari:GorusmeAraligiHedefiGun` (örnek
120). Değerler örnektir, gerçek yönetmelik değildir.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`.

- `konu`: `dersSecimi`, `akademikIlerleme`, `stajPlani`, `kariyerPlani`
- `durum` (görüşme talebi): `bekliyor`, `onaylandi`, `yapildi`, `reddedildi`
- Aşamalar: Talep → Danışman onayı → Görüşme yapıldı.
- Danışmanı atanmamış öğrencide `danismanKullaniciAdi` ve `danismanAdi` `null`'dır; hiç görüşmesi olmayan öğrencide
  `sonGorusme` `null`'dır.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/danismanlik/kaydim` | `danismanlik.oku` | `ogrenci` | `DanismanlikOgrencisi[]` (yalnızca kendi; yoksa boş) | Danışmanım |
| `GET /api/danismanlik/taleplerim` | `danismanlik.oku` | `ogrenci` | `GorusmeTalebi[]` (yalnızca kendi) | Görüşme talebi |
| `POST /api/danismanlik/talepler` | `danismanlik.yaz` | `ogrenci` | 201 + `GorusmeTalebi` | Görüşme talebi |
| `GET /api/danismanlik/ogrencilerim` | `danismanlik.oku` | `danisman` | `DanismanlikOgrencisi[]` (danışmanı olduğu) | Öğrenci listesi, Görüşme notları |
| `GET /api/danismanlik/gorusme-notlarim` | `danismanlik.oku` | `danisman` | `GorusmeNotu[]` (kendi yazdıkları) | Görüşme notları |
| `POST /api/danismanlik/gorusme-notlari` | `danismanlik.yaz` | `danisman` (yalnızca kendi öğrencisi) | 201 + `GorusmeNotu` | Görüşme notları |
| `GET /api/danismanlik/bolum-ogrencileri` | `danismanlik.oku` | `bolum_baskani` | `DanismanlikOgrencisi[]` (bölüm) | Danışman atama |
| `POST /api/danismanlik/ogrenciler/{ogrenciNo}/danisman` | `danismanlik.yaz` | `bolum_baskani` | 204 | Danışman atama |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir.

## Örnek

`GET /api/danismanlik/kaydim` (200):

```json
[
  {
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "sinif": 4,
    "toplamAkts": 176,
    "gno": 2.65,
    "danismanKullaniciAdi": "hoca.test",
    "danismanAdi": "Test Akademisyen",
    "sonGorusme": "2026-05-20"
  }
]
```

`POST /api/danismanlik/talepler` gövdesi: `{ "konu": "dersSecimi", "aciklama": "Seçmeli ders planı" }`.

Örneklerdeki öğrenciler ve danışmanlar uydurma test kullanıcıları veya örnek veridir.
