// Ekran görüntüleri: kimlik sunucusu, Demo OBİS, Demo Yemekhane, bölüm otomasyonları (Staj, İntibak, Mezuniyet,
// ÇAP-Yandal) ve Tek Kapı ekranlarını Playwright ile çeker.
//
//   docs/tasarim/d2/    gözden geçirme için her ekranın açık, koyu (1280 × 800) ve 390px (mobil) hâli, tam sayfa
//   docs/tez-gorselleri/ tez için seçilmiş ekranlar, açık tema, 1440px genişlik, tam sayfa
//
// Kullanım (proje kökünden; bir kez: cd araclar\tek-kapi; npm ci):
//   node --use-system-ca scripts/ekran-goruntuleri.mjs
//   node --use-system-ca scripts/ekran-goruntuleri.mjs --sadece intibak,mezuniyet,cap
//
// --sadece: yalnızca verilen grupların ekranları çekilir ve yalnızca onların eski PNG'leri silinir (diğer PNG'lere
// dokunulmaz). Gruplar: kimlik, obis, yemekhane, staj, tekkapi, intibak, mezuniyet, cap. Verilmezse hepsi (ve bütün
// eski PNG'ler silinir).
//
// Ne yapar:
//   - 4545, 5001, 5101, 5102, 5201, 5301, 5302, 5304, 5311 portlarından biri doluysa durur (kullanıcının uygulamalarına dokunmaz).
//   - Kimlik sunucusu için geçici bir LocalDB veritabanı (SelcukSSO_Ekran_<rastgele>) kurar; test kullanıcılarının
//     şifreleri ve demo istemci sırrı bu çalıştırma için rastgele üretilir, yalnızca bellekte ve başlatılan süreçlerin
//     ortam değişkenlerinde durur (yazdırılmaz, dosyaya yazılmaz). Yalnızca test kullanıcıları (203011001,
//     203011002, hoca.test, personel.test, komisyon.test, baskan.test, koordinator.test) kullanılır.
//   - Uygulamaları scripts/run-all.ps1 -TarayiciAcma ile başlatır; Tek Kapı'yı GEÇİCİ bir veri klasörüyle
//     (PORTAL_DATA_DIR) SSO modunda kendisi başlatır. Gerçek kasaya (araclar/tek-kapi/data) dokunmaz, okumaz.
//     Tek Kapı'da yalnızca portal ekranları çekilir: hiçbir sistem "Aç"ılmaz, yönetilen tarayıcı başlatılmaz; kasaya
//     yalnızca test/mock-sites.js'teki sahte bilgiler girilir.
//   - QR kod, doğrulayıcı anahtarı ve kurtarma kodları çekimden önce örnek değerlerle değiştirilir (D1'deki gibi);
//     her çekimden önce sayfada bu çalıştırmanın sırlarından (şifreler, anahtar, kodlar) biri var mı diye bakılır.
//   - Bitince yalnızca kendi başlattığı süreçleri durdurur, geçici veritabanını ve veri klasörünü siler.
//
// Bağımlılık: araclar/tek-kapi'deki Playwright (yeni paket kurulmaz). TLS doğrulaması kapatılmaz: tarayıcı ve Tek
// Kapı .NET geliştirme sertifikasına Windows sertifika deposu üzerinden güvenir (dotnet dev-certs https --trust).

import { spawn, spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
import crypto from 'node:crypto';
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const KOK = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const TEK_KAPI = path.join(KOK, 'araclar', 'tek-kapi');
const tekKapiden = createRequire(path.join(TEK_KAPI, 'package.json'));
const { chromium } = tekKapiden('playwright');
const { USER: SAHTE } = tekKapiden('./test/mock-sites.js');

const D2 = path.join(KOK, 'docs', 'tasarim', 'd2');
const TEZ = path.join(KOK, 'docs', 'tez-gorselleri');
const KIMLIK = 'https://localhost:5001';
const OBIS = 'https://localhost:5101';
const YEMEKHANE = 'https://localhost:5102';
const STAJ = 'https://localhost:5302';
const INTIBAK = 'https://localhost:5301';
const MEZUNIYET = 'https://localhost:5304';
const CAP = 'https://localhost:5311';
const TEK_KAPI_ADRES = 'http://127.0.0.1:4545';
const PORTLAR = [4545, 5001, 5101, 5102, 5201, 5301, 5302, 5304, 5311];

// Ekran grupları (--sadece). Ekran adının öneki grubunu belirler; önekli olmayanlar kimlik sunucusunundur.
const GRUPLAR = ['kimlik', 'obis', 'yemekhane', 'staj', 'tekkapi', 'intibak', 'mezuniyet', 'cap'];
const ESKI_GRUPLAR = ['kimlik', 'obis', 'yemekhane', 'staj', 'tekkapi'];
const grubu = (ad) => GRUPLAR.find((g) => g !== 'kimlik' && ad.startsWith(g + '-')) ?? 'kimlik';
const sadeceArg = process.argv.indexOf('--sadece');
const SECILI = sadeceArg >= 0 ? (process.argv[sadeceArg + 1] ?? '').split(',').map((g) => g.trim()).filter(Boolean) : GRUPLAR;
const bilinmeyen = SECILI.filter((g) => !GRUPLAR.includes(g));
if (sadeceArg >= 0 && (SECILI.length === 0 || bilinmeyen.length)) {
  console.error(`--sadece: geçersiz grup (${bilinmeyen.join(', ') || 'boş'}). Gruplar: ${GRUPLAR.join(', ')}`);
  process.exit(1);
}
const seciliMi = (ad) => SECILI.includes(grubu(ad));

// Bu çalıştırmanın sırları: yalnızca bellekte.
const rastgele = (n) => crypto.randomBytes(n).toString('base64url');
const sifreUret = () => `Ek${rastgele(12)}7a`;
const SIFRELER = {
  ogrenci: sifreUret(), ogrenci2: sifreUret(), akademisyen: sifreUret(), personel: sifreUret(), komisyon: sifreUret(),
  baskan: sifreUret(), koordinator: sifreUret(),
};
const ISTEMCI_SIRRI = rastgele(32);
const ANA_SIFRE = `Kasa-${rastgele(12)}`;
const sirlar = [...Object.values(SIFRELER), ISTEMCI_SIRRI, ANA_SIFRE, SAHTE.obisPass, SAHTE.mailPass];

const VERITABANI = `SelcukSSO_Ekran_${crypto.randomBytes(4).toString('hex')}`;
const ORTAM = {
  ...process.env,
  ConnectionStrings__SelcukSSO: `Server=(localdb)\\MSSQLLocalDB;Database=${VERITABANI};Trusted_Connection=True;MultipleActiveResultSets=true`,
  TestKullanicilari__Ogrenci__Sifre: SIFRELER.ogrenci,
  TestKullanicilari__Akademisyen__Sifre: SIFRELER.akademisyen,
  TestKullanicilari__Personel__Sifre: SIFRELER.personel,
  TestKullanicilari__Komisyon__Sifre: SIFRELER.komisyon,
  TestKullanicilari__BolumBaskani__Sifre: SIFRELER.baskan,
  TestKullanicilari__Ogrenci2__Sifre: SIFRELER.ogrenci2,
  TestKullanicilari__Koordinator__Sifre: SIFRELER.koordinator,
  // Gizli istemciler aynı sırrı kullanır (hepsi Kimlik:ClientSecret okur; ortam değişkeni ortak).
  Istemciler__DemoObis__ClientSecret: ISTEMCI_SIRRI,
  Istemciler__DemoYemekhane__ClientSecret: ISTEMCI_SIRRI,
  Istemciler__StajOtomasyonu__ClientSecret: ISTEMCI_SIRRI,
  Istemciler__IntibakOtomasyonu__ClientSecret: ISTEMCI_SIRRI,
  Istemciler__MezuniyetOtomasyonu__ClientSecret: ISTEMCI_SIRRI,
  Istemciler__CapYandalOtomasyonu__ClientSecret: ISTEMCI_SIRRI,
  Kimlik__ClientSecret: ISTEMCI_SIRRI,
};
delete ORTAM.Olcum__HizSiniriGevset;

const sorunlar = [];
const cekilenler = [];

function calistir(komut, argumanlar, ortam = process.env) {
  const sonuc = spawnSync(komut, argumanlar, { cwd: KOK, env: ortam, stdio: 'inherit', shell: false });
  if (sonuc.status !== 0) throw new Error(`${komut} ${argumanlar[0]} başarısız (çıkış kodu ${sonuc.status})`);
}

function powershell(betik) {
  return spawnSync('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', betik], { cwd: KOK, encoding: 'utf8' });
}

const portDoluMu = (port, host) => new Promise((tamam) => {
  const soket = net.connect({ port, host });
  soket.once('connect', () => { soket.destroy(); tamam(true); });
  soket.once('error', () => tamam(false));
  soket.setTimeout(1000, () => { soket.destroy(); tamam(false); });
});

const bekle = (ms) => new Promise((r) => setTimeout(r, ms));

// RFC 6238 TOTP (SHA-1, 30 sn, 6 hane): yalnızca bu betikte, test kullanıcısının doğrulayıcı uygulaması yerine.
function base32(metin) {
  const abc = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bitler = '';
  for (const c of metin.replace(/[\s=]/g, '').toUpperCase()) bitler += abc.indexOf(c).toString(2).padStart(5, '0');
  const bayt = [];
  for (let i = 0; i + 8 <= bitler.length; i += 8) bayt.push(parseInt(bitler.slice(i, i + 8), 2));
  return Buffer.from(bayt);
}

let sonAdim = 0;

function totp(anahtar) {
  sonAdim = Math.floor(Date.now() / 30000);
  const adim = Buffer.alloc(8);
  adim.writeBigUInt64BE(BigInt(sonAdim));
  const h = crypto.createHmac('sha1', base32(anahtar)).update(adim).digest();
  const o = h[h.length - 1] & 15;
  return String((h.readUInt32BE(o) & 0x7fffffff) % 1000000).padStart(6, '0');
}

// Kabul edilen kod 3 dk tekrar kabul edilmez (TOTP tekrar koruması): önceki koddan sonraki zaman adımını bekler.
async function yeniTotp(anahtar) {
  while (Math.floor(Date.now() / 30000) <= sonAdim) await bekle(1000);
  return totp(anahtar);
}

// ---------- Çekim ----------

async function hazirla(sayfa) {
  await sayfa.mouse.move(0, 0);
  await sayfa.evaluate(async () => {
    if (document.activeElement && document.activeElement !== document.body) document.activeElement.blur();
    await document.fonts.ready;
  });
  await bekle(150);
}

async function sirKontrolu(sayfa, ad) {
  const icerik = await sayfa.content();
  const bulunan = sirlar.filter((s) => s && icerik.includes(s));
  if (bulunan.length) throw new Error(`Sır kontrolü: "${ad}" sayfasında bu çalıştırmanın sırlarından biri var; çekilmedi.`);
}

async function tasmaKontrolu(sayfa, ad, gorunum) {
  const tasma = await sayfa.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  if (tasma > 0) sorunlar.push(`Yatay taşma (${tasma}px): ${ad} ${gorunum}`);
}

const TEZ_SIRASI = ['giris', 'onay', 'uygulamalarim', 'obis-notlarim', 'obis-token-inceleme', 'yemekhane-menu',
  'tekkapi-ana', 'tekkapi-kilit', 'guvenlik-gecmisi', 'iki-adim-kurulum',
  'staj-stajlarim', 'staj-basvuru-inceleme', 'staj-ozet', 'staj-erisim-reddedildi',
  'intibak-basvurum', 'intibak-ders-eslestirme', 'mezuniyet-durumum', 'mezuniyet-adaylar', 'cap-basvurularim',
  'cap-gelen-basvurular'];

async function cek(sayfa, ad, { mobil = true } = {}) {
  if (!seciliMi(ad)) return;
  await hazirla(sayfa);
  await sirKontrolu(sayfa, ad);
  const dosya = (son) => path.join(D2, `${ad}-${son}.png`);

  await sayfa.setViewportSize({ width: 1280, height: 800 });
  await sayfa.emulateMedia({ colorScheme: 'light' });
  await hazirla(sayfa);
  await tasmaKontrolu(sayfa, ad, 'açık');
  await sayfa.screenshot({ path: dosya('acik'), fullPage: true });

  await sayfa.emulateMedia({ colorScheme: 'dark' });
  await hazirla(sayfa);
  await sayfa.screenshot({ path: dosya('koyu'), fullPage: true });
  await sayfa.emulateMedia({ colorScheme: 'light' });

  if (mobil) {
    await sayfa.setViewportSize({ width: 390, height: 844 });
    await hazirla(sayfa);
    await tasmaKontrolu(sayfa, ad, 'mobil');
    await sayfa.screenshot({ path: dosya('mobil'), fullPage: true });
  }

  const sira = TEZ_SIRASI.indexOf(ad);
  if (sira >= 0) {
    await sayfa.setViewportSize({ width: 1440, height: 900 });
    await hazirla(sayfa);
    await sayfa.screenshot({ path: path.join(TEZ, `${String(sira + 1).padStart(2, '0')}-${ad}.png`), fullPage: true });
  }

  await sayfa.setViewportSize({ width: 1280, height: 800 });
  cekilenler.push(ad);
  console.log(`  ✓ ${ad}`);
}

// QR kod, anahtar ve kurtarma kodları örnek değerlerle (D1'deki gibi). Gerçek değerler sayfadan silinir.
async function ornekDegerler(sayfa) {
  await sayfa.evaluate(() => {
    const qr = document.querySelector('img.qr');
    if (qr) {
      qr.src = 'data:image/svg+xml,' + encodeURIComponent(
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 184 184"><rect width="184" height="184" fill="#fff"/>' +
        '<rect x="12" y="12" width="160" height="160" fill="none" stroke="#12263A" stroke-width="2" stroke-dasharray="6 6"/>' +
        '<text x="92" y="98" font-family="sans-serif" font-size="16" text-anchor="middle" fill="#12263A">Örnek QR</text></svg>');
    }
    const anahtar = document.querySelector('#anahtar');
    if (anahtar) anahtar.textContent = 'ABCD EFGH IJKL MNOP QRST UVWX YZ23 4567';
    document.querySelectorAll('a[href^="otpauth:"]').forEach((a) => a.setAttribute('href', '#'));
    document.querySelectorAll('#kurtarma-kodlari code').forEach((c, i) => { c.textContent = 'ORNEK-' + String(i + 1).padStart(5, '0'); });
  });
}

async function girisYap(sayfa, kullaniciAdi, sifre) {
  await sayfa.fill('#Input_KullaniciAdi', kullaniciAdi);
  await sayfa.fill('#Input_Sifre', sifre);
  await Promise.all([sayfa.waitForLoadState('load'), sayfa.click('form button[type=submit]')]);
}

async function izinVer(sayfa) {
  await sayfa.waitForSelector('button[name=karar][value=kabul]');
  await Promise.all([sayfa.waitForNavigation(), sayfa.click('button[name=karar][value=kabul]')]);
}

// Adres verilen önekle başlayana (veya düzenli ifadeye uyana) kadar bekler; olmazsa hatada gerçek adresi ve sayfa
// başlığını gösterir (teşhis için).
async function adresiBekle(sayfa, onek, ms = 30000) {
  const uyuyor = () => (onek instanceof RegExp ? onek.test(sayfa.url()) : sayfa.url().startsWith(onek));
  const bitis = Date.now() + ms;
  while (Date.now() < bitis) {
    if (uyuyor()) {
      await sayfa.waitForLoadState('load');
      return;
    }
    await bekle(200);
  }
  const baslik = await sayfa.title().catch(() => '');
  throw new Error(`Beklenen adres ${onek}, gelinen ${sayfa.url().split('?')[0]} ("${baslik}")`);
}

async function git(sayfa, adres) {
  const yanit = await sayfa.goto(adres, { waitUntil: 'load' });
  return yanit;
}

async function yeniBaglam(tarayici) {
  const baglam = await tarayici.newContext({
    viewport: { width: 1280, height: 800 }, locale: 'tr-TR', timezoneId: 'Europe/Istanbul', reducedMotion: 'reduce',
    colorScheme: 'light',
  });
  const sayfa = await baglam.newPage();
  // Kasıtlı açılan 403 (Tek Kapı erişim izni yok) ve 429 (istek sınırı) sayfalarının durum kodu iletisi hata sayılmaz.
  sayfa.on('console', (m) => {
    if (m.type() === 'error' && !/status of (403|429)/.test(m.text())) sorunlar.push(`Konsol hatası (${sayfa.url()}): ${m.text()}`);
  });
  sayfa.on('pageerror', (h) => sorunlar.push(`Betik hatası (${sayfa.url()}): ${h.message}`));
  return { baglam, sayfa };
}

// Uygulamaya Tek Şifre ile girer: kimlik sunucusunda oturum yoksa giriş formu, ilk kez giriliyorsa onay ekranı gelir.
async function uygulamayaGir(sayfa, adres, kullaniciAdi, sifre) {
  await git(sayfa, adres + '/Giris');
  if (await sayfa.$('#Input_KullaniciAdi')) await girisYap(sayfa, kullaniciAdi, sifre);
  if (await sayfa.$('button[name=karar][value=kabul]')) await izinVer(sayfa);
  await adresiBekle(sayfa, new RegExp(`^${adres}/$`));
}

// İntibak, Mezuniyet ve ÇAP-Yandal (temsili, örnek veri): öğrenci (203011002), komisyon üyesi ve koordinatör.
async function otomasyonCekimleri(tarayici) {
  const gruplar = ['intibak', 'mezuniyet', 'cap'].filter((g) => SECILI.includes(g));
  if (gruplar.length === 0) return;
  console.log('İntibak, Mezuniyet ve ÇAP-Yandal otomasyonları (temsili, örnek veri)');

  const ogrenci = await yeniBaglam(tarayici);
  if (gruplar.includes('intibak')) {
    await uygulamayaGir(ogrenci.sayfa, INTIBAK, '203011002', SIFRELER.ogrenci2);
    await cek(ogrenci.sayfa, 'intibak-ana');
    await git(ogrenci.sayfa, INTIBAK + '/Ogrenci/Basvurum');
    await cek(ogrenci.sayfa, 'intibak-basvurum');
    await git(ogrenci.sayfa, INTIBAK + '/Ogrenci/Basvuru');
    await cek(ogrenci.sayfa, 'intibak-basvuru');
  }
  if (gruplar.includes('mezuniyet')) {
    await uygulamayaGir(ogrenci.sayfa, MEZUNIYET, '203011002', SIFRELER.ogrenci2);
    await git(ogrenci.sayfa, MEZUNIYET + '/Ogrenci/Durumum');
    await cek(ogrenci.sayfa, 'mezuniyet-durumum');
    await git(ogrenci.sayfa, MEZUNIYET + '/Ogrenci/Basvuru');
    await cek(ogrenci.sayfa, 'mezuniyet-basvuru');
    await git(ogrenci.sayfa, MEZUNIYET + '/Ogrenci/IlisikKesme');
    await cek(ogrenci.sayfa, 'mezuniyet-ilisik-kesme');
  }
  if (gruplar.includes('cap')) {
    await uygulamayaGir(ogrenci.sayfa, CAP, '203011002', SIFRELER.ogrenci2);
    await git(ogrenci.sayfa, CAP + '/Ogrenci/Basvurularim');
    await cek(ogrenci.sayfa, 'cap-basvurularim');
    await git(ogrenci.sayfa, CAP + '/Ogrenci/Basvuru');
    await cek(ogrenci.sayfa, 'cap-basvuru');
    await git(ogrenci.sayfa, CAP + '/Koordinator/GelenBasvurular');
    await adresiBekle(ogrenci.sayfa, /ErisimReddedildi/);
    await cek(ogrenci.sayfa, 'cap-erisim-reddedildi');
  }
  await ogrenci.baglam.close();

  if (gruplar.includes('intibak') || gruplar.includes('mezuniyet')) {
    const komisyon = await yeniBaglam(tarayici);
    if (gruplar.includes('intibak')) {
      await uygulamayaGir(komisyon.sayfa, INTIBAK, 'komisyon.test', SIFRELER.komisyon);
      await git(komisyon.sayfa, INTIBAK + '/Komisyon/BekleyenBasvurular');
      await cek(komisyon.sayfa, 'intibak-bekleyen-basvurular');
      await git(komisyon.sayfa, INTIBAK + '/Komisyon/DersEslestirme?id=I26-02');
      await cek(komisyon.sayfa, 'intibak-ders-eslestirme');
    }
    if (gruplar.includes('mezuniyet')) {
      await uygulamayaGir(komisyon.sayfa, MEZUNIYET, 'komisyon.test', SIFRELER.komisyon);
      await git(komisyon.sayfa, MEZUNIYET + '/Komisyon/Adaylar');
      await cek(komisyon.sayfa, 'mezuniyet-adaylar');
      // Temsili işlem: Onayla gerçek bir POST yapar, veri değişmez, mesaj gösterilir.
      await Promise.all([komisyon.sayfa.waitForNavigation(), komisyon.sayfa.click('form[action*="handler=Onayla"] button')]);
      await komisyon.sayfa.waitForSelector('.bant-uygulama[role=status]');
      await cek(komisyon.sayfa, 'mezuniyet-temsili-islem', { mobil: false });
    }
    await komisyon.baglam.close();
  }

  if (gruplar.includes('cap')) {
    const koordinator = await yeniBaglam(tarayici);
    await uygulamayaGir(koordinator.sayfa, CAP, 'koordinator.test', SIFRELER.koordinator);
    await git(koordinator.sayfa, CAP + '/Koordinator/GelenBasvurular');
    await cek(koordinator.sayfa, 'cap-gelen-basvurular');
    await git(koordinator.sayfa, CAP + '/Koordinator/OgrenciTakibi');
    await cek(koordinator.sayfa, 'cap-ogrenci-takibi');
    await koordinator.baglam.close();
  }
}

async function cekimler(tarayici) {
  // Mevcut ekranlar tek akıştır (kimlik sunucusu oturumu, 2FA kurulumu ve Tek Kapı birbirine bağlı); bu gruplardan
  // biri seçilmişse akış baştan sona yürür, seçilmeyen grupların ekranları çekilmez (cek).
  if (ESKI_GRUPLAR.some((g) => SECILI.includes(g))) await mevcutCekimler(tarayici);
  await otomasyonCekimleri(tarayici);
}

async function mevcutCekimler(tarayici) {
  console.log('Kimlik sunucusu');
  const { sayfa } = await yeniBaglam(tarayici);
  await git(sayfa, KIMLIK + '/');
  await sayfa.waitForSelector('#Input_KullaniciAdi');
  await cek(sayfa, 'giris');
  await girisYap(sayfa, '203011001', SIFRELER.ogrenci);
  await sayfa.waitForSelector('.uygulamalar');
  await cek(sayfa, 'uygulamalarim');
  await sayfa.click('[data-menu-dugme]');
  await cek(sayfa, 'hesap-menusu', { mobil: false });
  await sayfa.keyboard.press('Escape');

  // 2FA kurulumu: anahtar sayfadan okunur (TOTP için), sonra örnek değerle değiştirilir.
  await git(sayfa, KIMLIK + '/Account/IkiAdimDogrulama');
  await Promise.all([sayfa.waitForNavigation(), sayfa.click('form button[type=submit]')]);
  const anahtar = (await sayfa.textContent('#anahtar')).trim();
  sirlar.push(anahtar, anahtar.replace(/\s/g, ''));
  await ornekDegerler(sayfa);
  await cek(sayfa, 'iki-adim-kurulum');
  await sayfa.fill('#Etkinlestirme_Kod', totp(anahtar));
  await Promise.all([sayfa.waitForNavigation(), sayfa.click('form[action*="Etkinlestir"] button[type=submit]')]);
  const kodlar = (await sayfa.$$eval('#kurtarma-kodlari code', (k) => k.map((c) => c.textContent.trim())));
  if (kodlar.length !== 10) throw new Error('Kurtarma kodları okunamadı.');
  sirlar.push(...kodlar);
  await ornekDegerler(sayfa);
  await cek(sayfa, 'kurtarma-kodlari');

  await git(sayfa, KIMLIK + '/Account/GuvenlikGecmisi');
  await cek(sayfa, 'guvenlik-gecmisi');
  await git(sayfa, KIMLIK + '/Account/AccessDenied');
  await cek(sayfa, 'erisim-reddedildi');
  await git(sayfa, KIMLIK + '/Hata');
  await cek(sayfa, 'hata');

  console.log('Demo OBİS');
  await git(sayfa, OBIS + '/');
  await cek(sayfa, 'obis-karsilama');
  await sayfa.click('.tek-sifre-girisi');
  await adresiBekle(sayfa, /localhost:5001\/connect\/authorize/);
  await cek(sayfa, 'onay');
  await izinVer(sayfa);
  await adresiBekle(sayfa, new RegExp(`^${OBIS}/$`));
  await cek(sayfa, 'obis-ana');
  await git(sayfa, OBIS + '/Notlarim');
  await sayfa.waitForSelector('.notlar');
  await cek(sayfa, 'obis-notlarim');
  await git(sayfa, OBIS + '/TokenInceleme');
  await sayfa.waitForSelector('.claimler');
  if ((await sayfa.content()).includes('Bu alan için açıklama yok')) sorunlar.push('Token İnceleme: açıklaması olmayan claim var');
  await cek(sayfa, 'obis-token-inceleme');
  await git(sayfa, OBIS + '/Profilim');
  await cek(sayfa, 'obis-profilim');
  await sayfa.click('[data-menu-dugme]');
  await cek(sayfa, 'obis-hesap-menusu', { mobil: false });
  await sayfa.keyboard.press('Escape');
  await git(sayfa, OBIS + '/DersYonetimi');
  await adresiBekle(sayfa, /ErisimReddedildi/);
  await cek(sayfa, 'obis-erisim-reddedildi');
  await git(sayfa, OBIS + '/GirisHatasi?neden=oturum-suresi');
  await cek(sayfa, 'obis-giris-hatasi');
  await git(sayfa, OBIS + '/Cikis');
  await cek(sayfa, 'obis-cikis');

  console.log('Demo Yemekhane');
  await git(sayfa, YEMEKHANE + '/');
  await cek(sayfa, 'yemekhane-karsilama');
  await sayfa.click('.tek-sifre-girisi');
  await izinVer(sayfa);
  // Yemekhane'nin girişi varsayılan olarak Rezervasyonlarım'a döner (Giris.cshtml.cs).
  await adresiBekle(sayfa, `${YEMEKHANE}/Rezervasyonlarim`);
  await git(sayfa, YEMEKHANE + '/');
  await cek(sayfa, 'yemekhane-menu');
  await git(sayfa, YEMEKHANE + '/Rezervasyonlarim');
  await Promise.all([sayfa.waitForNavigation(), sayfa.click('.rezervasyonlar li:nth-child(2) .dugme-uygulama')]);
  await Promise.all([sayfa.waitForNavigation(), sayfa.click('.rezervasyonlar li:nth-child(4) .dugme-uygulama')]);
  await sayfa.waitForSelector('[data-duyur]');
  await cek(sayfa, 'yemekhane-rezervasyonlarim');
  await git(sayfa, YEMEKHANE + '/Profilim');
  await cek(sayfa, 'yemekhane-profilim');

  console.log('Staj Otomasyonu (temsili, örnek veri)');
  await git(sayfa, STAJ + '/Giris');
  await izinVer(sayfa);
  await adresiBekle(sayfa, new RegExp(`^${STAJ}/$`));
  await cek(sayfa, 'staj-ana');
  await git(sayfa, STAJ + '/Ogrenci/Stajlarim');
  await cek(sayfa, 'staj-stajlarim');
  await git(sayfa, STAJ + '/Komisyon/BasvuruInceleme');
  await adresiBekle(sayfa, /ErisimReddedildi/);
  await cek(sayfa, 'staj-erisim-reddedildi');

  console.log('Tek Kapı (geçici veri klasörü, SSO modu)');
  // 2FA kurulunca bu oturum iki aşamalı doğrulanmış sayılır (amr mfa); tek-kapi doğrudan onay ister.
  await git(sayfa, TEK_KAPI_ADRES + '/');
  await izinVer(sayfa);
  await sayfa.waitForSelector('#viewSetup:not([hidden])');
  await cek(sayfa, 'tekkapi-kurulum');
  await sayfa.fill('#setupMaster', ANA_SIFRE);
  await sayfa.fill('#setupMaster2', ANA_SIFRE);
  await sayfa.click('#formSetup button[type=submit]');
  await sayfa.waitForSelector('#viewSettings:not([hidden])');
  await cek(sayfa, 'tekkapi-ayarlar');
  await sayfa.fill('#ogrNo', SAHTE.ogrNo);
  await sayfa.fill('#obisPass', SAHTE.obisPass);
  await sayfa.click('#formCreds button[type=submit]');
  await sayfa.waitForSelector('#viewHome:not([hidden]) .sistemler li');
  await cek(sayfa, 'tekkapi-ana-eksik');
  await sayfa.click('#btnSettings');
  await sayfa.waitForSelector('#viewSettings:not([hidden])');
  await sayfa.fill('#ogrMail', SAHTE.ogrMail);
  await sayfa.fill('#mailPass', SAHTE.mailPass);
  await sayfa.click('#formCreds button[type=submit]');
  await sayfa.waitForSelector('#viewHome:not([hidden]) #setupNotice[hidden]', { state: 'attached' });
  await cek(sayfa, 'tekkapi-ana');
  await sayfa.click('#btnLock');
  await sayfa.waitForSelector('#viewLocked:not([hidden])');
  await cek(sayfa, 'tekkapi-kilit');
  await git(sayfa, TEK_KAPI_ADRES + '/auth/callback?error=access_denied');
  await cek(sayfa, 'tekkapi-erisim-yok');

  console.log('Girişin ikinci adımı (2FA), akademisyen ve personel');
  const ikinci = await yeniBaglam(tarayici);
  await git(ikinci.sayfa, KIMLIK + '/');
  await girisYap(ikinci.sayfa, '203011001', SIFRELER.ogrenci);
  await adresiBekle(ikinci.sayfa, /Account\/IkiAdim/);
  await cek(ikinci.sayfa, 'iki-adim-giris');
  await ikinci.sayfa.click('[data-kod-modu]');
  await cek(ikinci.sayfa, 'iki-adim-giris-kurtarma', { mobil: false });
  // "Bu cihazı hatırla" ile gir, çık, yeniden gir: oturum yalnızca şifreyle açılır (amr pwd). Tek Kapı 2FA istediği
  // için kimlik sunucusu ek doğrulama (step-up) gösterir; tamamlanmaz.
  await ikinci.sayfa.click('[data-kod-modu]');
  await ikinci.sayfa.check('#Input_CihaziHatirla');
  await ikinci.sayfa.fill('#Input_Kod', await yeniTotp(anahtar));
  await Promise.all([ikinci.sayfa.waitForNavigation(), ikinci.sayfa.click('form button[type=submit]')]);
  await ikinci.sayfa.waitForSelector('.uygulamalar');
  await git(ikinci.sayfa, KIMLIK + '/connect/logout');
  await Promise.all([ikinci.sayfa.waitForNavigation(), ikinci.sayfa.click('form button[type=submit]')]);
  await git(ikinci.sayfa, KIMLIK + '/');
  await girisYap(ikinci.sayfa, '203011001', SIFRELER.ogrenci);
  await ikinci.sayfa.waitForSelector('.uygulamalar');
  await git(ikinci.sayfa, TEK_KAPI_ADRES + '/');
  await adresiBekle(ikinci.sayfa, /Account\/EkDogrulama/);
  await cek(ikinci.sayfa, 'ek-dogrulama');
  await ikinci.sayfa.click('[data-kod-modu]');
  await cek(ikinci.sayfa, 'ek-dogrulama-kurtarma', { mobil: false });
  await ikinci.baglam.close();

  const hoca = await yeniBaglam(tarayici);
  await git(hoca.sayfa, OBIS + '/Giris');
  await girisYap(hoca.sayfa, 'hoca.test', SIFRELER.akademisyen);
  await izinVer(hoca.sayfa);
  await git(hoca.sayfa, OBIS + '/DersYonetimi');
  await cek(hoca.sayfa, 'obis-ders-yonetimi');
  await hoca.baglam.close();

  const komisyon = await yeniBaglam(tarayici);
  await git(komisyon.sayfa, STAJ + '/Giris');
  await girisYap(komisyon.sayfa, 'komisyon.test', SIFRELER.komisyon);
  await izinVer(komisyon.sayfa);
  await adresiBekle(komisyon.sayfa, new RegExp(`^${STAJ}/$`));
  await git(komisyon.sayfa, STAJ + '/Komisyon/BasvuruInceleme');
  await cek(komisyon.sayfa, 'staj-basvuru-inceleme');
  // Temsili işlem: Onayla gerçek bir POST yapar, veri değişmez, mesaj gösterilir.
  await Promise.all([komisyon.sayfa.waitForNavigation(), komisyon.sayfa.click('form[action*="handler=Onayla"] button')]);
  await komisyon.sayfa.waitForSelector('.bant-uygulama[role=status]');
  await cek(komisyon.sayfa, 'staj-temsili-islem', { mobil: false });
  await komisyon.baglam.close();

  const baskan = await yeniBaglam(tarayici);
  await git(baskan.sayfa, STAJ + '/Giris');
  await girisYap(baskan.sayfa, 'baskan.test', SIFRELER.baskan);
  await izinVer(baskan.sayfa);
  await adresiBekle(baskan.sayfa, new RegExp(`^${STAJ}/$`));
  await git(baskan.sayfa, STAJ + '/Baskan/Ozet');
  await cek(baskan.sayfa, 'staj-ozet');
  await baskan.baglam.close();

  const personel = await yeniBaglam(tarayici);
  await git(personel.sayfa, YEMEKHANE + '/Giris');
  await girisYap(personel.sayfa, 'personel.test', SIFRELER.personel);
  await izinVer(personel.sayfa);
  await git(personel.sayfa, YEMEKHANE + '/YemekhaneYonetimi');
  await cek(personel.sayfa, 'yemekhane-yonetimi');

  // En sonda: istek sınırı (şifremi unuttum sayfası, var olmayan bir @test.local adresiyle; dakikada 10 POST) →
  // 11. gönderimde 429 sayfası. Yalnızca bu sayfanın sayacı dolar.
  await git(personel.sayfa, KIMLIK + '/Account/SifremiUnuttum');
  for (let i = 0; i < 10; i++) {
    await personel.sayfa.request.post(KIMLIK + '/Account/SifremiUnuttum',
      { form: { 'Input.KullaniciAdiVeyaEposta': 'yok@test.local' }, maxRedirects: 0 });
  }
  await personel.sayfa.fill('#Input_KullaniciAdiVeyaEposta', 'yok@test.local');
  const [cokFazla] = await Promise.all([personel.sayfa.waitForNavigation(), personel.sayfa.click('form button[type=submit]')]);
  if (cokFazla?.status() !== 429) throw new Error('İstek sınırı sayfası (429) gelmedi.');
  await cek(personel.sayfa, 'cok-fazla-istek');
  await personel.baglam.close();
}

// ---------- Ana akış ----------

async function main() {
  for (const port of PORTLAR) {
    if (await portDoluMu(port, '127.0.0.1') || await portDoluMu(port, '::1')) {
      console.error(`${port} portu kullanımda. Çalışan uygulamaları kapatıp tekrar deneyin; bu betik onları kapatmaz.`);
      process.exit(1);
    }
  }

  fs.mkdirSync(D2, { recursive: true });
  fs.mkdirSync(TEZ, { recursive: true });
  // Yalnızca seçili grupların eski PNG'leri silinir (d2: <ad>-acik.png; tez: NN-<ad>.png).
  for (const f of fs.readdirSync(D2).filter((x) => x.endsWith('.png'))) {
    if (seciliMi(f.replace(/-(acik|koyu|mobil)\.png$/, ''))) fs.rmSync(path.join(D2, f));
  }
  for (const f of fs.readdirSync(TEZ).filter((x) => x.endsWith('.png'))) {
    if (seciliMi(f.replace(/^\d+-/, '').replace(/\.png$/, ''))) fs.rmSync(path.join(TEZ, f));
  }

  const baslangic = new Date();
  const tekKapiVeri = fs.mkdtempSync(path.join(os.tmpdir(), 'tekkapi-ekran-'));
  if (path.resolve(tekKapiVeri).startsWith(path.join(TEK_KAPI, 'data'))) throw new Error('Geçici veri klasörü gerçek kasanın içinde olamaz.');
  let tekKapi = null;
  let tarayici = null;
  let veritabaniKuruldu = false;
  try {
    console.log('Çözüm derleniyor ve geçici veritabanı kuruluyor...');
    calistir('dotnet', ['build', 'SelcukSSO.sln', '--nologo', '-v', 'q']);
    calistir('dotnet', ['tool', 'restore']);
    veritabaniKuruldu = true;
    calistir('dotnet', ['ef', 'database', 'update', '--no-build', '--project', 'src/Selcuk.Identity'], ORTAM);

    console.log('Uygulamalar başlatılıyor (run-all.ps1 -TarayiciAcma)...');
    calistir('powershell', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join('scripts', 'run-all.ps1'), '-TarayiciAcma'], ORTAM);

    tekKapi = spawn(process.execPath, ['--use-system-ca', 'src/server.js', '--no-browser'], {
      cwd: TEK_KAPI,
      env: { ...process.env, PORTAL_DATA_DIR: tekKapiVeri, HEADLESS: '1', SSO_ISSUER: KIMLIK + '/', SSO_CLIENT_ID: 'tek-kapi' },
      stdio: ['ignore', 'ignore', 'pipe'],
    });
    let tekKapiHatasi = '';
    tekKapi.stderr.on('data', (d) => { tekKapiHatasi += d; });

    tarayici = await chromium.launch();
    // Kimlik sunucusu açılıştaki seed (test kullanıcıları, istemciler) bitince discovery'yi yanıtlar.
    const deneme = await tarayici.newPage();
    let hazir = false;
    for (let i = 0; i < 180 && !hazir; i++) {
      try { hazir = (await deneme.goto(KIMLIK + '/.well-known/openid-configuration'))?.ok() ?? false; } catch { await bekle(500); }
    }
    for (let i = 0; i < 60 && hazir; i++) {
      try { if ((await fetch(TEK_KAPI_ADRES + '/style.css')).ok) break; } catch { await bekle(500); }
    }
    await deneme.close();
    if (!hazir) throw new Error('Kimlik sunucusu hazır olmadı.');
    // Seed'in istemcileri de kaydetmesi için kısa bir pay.
    await bekle(2000);

    await cekimler(tarayici);
    if (tekKapiHatasi.trim()) sorunlar.push('Tek Kapı hata çıktısı: ' + tekKapiHatasi.trim().split('\n')[0]);
  } finally {
    if (tarayici) await tarayici.close();
    if (tekKapi) tekKapi.kill();
    // Yalnızca run-all.ps1'in bu çalıştırmada açtığı pencereler (ve içlerindeki dotnet süreçleri).
    const kapat = powershell(
      `$baslangic = [datetime]::Parse('${baslangic.toISOString()}').ToLocalTime(); ` +
      `Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" | Where-Object { ` +
      `$_.CommandLine -like '*dotnet run --no-build --project*' -and $_.CommandLine -like '*${KOK.replace(/'/g, "''")}*' -and ` +
      `$_.CreationDate -ge $baslangic } | ForEach-Object { taskkill /PID $_.ProcessId /T /F | Out-Null; $_.ProcessId }`);
    const kapanan = (kapat.stdout || '').trim().split(/\s+/).filter(Boolean).length;
    console.log(`Uygulamalar durduruldu (${kapanan} pencere).`);
    await bekle(1500);
    if (veritabaniKuruldu) {
      const silme = spawnSync('dotnet', ['ef', 'database', 'drop', '--force', '--no-build', '--project', 'src/Selcuk.Identity'],
        { cwd: KOK, env: ORTAM, stdio: 'ignore' });
      console.log(silme.status === 0 ? `Geçici veritabanı silindi (${VERITABANI}).` : `Geçici veritabanı silinemedi: ${VERITABANI}`);
    }
    fs.rmSync(tekKapiVeri, { recursive: true, force: true });
  }

  console.log(`\n${cekilenler.length} ekran çekildi: docs/tasarim/d2/, ${cekilenler.filter((a) => TEZ_SIRASI.includes(a)).length} tez görseli: docs/tez-gorselleri/`);
  if (sorunlar.length) {
    console.log('\nDikkat:');
    for (const s of [...new Set(sorunlar)]) console.log('  - ' + s);
  }
}

main().catch((hata) => {
  console.error('\nHATA: ' + hata.message);
  process.exitCode = 1;
});
