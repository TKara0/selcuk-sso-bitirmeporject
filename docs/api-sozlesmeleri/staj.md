# Staj API sözleşmesi

**Durum: sözleşme.** Staj API'si henüz yazılmadı. Staj Otomasyonu (`src/Selcuk.Otomasyon.Staj`, https://localhost:5302)
varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca ayar değişir:

```json
"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<staj-api-adresi>/" }
```

İstemci tarafı hazırdır: `ApiStajVeriKaynagi` aşağıdaki uç noktaları çağırır, DTO'lar
`src/Selcuk.Otomasyon.Staj/Sozlesme/StajSozlesmesi.cs`'tedir (Mock ve Api aynı türleri kullanır).

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır. Token kimlik sunucusunun (`https://localhost:5001/`)
  `staj-otomasyonu` istemcisine verdiği imzalı JWT'dir: `typ` `at+jwt`, `aud` `staj-api`, `scope` içinde `staj.oku`
  (okuma) ve `staj.yaz` (yazma). API token'ı Demo API gibi kendisi doğrular (JWKS ile imza, `iss`, `aud`, `typ`, süre).
- Token'daki claim'ler: `sub`, `role` (tek değer veya JSON dizisi), `scope`, `sid`, `client_id`. Ad, kullanıcı adı,
  bölüm ve fakülte access token'da yoktur (veri minimizasyonu); API bunları `sub` ile kendi kullanıcı kaydından (veya
  kimlik sunucusunun userinfo'sundan) okur.
- **Kapsam filtresini API yapar.** İstemci kullanıcı, bölüm veya öğrenci numarası göndermez; gönderse de API buna
  güvenmez. Öğrenci uç noktaları yalnızca `sub`'ın kendi kayıtlarını, diğerleri yalnızca kullanıcının bölümünün
  (`bolum_baskani`, `bolum_sekreteri`, `staj_komisyonu`) kayıtlarını döndürür. Mock veri kaynağı aynı kuralı
  oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401 (gövdede ayrıntı yok, `IncludeErrorDetails = false`). İstemci
  401/403'te "Bu verilere erişim yetkin yok", bağlantı hatası, zaman aşımı (5 sn) veya 5xx'te "Veri kaynağına şu an
  ulaşılamıyor" sayfasını gösterir.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`.

- `tur`: `stajI`, `stajII`, `ime` (İşletmede Mesleki Eğitim)
- `durum`: `basvuruBekliyor`, `onaylandi`, `devamEdiyor`, `defterTeslimEdildi`, `degerlendirildi`, `reddedildi`

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/staj/basvurularim` | `staj.oku` | `ogrenci` | `StajKaydi[]` (yalnızca kendi) | Stajlarım, Sonuç |
| `GET /api/staj/defterim` | `staj.oku` | `ogrenci` | `StajDefteri` (güncel staj ve girişleri) | Staj defterim |
| `POST /api/staj/basvurular` | `staj.yaz` | `ogrenci` | 201 + `StajKaydi` | Staj başvurusu |
| `POST /api/staj/defterim/girisler` | `staj.yaz` | `ogrenci` | 201 + `DefterGirisi` | Staj defterim |
| `GET /api/staj/basvurular?durum=…` | `staj.oku` | `staj_komisyonu`, `bolum_baskani`, `bolum_sekreteri` | `StajKaydi[]` (bölüm) | Başvuru inceleme |
| `POST /api/staj/basvurular/{id}/karar` | `staj.yaz` | `staj_komisyonu` | 204 | Başvuru inceleme |
| `GET /api/staj/defterler` | `staj.oku` | `staj_komisyonu` | `StajKaydi[]` (teslim edilmiş defterler, bölüm) | Defter değerlendirme |
| `POST /api/staj/defterler/{id}/degerlendirme` | `staj.yaz` | `staj_komisyonu` | 204 | Defter değerlendirme |
| `GET /api/staj/ozet?yil=…` | `staj.oku` | `staj_komisyonu`, `bolum_baskani` | `StajOzeti` (bölüm) | Yıllara göre takip, Özet |
| `GET /api/staj/evraklar` | `staj.oku` | `bolum_sekreteri` | `StajEvragi[]` (bölüm) | Evrak takibi |

`POST` uç noktaları gelecekteki işlevler içindir: ekranlar şu an temsilidir; formlar antiforgery korumalı POST yapar ama
sunucu veriyi değiştirmez ("Bu ekran temsilidir; işlem kaydedilmedi."). İşlevler eklendiğinde `IStajVeriKaynagi`'ye
yazma yöntemleri eklenir ve sayfalar bunları çağırır.

## Örnekler

`GET /api/staj/basvurularim` (200):

```json
[
  {
    "id": "S26-01",
    "ogrenciNo": "203011001",
    "ogrenciAdi": "Test Öğrenci",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "tur": "stajII",
    "firmaAdi": "Deneme Elektronik Ltd.",
    "firmaAdresi": "Deneme Mah. Kurgu Sok. No: 7, Konya",
    "baslangic": "2026-07-06",
    "bitis": "2026-07-31",
    "isGunu": 20,
    "durum": "defterTeslimEdildi",
    "basvuruTarihi": "2026-05-11",
    "kabulEdilenGun": null,
    "komisyonNotu": null
  }
]
```

`GET /api/staj/defterim` (200; defter tutulacak staj yoksa `{"staj": null, "girisler": []}`):

```json
{
  "staj": { "id": "S26-01", "tur": "stajII", "durum": "defterTeslimEdildi", "...": "StajKaydi alanları" },
  "girisler": [
    { "stajId": "S26-01", "tarih": "2026-07-06", "calisma": "Firmanın yazılım geliştirme süreci ve kullanılan araçlar tanıtıldı." }
  ]
}
```

`POST /api/staj/basvurular` gövdesi (`StajBasvurusu`; öğrenci token'dan belirlenir):

```json
{
  "tur": "ime",
  "firmaAdi": "Örnek Yazılım A.Ş.",
  "firmaAdresi": "Örnek Mah. Deneme Cad. No: 1, Konya",
  "baslangic": "2027-02-15",
  "bitis": "2027-05-28",
  "isGunu": 70
}
```

`POST /api/staj/basvurular/{id}/karar` gövdesi: `{ "karar": "onayla" }` veya `{ "karar": "reddet", "not": "…" }`.

`POST /api/staj/defterler/{id}/degerlendirme` gövdesi: `{ "kabulEdilenGun": 18, "not": "İki günün girişi eksik." }`.

`GET /api/staj/ozet?yil=2026` (200):

```json
{
  "yil": 2026,
  "aktifStaj": 3,
  "bekleyenBasvuru": 3,
  "tamamlanan": 2,
  "yillar": [
    { "yil": 2026, "tur": "stajII", "basvuru": 6, "onay": 4, "tamamlanan": 1 }
  ]
}
```

Tanımlar: `aktifStaj` onaylanmış veya devam eden staj; `bekleyenBasvuru` başvurusu bekleyen; `tamamlanan`
değerlendirilmiş ve bitişi `yil` içinde olan; `yillar` stajın başlangıç yılına ve türüne göre (`onay`: başvurusu
bekleyen ve reddedilen dışındakiler, `tamamlanan`: değerlendirilmiş).

`GET /api/staj/evraklar` (200):

```json
[
  {
    "stajId": "S26-05",
    "ogrenciNo": "203011003",
    "ogrenciAdi": "Test Öğrenci Üç",
    "tur": "stajII",
    "durum": "onaylandi",
    "kabulBelgesi": true,
    "sgkGirisi": false,
    "defterTeslim": false
  }
]
```

Örneklerdeki öğrenciler kimlik sunucusunun uydurma test kullanıcılarıdır; firma adları ve adresleri uydurmadır.
