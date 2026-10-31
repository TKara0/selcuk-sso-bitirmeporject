# Bölüm Kurul Karar API sözleşmesi

**Durum: sözleşme.** Bölüm Kurul Karar API'si henüz yazılmadı. Bölüm Kurul Karar Otomasyonu
(`src/Selcuk.Otomasyon.BolumKurul`, https://localhost:5305) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`)
çalışır. API yazıldığında yalnızca ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<bolum-kurul-api-adresi>/" }`.
İstemci tarafı hazırdır (`ApiBolumKurulVeriKaynagi`); DTO'lar `src/Selcuk.Otomasyon.BolumKurul/Sozlesme/BolumKurulSozlesmesi.cs`'te.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: `bolum-kurul-otomasyonu` istemcisine verilen JWT, `typ`
  `at+jwt`, `aud` `bolum-kurul-api`, `scope` içinde `bolum-kurul.oku` / `bolum-kurul.yaz`. API token'ı kendisi doğrular.
- Token'da bölüm ve fakülte yoktur; API bunları `sub` ile kendi kullanıcı kaydından okur.
- **Kapsam filtresini API yapar.** `bolum_baskani`, `bolum_kurulu_uyesi` ve `bolum_sekreteri` uç noktaları kullanıcının
  bölümünün toplantılarını, `fakulte_sekreteri` uç noktası kullanıcının fakültesinin kararlarını döndürür. Mock veri
  kaynağı aynı kuralı oturumdaki `bolum` ve `fakulte` claim'leriyle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401; istemcinin hata sayfaları diğer otomasyonlardaki gibi.

## Şartlar

Toplantı yeter sayısı ve karar çoğunluğu koda gömülmez; otomasyonun `appsettings.json`'ında
`ToplantiSartlari:YeterSayiOrani` (örnek 0,5: katılan üye sayısı, üye tamsayısının bu oranını aşmalı) ve
`ToplantiSartlari:KararCogunluguOrani` (örnek 0,5: kabul oyu, katılanların bu oranını aşmalı). Değerler örnektir,
gerçek yönetmelik değildir. API ham sayıları (üye, katılan, oylar) döndürür; göstergeler istemcide hesaplanır.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`.

- Toplantı `durum`: `planlandi`, `yapildi`, `ertelendi`
- Madde `durum`: `gundemde`, `oylamaAcik`, `kabulEdildi`, `reddedildi`, `ertelendi`; karar verilen maddede `kararNo`

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/bolum-kurul/toplantilar` | `bolum-kurul.oku` | `bolum_baskani`, `bolum_kurulu_uyesi`, `bolum_sekreteri` | `Toplanti[]` (bölüm, maddeleriyle) | Toplantı ve gündem, Gündem, Oylama, Tutanak ve arşiv |
| `POST /api/bolum-kurul/toplantilar` | `bolum-kurul.yaz` | `bolum_baskani` | 201 + `Toplanti` | Toplantı ve gündem |
| `POST /api/bolum-kurul/toplantilar/{id}/maddeler` | `bolum-kurul.yaz` | `bolum_baskani` | 201 + `GundemMaddesi` | Toplantı ve gündem |
| `POST /api/bolum-kurul/maddeler/{id}/oy` | `bolum-kurul.yaz` | `bolum_kurulu_uyesi` | 204 | Oylama |
| `GET /api/bolum-kurul/fakulte-kararlari` | `bolum-kurul.oku` | `fakulte_sekreteri` | `Toplanti[]` (fakülte, yapılmış toplantılar) | Gelen kararlar |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir (oy ve kayıtlar değişmez).

## Örnek

`GET /api/bolum-kurul/toplantilar` (200, kısaltılmış):

```json
[
  {
    "id": "T26-03",
    "no": "2026/11",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "tarih": "2026-11-05",
    "yer": "Örnek Toplantı Salonu A-101",
    "durum": "planlandi",
    "uyeSayisi": 11,
    "katilanSayisi": 7,
    "maddeler": [
      { "id": "T26-03-1", "sira": 1, "konu": "Bahar dönemi ders programı", "durum": "oylamaAcik", "kabul": 4, "ret": 2, "cekimser": 1, "kararNo": null }
    ],
    "tutanakHazir": false
  }
]
```

`POST /api/bolum-kurul/maddeler/{id}/oy` gövdesi: `{ "oy": "kabul" }` (`kabul`, `ret`, `cekimser`); oyu veren token'dan
belirlenir.

Konular, yerler ve toplantı numaraları uydurmadır.
