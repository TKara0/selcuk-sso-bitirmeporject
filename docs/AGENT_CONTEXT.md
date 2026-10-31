# AGENT_CONTEXT (giriş noktası)

Önce bunu oku; ayrıntı için aşağıdaki dosyalara git. Kök `AGENTS.md` de bağlayıcıdır (değişmez kurallar a-h).

## Özet

"Tek Şifre Giriş Sistemi" (SelcukSSO), bitirme projesi: fakülte/bölüm web uygulamaları için merkezi kimlik sunucusu.
Tek kullanıcı adı + şifre, OpenID Connect (Authorization Code + PKCE S256, refresh token), JWT, Back-Channel
Logout ile tek çıkış. .NET 10, ASP.NET Core Identity + OpenIddict 7.7.2, EF Core + SQL Server LocalDB. Arayüz
Türkçe. Tüm veri kurgusaldır; gerçek Selçuk sistemlerine yalnızca `araclar/tek-kapi` (kullanıcının yerel aracı) bağlanır.

## Kritik modüller

| Yol | Rol |
|---|---|
| `src/Selcuk.Identity` (:5001) | Kimlik sunucusu: OIDC uç noktaları, Razor Pages, 2FA, denetim, e-posta |
| `src/Selcuk.Istemci.Ortak` | `AddSelcukSSO`: alt uygulamaların çerez+OIDC, Back-Channel alıcısı, token yenileme |
| `src/Selcuk.Otomasyon.Ortak` | Bölüm otomasyon şablonu (menü, rol yetkisi, Mock/Api veri kaynağı) |
| `src/Selcuk.Otomasyon.{Intibak,Staj,Mezuniyet,CapYandal}` (:5301/5302/5304/5311) | Temsili ekranlar, örnek veri |
| `src/Selcuk.Demo.{Obis,Yemekhane,Api}` (:5101/5102/5201) | Demo istemciler ve kaynak sunucusu |
| `src/Selcuk.Roller`, `src/Selcuk.Arayuz` | Rol kataloğu (tek kaynak), ortak tasarım sistemi |
| `araclar/tek-kapi` (:4545) | Node.js yerel ESSO portalı; kendi `AGENTS.md`'si var |
| `tools/Selcuk.Olcum`, `tests/` | Ölçüm aracı, 11 .NET test projesi (+ `TestOrtak`) |

Kritik dosyalar: `src/Selcuk.Identity/Program.cs` (OpenIddict/Identity/ara katman kurulumu), `Data/Istemciler.cs`
(istemci kayıtları), `Oidc/` (token yaşam döngüsü, tek çıkış, erişim izni), `Data/BaslangicVerisi.cs` (seed).

## Mevcut durum (ayrıntı: [CURRENT_STATE.md](CURRENT_STATE.md))

Çekirdek özellikler tamam ve testli. Eksik: yönetim paneli, production sertifika/anahtar yönetimi, elle ölçümler
ve SUS anketi, otomasyon API'leri (yalnızca sözleşme var).

## Kod değiştirmeden önce

1. `AGENTS.md` kuralları: kapsam dışına çıkma, yeni NuGet/araç ekleme, sır yazma/loglama yok, kriptografiyi elle
   yazma, PKCE zorunlu (implicit/ROPC yok), gerçek Selçuk sistemlerine test/istek yok.
2. Çalışan 5001/5101/5102 varsa **dur ve raporla**; kullanıcının süreçlerini kapatma.
3. Her adım sonunda `dotnet build` + `dotnet test` geçmeli; sır kontrolü sonrası tek commit + `origin/main` push
   (force push yok).
4. `Selcuk.Arayuz/wwwroot` değişirse `scripts/arayuz-esitle.ps1` çalıştır (Tek Kapı kopyası SHA-256 ile test edilir).
5. Belgelere ölçülmemiş sayı yazma ("henüz ölçülmedi").
6. Ayrıntılı davranış belgesi `README.md` (büyük, ~1750 satır): konu başlığına `grep -n "^#" README.md` ile git; baştan okuma.

## Harita

- [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) — amaç, teknoloji, genel akış
- [ARCHITECTURE.md](ARCHITECTURE.md) — modüller, akışlar, kritik kod yolları
- [CURRENT_STATE.md](CURRENT_STATE.md) — çalışan/çalışmayan, doğrulama durumu
- [KNOWN_ISSUES.md](KNOWN_ISSUES.md) — açık riskler ve sınırlamalar
- [DECISIONS.md](DECISIONS.md) — değiştirilmemesi gereken teknik kararlar
- [API_ENDPOINTS.md](API_ENDPOINTS.md) — OIDC uç noktaları, Demo API, Back-Channel
- [NEXT_TASKS.md](NEXT_TASKS.md) — sonraki işler

Mevcut konu belgeleri: `mimari.md` (Mermaid + port tablosu), `roller.md`, `otomasyon-sablonu.md`,
`tehdit-modeli.md` (STRIDE, R1-R14), `test-kapsami.md`, `guvenlik-karsilastirma.md`, `api-sozlesmeleri/`, `olcumler/`.

## Güncelleme kuralı

Mimari → ARCHITECTURE; durum → CURRENT_STATE; yeni sorun → KNOWN_ISSUES; karar → DECISIONS; görev → NEXT_TASKS;
uç nokta → API_ENDPOINTS. Bilgiyi tekrarlama; ayrıntı varsa ilgili belgeye bağlantı ver.
