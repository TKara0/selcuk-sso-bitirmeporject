// vault.js — şifre kasası
//
// Ana şifre hiçbir yerde saklanmaz. Ondan scrypt ile 256 bitlik bir anahtar türetilir,
// tüm site bilgileri tek bir JSON olarak AES-256-GCM ile şifrelenip data/vault.json'a yazılır.
// Anahtar yalnızca kasa açıkken bellekte durur; kilitlenince silinir.

const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const KDF = { N: 2 ** 17, r: 8, p: 1, keyLen: 32 }; // ~128 MB bellek, ~0.3-0.5 sn
const VERIFIER = 'tek-kapi-verifier-v1';

function deriveKey(masterPassword, salt, params = KDF) {
  return new Promise((resolve, reject) => {
    crypto.scrypt(
      masterPassword, salt, params.keyLen,
      { N: params.N, r: params.r, p: params.p, maxmem: 256 * 1024 * 1024 },
      (err, key) => (err ? reject(err) : resolve(key))
    );
  });
}

function seal(key, plaintext) {
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);
  const ct = Buffer.concat([cipher.update(plaintext, 'utf8'), cipher.final()]);
  return { iv: iv.toString('base64'), ct: ct.toString('base64'), tag: cipher.getAuthTag().toString('base64') };
}

function open(key, box) {
  const decipher = crypto.createDecipheriv('aes-256-gcm', key, Buffer.from(box.iv, 'base64'));
  decipher.setAuthTag(Buffer.from(box.tag, 'base64'));
  return Buffer.concat([decipher.update(Buffer.from(box.ct, 'base64')), decipher.final()]).toString('utf8');
}

class Vault {
  constructor(file, { idleMs = 15 * 60 * 1000 } = {}) {
    this.file = file;
    this.idleMs = idleMs;
    this.key = null;
    this.data = null;
    this.lastUse = 0;
    this.failedUnlocks = 0;
    this.blockedUntil = 0;
  }

  exists() { return fs.existsSync(this.file); }
  isUnlocked() {
    if (this.key && Date.now() - this.lastUse > this.idleMs) this.lock();
    return !!this.key;
  }
  touch() { this.lastUse = Date.now(); }

  async create(masterPassword) {
    if (this.exists()) throw new Error('Kasa zaten var');
    const salt = crypto.randomBytes(16);
    const key = await deriveKey(masterPassword, salt);
    this.key = key;
    this.data = {};
    this.salt = salt;
    this._write();
    this.touch();
  }

  async unlock(masterPassword) {
    if (Date.now() < this.blockedUntil) {
      const s = Math.ceil((this.blockedUntil - Date.now()) / 1000);
      throw new Error(`Çok fazla hatalı deneme. ${s} sn bekle.`);
    }
    const stored = JSON.parse(fs.readFileSync(this.file, 'utf8'));
    const salt = Buffer.from(stored.kdf.salt, 'base64');
    const key = await deriveKey(masterPassword, salt, stored.kdf);
    try {
      if (open(key, stored.verifier) !== VERIFIER) throw new Error();
      this.data = JSON.parse(open(key, stored.data));
    } catch {
      this.failedUnlocks += 1;
      if (this.failedUnlocks >= 5) this.blockedUntil = Date.now() + 60_000 * (this.failedUnlocks - 4);
      throw new Error('Ana şifre yanlış');
    }
    this.failedUnlocks = 0;
    this.key = key;
    this.salt = salt;
    this.touch();
  }

  lock() {
    if (this.key) this.key.fill(0);
    this.key = null;
    this.data = null;
  }

  get() {
    if (!this.isUnlocked()) throw new Error('Kasa kilitli');
    this.touch();
    return { ...this.data };
  }

  update(patch) {
    if (!this.isUnlocked()) throw new Error('Kasa kilitli');
    for (const [k, v] of Object.entries(patch)) {
      if (v === null) delete this.data[k];
      else if (v !== undefined) this.data[k] = v;
    }
    this._write();
    this.touch();
  }

  _write() {
    const out = {
      version: 1,
      kdf: { alg: 'scrypt', salt: this.salt.toString('base64'), N: KDF.N, r: KDF.r, p: KDF.p, keyLen: KDF.keyLen },
      verifier: seal(this.key, VERIFIER),
      data: seal(this.key, JSON.stringify(this.data)),
    };
    fs.mkdirSync(path.dirname(this.file), { recursive: true });
    const tmp = this.file + '.tmp';
    fs.writeFileSync(tmp, JSON.stringify(out, null, 2), { mode: 0o600 });
    fs.renameSync(tmp, this.file);
  }
}

module.exports = { Vault };
