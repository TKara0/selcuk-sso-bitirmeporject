// sso.js — isteğe bağlı SSO modu: Tek Şifre Giriş Sistemi'ne (OIDC) public istemci olarak bağlanma
//
// SSO_ISSUER tanımlı değilse hiçbir şey yapmaz; Tek Kapı eskisi gibi tek başına (bağımsız mod) çalışır.
// Tanımlıysa portala erişim iki katmanlıdır: önce SSO oturumu, sonra kasa ana şifresi (kasa anahtarı kimlik
// sunucusundan türetilmez). Tek Kapı public istemcidir (RFC 8252): client_secret yok, PKCE (S256) zorunlu.
//
// Rotalar: GET /auth/login, GET /auth/callback, POST /auth/logout (x-tek-kapi başlığı),
//          POST /auth/backchannel-logout (OIDC Back-Channel Logout 1.0; sunucudan sunucuya, başlık ve oturum gerekmez)
//
// Güvenlik: token'lar, yetkilendirme kodu ve PKCE doğrulayıcısı yalnızca bellekte tutulur; loglanmaz, diske yazılmaz.
// TLS doğrulaması hiçbir koşulda kapatılmaz. Kimlik sunucusunun .NET geliştirme sertifikasına `node --use-system-ca`
// ile (Windows sertifika deposu) güvenilir. Düz http yalnızca test modunda, 127.0.0.1'deki sahte sunucu için açılır.

const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const express = require('express');

const KAPSAM = 'openid profile roles';
const GIRIS_CEREZI = 'tk_sso_giris';
const OTURUM_CEREZI = 'tk_sso';
const GIRIS_SURESI_MS = 10 * 60 * 1000; // bekleyen girişin (state, nonce, PKCE) ömrü
const OTURUM_SURESI_MS = 8 * 60 * 60 * 1000; // kimlik sunucusu oturumuyla aynı (8 saat)
const JTI_SURESI_MS = 15 * 60 * 1000; // logout_token jti tekrar koruması
const EN_FAZLA_BEKLEYEN = 20;
const ASIMETRIK_ALGORITMALAR = ['RS256', 'RS384', 'RS512', 'PS256', 'PS384', 'PS512', 'ES256', 'ES384', 'ES512'];
const BCL_OLAYI = 'http://schemas.openid.net/event/backchannel-logout';

// Ortam değişkeni varsa o, yoksa data/sso.json. Boş ortam değişkeni (SSO_ISSUER=) dosyayı da geçersiz kılar.
function ayarlariOku(dataDir) {
  let dosya = {};
  const yol = path.join(dataDir, 'sso.json');
  if (fs.existsSync(yol)) {
    try { dosya = JSON.parse(fs.readFileSync(yol, 'utf8')); } catch (e) { throw new Error(`data/sso.json okunamadı: ${e.message}`); }
  }
  const al = (ad) => (process.env[ad] !== undefined ? process.env[ad] : dosya[ad]);
  const issuer = al('SSO_ISSUER');
  if (!issuer) return null;
  const izinli = al('SSO_IZINLI_KULLANICILAR') ?? '203011001';
  const mfa = al('SSO_MFA_ZORUNLU');
  return {
    // Varsayılan açık: yalnızca açıkça false/0 verilirse kapanır (kimlik sunucusu da tek-kapi için 2FA ister).
    mfaZorunlu: !(mfa === false || ['false', '0'].includes(String(mfa ?? '').trim().toLowerCase())),
    issuer: new URL(issuer),
    clientId: al('SSO_CLIENT_ID') || 'tek-kapi',
    izinliler: (Array.isArray(izinli) ? izinli : String(izinli).split(','))
      .map((k) => String(k).trim()).filter(Boolean),
  };
}

const kacis = (t) => String(t).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

// Portal tasarımıyla aynı stil dosyalarını kullanan küçük Türkçe bilgi sayfası: üst çubuk, kapalı kapı işareti, başlık
// ve metin (CSP: satır içi stil/betik yok)
function sayfa(res, durum, baslik, metin, tekrarDene = true) {
  res.status(durum).type('html').send(`<!doctype html>
<html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="color-scheme" content="light dark"><title>Tek Kapı</title>
<link rel="stylesheet" href="/arayuz/css/selcuk.css"><link rel="stylesheet" href="/style.css"></head>
<body><header class="ust-cubuk"><a class="marka" href="/"><span class="isaret isaret-kucuk" aria-hidden="true"></span><span>Tek Kapı</span>
<span class="etiket etiket-notr">Yerel</span></a></header>
<main class="icerik icerik-dar"><div class="durum-sayfasi"><span class="isaret isaret-kapali" aria-hidden="true"></span>
<h1>${kacis(baslik)}</h1><p class="giris-metni">${kacis(metin)}</p>
${tekrarDene ? '<a class="dugme dugme-ana" href="/auth/login">Tekrar giriş yap</a>' : ''}</div></main>
<footer class="alt-bilgi"><p>Şifreler bu bilgisayarda AES-256-GCM ile şifreli durur, hiçbir sunucuya gönderilmez.</p></footer></body></html>`);
}

function cerezOku(req, ad) {
  const m = (req.headers.cookie || '').split(/;\s*/).find((c) => c.startsWith(ad + '='));
  return m ? decodeURIComponent(m.slice(ad.length + 1)) : null;
}

const rastgele = () => crypto.randomBytes(32).toString('base64url');

// Hata mesajı token veya kod içermez (openid-client/jose mesajları yalnızca hangi denetimin başarısız olduğunu söyler)
const kisaHata = (e) => String((e && (e.code || e.message)) || e).slice(0, 120);

/**
 * @param {object} p
 * @param {string} p.dataDir   data/ klasörü (sso.json burada aranır)
 * @param {number} p.port      portal portu (redirect_uri http://127.0.0.1:<port>/auth/callback)
 * @param {boolean} p.test     PORTAL_TEST=1: yalnızca 127.0.0.1'deki http sahte sunucuya izin verilir
 * @param {Function} p.log     olay günlüğü (kaynak, metin)
 * @param {Function} p.kapat   kasayı kilitle + yönetilen tarayıcıda "Hepsinden çık" (logoutAll)
 */
function createSso({ dataDir, port, test, log, kapat }) {
  const ayar = ayarlariOku(dataDir);
  const kapali = {
    enabled: false,
    oturum: () => null,
    durum: () => ({ etkin: false }),
    gerekli: (req, res, next) => next(),
    sayfaGeregi: (req, res, next) => next(),
    rotalar: () => {},
  };
  if (!ayar) return kapali;

  const httpTestSunucusu = test && ayar.issuer.protocol === 'http:' && ayar.issuer.hostname === '127.0.0.1';
  if (ayar.issuer.protocol !== 'https:' && !httpTestSunucusu) {
    throw new Error('SSO_ISSUER https olmalı (düz http yalnızca test modunda 127.0.0.1 için kabul edilir).');
  }

  const tabanUrl = `http://127.0.0.1:${port}`;
  const redirectUri = `${tabanUrl}/auth/callback`;
  const postLogoutRedirectUri = `${tabanUrl}/`;
  const bekleyenler = new Map(); // tarayıcı anahtarı -> { state, nonce, verifier, bitis } (tek kullanımlık)
  const oturumlar = new Map(); // oturum anahtarı -> { sid, sub, ad, kullanici, idToken, bitis }
  const kullanilanJtiler = new Map(); // jti -> son kullanma

  let istemciModulu = null;
  let joseModulu = null;
  let yapilandirma = null; // openid-client Configuration (discovery sonucu)
  let jwks = null;

  // openid-client ve jose yalnızca ES modülü; bağımsız modda hiç yüklenmesinler diye burada, gerektiğinde içe aktarılır.
  async function moduller() {
    if (!istemciModulu) [istemciModulu, joseModulu] = await Promise.all([import('openid-client'), import('jose')]);
    return { client: istemciModulu, jose: joseModulu };
  }

  async function config() {
    if (yapilandirma) return yapilandirma;
    const { client } = await moduller();
    const secenekler = httpTestSunucusu ? { execute: [client.allowInsecureRequests] } : undefined;
    const c = await client.discovery(ayar.issuer, ayar.clientId, undefined, client.None(), secenekler);
    // id_token imzası da kimlik sunucusunun JWKS'iyle doğrulansın (iss, aud, nonce, exp zaten doğrulanır)
    client.enableNonRepudiationChecks(c);
    yapilandirma = c;
    return c;
  }

  // logout_token doğrulaması için JWKS. Erişim hataları "jwksErisilemedi" ile işaretlenir (503), anahtar uyuşmazlığı değil.
  async function anahtarlar() {
    if (jwks) return jwks;
    const { jose } = await moduller();
    const uzak = jose.createRemoteJWKSet(new URL((await config()).serverMetadata().jwks_uri), { timeoutDuration: 5000 });
    jwks = async (baslik, token) => {
      try {
        return await uzak(baslik, token);
      } catch (e) {
        if (!['ERR_JWKS_NO_MATCHING_KEY', 'ERR_JWKS_MULTIPLE_MATCHING_KEYS'].includes(e.code)) e.jwksErisilemedi = true;
        throw e;
      }
    };
    return jwks;
  }

  function temizle() {
    const simdi = Date.now();
    for (const [k, v] of bekleyenler) if (v.bitis < simdi) bekleyenler.delete(k);
    for (const [k, v] of oturumlar) if (v.bitis < simdi) oturumlar.delete(k);
    for (const [k, v] of kullanilanJtiler) if (v < simdi) kullanilanJtiler.delete(k);
  }

  function oturum(req) {
    const anahtar = cerezOku(req, OTURUM_CEREZI);
    const o = anahtar && oturumlar.get(anahtar);
    if (!o) return null;
    if (o.bitis < Date.now()) { oturumlar.delete(anahtar); return null; }
    return { anahtar, ...o };
  }

  const wrap = (fn) => (req, res) => Promise.resolve(fn(req, res)).catch((e) => {
    log('sso', 'Kimlik sunucusuna ulaşılamadı: ' + kisaHata(e));
    sayfa(res, 503, 'Kimlik sunucusuna ulaşılamadı', 'Tek Şifre Giriş Sistemi şu an yanıt vermiyor. Biraz sonra tekrar deneyin.');
  });

  function rotalar(app) {
    // Uygulamalarım kartından (initiate_login_uri?iss=…) veya portaldan gelinir. Giriş bilgileri sunucuda kalır;
    // tarayıcıya yalnızca rastgele bir anahtar içeren çerez verilir.
    app.get('/auth/login', wrap(async (req, res) => {
      // Çerezler 127.0.0.1'e bağlı; redirect_uri da 127.0.0.1 olduğu için localhost'tan gelinirse önce oraya geçilir.
      if (req.headers.host !== `127.0.0.1:${port}`) return res.redirect(302, tabanUrl + req.originalUrl);
      // OIDC Core 1.0 bölüm 4: üçüncü taraf başlatılan girişte iss bizim kimlik sunucumuz olmalı.
      if (req.query.iss !== undefined && req.query.iss !== ayar.issuer.href) {
        return sayfa(res, 400, 'Geçersiz giriş isteği', 'Giriş başka bir kimlik sunucusu adına başlatılmış. İstek reddedildi.', false);
      }
      const c = await config();
      const { client } = await moduller();
      temizle();
      while (bekleyenler.size >= EN_FAZLA_BEKLEYEN) bekleyenler.delete(bekleyenler.keys().next().value);
      const verifier = client.randomPKCECodeVerifier();
      const g = { verifier, state: client.randomState(), nonce: client.randomNonce(), bitis: Date.now() + GIRIS_SURESI_MS };
      const anahtar = rastgele();
      bekleyenler.set(anahtar, g);
      const adres = client.buildAuthorizationUrl(c, {
        redirect_uri: redirectUri,
        scope: KAPSAM,
        code_challenge: await client.calculatePKCECodeChallenge(verifier),
        code_challenge_method: 'S256',
        state: g.state,
        nonce: g.nonce,
      });
      // SameSite=Lax (Strict değil): callback, kimlik sunucusundan (başka site) gelen üst düzey bir yönlendirmedir;
      // Strict çerez o istekte gönderilmezdi. Lax çerez yalnızca üst düzey GET gezinmelerinde gider, POST'larda gitmez.
      res.setHeader('Set-Cookie', `${GIRIS_CEREZI}=${anahtar}; HttpOnly; SameSite=Lax; Path=/auth; Max-Age=${GIRIS_SURESI_MS / 1000}`);
      res.redirect(302, adres.href);
    }));

    app.get('/auth/callback', wrap(async (req, res) => {
      const anahtar = cerezOku(req, GIRIS_CEREZI);
      const g = anahtar ? bekleyenler.get(anahtar) : null;
      if (anahtar) bekleyenler.delete(anahtar); // tek kullanımlık: aynı state ikinci kez kullanılamaz
      res.setHeader('Set-Cookie', `${GIRIS_CEREZI}=; HttpOnly; SameSite=Lax; Path=/auth; Max-Age=0`);

      if (req.query.error) {
        log('sso', req.query.error === 'access_denied' ? 'SSO girişi reddedildi: erişim izni yok' : 'SSO girişi tamamlanamadı');
        return req.query.error === 'access_denied'
          ? sayfa(res, 403, 'Erişim izni yok', "Bu hesabın Tek Kapı'ya erişim izni yok.", false)
          : sayfa(res, 400, 'Giriş tamamlanamadı', 'Kimlik sunucusu girişi tamamlamadı. Tekrar deneyin.');
      }
      if (!g || g.bitis < Date.now()) {
        return sayfa(res, 400, 'Giriş isteği geçersiz', 'Giriş isteği bulunamadı veya süresi doldu. Tekrar deneyin.');
      }

      const c = await config();
      const { client } = await moduller();
      let tokenlar;
      try {
        // state, iss (RFC 9207), PKCE, id_token imzası, iss, aud (tek-kapi), nonce ve exp openid-client tarafından doğrulanır.
        tokenlar = await client.authorizationCodeGrant(c, new URL(req.originalUrl, tabanUrl), {
          pkceCodeVerifier: g.verifier,
          expectedState: g.state,
          expectedNonce: g.nonce,
          idTokenExpected: true,
        });
      } catch (e) {
        log('sso', 'SSO girişi doğrulanamadı: ' + kisaHata(e));
        return sayfa(res, 400, 'Giriş doğrulanamadı', 'Kimlik sunucusundan gelen yanıt doğrulanamadı. Tekrar deneyin.');
      }

      const claims = tokenlar.claims();
      // Derinlemesine savunma: kimlik sunucusu tek-kapi için zaten 2FA ister; yine de id_token'daki amr (RFC 8176)
      // "mfa" içermiyorsa (ör. sunucu yanlış yapılandırıldıysa) oturum açılmaz.
      const amr = Array.isArray(claims.amr) ? claims.amr : (typeof claims.amr === 'string' ? [claims.amr] : []);
      if (ayar.mfaZorunlu && !amr.includes('mfa')) {
        log('sso', 'İki aşamalı doğrulama olmadan SSO girişi reddedildi');
        return sayfa(res, 403, 'İki aşamalı doğrulama gerekli',
          "Tek Kapı iki aşamalı doğrulama gerektiriyor. Tek Şifre Giriş Sistemi'nde iki aşamalı doğrulamayı açıp tekrar giriş yapın.");
      }
      if (!claims.preferred_username || !ayar.izinliler.includes(claims.preferred_username)) {
        log('sso', 'İzinli olmayan bir hesapla SSO girişi denendi');
        return sayfa(res, 403, 'Erişim izni yok', "Bu hesabın Tek Kapı'ya erişim izni yok.", false);
      }

      temizle();
      const oturumAnahtari = rastgele();
      oturumlar.set(oturumAnahtari, {
        sid: claims.sid,
        sub: claims.sub,
        ad: claims.name || claims.preferred_username,
        kullanici: claims.preferred_username,
        idToken: tokenlar.id_token, // yalnızca bellekte; çıkışta id_token_hint için
        bitis: Date.now() + OTURUM_SURESI_MS,
      });
      // Lax: bu yanıt da başka siteden gelen yönlendirme zincirinin parçası; Strict çerez ana sayfaya gönderilmezdi.
      res.setHeader('Set-Cookie', `${OTURUM_CEREZI}=${oturumAnahtari}; HttpOnly; SameSite=Lax; Path=/; Max-Age=${OTURUM_SURESI_MS / 1000}`);
      log('sso', 'SSO ile giriş yapıldı');
      res.redirect(302, '/');
    }));

    // Kullanıcının "SSO'dan çık" düğmesi: yerel çıkış + kimlik sunucusunda çıkış (RP-initiated logout) adresi
    app.post('/auth/logout', async (req, res) => {
      if (req.get('x-tek-kapi') !== '1') return res.status(403).json({ error: 'Eksik başlık' });
      const o = oturum(req);
      if (o) oturumlar.delete(o.anahtar);
      await kapat().catch((e) => log('sistem', e.message));
      res.setHeader('Set-Cookie', [
        `${OTURUM_CEREZI}=; HttpOnly; SameSite=Lax; Path=/; Max-Age=0`,
        'tk_session=; HttpOnly; SameSite=Strict; Path=/; Max-Age=0',
      ]);
      let url = '/';
      try {
        const { client } = await moduller();
        url = client.buildEndSessionUrl(await config(), {
          post_logout_redirect_uri: postLogoutRedirectUri,
          ...(o ? { id_token_hint: o.idToken } : {}),
        }).href;
      } catch (e) {
        log('sso', 'Kimlik sunucusunda çıkış adresi alınamadı: ' + kisaHata(e));
      }
      log('sso', "SSO'dan çıkış yapıldı, kasa kilitlendi");
      res.json({ url });
    });

    // OIDC Back-Channel Logout 1.0: kimlik sunucusu çıkışta imzalı logout_token POST eder.
    app.post('/auth/backchannel-logout', express.urlencoded({ extended: false, limit: '16kb' }), async (req, res) => {
      const token = req.body && req.body.logout_token;
      if (typeof token !== 'string' || !token) return res.status(400).json({ error: 'logout_token yok' });

      let payload;
      try {
        const { jose } = await moduller();
        ({ payload } = await jose.jwtVerify(token, await anahtarlar(), {
          issuer: (await config()).serverMetadata().issuer,
          audience: ayar.clientId,
          typ: 'logout+jwt',
          algorithms: ASIMETRIK_ALGORITMALAR,
          requiredClaims: ['iat', 'jti'],
          maxTokenAge: '5m',
          clockTolerance: '1m',
        }));
      } catch (e) {
        if (e.jwksErisilemedi || !yapilandirma) {
          log('sso', 'Çıkış bildirimi doğrulanamadı: kimlik sunucusunun anahtarlarına ulaşılamadı');
          return res.status(503).json({ error: 'JWKS alınamadı' });
        }
        log('sso', 'Geçersiz çıkış bildirimi reddedildi: ' + kisaHata(e));
        return res.status(400).json({ error: 'Geçersiz logout_token' });
      }

      temizle();
      const olaylar = payload.events;
      const gecerli = olaylar && typeof olaylar === 'object' && !Array.isArray(olaylar)
        && olaylar[BCL_OLAYI] && typeof olaylar[BCL_OLAYI] === 'object'
        && (typeof payload.sid === 'string' || typeof payload.sub === 'string')
        && payload.nonce === undefined
        && typeof payload.jti === 'string' && !kullanilanJtiler.has(payload.jti);
      if (!gecerli) {
        log('sso', 'Geçersiz çıkış bildirimi reddedildi');
        return res.status(400).json({ error: 'Geçersiz logout_token' });
      }
      kullanilanJtiler.set(payload.jti, Date.now() + JTI_SURESI_MS);

      const eslesenler = [...oturumlar].filter(([, o]) => (payload.sid
        ? o.sid === payload.sid && (!payload.sub || o.sub === payload.sub)
        : o.sub === payload.sub));
      if (eslesenler.length) {
        for (const [k] of eslesenler) oturumlar.delete(k);
        await kapat().catch((e) => log('sistem', e.message));
        log('sso', "SSO'dan çıkış yapıldı, kasa kilitlendi");
      }
      res.status(200).end();
    });
  }

  return {
    enabled: true,
    oturum,
    durum: (req) => {
      const o = oturum(req);
      return { etkin: true, girisYapildi: !!o, ad: o ? o.ad : null };
    },
    // API: SSO oturumu yoksa 401 (arayüz /auth/login'e gider)
    gerekli: (req, res, next) => (oturum(req) ? next() : res.status(401).json({ error: 'SSO oturumu gerekli', sso: true })),
    // Portal sayfası: SSO oturumu yoksa girişe yönlendir
    sayfaGeregi: (req, res, next) => (oturum(req) ? next() : res.redirect(302, '/auth/login')),
    rotalar,
  };
}

module.exports = { createSso };
