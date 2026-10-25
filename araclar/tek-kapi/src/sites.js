// sites.js — desteklenen otomasyonlar ve her birinin giriş adaptörü
//
// Her site için:
//   startUrl   : karta tıklayınca açılacak adres
//   mode       : 'auto'   -> alanları doldurur ve formu gönderir
//                'assist' -> alanları doldurur, gönderme/doğrulama kullanıcıda kalır
//                            (CAPTCHA veya bot korumalı sayfalar; bunları atlatmaya çalışmıyoruz)
//   needs      : kasada bulunması gereken alanlar
//   loginPage  : { match(url), detect(page), fill(page, creds) }
//
// Form alan adları 18.10.2026 tarihinde canlı giriş sayfalarından okunmuştur.
// Üniversite formu değiştirirse yalnızca ilgili adaptör güncellenir.

// Testlerde gerçek alan adları yerine sahte sunucular kullanmak için:
// SITE_ORIGIN_OVERRIDES='{"obis":"http://127.0.0.1:5101", ...}'
const OVERRIDES = (() => {
  try { return JSON.parse(process.env.SITE_ORIGIN_OVERRIDES || '{}'); } catch { return {}; }
})();
const origin = (key, real) => OVERRIDES[key] || real;

const ORIGINS = {
  obisEntry: origin('obisEntry', 'https://obis.selcuk.edu.tr'),
  obis: origin('obis', 'https://obis2.selcuk.edu.tr'),
  posta: origin('posta', 'https://ogrposta.selcuk.edu.tr'),
  uzem: origin('uzem', 'https://uzaktanogrenme5.selcuk.edu.tr'),
  sinav: origin('sinav', 'https://uzaktansinav.selcuk.edu.tr'),
  yemek: origin('yemek', 'https://yemek.selcuk.edu.tr'),
  sso: origin('sso', 'https://sso.selcuk.edu.tr'),
};

const onOrigin = (url, o) => {
  try { return new URL(url).origin === new URL(o).origin; } catch { return false; }
};
const visible = async (page, sel) => (await page.locator(sel).count()) > 0;

const localPart = (mail) => String(mail || '').split('@')[0];
const domainPart = (mail) => String(mail || '').split('@')[1] || 'ogr.selcuk.edu.tr';

// UZEM ve Uzaktan Sınav aynı Moodle giriş formunu kullanır
const moodleLoginPage = (originUrl) => ({
  match: (url) => onOrigin(url, originUrl) && new URL(url).pathname.startsWith('/login'),
  detect: async (page) =>
    (await visible(page, 'input[name="logintoken"]')) && (await visible(page, 'input[name="username"]')),
  fill: async (page, c) => {
    await page.fill('input[name="username"]', c.ogrNo);
    await page.fill('input[name="password"]', c.obisPass);
    const btn = (await visible(page, '#loginbtn'))
      ? page.locator('#loginbtn')
      : page.locator('form:has(input[name="logintoken"]) [type="submit"]').first();
    await Promise.all([page.waitForLoadState('load').catch(() => {}), btn.click()]);
    return { submitted: true, message: 'Giriş yapılıyor…' };
  },
});

const SITES = [
  {
    id: 'obis',
    name: 'OBİS',
    desc: 'Öğrenci Bilgi Sistemi: notlar, ders kaydı, transkript',
    startUrl: ORIGINS.obisEntry + '/',
    origins: [ORIGINS.obis, ORIGINS.obisEntry],
    mode: 'assist',
    assistNote: 'Doğrulama kodunu (CAPTCHA) sen yazıp Enter\'a basıyorsun.',
    userSteps: 1, // kodu yaz + Enter
    needs: ['ogrNo', 'obisPass'],
    loginPage: {
      match: (url) => onOrigin(url, ORIGINS.obis) || onOrigin(url, ORIGINS.obisEntry),
      detect: async (page) =>
        (await visible(page, 'input[name="id"]')) && (await visible(page, 'input[name="pass"][type="password"]')),
      fill: async (page, c) => {
        await page.fill('input[name="id"]', c.ogrNo);
        await page.fill('input[name="pass"]', c.obisPass);
        if (await visible(page, '#TxtCaptcha')) await page.focus('#TxtCaptcha');
        return { submitted: false, message: 'Bilgiler dolduruldu. Resimdeki doğrulama kodunu yaz ve Enter\'a bas.' };
      },
    },
  },
  {
    id: 'posta',
    name: 'Öğrenci E-postası',
    desc: 'Zimbra: @ogr.selcuk.edu.tr posta kutusu',
    startUrl: ORIGINS.posta + '/',
    origins: [ORIGINS.posta],
    mode: 'auto',
    needs: ['ogrMail', 'mailPass'],
    loginPage: {
      match: (url) => onOrigin(url, ORIGINS.posta),
      detect: async (page) =>
        (await visible(page, 'input[name="login_csrf"]')) && (await visible(page, 'input[name="username"]')),
      fill: async (page, c) => {
        await page.fill('input[name="username"]', c.ogrMail);
        await page.fill('input[name="password"]', c.mailPass);
        if (await visible(page, 'input[name="zrememberme"]')) await page.check('input[name="zrememberme"]');
        await Promise.all([
          page.waitForLoadState('load').catch(() => {}),
          page.locator('form:has(input[name="login_csrf"]) [type="submit"]').first().click(),
        ]);
        return { submitted: true, message: 'Giriş yapılıyor…' };
      },
    },
  },
  {
    id: 'uzem',
    name: 'UZEM',
    desc: 'Uzaktan Eğitim Sistemi (Moodle): ders içerikleri, ödevler',
    startUrl: ORIGINS.uzem + '/login/index.php',
    origins: [ORIGINS.uzem],
    mode: 'auto',
    needs: ['ogrNo', 'obisPass'],
    loginPage: moodleLoginPage(ORIGINS.uzem),
  },
  {
    id: 'sinav',
    name: 'Uzaktan Sınav',
    desc: 'Uzaktan Sınav Sistemi (Moodle): ortak zorunlu ders sınavları',
    startUrl: ORIGINS.sinav + '/login/index.php',
    origins: [ORIGINS.sinav],
    mode: 'auto',
    needs: ['ogrNo', 'obisPass'],
    loginPage: moodleLoginPage(ORIGINS.sinav),
  },
  {
    id: 'yemek',
    name: 'Yemekhane',
    desc: 'Yemek rezervasyonu (üniversitenin SSO sunucusu üzerinden)',
    startUrl: ORIGINS.yemek + '/',
    origins: [ORIGINS.yemek, ORIGINS.sso],
    appOrigins: [ORIGINS.yemek], // SSO'nun ara sayfaları (2FA vb.) "giriş tamam" sayılmaz
    mode: 'assist',
    assistNote: 'Selçuk SSO reCAPTCHA ve iki aşamalı doğrulama kullanıyor. "Giriş Yap"a ve 2FA koduna sen basıyorsun.',
    userSteps: 2, // "Giriş Yap" + 2FA kodu
    needs: ['ogrMail', 'mailPass'],
    loginPage: {
      // sso.selcuk.edu.tr'ye yönlenen her uygulama için çalışır (yemekhane ve diğerleri)
      match: (url) => onOrigin(url, ORIGINS.sso) && /\/account\/login/i.test(new URL(url).pathname),
      detect: async (page) =>
        (await visible(page, 'input[name="Username"]')) && (await visible(page, 'input[name="Password"]')),
      fill: async (page, c) => {
        await page.fill('input[name="Username"]', localPart(c.ogrMail));
        if (await visible(page, 'select[name="EmailDomain"]')) {
          const want = domainPart(c.ogrMail);
          const value = await page.$eval('select[name="EmailDomain"]', (sel, want) => {
            const opt = [...sel.options].find((o) => (o.value + ' ' + o.textContent).toLowerCase().includes(want.toLowerCase()));
            return opt ? opt.value : null;
          }, want);
          if (value !== null) await page.selectOption('select[name="EmailDomain"]', value);
        }
        await page.fill('input[name="Password"]', c.ssoPass || c.mailPass);
        await page.locator('form:has(input[name="Password"]) [type="submit"]').first().focus();
        return { submitted: false, message: 'Bilgiler dolduruldu. "Giriş Yap"a bas, ardından 2FA kodunu gir.' };
      },
    },
  },
];

for (const s of SITES) {
  s.origins = s.origins.map((o) => new URL(o).origin);
  s.appOrigins = (s.appOrigins || s.origins).map((o) => new URL(o).origin);
}

module.exports = { SITES, ORIGINS };
