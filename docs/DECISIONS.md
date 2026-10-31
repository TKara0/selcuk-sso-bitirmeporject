# DECISIONS

Kaynak: `AGENTS.md`, README, tehdit modeli. Her karar değiştirilmemesi gereken varsayımı içerir; değiştirmek için
önce kullanıcıya sor. "Alternatif / neden değil" yalnızca depoda yazılı olduğu kadarıyla; olmayan BİLİNMİYOR.

| Karar | Neden | Alternatif → neden değil | Değişmemesi gereken varsayım |
|---|---|---|---|
| Yalnızca Authorization Code + PKCE (S256) ve refresh; implicit/ROPC yok | OIDC/OAuth2 güvenli pratikleri; AGENTS kural (d) | `plain` PKCE, implicit, password → güvensiz | PKCE her istemcide zorunlu; yeni akış RFC'ye uyar |
| Kriptografi ASP.NET Core Identity/OpenIddict'e bırakılır | Elle kripto riski (kural e) | Özel hash/imza → yasak | Elle kripto yazılmaz |
| Access token küçültülmüş JWT (15 dk); profil id_token/userinfo'da | Kişisel veri sızıntısını azaltır | Uzun ömür, profilli token | `OidcKimlik.Hedefler` ile hedefleme |
| Introspection yok (yalnızca belgelendi) | Kapsam/karmaşıklık | RFC 7662 → R1 kabul edildi | Demo API JWT'yi kendi doğrular |
| Kayan refresh (8 sa) + mutlak 12 sa sınır, tekrar kullanım payı yok | Çalınan refresh'in kalıcı erişimi (E4) | OpenIddict'te kayan+mutlak ayarı yok → `YenilemeUstSiniri` | `TokenBudama:EnAzYas` ≥ mutlak süre |
| Tek çıkış: yalnızca Back-Channel Logout 1.0; front-channel yok | Güvenilir sunucudan sunucuya iptal | Front-channel → eklenmedi | Her istemci `backchannel_logout_uri` verir; yalnızca https veya `http://127.0.0.1` |
| Kilit yalnızca yeni girişi engeller; authorize/token kilidi kontrol etmez | Kilitle hizmet engellemeyi (D1) sınırlar | Tam kilit → saldırgan kullanıcıyı dışarıda bırakır | Davranış `GirisTests`'te sabit |
| 2FA isteğe bağlı (`IkiAdimZorunluRoller` boş); istemci bazlı `mfaZorunlu` yalnızca tek-kapi | Kullanılabilirlik; `RolModeliTests` sabitler | Rol bazlı zorunluluk → ürün kararı bekliyor | Test, hiçbir rolün zorunlu olmadığını doğrular |
| Hesap var/yok ayırt edilmez (aynı mesaj, sahte PBKDF2, şifremi unuttum aynı iş) | Kullanıcı adı taraması (I1) | Farklı hata mesajı → sızdırır | Metinler değiştirilmez |
| Denetim kaydında sır yok; olmayan kullanıcı adı HMAC özetiyle | KVKK/gizlilik, geri çevirme (I6) | Düz ad / SHA-256 → kırılabilir | Anahtar yoksa Development dışında açılmaz |
| Dev dışı ortamda uygulama açılmaz (production sertifikası yok) | Yanlışlıkla Development anahtarlarıyla yayın | – | R8 çözülmeden production yok |
| Tek DbContext, SQL Server LocalDB; testte SQLite in-memory | Basitlik; hızlı izole test | – | Test başına adlandırılmış paylaşımlı önbellekli DB |
| Selcuk.Roller tek kaynak, ad alanı kökte (`Selcuk.Roller`) | `Roller` adı gölgelenmesin | Ayrı ad alanı → gölgeleme | Yeni rol = katalogda bir satır; doğrulayıcı geçmeli |
| Otomasyonlar temsili ekran + Mock veri; Api modu `https` zorunlu | API'ler henüz yok | – | Ekranlar gerçek veri iddia etmez ("Temsili ekran" bandı) |
| Çerez yerine sunucu tarafı oturum bileti (`OturumBiletDeposu`) | localhost'ta çerez birikimi 431 hatası verdi | Çerezde token → başlık sınırı aşılıyor | Çerezde yalnızca anahtar |
| Tasarım: shadow yok, CSP gevşetilmez, satır içi stil/betik yok | Erişilebilirlik + CSP | – | Yeni bileşenler yalnızca token'larla |
| Gerçek Selçuk sistemlerine yalnızca `araclar/tek-kapi`; test/demo/kimlik sunucusu bağlanmaz | Kural (c) | – | Test verisi kurgusal, `@test.local` |
| Ölçüm aracı yalnızca localhost + https | Yanlışlıkla dış istek (I8) | – | `YerelAg.Dogrula` + `YalnizcaYerelHandler` |
| Her otomasyon ayrı uygulama: kendi portu (5301-5311) ve kendi OIDC istemcisi (`<ad>-otomasyonu`, `aud` `<ad>-api`) | Bağımsız dağıtım, istemci bazlı erişim izni (`izinliRoller`), Uygulamalarım'da ayrı kart | Tek uygulamada modüller → BİLİNMİYOR (depoda yazılı gerekçe yok) | Yeni otomasyon [otomasyon-sablonu.md](otomasyon-sablonu.md) ile ayrı proje olarak eklenir; uygulamalar birbirini çağırmaz |
| Tek Kapı, toplantı kararıyla yeni işlere dahil edilmez; sonra iptal edilecek | Toplantı kararı (kullanıcı bildirdi) | – | Yeni işler Tek Kapı'ya dokunmaz; kaldırılana kadar mevcut testleri (49 kontrol) geçmeli |
| Kişiye bağlı ilişkiler (kurul üyesine atanan dosya, projenin hocası, danışmanın öğrencisi) rol değil, uygulama verisidir | Rol yalnızca ekranın kime açık olduğunu belirler | Her ilişki için rol → rol sayısı patlar, atama yönetim paneli ister | Yeni rol eklenmez; süzme kaydın alanıyla (`UyeKullaniciAdi`, `HocaKullaniciAdi`) yapılır |
| Kapsam kullanıcının kendi `bolum` ve `fakulte` claim'inden gelir (`KullaniciKapsami`); rol başına kapsam sonraya bırakıldı | Basit, tek kaynak (kullanıcı kaydı) | Rol başına birim (ör. iki bölümde başkan) → model ve yönetim gerektirir | Bir kullanıcının bütün bölüm rolleri aynı bölüm için geçerlidir ([KNOWN_ISSUES](KNOWN_ISSUES.md)) |
| Her adım: sır kontrollü tek commit + push, force push yok | Kural (g) | – | Geçmiş yeniden yazılmaz |
