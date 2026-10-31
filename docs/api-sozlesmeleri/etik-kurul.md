# Etik Kurul API sözleşmesi

**Durum: sözleşme.** Etik Kurul API'si henüz yazılmadı. Etik Kurul Otomasyonu (`src/Selcuk.Otomasyon.EtikKurul`,
https://localhost:5303) varsayılan olarak örnek veriyle (`VeriKaynagi:Mod` = `Mock`) çalışır. API yazıldığında yalnızca
ayar değişir: `"VeriKaynagi": { "Mod": "Api", "ApiAdresi": "https://<etik-kurul-api-adresi>/" }`. İstemci tarafı
hazırdır (`ApiEtikKurulVeriKaynagi`); DTO'lar `src/Selcuk.Otomasyon.EtikKurul/Sozlesme/EtikKurulSozlesmesi.cs`'te.

## Kimlik doğrulama ve yetki

- Her istek `Authorization: Bearer <access token>` taşır: kimlik sunucusunun `etik-kurul-otomasyonu` istemcisine
  verdiği JWT, `typ` `at+jwt`, `aud` `etik-kurul-api`, `scope` içinde `etik-kurul.oku` / `etik-kurul.yaz`. API token'ı
  kendisi doğrular (JWKS, `iss`, `aud`, `typ`, süre).
- Token'da `sub`, `role`, `scope`, `sid`, `client_id` vardır; ad, kullanıcı adı, bölüm ve fakülte yoktur. API bunları
  `sub` ile kendi kullanıcı kaydından okur.
- **Kapsam filtresini API yapar.** Akademisyen uç noktası yalnızca `sub`'ın kendi başvurularını döndürür. Kurul üyesi
  uç noktası yalnızca **kendisine atanmış** dosyaları döndürür: atama kişiye bağlı kayıt verisidir (rol değildir) ve
  bölümden bağımsızdır. Bölüm başkanı uç noktası kullanıcının bölümünün kararlarını döndürür. Mock veri kaynağı aynı
  kuralı oturumdaki `preferred_username`, `bolum` ve `fakulte` claim'leriyle uygular (`KullaniciKapsami`).
- Kapsam veya rol eksikse 403, token geçersizse 401. İstemci 401/403'te "Bu verilere erişim yetkin yok", bağlantı
  hatası, zaman aşımı (5 sn) veya 5xx'te "Veri kaynağına şu an ulaşılamıyor" sayfasını gösterir.

## Şartlar

İnceleme süresi hedefi (gün) ve karar için gereken üye görüşü sayısı koda gömülmez; otomasyonun `appsettings.json`'ında
`EtikKurulSartlari:IncelemeSuresiHedefiGun` (örnek 30) ve `EtikKurulSartlari:KararIcinGerekenGorusSayisi` (örnek 3).
Değerler örnektir, gerçek yönetmelik değildir. API ham veriyi (tarihler, görüşler) döndürür; göstergeler istemcide
hesaplanır.

## JSON biçimi

camelCase alanlar; enum'lar camelCase metin; tarihler `yyyy-MM-dd`.

- `arastirmaTuru`: `anket`, `gorusme`, `deney`, `ikincilVeri`
- `durum`: `onIncelemede`, `kurulDegerlendirmesinde`, `duzeltmeIstendi`, `onaylandi`, `reddedildi`
- `gorus` (üye görüşü; `null`: henüz bildirilmedi): `uygun`, `duzeltmeGerekli`, `uygunDegil`
- Aşamalar: Başvuru → Ön inceleme → Kurul değerlendirmesi → Karar.

## Uç noktalar

| Yöntem ve yol | Kapsam | Rol | Döner | Ekran |
|---|---|---|---|---|
| `GET /api/etik-kurul/basvurularim` | `etik-kurul.oku` | `akademisyen` | `EtikBasvurusu[]` (yalnızca kendi) | Başvurularım |
| `POST /api/etik-kurul/basvurular` | `etik-kurul.yaz` | `akademisyen` | 201 + `EtikBasvurusu` (belgeler `multipart/form-data`) | Etik başvurusu |
| `GET /api/etik-kurul/atanan-dosyalarim` | `etik-kurul.oku` | `etik_kurul_uyesi` | `EtikBasvurusu[]` (kendisine atananlar) | Atanan dosyalar, Görüş |
| `POST /api/etik-kurul/basvurular/{id}/gorus` | `etik-kurul.yaz` | `etik_kurul_uyesi` (yalnızca atandığı dosya) | 204 | Görüş |
| `GET /api/etik-kurul/kararlar` | `etik-kurul.oku` | `bolum_baskani` | `EtikBasvurusu[]` (bölüm; onaylanan, reddedilen, düzeltme istenen) | Karar listesi |

`POST` uç noktaları gelecekteki işlevler içindir; ekranlar şu an temsilidir (veri değişmez, belge okunmaz).

## Örnek

`GET /api/etik-kurul/atanan-dosyalarim` (200):

```json
[
  {
    "id": "E26-03",
    "basvuranKullaniciAdi": "orn.akademisyen02",
    "basvuranAdi": "Örnek Akademisyen İki",
    "fakulte": "Teknoloji Fakültesi",
    "bolum": "Bilgisayar Mühendisliği",
    "projeBasligi": "Uzaktan çalışan yazılımcılarla görüşmeler",
    "arastirmaTuru": "gorusme",
    "katilimciGrubu": "Yazılım geliştiriciler (gönüllü)",
    "veriToplamaYontemi": "Çevrim içi görüşme ve ses kaydı",
    "basvuruTarihi": "2026-09-28",
    "durum": "kurulDegerlendirmesinde",
    "gorusler": [
      { "uyeKullaniciAdi": "etik.test", "uyeAdi": "Test Etik Kurul Üyesi", "gorus": "uygun", "gerekce": null },
      { "uyeKullaniciAdi": "orn.etikuye03", "uyeAdi": "Örnek Kurul Üyesi Üç", "gorus": null, "gerekce": null }
    ],
    "kararTarihi": null,
    "kararNotu": null
  }
]
```

`POST /api/etik-kurul/basvurular/{id}/gorus` gövdesi: `{ "gorus": "duzeltmeGerekli", "gerekce": "…" }`.

Örneklerdeki kişiler kimlik sunucusunun uydurma test kullanıcıları veya örnek veridir; proje başlıkları uydurmadır.
