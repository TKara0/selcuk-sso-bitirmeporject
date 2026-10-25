// fake-oidc.js — test için sahte OIDC sağlayıcı (Tek Şifre Giriş Sistemi'nin yerine)
// Yalnızca test: 127.0.0.1'de düz HTTP. Anahtar çifti jose ile çalışma anında üretilir. Discovery, JWKS, authorize,
// token (PKCE S256'yı gerçekten doğrular), end_session uç noktaları ve imzalı logout_token üretimi vardır.
// Testler kullanıcıyı ve id_token'daki sapmaları (yanlış aud, nonce, imza) buradan ayarlar.

const express = require('express');
const crypto = require('crypto');

const PORT = 5190;
const ISSUER = `http://127.0.0.1:${PORT}/`;
const CLIENT_ID = 'tek-kapi';
const BCL_OLAYI = 'http://schemas.openid.net/event/backchannel-logout';

const VARSAYILAN_KULLANICI = {
  sub: 'kullanici-1', sid: 'oturum-1', name: 'Test Öğrenci', preferred_username: '203011001', role: 'ogrenci',
};

const durum = {
  redirectUri: null,
  kullanici: { ...VARSAYILAN_KULLANICI },
  idTokenSapmasi: {}, // ör. { aud: 'baska' }, { nonce: 'yanlis' }, { yabanciImza: true }
  reddet: false, // authorize error=access_denied ile döner
  jwksKapali: false,
  sonAuthorize: null,
  sonEndSession: null,
  pkceDogrulandi: 0,
  kodlar: new Map(),
  // l) testi için: sahte sunucunun ürettiği/aldığı tüm sırlar
  sirlar: [],
};

let jose;
let anahtar;
let yabanciAnahtar;
let acikJwk;
const KID = 'test-anahtari-1';

const b64url = (buf) => Buffer.from(buf).toString('base64url');
const sir = (s) => { durum.sirlar.push(s); return s; };

async function imzala(payload, { typ = 'JWT', yabanci = false } = {}) {
  return new jose.SignJWT(payload)
    .setProtectedHeader({ alg: 'RS256', kid: KID, typ })
    .sign(yabanci ? yabanciAnahtar.privateKey : anahtar.privateKey);
}

const app = express();
app.use(express.urlencoded({ extended: false }));

app.get('/.well-known/openid-configuration', (req, res) => res.json({
  issuer: ISSUER,
  authorization_endpoint: ISSUER + 'authorize',
  token_endpoint: ISSUER + 'token',
  jwks_uri: ISSUER + 'jwks',
  end_session_endpoint: ISSUER + 'logout',
  response_types_supported: ['code'],
  subject_types_supported: ['public'],
  id_token_signing_alg_values_supported: ['RS256'],
  code_challenge_methods_supported: ['S256'],
  token_endpoint_auth_methods_supported: ['none'],
  authorization_response_iss_parameter_supported: true,
  backchannel_logout_supported: true,
  backchannel_logout_session_supported: true,
}));

app.get('/jwks', (req, res) => {
  if (durum.jwksKapali) return res.status(500).send('kapali');
  res.json({ keys: [acikJwk] });
});

app.get('/authorize', (req, res) => {
  const q = req.query;
  durum.sonAuthorize = { ...q };
  const gecerli = q.client_id === CLIENT_ID && q.redirect_uri === durum.redirectUri && q.response_type === 'code'
    && q.code_challenge_method === 'S256' && q.code_challenge && q.state && q.nonce
    && String(q.scope).split(' ').includes('openid');
  if (!gecerli) return res.status(400).json({ error: 'invalid_request' });
  const donus = new URL(q.redirect_uri);
  donus.searchParams.set('state', q.state);
  donus.searchParams.set('iss', ISSUER);
  if (durum.reddet) {
    donus.searchParams.set('error', 'access_denied');
    return res.redirect(302, donus.href);
  }
  const kod = sir(b64url(crypto.randomBytes(24)));
  durum.kodlar.set(kod, { challenge: q.code_challenge, nonce: q.nonce, redirectUri: q.redirect_uri, kullanici: { ...durum.kullanici } });
  donus.searchParams.set('code', kod);
  res.redirect(302, donus.href);
});

app.post('/token', async (req, res) => {
  const b = req.body;
  const k = durum.kodlar.get(b.code);
  durum.kodlar.delete(b.code); // tek kullanımlık
  if (k && b.code_verifier) sir(b.code_verifier);
  if (b.grant_type !== 'authorization_code' || !k || b.client_id !== CLIENT_ID || b.redirect_uri !== k.redirectUri) {
    return res.status(400).json({ error: 'invalid_grant' });
  }
  if (b.client_secret) return res.status(401).json({ error: 'invalid_client' }); // public istemci
  // PKCE (RFC 7636, S256): BASE64URL(SHA256(code_verifier)) == code_challenge
  const hesaplanan = b64url(crypto.createHash('sha256').update(String(b.code_verifier || '')).digest());
  if (!b.code_verifier || hesaplanan !== k.challenge) return res.status(400).json({ error: 'invalid_grant', error_description: 'PKCE' });
  durum.pkceDogrulandi++;

  const simdi = Math.floor(Date.now() / 1000);
  const { yabanciImza, ...sapma } = durum.idTokenSapmasi;
  const idToken = sir(await imzala({
    iss: ISSUER, aud: CLIENT_ID, iat: simdi, exp: simdi + 300, nonce: k.nonce, ...k.kullanici, ...sapma,
  }, { yabanci: yabanciImza }));
  res.set('Cache-Control', 'no-store').json({
    access_token: sir(b64url(crypto.randomBytes(24))), token_type: 'Bearer', expires_in: 300, id_token: idToken,
  });
});

app.get('/logout', (req, res) => {
  durum.sonEndSession = { ...req.query };
  res.send('cikis');
});

/** İmzalı logout_token (OIDC Back-Channel Logout 1.0). Alanlar testte bozulabilir; null verilen alan çıkarılır. */
async function logoutToken(o = {}) {
  const simdi = Math.floor(Date.now() / 1000);
  const payload = {
    iss: ISSUER, aud: CLIENT_ID, iat: simdi, exp: simdi + 120, jti: crypto.randomUUID(),
    sub: durum.kullanici.sub, sid: durum.kullanici.sid, events: { [BCL_OLAYI]: {} },
    ...o.alanlar,
  };
  for (const [k, v] of Object.entries(payload)) if (v === null) delete payload[k];
  return sir(await imzala(payload, { typ: o.typ || 'logout+jwt', yabanci: o.yabanciImza }));
}

async function start(redirectUri) {
  jose = await import('jose');
  anahtar = await jose.generateKeyPair('RS256', { extractable: true });
  yabanciAnahtar = await jose.generateKeyPair('RS256', { extractable: true });
  acikJwk = { ...(await jose.exportJWK(anahtar.publicKey)), kid: KID, alg: 'RS256', use: 'sig' };
  durum.redirectUri = redirectUri;
  await new Promise((r) => app.listen(PORT, '127.0.0.1', r));
}

module.exports = { start, durum, logoutToken, ISSUER, CLIENT_ID, VARSAYILAN_KULLANICI };
