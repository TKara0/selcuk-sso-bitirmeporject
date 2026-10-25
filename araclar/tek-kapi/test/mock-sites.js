// mock-sites.js — test için sahte Selçuk giriş sayfaları
// Alan adları, oturuma bağlı tokenlar ve CAPTCHA/2FA adımları gerçek sitelerdekiyle aynı yapıda.

const express = require('express');
const crypto = require('crypto');

const PORTS = { obisEntry: 5100, obis: 5101, posta: 5102, uzem: 5103, yemek: 5104, sso: 5105, sinav: 5106 };
const URLS = Object.fromEntries(Object.entries(PORTS).map(([k, p]) => [k, `http://127.0.0.1:${p}`]));

const USER = {
  ogrNo: '203011001',
  obisPass: 'Obis#Pass123',
  ogrMail: 'talha.test@ogr.selcuk.edu.tr',
  mailPass: 'Mail#Pass456',
};
const CAPTCHA = '7K3P';
const TOTP = '123456';

const counters = { obisPost: 0, postaPost: 0, uzemPost: 0, sinavPost: 0, ssoPost: 0 };

const rnd = () => crypto.randomBytes(12).toString('hex');
const cookie = (req, n) => {
  const m = (req.headers.cookie || '').split(/;\s*/).find((c) => c.startsWith(n + '='));
  return m ? m.slice(n.length + 1) : null;
};
const page = (title, body) => `<!doctype html><html><head><meta charset="utf-8"><title>${title}</title></head><body>${body}</body></html>`;

function mk() { const a = express(); a.use(express.urlencoded({ extended: false })); return a; }

const apps = {};

// obis.selcuk.edu.tr -> obis2
apps.obisEntry = mk();
apps.obisEntry.get('/', (req, res) => res.redirect(302, URLS.obis + '/'));

// OBİS: id + pass + resimli CAPTCHA
apps.obis = mk();
const obisForm = (err = '') => page('S.Ü. Öğrenci Bilgi Sistemi', `
  ${err ? `<div class="alert">${err}</div>` : ''}
  <form method="post" action="/Home/Index">
    <input type="text" name="id" placeholder="Öğrenci No">
    <input type="password" name="pass" placeholder="Şifre">
    <img alt="captcha ${CAPTCHA}" src="data:,">
    <input type="text" id="TxtCaptcha" name="TxtCaptcha">
    <button type="submit">Giriş</button>
  </form>`);
apps.obis.get('/', (req, res) => res.send(obisForm()));
apps.obis.post('/Home/Index', (req, res) => {
  counters.obisPost++;
  const b = req.body;
  if (b.id === USER.ogrNo && b.pass === USER.obisPass && b.TxtCaptcha === CAPTCHA) return res.redirect(302, '/Home/Dashboard');
  res.send(obisForm('Hatalı giriş'));
});
apps.obis.get('/Home/Dashboard', (req, res) => res.send(page('OBİS', '<h1>OBİS Ana Sayfa</h1><p>Hoş geldin</p>')));

// Zimbra: login_csrf çereze bağlı
apps.posta = mk();
const postaSessions = new Map();
apps.posta.get('/', (req, res) => {
  const sid = cookie(req, 'ZM_TEST') || rnd();
  const s = postaSessions.get(sid) || { csrf: rnd(), user: null };
  postaSessions.set(sid, s);
  res.setHeader('Set-Cookie', `ZM_TEST=${sid}; Path=/; HttpOnly`);
  if (s.user) return res.send(page('Zimbra: Gelen Kutusu', `<h1>Gelen Kutusu</h1><p>${s.user}</p><a href="/logout">Çıkış</a>`));
  res.send(page('Zimbra Web İstemcisi Oturum Açma Bilgileri', `
    ${s.err ? '<div id="ZLoginErrorPanel">Kullanıcı adı veya parola yanlış.</div>' : ''}
    <form method="post" action="/">
      <input type="hidden" name="loginOp" value="login">
      <input type="hidden" name="login_csrf" value="${s.csrf}">
      <input type="text" name="username"><input type="password" name="password">
      <input type="submit" value="Oturum Aç">
      <input type="checkbox" name="zrememberme" value="1">
      <select name="client"><option value="preferred">Varsayılan</option></select>
    </form>`));
});
apps.posta.post('/', (req, res) => {
  counters.postaPost++;
  const s = postaSessions.get(cookie(req, 'ZM_TEST'));
  if (!s || req.body.login_csrf !== s.csrf) return res.status(403).send('csrf');
  if (req.body.username === USER.ogrMail && req.body.password === USER.mailPass) { s.user = req.body.username; s.err = false; }
  else { s.err = true; s.csrf = rnd(); }
  res.redirect(302, '/');
});
apps.posta.get('/logout', (req, res) => {
  const s = postaSessions.get(cookie(req, 'ZM_TEST'));
  if (s) s.user = null;
  res.redirect(302, '/');
});

// Moodle: logintoken çereze bağlı, kullanıcı adı öğrenci no
apps.uzem = mk();
const moodle = new Map();
apps.uzem.get('/', (req, res) => res.send(page('UZEM', '<a href="/login/index.php">Giriş yap</a>')));
apps.uzem.get('/login/index.php', (req, res) => {
  const sid = cookie(req, 'MoodleSession') || rnd();
  const s = moodle.get(sid) || {};
  s.token = rnd();
  moodle.set(sid, s);
  res.setHeader('Set-Cookie', `MoodleSession=${sid}; Path=/; HttpOnly`);
  res.send(page('Siteye giriş yap', `
    ${s.err ? '<div class="alert">Geçersiz giriş, lütfen tekrar deneyiniz</div>' : ''}
    <form method="post" action="/login/index.php" id="login">
      <input type="hidden" name="logintoken" value="${s.token}">
      <input type="text" name="username" id="username"><input type="password" name="password" id="password">
      <button type="submit" id="loginbtn">Giriş yap</button>
    </form>`));
});
apps.uzem.post('/login/index.php', (req, res) => {
  counters.uzemPost++;
  const s = moodle.get(cookie(req, 'MoodleSession'));
  if (!s || req.body.logintoken !== s.token) return res.redirect(303, '/login/index.php');
  if (req.body.username === USER.ogrNo && req.body.password === USER.obisPass) { s.user = req.body.username; s.err = false; return res.redirect(303, '/my/'); }
  s.err = true;
  res.redirect(303, '/login/index.php');
});
apps.uzem.get('/my/', (req, res) => res.send(page('Panom', '<h1>Panom</h1>')));

// Uzaktan Sınav: UZEM ile aynı Moodle formu
apps.sinav = mk();
const moodleSinav = new Map();
apps.sinav.get('/', (req, res) => res.send(page('Uzaktan Sınav', '<a href="/login/index.php">Giriş yap</a>')));
apps.sinav.get('/login/index.php', (req, res) => {
  const sid = cookie(req, 'MoodleSession') || rnd();
  const s = moodleSinav.get(sid) || {};
  s.token = rnd();
  moodleSinav.set(sid, s);
  res.setHeader('Set-Cookie', `MoodleSession=${sid}; Path=/; HttpOnly`);
  res.send(page('Siteye giriş yap', `
    ${s.err ? '<div class="alert">Geçersiz giriş, lütfen tekrar deneyiniz</div>' : ''}
    <form method="post" action="/login/index.php" id="login">
      <input type="hidden" name="logintoken" value="${s.token}">
      <input type="text" name="username" id="username"><input type="password" name="password" id="password">
      <button type="submit" id="loginbtn">Giriş yap</button>
    </form>`));
});
apps.sinav.post('/login/index.php', (req, res) => {
  counters.sinavPost++;
  const s = moodleSinav.get(cookie(req, 'MoodleSession'));
  if (!s || req.body.logintoken !== s.token) return res.redirect(303, '/login/index.php');
  if (req.body.username === USER.ogrNo && req.body.password === USER.obisPass) { s.user = req.body.username; s.err = false; return res.redirect(303, '/my/'); }
  s.err = true;
  res.redirect(303, '/login/index.php');
});
apps.sinav.get('/my/', (req, res) => res.send(page('Panom', '<h1>Panom</h1>')));

// Yemekhane: oturum yoksa SSO'ya yönlendirir
apps.yemek = mk();
apps.yemek.get('/', (req, res) => {
  if (cookie(req, 'YEMEK') === 'ok') return res.send(page('Yemek Rezervasyon', '<h1>Yemek Rezervasyon</h1>'));
  res.redirect(302, `${URLS.sso}/Account/Login?ReturnUrl=${encodeURIComponent('/connect/authorize/callback?client_id=YemekApp')}`);
});
apps.yemek.get('/signin-oidc', (req, res) => {
  res.setHeader('Set-Cookie', 'YEMEK=ok; Path=/');
  res.redirect(302, '/');
});

// SSO: Username + EmailDomain + Password + reCAPTCHA token + __RequestVerificationToken, ardından 2FA
apps.sso = mk();
apps.sso.get('/Account/Login', (req, res) => res.send(page('Selçuk Üniversitesi', `
  <form method="post" action="/Account/Login">
    <input type="hidden" name="ReturnUrl" value="${req.query.ReturnUrl || ''}">
    <input type="hidden" name="g-recaptcha-response" value="fake-v3-token">
    <input type="text" name="Username">
    <select name="EmailDomain"><option value="1">@selcuk.edu.tr</option><option value="2">@ogr.selcuk.edu.tr</option><option value="3">@lisansustu.selcuk.edu.tr</option></select>
    <input type="password" name="Password">
    <button type="submit" name="button" value="login">Giriş Yap</button>
    <input type="hidden" name="__RequestVerificationToken" value="${rnd()}">
  </form>`)));
apps.sso.post('/Account/Login', (req, res) => {
  counters.ssoPost++;
  const b = req.body;
  if (b.Username + '@ogr.selcuk.edu.tr' === USER.ogrMail && b.EmailDomain === '2' && b.Password === USER.mailPass) {
    return res.send(page('İki Aşamalı Doğrulama', `<form method="post" action="/Account/TwoFactor"><input name="Code" id="Code"><button type="submit">Doğrula</button></form>`));
  }
  res.status(200).send(page('Selçuk Üniversitesi', '<p>Kullanıcı adı veya şifre hatalı</p>'));
});
apps.sso.post('/Account/TwoFactor', (req, res) => {
  if (req.body.Code === TOTP) return res.redirect(302, URLS.yemek + '/signin-oidc?code=abc');
  res.send(page('İki Aşamalı Doğrulama', 'Kod hatalı'));
});

function start() {
  return Promise.all(Object.entries(apps).map(([k, a]) => new Promise((r) => {
    const s = a.listen(PORTS[k], '127.0.0.1', () => r(s));
  })));
}

module.exports = { start, URLS, USER, CAPTCHA, TOTP, counters };
