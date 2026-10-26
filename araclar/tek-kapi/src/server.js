// server.js — Tek Kapı yerel portal sunucusu
//
// Yalnızca 127.0.0.1'de dinler. Başka sitelerin bu portala istek atamaması için:
//   - Host başlığı denetlenir (DNS rebinding koruması)
//   - API çağrıları özel bir başlık ister (tarayıcı bunu çapraz sitede preflight'sız gönderemez)
//   - Oturum çerezi HttpOnly + SameSite=Strict
// SSO modunda (SSO_ISSUER tanımlıysa, bkz. sso.js) portala önce SSO oturumu, sonra kasa ana şifresiyle girilir.

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const express = require('express');
const { Vault } = require('./vault');
const { SITES } = require('./sites');
const { BrowserEngine } = require('./browser');
const { createSso } = require('./sso');

const PORT = Number(process.env.PORT || 4545);
const HOST = '127.0.0.1';
const DATA_DIR = process.env.PORTAL_DATA_DIR || path.join(__dirname, '..', 'data');
const HEADLESS = process.env.HEADLESS === '1';
const TEST = process.env.PORTAL_TEST === '1';
const NO_BROWSER_ON_START = process.argv.includes('--no-browser') || HEADLESS;
const PORTAL_URL = `http://${HOST}:${PORT}/`;

const CRED_FIELDS = ['ogrNo', 'ogrMail'];
const SECRET_FIELDS = ['obisPass', 'mailPass', 'ssoPass'];

const vault = new Vault(path.join(DATA_DIR, 'vault.json'), {
  idleMs: Number(process.env.IDLE_LOCK_MIN || 15) * 60 * 1000,
});

// Basit olay günlüğü (arayüzdeki "son işlemler" listesi). Şifre içermez.
const events = [];
let eventSeq = 0;
function log(source, text) {
  events.push({ id: ++eventSeq, at: Date.now(), source, text });
  if (events.length > 100) events.shift();
  console.log(`[${new Date().toLocaleTimeString('tr-TR')}] ${source}: ${text}`);
}

// Ölçüm kaydı: data/metrics.csv (tez değerlendirmesi için). Yalnızca süre ve sonuç tutulur, şifre ya da kullanıcı adı tutulmaz.
const METRICS_FILE = path.join(DATA_DIR, 'metrics.csv');
const METRIC_COLS = ['at', 'siteId', 'site', 'mode', 'kaynak', 'sonuc', 'toplamMs', 'otomatikMs', 'kullaniciMs', 'kullaniciAdimi'];
const csvCell = (v) => { const t = String(v ?? ''); return /[",;\n]/.test(t) ? '"' + t.replace(/"/g, '""') + '"' : t; };
function recordMetric(m) {
  try {
    fs.mkdirSync(DATA_DIR, { recursive: true });
    if (!fs.existsSync(METRICS_FILE)) fs.writeFileSync(METRICS_FILE, '\ufeff' + METRIC_COLS.join(',') + '\n');
    fs.appendFileSync(METRICS_FILE, METRIC_COLS.map((k) => csvCell(m[k])).join(',') + '\n');
  } catch (e) { console.error('Ölçüm yazılamadı:', e.message); }
  const sn = (m.toplamMs / 1000).toFixed(1).replace('.', ',');
  const label = { giris: 'Giriş tamamlandı', 'oturum-hatirlandi': 'Oturum hatırlandı', basarisiz: 'Giriş başarısız' }[m.sonuc] || m.sonuc;
  log(m.site, `${label}, ${sn} sn ölçüldü`);
}
function readMetrics() {
  if (!fs.existsSync(METRICS_FILE)) return [];
  const lines = fs.readFileSync(METRICS_FILE, 'utf8').replace(/^\ufeff/, '').trim().split('\n').slice(1);
  return lines.filter(Boolean).map((line) => {
    const cells = line.match(/("([^"]|"")*"|[^,]*)(,|$)/g).map((c) => c.replace(/,$/, '').replace(/^"|"$/g, '').replace(/""/g, '"'));
    const o = {}; METRIC_COLS.forEach((k, i) => { o[k] = cells[i]; });
    for (const k of ['toplamMs', 'otomatikMs', 'kullaniciMs', 'kullaniciAdimi']) o[k] = Number(o[k]) || 0;
    return o;
  });
}

const engine = new BrowserEngine({
  profileDir: path.join(DATA_DIR, 'browser-profile'),
  headless: HEADLESS,
  sites: SITES,
  getCreds: () => (vault.isUnlocked() ? vault.get() : null),
  log,
  onMeasure: recordMetric,
});

let sessionToken = null;

// SSO modu (isteğe bağlı). Çıkışta (kullanıcı veya Back-Channel Logout) kasa kilitlenir ve yönetilen tarayıcıdaki
// tüm site oturumları kapatılır (mevcut "Hepsinden çık").
let sso;
try {
  sso = createSso({
    dataDir: DATA_DIR,
    port: PORT,
    test: TEST,
    log,
    kapat: async () => {
      vault.lock();
      sessionToken = null;
      await engine.logoutAll();
    },
  });
} catch (e) {
  console.error('SSO ayarları geçersiz: ' + e.message);
  process.exit(1);
}

const app = express();
app.disable('x-powered-by');
app.use(express.json({ limit: '16kb' }));

app.use((req, res, next) => {
  const ok = [`${HOST}:${PORT}`, `localhost:${PORT}`];
  if (!ok.includes(req.headers.host)) return res.status(421).send('Yanlış host');
  res.set({
    'X-Frame-Options': 'DENY',
    'Referrer-Policy': 'no-referrer',
    'Cache-Control': 'no-store',
    'Content-Security-Policy': "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'",
  });
  next();
});

app.use('/api', (req, res, next) => {
  if (req.get('x-tek-kapi') !== '1') return res.status(403).json({ error: 'Eksik başlık' });
  next();
});

// SSO modunda API önce SSO oturumu ister. Durum (arayüz yönlendirmesi), odaklama (ikinci kopya) ve kilitleme serbest.
const SSO_SERBEST = new Set(['/state', '/focus', '/lock']);
app.use('/api', (req, res, next) => (SSO_SERBEST.has(req.path) ? next() : sso.gerekli(req, res, next)));
sso.rotalar(app);

function readCookie(req, name) {
  const m = (req.headers.cookie || '').split(/;\s*/).find((c) => c.startsWith(name + '='));
  return m ? decodeURIComponent(m.slice(name.length + 1)) : null;
}

function startSession(res) {
  sessionToken = crypto.randomBytes(32).toString('hex');
  res.setHeader('Set-Cookie', `tk_session=${sessionToken}; HttpOnly; SameSite=Strict; Path=/`);
}

function auth(req, res, next) {
  const t = readCookie(req, 'tk_session');
  const valid = t && sessionToken && t.length === sessionToken.length &&
    crypto.timingSafeEqual(Buffer.from(t), Buffer.from(sessionToken));
  if (!valid || !vault.isUnlocked()) return res.status(401).json({ error: 'Kilitli' });
  next();
}

const wrap = (fn) => (req, res) => Promise.resolve(fn(req, res)).catch((e) => res.status(400).json({ error: e.message }));

app.get('/api/state', (req, res) => {
  const t = readCookie(req, 'tk_session');
  const ssoDurum = sso.durum(req);
  res.json({
    vaultExists: vault.exists(),
    unlocked: vault.isUnlocked() && !!t && t === sessionToken && (!ssoDurum.etkin || ssoDurum.girisYapildi),
    browserRunning: engine.running(),
    sso: ssoDurum,
  });
});

app.post('/api/setup', wrap(async (req, res) => {
  const { master } = req.body || {};
  if (typeof master !== 'string' || master.length < 6) throw new Error('Ana şifre en az 6 karakter olmalı');
  await vault.create(master);
  startSession(res);
  log('sistem', 'Kasa oluşturuldu');
  res.json({ ok: true });
}));

app.post('/api/unlock', wrap(async (req, res) => {
  const { master } = req.body || {};
  if (typeof master !== 'string') throw new Error('Ana şifre gerekli');
  await vault.unlock(master);
  startSession(res);
  log('sistem', 'Kasa açıldı');
  res.json({ ok: true });
}));

app.post('/api/lock', (req, res) => {
  vault.lock();
  sessionToken = null;
  res.setHeader('Set-Cookie', 'tk_session=; HttpOnly; SameSite=Strict; Path=/; Max-Age=0');
  log('sistem', 'Kasa kilitlendi');
  res.json({ ok: true });
});

app.get('/api/sites', auth, (req, res) => {
  const c = vault.get();
  res.json(SITES.map((s) => ({
    id: s.id, name: s.name, desc: s.desc, mode: s.mode, assistNote: s.assistNote || '',
    host: new URL(s.startUrl).host,
    ready: s.needs.every((k) => !!c[k]),
    failed: engine.siteState(s.id).failed,
  })));
});

app.get('/api/credentials', auth, (req, res) => {
  const c = vault.get();
  const out = { has: {} };
  for (const k of CRED_FIELDS) out[k] = c[k] || '';
  for (const k of SECRET_FIELDS) out.has[k] = !!c[k];
  res.json(out); // şifreler asla arayüze geri gönderilmez
});

app.put('/api/credentials', auth, wrap(async (req, res) => {
  const b = req.body || {};
  const patch = {};
  if (typeof b.ogrNo === 'string') {
    const v = b.ogrNo.trim();
    if (v && !/^[A-Za-z0-9]{4,20}$/.test(v)) throw new Error('Öğrenci numarası geçersiz görünüyor');
    patch.ogrNo = v || null;
  }
  if (typeof b.ogrMail === 'string') {
    const v = b.ogrMail.trim().toLowerCase();
    if (v && !/^[^@\s]+@[a-z0-9.-]+\.[a-z]{2,}$/.test(v)) throw new Error('E-posta adresi geçersiz görünüyor');
    patch.ogrMail = v || null;
  }
  for (const k of SECRET_FIELDS) {
    if (typeof b[k] === 'string' && b[k].length) patch[k] = b[k];
  }
  for (const k of Array.isArray(b.clear) ? b.clear : []) if (SECRET_FIELDS.includes(k)) patch[k] = null;
  vault.update(patch);
  engine.resetFailures();
  log('sistem', 'Bilgiler güncellendi');
  res.json({ ok: true });
}));

app.post('/api/open/:id', auth, wrap(async (req, res) => {
  const site = SITES.find((s) => s.id === req.params.id);
  if (!site) throw new Error('Bilinmeyen site');
  engine.open(site.id).catch((e) => log(site.name, e.message));
  res.json({ ok: true });
}));

app.post('/api/logout-all', auth, wrap(async (req, res) => {
  res.json(await engine.logoutAll());
}));

// İkinci kez başlatılınca çalışan örneğin penceresini öne getirmek için (kimlik doğrulaması gerekmez, sadece portal sekmesini açar)
app.post('/api/focus', wrap(async (req, res) => {
  await engine.focus(PORTAL_URL);
  res.json({ ok: true });
}));

app.get('/api/metrics/summary', auth, (req, res) => {
  const rows = readMetrics();
  const avg = (a) => (a.length ? Math.round(a.reduce((x, y) => x + y, 0) / a.length) : null);
  const out = SITES.map((s) => {
    const mine = rows.filter((r) => r.siteId === s.id);
    const giris = mine.filter((r) => r.sonuc === 'giris');
    const hatir = mine.filter((r) => r.sonuc === 'oturum-hatirlandi');
    return {
      id: s.id, name: s.name, mode: s.mode,
      toplam: mine.length,
      giris: giris.length,
      oturumHatirlandi: hatir.length,
      basarisiz: mine.filter((r) => r.sonuc === 'basarisiz').length,
      ortGirisMs: avg(giris.map((r) => r.toplamMs)),
      ortKullaniciMs: avg(giris.map((r) => r.kullaniciMs)),
      ortHatirlandiMs: avg(hatir.map((r) => r.toplamMs)),
    };
  });
  res.json({ count: rows.length, sites: out });
});

app.get('/api/events', auth, (req, res) => {
  const since = Number(req.query.since || 0);
  res.json(events.filter((e) => e.id > since));
});

if (TEST) {
  app.get('/api/_debug/pages', auth, wrap(async (req, res) => res.json(await engine.pages())));
  app.post('/api/_debug/eval', auth, wrap(async (req, res) => {
    // Yalnızca test modunda: belirli bir sekmede kullanıcı eylemini taklit etmek için
    const { urlIncludes, action, selector, value } = req.body;
    const page = engine.context.pages().find((p) => p.url().includes(urlIncludes));
    if (!page) throw new Error('Sekme yok');
    if (action === 'value') return res.json({ value: await page.inputValue(selector) });
    if (action === 'fillAndEnter') { await page.fill(selector, value); await page.press(selector, 'Enter'); return res.json({ ok: true }); }
    if (action === 'click') { await page.click(selector); return res.json({ ok: true }); }
    if (action === 'focused') return res.json({ value: await page.evaluate(() => document.activeElement && (document.activeElement.name || document.activeElement.id)) });
    if (action === 'text') return res.json({ value: await page.innerText('body') });
    if (action === 'goto') { await page.goto(value); return res.json({ ok: true }); }
    throw new Error('Bilinmeyen eylem');
  }));
}

// Normal bir bağlantıyla indirilebilsin diye /api dışında; SameSite=Strict oturum çereziyle korunuyor
app.get('/export/metrics.csv', sso.gerekli, auth, (req, res) => {
  res.set({ 'Content-Type': 'text/csv; charset=utf-8', 'Content-Disposition': 'attachment; filename="tek-kapi-olcumler.csv"' });
  if (!fs.existsSync(METRICS_FILE)) return res.send('\ufeff' + METRIC_COLS.join(',') + '\n');
  res.send(fs.readFileSync(METRICS_FILE));
});

// SSO modunda portal sayfası SSO oturumu olmadan açılmaz (girişe yönlendirilir); stil ve betik dosyaları serbest.
app.get(['/', '/index.html'], sso.sayfaGeregi);
app.use(express.static(path.join(__dirname, '..', 'public'), { index: 'index.html' }));

const server = app.listen(PORT, HOST, async () => {
  log('sistem', `Portal hazır: ${PORTAL_URL}`);
  if (!NO_BROWSER_ON_START) {
    try { await engine.openUrl(PORTAL_URL); } catch (e) { log('sistem', e.message); }
  }
});

// Tek Kapı zaten çalışıyorsa ikinci bir kopya açma; çalışanın penceresini öne getir
server.on('error', async (err) => {
  if (err.code !== 'EADDRINUSE') { console.error(err); process.exit(1); }
  try {
    const r = await fetch(PORTAL_URL + 'api/focus', { method: 'POST', headers: { 'x-tek-kapi': '1' } });
    if (!r.ok) throw new Error();
    console.log('Tek Kapı zaten çalışıyor; penceresi öne getirildi.');
    process.exit(0);
  } catch {
    console.error(`${PORT} portu başka bir program tarafından kullanılıyor. PORT ortam değişkeniyle başka bir port seç.`);
    process.exit(1);
  }
});

async function shutdown() {
  vault.lock();
  await engine.close();
  server.close(() => process.exit(0));
  setTimeout(() => process.exit(0), 2000).unref();
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
