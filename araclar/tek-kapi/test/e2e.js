// e2e.js — sahte sitelerle uçtan uca test
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const assert = require('assert/strict');
const mock = require('./mock-sites');

const PORT = 4646;
const BASE = `http://127.0.0.1:${PORT}`;
const DATA = fs.mkdtempSync(path.join(os.tmpdir(), 'tekkapi-'));
const MASTER = 'cok-gizli-ana-sifre';
let cookie = '';
let passed = 0;

async function api(p, { method = 'GET', body, headers = {} } = {}) {
  const res = await fetch(BASE + '/api' + p, {
    method,
    headers: { 'x-tek-kapi': '1', cookie, ...(body ? { 'content-type': 'application/json' } : {}), ...headers },
    body: body ? JSON.stringify(body) : undefined,
  });
  const sc = res.headers.get('set-cookie');
  if (sc) cookie = sc.split(';')[0];
  const data = await res.json().catch(() => ({}));
  return { status: res.status, data };
}
const dbg = (body) => api('/_debug/eval', { method: 'POST', body }).then((r) => r.data);
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function waitFor(fn, label, ms = 15000) {
  const t0 = Date.now();
  while (Date.now() - t0 < ms) { try { const v = await fn(); if (v) return v; } catch {} await sleep(250); }
  throw new Error('Zaman aşımı: ' + label);
}
const pageWith = async (part) => (await api('/_debug/pages')).data.find((p) => p.url.includes(part));
function ok(name) { passed++; console.log('  ✓ ' + name); }

(async () => {
  await mock.start();
  const srv = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'server.js')], {
    env: {
      ...process.env, PORT: String(PORT), HEADLESS: '1', PORTAL_TEST: '1', PORTAL_DATA_DIR: DATA,
      SITE_ORIGIN_OVERRIDES: JSON.stringify(mock.URLS),
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const logs = [];
  srv.stdout.on('data', (d) => logs.push(d.toString()));
  srv.stderr.on('data', (d) => logs.push(d.toString()));
  try {
    await waitFor(async () => (await fetch(BASE + '/')).ok, 'sunucu');

    console.log('Güvenlik');
    assert.equal((await fetch(BASE + '/api/state')).status, 403); ok('Özel başlık olmadan API reddediliyor');
    const badHost = await new Promise((resolve) => {
      require('http').get({ host: '127.0.0.1', port: PORT, path: '/', headers: { host: 'evil.example:4646' } }, (r) => resolve(r.statusCode));
    });
    assert.equal(badHost, 421); ok('Yabancı Host başlığı reddediliyor (DNS rebinding)');
    assert.equal((await api('/sites')).status, 401); ok('Oturum olmadan siteler gizli');

    console.log('Kasa');
    assert.equal((await api('/setup', { method: 'POST', body: { master: 'kisa' } })).status, 400); ok('Kısa ana şifre reddediliyor');
    assert.equal((await api('/setup', { method: 'POST', body: { master: MASTER } })).status, 200); ok('Kasa oluşturuldu');
    let r = await api('/credentials', { method: 'PUT', body: { ogrNo: mock.USER.ogrNo, obisPass: mock.USER.obisPass, ogrMail: mock.USER.ogrMail, mailPass: mock.USER.mailPass } });
    assert.equal(r.status, 200); ok('Bilgiler kaydedildi');
    r = await api('/credentials');
    assert.equal(r.data.ogrNo, mock.USER.ogrNo);
    assert.ok(!JSON.stringify(r.data).includes(mock.USER.obisPass) && !JSON.stringify(r.data).includes(mock.USER.mailPass));
    ok('Şifreler arayüze geri gönderilmiyor');
    const raw = fs.readFileSync(path.join(DATA, 'vault.json'), 'utf8');
    assert.ok(!raw.includes(mock.USER.obisPass) && !raw.includes(mock.USER.mailPass) && !raw.includes(mock.USER.ogrNo));
    ok('Kasa dosyasında düz metin yok');
    r = await api('/sites');
    assert.ok(r.data.every((s) => s.ready)); ok('Beş sistem de hazır');

    console.log('Otomatik giriş');
    await api('/open/posta', { method: 'POST' });
    await waitFor(async () => (await pageWith('5102'))?.title.includes('Gelen Kutusu'), 'posta gelen kutusu');
    assert.equal(mock.counters.postaPost, 1); ok('E-posta (Zimbra): tek tıkla gelen kutusu');

    await api('/open/posta', { method: 'POST' });
    await waitFor(async () => (await api('/events')).data.some((e) => /Oturum zaten açık/.test(e.text)), 'oturum hatırlandı');
    assert.equal(mock.counters.postaPost, 1); ok('E-posta: ikinci tıklamada oturum hatırlanıyor, yeniden giriş yok');

    await api('/open/uzem', { method: 'POST' });
    await waitFor(async () => (await pageWith('5103/my/')), 'uzem panom');
    ok('UZEM (Moodle): tek tıkla Panom');

    await api('/open/sinav', { method: 'POST' });
    await waitFor(async () => (await pageWith('5106/my/')), 'sinav panom');
    assert.equal(mock.counters.sinavPost, 1); ok('Uzaktan Sınav (Moodle): tek tıkla Panom');

    console.log('Yarı otomatik giriş');
    await api('/open/obis', { method: 'POST' });
    await waitFor(async () => (await dbg({ urlIncludes: '5101', action: 'value', selector: 'input[name="id"]' })).value === mock.USER.ogrNo, 'obis dolum');
    assert.equal((await dbg({ urlIncludes: '5101', action: 'focused' })).value, 'TxtCaptcha');
    assert.equal(mock.counters.obisPost, 0);
    ok('OBİS: bilgiler dolduruldu, imleç CAPTCHA\'da, form gönderilmedi');
    await dbg({ urlIncludes: '5101', action: 'fillAndEnter', selector: '#TxtCaptcha', value: mock.CAPTCHA });
    await waitFor(async () => (await pageWith('5101/Home/Dashboard')), 'obis dashboard');
    ok('OBİS: kullanıcı kodu yazınca giriş tamam');

    await api('/open/yemek', { method: 'POST' });
    await waitFor(async () => (await dbg({ urlIncludes: '5105', action: 'value', selector: 'input[name="Username"]' })).value === 'talha.test', 'sso dolum');
    assert.equal((await dbg({ urlIncludes: '5105', action: 'value', selector: 'select[name="EmailDomain"]' })).value, '2');
    assert.equal(mock.counters.ssoPost, 0);
    ok('SSO: kullanıcı adı + @ogr alan adı + şifre dolduruldu, gönderilmedi');
    await dbg({ urlIncludes: '5105', action: 'click', selector: 'button[type=submit]' });
    await waitFor(async () => (await dbg({ urlIncludes: '5105', action: 'text' })).value.includes('Doğrula'), 'sso 2fa');
    await dbg({ urlIncludes: '5105', action: 'fillAndEnter', selector: '#Code', value: mock.TOTP });
    await waitFor(async () => (await pageWith('5104'))?.title.includes('Yemek Rezervasyon'), 'yemek');
    ok('Yemekhane: kullanıcı "Giriş Yap" + 2FA ile içeride');

    console.log('Ölçüm kaydı');
    const sumOf = async (id) => (await api('/metrics/summary')).data.sites.find((s) => s.id === id);
    await waitFor(async () => (await sumOf('yemek')).giris === 1, 'yemek ölçümü');
    const mp = await sumOf('posta');
    assert.ok(mp.giris === 1 && mp.oturumHatirlandi === 1 && mp.ortGirisMs > 0); ok('Otomatik giriş ve hatırlanan oturum ayrı ölçülüyor');
    const mo = await sumOf('obis');
    assert.ok(mo.giris === 1 && mo.ortKullaniciMs > 0 && mo.ortKullaniciMs <= mo.ortGirisMs); ok('OBİS: kullanıcının CAPTCHA için harcadığı süre ayrı ölçülüyor');
    const my = await sumOf('yemek');
    assert.ok(my.ortKullaniciMs > 0 && my.basarisiz === 0); ok('Yemekhane: SSO ara sayfaları (2FA) erken "tamam" sayılmıyor');

    console.log('Hesap kilitletmeme koruması');
    await api('/credentials', { method: 'PUT', body: { mailPass: 'YANLIS-sifre' } });
    await dbg({ urlIncludes: '5102', action: 'goto', value: mock.URLS.posta + '/logout' });
    await waitFor(async () => (await api('/sites')).data.find((s) => s.id === 'posta').failed, 'posta failed işareti');
    const before = mock.counters.postaPost;
    await api('/open/posta', { method: 'POST' });
    await sleep(2500);
    assert.equal(mock.counters.postaPost, before); ok('Yanlış şifreyle bir kez denendi, sonra bir daha gönderilmedi');
    await api('/credentials', { method: 'PUT', body: { mailPass: mock.USER.mailPass } });
    assert.equal((await api('/sites')).data.find((s) => s.id === 'posta').failed, false); ok('Şifre düzeltilince işaret kalkıyor');
    assert.equal((await sumOf('posta')).basarisiz, 1); ok('Başarısız deneme ölçüme "basarisiz" olarak yazılıyor');

    console.log('Hepsinden çık');
    const lo = await api('/logout-all', { method: 'POST' });
    const mockOrigins = Object.values(mock.URLS);
    const left = (await api('/_debug/pages')).data.filter((p) => mockOrigins.some((o) => p.url.startsWith(o)));
    assert.ok(lo.data.tabsClosed >= 1 && lo.data.cookiesCleared >= 1 && left.length === 0); ok('Tüm site sekmeleri kapandı, oturum çerezleri silindi');
    const beforeRelogin = mock.counters.postaPost;
    await api('/open/posta', { method: 'POST' });
    await waitFor(async () => (await pageWith('5102'))?.title.includes('Gelen Kutusu'), 'posta yeniden giriş');
    assert.equal(mock.counters.postaPost, beforeRelogin + 1); ok('Çıkıştan sonra site gerçekten yeniden giriş istiyor');

    console.log('Ölçüm dosyası ve başlatma');
    const csvRes = await fetch(BASE + '/export/metrics.csv', { headers: { cookie } });
    const csv = await csvRes.text();
    const noAuth = await fetch(BASE + '/export/metrics.csv');
    assert.equal(csvRes.status, 200); assert.equal(noAuth.status, 401);
    assert.ok(csv.includes('siteId,site,mode') && csv.includes(',giris,') && csv.includes(',oturum-hatirlandi,'));
    for (const secret of [mock.USER.obisPass, mock.USER.mailPass, mock.USER.ogrNo, mock.USER.ogrMail, 'YANLIS-sifre']) assert.ok(!csv.includes(secret));
    ok('CSV indiriliyor, oturumsuz erişilemiyor, içinde kimlik bilgisi yok');
    assert.equal((await api('/focus', { method: 'POST' })).status, 200);
    assert.ok((await api('/_debug/pages')).data.some((p) => p.url.startsWith(BASE))); ok('Portal sekmesi öne getirilebiliyor');
    const second = await new Promise((resolve) => {
      const p2 = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'server.js')], {
        env: { ...process.env, PORT: String(PORT), HEADLESS: '1', PORTAL_DATA_DIR: DATA }, stdio: ['ignore', 'pipe', 'pipe'],
      });
      let out = '';
      p2.stdout.on('data', (d) => { out += d; });
      p2.on('exit', (code) => resolve({ code, out }));
    });
    assert.ok(second.code === 0 && second.out.includes('zaten çalışıyor')); ok('İkinci kez başlatınca yeni kopya açılmıyor, mevcut pencere öne geliyor');

    console.log('Kilit');
    await api('/lock', { method: 'POST' });
    assert.equal((await api('/sites')).status, 401); ok('Kilitlenince API kapalı');
    assert.equal((await api('/unlock', { method: 'POST', body: { master: 'yanlis-ana-sifre' } })).status, 400); ok('Yanlış ana şifre reddediliyor');
    assert.equal((await api('/unlock', { method: 'POST', body: { master: MASTER } })).status, 200); ok('Doğru ana şifreyle açılıyor');
    assert.equal((await api('/credentials')).data.ogrMail, mock.USER.ogrMail); ok('Bilgiler diskten doğru çözülüyor');

    const ev = (await api('/events')).data.map((e) => e.text).join('\n');
    for (const secret of [mock.USER.obisPass, mock.USER.mailPass, 'YANLIS-sifre', MASTER]) assert.ok(!ev.includes(secret) && !logs.join('').includes(secret));
    ok('Günlüklerde şifre yok');

    console.log(`\n${passed} kontrol geçti.`);
  } catch (e) {
    console.error('\nHATA:', e.message);
    console.error(logs.join('').split('\n').slice(-30).join('\n'));
    process.exitCode = 1;
  } finally {
    srv.kill('SIGTERM');
    setTimeout(() => process.exit(), 1500);
  }
})();
