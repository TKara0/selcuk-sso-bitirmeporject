# İntibak API sözleşmesi

**Durum: sözleşme.** İntibak API'si henüz yazılmadı. İntibak Otomasyonu (`src/Selcuk.Otomasyon.Intibak`,
https://localhost:5301) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir:

```json
"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<intibak-api-adresi>/" }
```

İstemci tarafı hazırdır: `ApiIntibakVeriKaynagi` aşağıdaki uç noktaları çağırır, DTO'lar
`src/Selcuk.Otomasyon.Intibak/Sozlesme/IntibakSozlesmesi.cs`'tedir (Mock ve Api aynı türleri kullanır).

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır. Token kimlik sunucusunun (`https://localhost:5001/`)
  `intibak-otomasyonu` istemcisine verdiği imzalı JWT'dir: `typ` `at+jwt`, `aud` `intibak-api`, `scope` içinde
  `intibak.oku` (okuma) ve `intibak.yaz` (yazma). API token'ı Demo API gibi kendisi doğrular (JWKS, `iss`, `aud`, `typ`,
  süre).
- Token'da `sub`, `role`, `scope`, `sid`, `client_id` vardır; ad, bölüm ve fakülte yoktur. API bunları `sub` ile kendi
  kullanıcı kaydından (veya userinfo'dan) okur.
- **Kapsam filtresini API yapar.** İstemci kullanıcı, bölüm veya öğrenci numarası göndermez. Öğrenci uç noktası yalnızca
  `sub`'ın kendi başvurularını; `intibak_komisyonu`, `bolum_baskani` ve `bolum_sekreteri` uç noktaları kullanıcının
  bölümünü; `fakulte_sekreteri` uç noktası kullanıcının fakültesini döndürür. Mock veri kaynağı aynı kuralı oturumdaki
  `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401 (gövdede ayrıntı yok). İstemci 401/403'te "Bu verilere erişim
  yetkin yok", bağlantı hatası, zaman aşımı (5 sn) veya 5xx'te "Veri kaynağına şu an ulaşılamıyor" sayfasını gösterir.

## Şartlar

Muafiyet için en düşük harf notu ve bir başvuruda muaf sayılabilecek en fazla AKTS yönetmeliğe bağlıdır; koda gömülmez,
otomasyonun `appsettings.json`'ındadır (`IntibakSartlari:MuafiyetIcinEnDusukNot`, `IntibakSartlari:EnFazlaMuafAkts`;
değerler örnektir). Ekranlar bu değerlerle önerilen kararı gösterir; kararın kendisi komisyonundur ve API'de saklanır.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`.

- `gelisTuru`: `yatayGecis`, `dikeyGecis`, `ekYerlestirme`, `af`
- `durum`: `komisyonIncelemesinde`, `geriGonderildi` (bölüm başkanı komisyona geri gönderdi), `baskanOnayinda`,
  `yonetimKurulunda`, `sonuclandi`, `reddedildi`
- Aşamalar: Başvuru → Komisyon incelemesi → Bölüm başkanı onayı → Yönetim kurulu → Sonuçlandı.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/intibak/basvurularim` | `intibak.oku` | `ogrenci` | `IntibakBasvurusu[]` (yalnızca kendi) | Başvurum, İntibak sonucu |
| `POST /api/intibak/basvurular` | `intibak.yaz` | `ogrenci` | 201 + `IntibakBasvurusu` (transkript `multipart/form-data`) | İntibak başvurusu |
| `GET /api/intibak/basvurular?durum=…` | `intibak.oku` | `intibak_komisyonu`, `bolum_baskani`, `bolum_sekreteri` | `IntibakBasvurusu[]` (bölüm) | Bekleyen başvurular, Ders eşleştirme, Komisyon kararları, Onay listesi, İntibak raporları |
| `GET /api/intibak/mufredat` | `intibak.oku` | `intibak_komisyonu` | `MufredatDersi[]` (kullanıcının bölümü) | Ders eşleştirme |
| `POST /api/intibak/basvurular/{id}/eslestirme` | `intibak.yaz` | `intibak_komisyonu` | 204 | Ders eşleştirme |
| `POST /api/intibak/basvurular/{id}/onay` | `intibak.yaz` | `bolum_baskani` | 204 | Onay listesi |
| `GET /api/intibak/yonetim-kurulu-evraki` | `intibak.oku` | `fakulte_sekreteri` | `IntibakBasvurusu[]` (fakülte, `yonetimKurulunda`) | Yönetim kurulu evrakı |

`POST` uç noktaları gelecekteki işlevler içindir: ekranlar şu an temsilidir; formlar antiforgery korumalı POST yapar ama
sunucu veriyi değiştirmez, transkript dosyası okunmaz ve kaydedilmez ("Bu ekran temsilidir; işlem kaydedilmedi.").

## Örnekler

`GET /api/intibak/basvurularim` (200):

```json
[
  {
    "id": "I26-01",
    "ogrenciNo": "203011002",
    "ogrenciAdi": "Test Öğrenci İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "gelisTuru": "yatayGecis",
    "oncekiUniversite": "Örnek Üniversitesi",
    "oncekiBolum": "Bilgisayar Mühendisliği",
    "basvuruTarihi": "2026-08-10",
    "durum": "baskanOnayinda",
    "dersler": [
      {
        "oncekiDersAdi": "Programlamaya Giriş", "oncekiAkts": 6, "oncekiNot": "BA",
        "esdegerKod": "BM101", "esdegerAd": "Programlamaya Giriş", "esdegerAkts": 6, "harfNotu": "BA", "muaf": true
      },
      {
        "oncekiDersAdi": "Teknik Resim", "oncekiAkts": 3, "oncekiNot": "BB",
        "esdegerKod": null, "esdegerAd": null, "esdegerAkts": null, "harfNotu": null, "muaf": false
      }
    ],
    "kararTarihi": null,
    "komisyonNotu": null
  }
]
```

Komisyon karar vermeden önce (`komisyonIncelemesinde`) derslerin `esdegerKod`, `esdegerAd`, `esdegerAkts`, `harfNotu`
ve `muaf` alanları `null`'dır.

`GET /api/intibak/mufredat` (200): `[{ "kod": "BM101", "ad": "Programlamaya Giriş", "akts": 6, "yariyil": 1 }]`

`POST /api/intibak/basvurular` (`multipart/form-data`): `gelisTuru`, `oncekiUniversite`, `oncekiBolum`, `transkript`
(PDF). `POST /api/intibak/basvurular/{id}/eslestirme` gövdesi: `{ "dersler": [{ "esdegerKod": "BM101", "muaf": true }] }`
(önceki derslerin sırasıyla). `POST /api/intibak/basvurular/{id}/onay` gövdesi: `{ "karar": "onayla" }` veya
`{ "karar": "geriGonder", "not": "…" }`.

Örneklerdeki öğrenciler kimlik sunucusunun uydurma test kullanıcılarıdır; kurum adları uydurmadır.
