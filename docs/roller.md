# Rol modeli

Kimlik sunucusunun rolleri tek bir katalogda tanımlıdır: `src/Selcuk.Roller/Roller.cs` (`Roller.Katalog`). Kimlik
sunucusu açılışta katalogdaki bütün rolleri oluşturur; kullanıcının bütün rolleri `roles` kapsamıyla id_token'a ve
access token'a `role` claim'i olarak yazılır (tek rolde metin, birden çok rolde JSON dizisi). Alt uygulamalar
`Selcuk.Istemci.Ortak` ile bağlanır ve `User.IsInRole(Roller.…)` ile ekranlarını gösterir; görünen adlar da
katalogdan gelir. Kapsamlı rollerin birimi id_token'daki `bolum` ve `fakulte` claim'leridir (yeni claim yok).

Roller şimdilik yalnızca başlangıç verisiyle atanır; yönetim paneli yoktur. İki aşamalı doğrulama hiçbir rol için
zorunlu değildir (`IkiAdimZorunluRoller` boş).

## Katmanlar

| Katman | Kullanıcı başına | Açıklama |
|---|---|---|
| Kullanıcı türü | Tam olarak bir | Kişinin kurumdaki temel konumu |
| İdari görev | Sıfır veya daha fazla | Bölüm veya fakültedeki görev |
| Komisyon ve kurul | Sıfır veya daha fazla | Komisyon ve kurul üyelikleri |

## Rol kataloğu

| Kimlik | Türkçe ad | Katman | Kural (verilebileceği tür) | Kapsam |
|---|---|---|---|---|
| `ogrenci` | Öğrenci | Kullanıcı türü | Görev ve komisyon rolü alamaz | — |
| `akademisyen` | Akademisyen | Kullanıcı türü | — | — |
| `personel` | İdari personel | Kullanıcı türü | — | — |
| `bolum_baskani` | Bölüm başkanı | İdari görev | Yalnızca akademisyen | Bölüm (`Bolum` dolu olmalı) |
| `bolum_sekreteri` | Bölüm sekreteri | İdari görev | Yalnızca personel | Bölüm (`Bolum` dolu olmalı) |
| `fakulte_sekreteri` | Fakülte sekreteri | İdari görev | Yalnızca personel | Fakülte (`Fakulte` dolu olmalı) |
| `danisman` | Danışman | İdari görev | Yalnızca akademisyen | — |
| `cap_yandal_koordinatoru` | ÇAP-Yandal koordinatörü | İdari görev | Yalnızca akademisyen | — |
| `iskur_sorumlusu` | İŞKUR sorumlusu | İdari görev | Yalnızca personel | — |
| `intibak_komisyonu` | İntibak komisyonu üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |
| `staj_komisyonu` | Staj komisyonu üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |
| `etik_kurul_uyesi` | Etik kurul üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |
| `mezuniyet_komisyonu` | Mezuniyet komisyonu üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |
| `bolum_kurulu_uyesi` | Bölüm kurulu üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |
| `mudek_komisyonu` | MÜDEK komisyonu üyesi | Komisyon ve kurul | Yalnızca akademisyen | — |

Kapsamlı rol, kullanıcının kendi `Bolum` veya `Fakulte` alanı için geçerlidir ve öyle gösterilir: "Bölüm başkanı
(Bilgisayar Mühendisliği)". Bölümü veya fakültesi boş kullanıcıya verilemez.

## Tutarlılık kuralları (`RolDogrulayici`)

1. Her kullanıcının tam olarak bir kullanıcı türü vardır (hiç yoksa veya iki tane varsa reddedilir).
2. Görev ve komisyon rolleri yalnızca katalogda belirtilen kullanıcı türüne verilir (tablodaki "Kural" sütunu).
3. Öğrenciye idari görev veya komisyon rolü verilmez.
4. `bolum_baskani` ve `bolum_sekreteri` için `Bolum`, `fakulte_sekreteri` için `Fakulte` dolu olmalıdır.
5. Katalogda olmayan rol reddedilir.

Development başlangıç verisi (`BaslangicVerisi.TestKullanicilari`) açılışta bu kurallardan geçer; kurala uymayan bir
tanım açılışı açık bir hatayla durdurur. Test kullanıcıları ve rolleri README'de (Roller ve test kullanıcıları).

## Yeni rol eklemek

`Roller.Katalog`'a bir satır eklenir: kimlik, Türkçe ad, katman, kısa açıklama, (görev rolüyse) verilebileceği
kullanıcı türü ve gerekiyorsa kapsam. `[Authorize(Roles = …)]` gibi kullanımlar için aynı dosyaya bir sabit de
eklenebilir. Kimlik sunucusu rolü bir sonraki açılışta oluşturur; Hesabım ve Profilim onu kendi katmanında gösterir.

## Plan matrisi

> **Plan: otomasyonlar sonraki adımlarda eklenecek.** Aşağıdaki ekranlar henüz yoktur; Bilgisayar Mühendisliği
> Bölümü'nün 11 otomasyonu ayrı uygulamalar olarak SSO'ya bağlandığında hangi rolün hangi temsili ekranları
> göreceğinin önerisidir.

| Otomasyon | Rol | Temsili ekranlar |
|---|---|---|
| İntibak | `ogrenci` | Başvuru ve durum |
| | `intibak_komisyonu` | Ders eşleştirme, muafiyet kararı |
| | `bolum_baskani` | Onay listesi |
| | `bolum_sekreteri` | İntibak raporları |
| Staj | `ogrenci` | Başvuru, staj defteri, sonuç |
| | `staj_komisyonu` | Başvuru inceleme, yıllara göre takip |
| | `bolum_sekreteri` | Evrak listesi |
| | `bolum_baskani` | Özet |
| Etik Kurul | `akademisyen` | Başvuru |
| | `etik_kurul_uyesi` | Atanan dosyalar, görüş |
| | `bolum_baskani` | Karar listesi |
| Mezuniyet | `ogrenci` | Başvuru ve durum |
| | `mezuniyet_komisyonu` | Onay |
| | `bolum_sekreteri` | Dekanlığa gidecek evrak |
| | `fakulte_sekreteri` | Gelen evrak |
| Bölüm Kurul Karar | `bolum_baskani` | Gündem ve toplantı oluşturma |
| | `bolum_kurulu_uyesi` | Gündem, oylama |
| | `bolum_sekreteri` | Tutanak ve arşiv |
| Öğrenci Danışmanlık | `danisman` | Öğrenci listesi, görüşme notları |
| | `ogrenci` | Danışmanım, görüşme talebi |
| İŞKUR | `iskur_sorumlusu` | Devam girişi, takip |
| | `ogrenci` | Kendi devam durumu |
| MÜDEK | `mudek_komisyonu` | Program çıktıları, eksikler, raporlar |
| | `akademisyen` | Ders çıktısı veri girişi |
| | `bolum_baskani` | Raporlar |
| Lisans Projeleri | `ogrenci` | Konu seçimi, aşamalar |
| | `akademisyen` | Konu önerisi, değerlendirme |
| | `bolum_baskani` | Eşleştirme |
| Sınav Başarısı Analizi | `akademisyen` | Kendi dersleri |
| | `bolum_baskani` | Bölüm geneli |
| | `mudek_komisyonu` | Çıktı bazlı analiz |
| ÇAP-Yandal | `ogrenci` | Başvuru ve durum |
| | `cap_yandal_koordinatoru` | Başvurular, takip |
| | `bolum_sekreteri` | Evrak |

Rol sahibi kullanıcılar kullanıcı türlerinin ekranlarını da görür: örneğin `bolum_baskani` aynı zamanda
`akademisyen`'dir ve Etik Kurul'da başvuru, Lisans Projeleri'nde konu önerisi ekranlarını da görür.
