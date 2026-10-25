// sso.js — SSO modu (OIDC public istemci + Back-Channel Logout) uçtan uca testi
// Gerçek kimlik sunucusuna bağlanmaz: test/fake-oidc.js sahte sağlayıcı, test/mock-sites.js sahte Selçuk siteleri.
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const assert = require('assert/strict');
const mock = require('./mock-sites');
const oidc = require('./fake-oidc');

const PORT = 4647;
const BASE = `http://127.0.0.1:${PORT}`;
const REDIRECT_URI = `${BASE}/auth/callback`;
const DATA = fs.mkdtempSync(path.join(os.tmpdir(), 'tekkapi-sso-'));
const MASTER = 'sso-testi-ana-sifre';
let passed = 0;

// Basit çerez kavanozu (Path ve süre yalnızca silme için dikkate alınır)
class Kavanoz {
  constructor() { this.c = new Map(); }
  al(res) {
    for (const sc of res.headers.getSetCookie()) {
      const [ad, ...deger] = sc.split(';')[0].split('=');
      if (/Max-Age=0/i.test(sc)) this.c.delete(ad); else this.c.set(ad, deger.join('='));
    }
  }
  baslik() { return [...this.c].map(([k, v]) => `${k}=${v}`).join('; '); }
}

async function istek(jar, url, { method = 'GET', body, form, headers = {} } = {}) {
  const res = await fetch(url.startsWith('http') ? url : BASE + url, {
    method,
    redirect: 'manual',
    headers: {
      ...(jar && jar.c.size ? { cookie: jar.baslik() } : {}),
      ...(body ? { 'content-type': 'application/json' } : {}),
      ...(form ? { 'content-type': 'application/x-www-form-urlencoded' } : {}),
      ...headers,
    },
    body: body ? JSON.stringify(body) : form ? new URLSearchParams(form).toString() : undefined,
  });
  if (jar) jar.al(res);
  return res;
}
const api = async (jar, p, opts = {}) => {
  const res = await istek(jar, '/api' + p, { ...opts, headers: { 'x-tek-kapi': '1', ...(opts.headers || {}) } });
  return { status: res.status, data: await res.json().catch(() => ({})) };
};
const metin = async (res) => res.text();
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
async function waitFor(fn, label, ms = 15000) {
  const t0 = Date.now();
  while (Date.now() - t0 < ms) { try { const v = await fn(); if (v) return v; } catch {} await sleep(250); }
  throw new Error('Zaman aşımı: ' + label);
}
function ok(name) { passed++; console.log('  ✓ ' + name); }

/** /auth/login -> sahte authorize -> callback adresi (callback'e henüz gidilmez). */
async function girisBaslat(jar) {
  const login = await istek(jar, '/auth/login');
  assert.equal(login.status, 302);
  const authorize = await istek(null, login.headers.get('location'));
  assert.equal(authorize.status, 302);
  return { login, authorizeAdresi: login.headers.get('location'), callback: authorize.headers.get('location') };
}
/** Tam SSO girişi; callback yanıtını döndürür. */
async function ssoGiris(jar) {
  const { callback } = await girisBaslat(jar);
  return istek(jar, callback);
}
const bclGonder = (token) => istek(null, '/auth/backchannel-logout', { method: 'POST', form: { logout_token: token } });

(async () => {
  await mock.start();
  await oidc.start(REDIRECT_URI);
  const srv = spawn(process.execPath, [path.join(__dirname, '..', 'src', 'server.js')], {
    env: {
      ...process.env, PORT: String(PORT), HEADLESS: '1', PORTAL_TEST: '1', PORTAL_DATA_DIR: DATA,
      SITE_ORIGIN_OVERRIDES: JSON.stringify(mock.URLS),
      SSO_ISSUER: oidc.ISSUER, SSO_CLIENT_ID: oidc.CLIENT_ID, SSO_IZINLI_KULLANICILAR: '203011001',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  const logs = [];
  srv.stdout.on('data', (d) => logs.push(d.toString()));
  srv.stderr.on('data', (d) => logs.push(d.toString()));
  try {
    await waitFor(async () => (await fetch(BASE + '/style.css')).ok, 'sunucu');

    console.log('SSO girişi');
    const jar = new Kavanoz();
    let r = await istek(jar, '/');
    assert.equal(r.status, 302); assert.equal(r.headers.get('location'), '/auth/login');
    assert.equal((await api(jar, '/sites')).status, 401);
    let st = (await api(jar, '/state')).data;
    assert.ok(st.sso.etkin && !st.sso.girisYapildi && !st.unlocked);
    const { login, authorizeAdresi } = await girisBaslat(new Kavanoz());
    const a = new URL(authorizeAdresi);
    assert.equal(a.origin + a.pathname, oidc.ISSUER + 'authorize');
    assert.equal(a.searchParams.get('client_id'), 'tek-kapi');
    assert.equal(a.searchParams.get('redirect_uri'), REDIRECT_URI);
    assert.equal(a.searchParams.get('scope'), 'openid profile roles');
    assert.equal(a.searchParams.get('code_challenge_method'), 'S256');
    assert.ok(a.searchParams.get('code_challenge') && a.searchParams.get('state') && a.searchParams.get('nonce'));
    assert.ok(!a.searchParams.has('client_secret'));
    const girisCerezi = login.headers.getSetCookie().find((c) => c.startsWith('tk_sso_giris='));
    assert.ok(/HttpOnly/i.test(girisCerezi) && /SameSite=Lax/i.test(girisCerezi));
    assert.ok(!girisCerezi.includes(a.searchParams.get('state')) && !girisCerezi.includes(a.searchParams.get('nonce')));
    ok('a) Oturumsuz istek /auth/login\'e, oradan authorize\'a (S256, state, nonce) yönleniyor; çerezde yalnızca rastgele anahtar');

    r = await ssoGiris(jar);
    assert.equal(r.status, 302); assert.equal(r.headers.get('location'), '/');
    const oturumCerezi = r.headers.getSetCookie().find((c) => c.startsWith('tk_sso='));
    assert.ok(/HttpOnly/i.test(oturumCerezi) && /SameSite=Lax/i.test(oturumCerezi));
    assert.equal(oidc.durum.pkceDogrulandi, 1);
    assert.equal((await istek(jar, '/')).status, 200);
    st = (await api(jar, '/state')).data;
    assert.ok(st.sso.girisYapildi && st.sso.ad === 'Test Öğrenci' && !st.vaultExists && !st.unlocked);
    assert.equal((await api(jar, '/setup', { method: 'POST', body: { master: MASTER } })).status, 200);
    await api(jar, '/lock', { method: 'POST' });
    st = (await api(jar, '/state')).data;
    assert.ok(st.sso.girisYapildi && st.vaultExists && !st.unlocked);
    assert.equal((await api(jar, '/sites')).status, 401);
    const yalnizcaKasa = new Kavanoz();
    assert.equal((await api(yalnizcaKasa, '/unlock', { method: 'POST', body: { master: MASTER } })).status, 401);
    assert.equal((await api(jar, '/unlock', { method: 'POST', body: { master: 'yanlis-ana-sifre' } })).status, 400);
    assert.equal((await api(jar, '/unlock', { method: 'POST', body: { master: MASTER } })).status, 200);
    assert.equal((await api(jar, '/sites')).status, 200);
    ok('b) Tam akış: SSO oturumu açılıyor, ardından kasa kilit ekranı (ana şifre hâlâ gerekli)');

    const sahteKod = await fetch(oidc.ISSUER + 'token', {
      method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ grant_type: 'authorization_code', code: 'yok', redirect_uri: REDIRECT_URI, client_id: 'tek-kapi', code_verifier: 'x' }),
    });
    assert.equal(sahteKod.status, 400);
    ok('Sahte sağlayıcının token uç noktası geçersiz kod/PKCE\'yi reddediyor');

    console.log('state, id_token ve izin denetimleri');
    const j2 = new Kavanoz();
    let g = await girisBaslat(j2);
    const yanlis = new URL(g.callback); yanlis.searchParams.set('state', 'yanlis-state');
    r = await istek(j2, yanlis.href);
    assert.equal(r.status, 400); assert.ok(!j2.c.has('tk_sso'));
    g = await girisBaslat(j2);
    const eskiGirisCerezi = j2.c.get('tk_sso_giris');
    assert.equal((await istek(j2, g.callback)).status, 302);
    const tekrar = new Kavanoz(); tekrar.c.set('tk_sso_giris', eskiGirisCerezi);
    r = await istek(tekrar, g.callback);
    assert.equal(r.status, 400); assert.ok(!tekrar.c.has('tk_sso'));
    ok('c) Yanlış state reddediliyor; aynı state (ve giriş anahtarı) ikinci kez kullanılamıyor');

    for (const [sapma, ad] of [[{ aud: 'baska-istemci' }, 'aud'], [{ nonce: 'yanlis-nonce' }, 'nonce'], [{ yabanciImza: true }, 'imza']]) {
      oidc.durum.idTokenSapmasi = sapma;
      const j = new Kavanoz();
      r = await ssoGiris(j);
      assert.equal(r.status, 400, ad);
      assert.match(await metin(r), /doğrulanamadı/);
      assert.ok(!j.c.has('tk_sso'), ad);
    }
    oidc.durum.idTokenSapmasi = {};
    ok('d) id_token\'da yanlış aud, yanlış nonce veya geçersiz imza reddediliyor');

    oidc.durum.kullanici = { ...oidc.VARSAYILAN_KULLANICI, sub: 'hoca-1', sid: 'oturum-hoca', name: 'Test Akademisyen', preferred_username: 'hoca.test' };
    let j = new Kavanoz();
    r = await ssoGiris(j);
    assert.equal(r.status, 403); assert.match(await metin(r), /erişim izni yok/);
    assert.ok(!j.c.has('tk_sso'));
    oidc.durum.kullanici = { ...oidc.VARSAYILAN_KULLANICI };
    oidc.durum.reddet = true;
    j = new Kavanoz();
    r = await ssoGiris(j);
    assert.equal(r.status, 403); assert.match(await metin(r), /Bu hesabın Tek Kapı&#39;ya erişim izni yok/);
    assert.ok(!j.c.has('tk_sso'));
    oidc.durum.reddet = false;
    ok('e) İzinli listede olmayan kullanıcı 403; error=access_denied Türkçe "erişim izni yok" sayfası');

    r = await istek(new Kavanoz(), '/auth/login?iss=' + encodeURIComponent('https://evil.example/'));
    assert.equal(r.status, 400); assert.equal(r.headers.get('location'), null);
    r = await istek(new Kavanoz(), '/auth/login?iss=' + encodeURIComponent(oidc.ISSUER));
    assert.equal(r.status, 302); assert.ok(r.headers.get('location').startsWith(oidc.ISSUER + 'authorize'));
    ok('f) Yanlış iss parametreli /auth/login 400, doğru iss ile authorize\'a gidiyor');

    console.log('Back-Channel Logout');
    // Kullanıcının bilgileri ve açık bir site sekmesi (sahte Zimbra)
    await api(jar, '/credentials', { method: 'PUT', body: { ogrNo: mock.USER.ogrNo, obisPass: mock.USER.obisPass, ogrMail: mock.USER.ogrMail, mailPass: mock.USER.mailPass } });
    await api(jar, '/open/posta', { method: 'POST' });
    await waitFor(async () => (await api(jar, '/_debug/pages')).data.some((p) => p.url.startsWith(mock.URLS.posta) && p.title.includes('Gelen Kutusu')), 'posta gelen kutusu');
    const acikMi = async () => { const s = (await api(jar, '/state')).data; return s.sso.girisYapildi && s.unlocked; };
    assert.ok(await acikMi());

    oidc.durum.jwksKapali = true;
    assert.equal((await bclGonder(await oidc.logoutToken())).status, 503);
    oidc.durum.jwksKapali = false;
    assert.ok(await acikMi());
    ok('JWKS\'e ulaşılamazsa 503, oturum etkilenmiyor');

    const tekrarJti = 'tekrar-jti-1';
    const gecersizler = [
      ['imza geçersiz', await oidc.logoutToken({ yabanciImza: true })],
      ['aud yanlış', await oidc.logoutToken({ alanlar: { aud: 'baska-istemci' } })],
      ['iss yanlış', await oidc.logoutToken({ alanlar: { iss: 'https://evil.example/' } })],
      ['events eksik', await oidc.logoutToken({ alanlar: { events: null } })],
      ['events yanlış', await oidc.logoutToken({ alanlar: { events: { 'http://example/baska': {} } } })],
      ['nonce var', await oidc.logoutToken({ alanlar: { nonce: 'n-1' } })],
      ['typ yanlış', await oidc.logoutToken({ typ: 'JWT' })],
      ['sid ve sub yok', await oidc.logoutToken({ alanlar: { sid: null, sub: null } })],
      ['iat çok eski', await oidc.logoutToken({ alanlar: { iat: Math.floor(Date.now() / 1000) - 3600, exp: Math.floor(Date.now() / 1000) + 60 } })],
    ];
    // Aynı jti: ilk kullanım geçerli ama başka bir oturuma ait (etkisiz), ikinci kullanım reddedilir.
    assert.equal((await bclGonder(await oidc.logoutToken({ alanlar: { jti: tekrarJti, sid: 'baska-oturum', sub: 'baska' } }))).status, 200);
    gecersizler.push(['aynı jti ikinci kez', await oidc.logoutToken({ alanlar: { jti: tekrarJti } })]);
    for (const [ad, token] of gecersizler) {
      assert.equal((await bclGonder(token)).status, 400, ad);
      assert.ok(await acikMi(), ad);
    }
    assert.equal((await istek(null, '/auth/backchannel-logout', { method: 'POST', form: {} })).status, 400);
    ok('h) İmza, aud, iss, events, nonce, typ, sid/sub, iat ve tekrar eden jti hatalı logout_token 400; hiçbir şey kilitlenmiyor');

    r = await bclGonder(await oidc.logoutToken({ alanlar: { sid: 'baska-bir-oturum' } }));
    assert.equal(r.status, 200);
    assert.ok(await acikMi());
    assert.ok((await api(jar, '/_debug/pages')).data.some((p) => p.url.startsWith(mock.URLS.posta)));
    ok('i) Başka bir sid\'e ait geçerli logout_token 200, mevcut oturumu etkilemiyor');

    r = await bclGonder(await oidc.logoutToken());
    assert.equal(r.status, 200);
    st = (await api(jar, '/state')).data;
    assert.ok(!st.sso.girisYapildi && !st.unlocked && st.vaultExists);
    assert.equal((await api(jar, '/sites')).status, 401);
    // Yeniden SSO + ana şifre ile girip motoru incele: site sekmeleri kapanmış, olay günlüğünde çıkış var.
    assert.equal((await ssoGiris(jar)).status, 302);
    assert.equal((await api(jar, '/unlock', { method: 'POST', body: { master: MASTER } })).status, 200);
    const sekmeler = (await api(jar, '/_debug/pages')).data;
    assert.ok(!sekmeler.some((p) => Object.values(mock.URLS).some((o) => p.url.startsWith(o))));
    const olaylar = (await api(jar, '/events')).data.map((e) => e.text);
    assert.ok(olaylar.includes("SSO'dan çıkış yapıldı, kasa kilitlendi"));
    assert.ok(olaylar.some((t) => /Hepsinden çıkıldı: 1 sekme kapandı/.test(t)));
    ok('g) Geçerli logout_token: kasa kilitlendi, logoutAll ile site sekmeleri kapandı, SSO oturumu silindi');

    console.log('SSO\'dan çıkış');
    assert.equal((await istek(jar, '/auth/logout', { method: 'POST' })).status, 403);
    assert.ok(await acikMi());
    const sonIdToken = oidc.durum.sirlar.filter((s) => s.split('.').length === 3).pop();
    r = await istek(jar, '/auth/logout', { method: 'POST', headers: { 'x-tek-kapi': '1' } });
    assert.equal(r.status, 200);
    const cikis = new URL((await r.json()).url);
    assert.equal(cikis.origin + cikis.pathname, oidc.ISSUER + 'logout');
    assert.equal(cikis.searchParams.get('id_token_hint'), sonIdToken);
    assert.equal(cikis.searchParams.get('post_logout_redirect_uri'), BASE + '/');
    st = (await api(jar, '/state')).data;
    assert.ok(!st.sso.girisYapildi && !st.unlocked);
    assert.ok(!jar.c.has('tk_sso') && !jar.c.has('tk_session'));
    ok('j) POST /auth/logout: end_session adresi id_token_hint ve post_logout_redirect_uri ile dönüyor, kasa kilitlendi');

    console.log('Günlükler');
    assert.equal((await ssoGiris(jar)).status, 302);
    assert.equal((await api(jar, '/unlock', { method: 'POST', body: { master: MASTER } })).status, 200);
    const tumOlaylar = JSON.stringify((await api(jar, '/events')).data);
    const gunluk = logs.join('');
    const sirlar = [...oidc.durum.sirlar, MASTER, mock.USER.obisPass, mock.USER.mailPass];
    assert.ok(sirlar.length > 20);
    for (const s of sirlar) assert.ok(!tumOlaylar.includes(s) && !gunluk.includes(s), 'sır günlükte');
    assert.ok(!fs.existsSync(path.join(DATA, 'sso.json')));
    for (const f of fs.readdirSync(DATA).filter((x) => fs.statSync(path.join(DATA, x)).isFile())) {
      const icerik = fs.readFileSync(path.join(DATA, f), 'utf8');
      for (const s of oidc.durum.sirlar) assert.ok(!icerik.includes(s), 'sır diskte: ' + f);
    }
    ok('l) Günlüklerde, olay listesinde ve data/ dosyalarında token, kod, PKCE doğrulayıcısı veya şifre yok');

    console.log(`\n${passed} SSO kontrolü geçti.`);
  } catch (e) {
    console.error('\nHATA:', e.message);
    console.error(logs.join('').split('\n').slice(-30).join('\n'));
    process.exitCode = 1;
  } finally {
    srv.kill('SIGTERM');
    setTimeout(() => process.exit(), 1500);
  }
})();
