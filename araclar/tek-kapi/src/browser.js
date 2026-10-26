// browser.js — portalın yönettiği tarayıcı penceresi ve otomatik giriş motoru
//
// Kalıcı bir tarayıcı profili (data/browser-profile) kullanılır; böylece sitelerin
// "beni hatırla" çerezleri ve SSO oturumu pencere kapansa da korunur.
// Bu penceredeki her sekme izlenir: bilinen bir giriş sayfası yüklendiğinde ilgili
// adaptör çalışır. Yanlış şifreyle hesabı kilitletmemek için, gönderimden hemen sonra
// tekrar giriş sayfasına düşülürse o site "başarısız" işaretlenir ve bir daha gönderilmez.

const { chromium } = require('playwright');

const FAIL_WINDOW_MS = 90_000;
const ATTEMPT_TTL_MS = 10 * 60_000; // yarım kalan ölçümler bu süreden sonra yok sayılır

class BrowserEngine {
  constructor({ profileDir, headless = false, sites, getCreds, log, onMeasure = () => {} }) {
    this.profileDir = profileDir;
    this.headless = headless;
    this.sites = sites;
    this.getCreds = getCreds; // () => creds | null (kasa kilitliyse null)
    this.log = log;
    this.onMeasure = onMeasure; // ölçüm kaydı (şifre içermez)
    this.context = null;
    this.launching = null;
    this.state = {}; // siteId -> { lastSubmitAt, failed, lastMessage, attempt }
    this.navIds = new WeakMap();
    this.handled = new WeakMap();
  }

  running() { return !!this.context; }

  siteState(id) {
    if (!this.state[id]) this.state[id] = { lastSubmitAt: 0, failed: false, lastMessage: '', attempt: null };
    return this.state[id];
  }

  resetFailures() {
    for (const s of Object.values(this.state)) { s.failed = false; s.lastSubmitAt = 0; }
  }

  async ensure() {
    if (this.context) return this.context;
    if (this.launching) return this.launching;
    this.launching = this._launch().finally(() => { this.launching = null; });
    return this.launching;
  }

  async _launch() {
    const base = { headless: this.headless, viewport: this.headless ? { width: 1280, height: 860 } : null };
    const wanted = process.env.PORTAL_BROWSER_CHANNEL;
    const attempts = wanted ? [wanted] : [undefined, 'chrome', 'msedge'];
    let lastErr;
    for (const channel of attempts) {
      try {
        const ctx = await chromium.launchPersistentContext(this.profileDir, channel ? { ...base, channel } : base);
        this.log('sistem', `Tarayıcı açıldı (${channel || 'Playwright Chromium'})`);
        ctx.on('page', (p) => this._attach(p));
        ctx.pages().forEach((p) => this._attach(p));
        ctx.on('close', () => { this.context = null; this.log('sistem', 'Tarayıcı penceresi kapandı'); });
        this.context = ctx;
        return ctx;
      } catch (err) {
        lastErr = err;
      }
    }
    throw new Error('Tarayıcı başlatılamadı. "npx playwright install chromium" komutunu çalıştır ya da Chrome/Edge kurulu olduğundan emin ol.\n' + (lastErr && lastErr.message));
  }

  async openUrl(url) {
    const ctx = await this.ensure();
    const blank = ctx.pages().find((p) => p.url() === 'about:blank');
    const page = blank || (await ctx.newPage());
    await page.goto(url, { waitUntil: 'load', timeout: 45_000 }).catch((e) => this.log('sistem', 'Sayfa açılamadı: ' + e.message));
    await page.bringToFront().catch(() => {});
    return page;
  }

  async open(siteId) {
    const site = this.sites.find((s) => s.id === siteId);
    if (!site) throw new Error('Bilinmeyen site');
    this.log(site.name, 'Açılıyor…');
    this.siteState(site.id).attempt = { openedAt: Date.now(), source: 'kart', sawLoginAt: 0, filledAt: 0 };
    const page = await this.openUrl(site.startUrl);
    await this._handle(page); // 'load' olayı kaçmış olabilir
    return page;
  }

  // Portal sekmesini öne getir; yoksa aç (ikinci kez başlatıldığında kullanılır)
  async focus(url) {
    const ctx = await this.ensure();
    const existing = ctx.pages().find((p) => p.url().startsWith(url));
    if (existing) { await existing.bringToFront().catch(() => {}); return existing; }
    return this.openUrl(url);
  }

  // "Hepsinden çık": yönetilen tarayıcıdaki tüm site oturumlarını yerelde sonlandırır.
  // Desteklenen sitelerin sekmeleri kapanır ve bu sitelere (ve üst alan adlarına) ait çerezler silinir.
  async logoutAll() {
    for (const st of Object.values(this.state)) { st.lastSubmitAt = 0; st.attempt = null; }
    if (!this.context) return { tabsClosed: 0, cookiesCleared: 0 };
    const origins = new Set(this.sites.flatMap((s) => s.origins));
    const hosts = [...origins].map((o) => new URL(o).hostname);
    let tabsClosed = 0;
    for (const p of this.context.pages()) {
      let o = null;
      try { o = new URL(p.url()).origin; } catch {}
      if (o && origins.has(o)) { await p.close().catch(() => {}); tabsClosed++; }
    }
    const belongs = (cookieDomain) => {
      const d = cookieDomain.replace(/^\./, '').toLowerCase();
      return hosts.some((h) => h === d || h.endsWith('.' + d));
    };
    const cookies = (await this.context.cookies()).filter((c) => belongs(c.domain));
    for (const c of cookies) {
      await this.context.clearCookies({ name: c.name, domain: c.domain, path: c.path }).catch(() => {});
    }
    this.log('sistem', `Hepsinden çıkıldı: ${tabsClosed} sekme kapandı, ${cookies.length} oturum çerezi silindi`);
    return { tabsClosed, cookiesCleared: cookies.length };
  }

  _finish(site, outcome) {
    const st = this.siteState(site.id);
    const a = st.attempt;
    st.attempt = null;
    if (!a) return;
    const now = Date.now();
    if (now - a.openedAt > ATTEMPT_TTL_MS) return;
    const userMs = site.mode === 'assist' && a.filledAt && outcome === 'giris' ? now - a.filledAt : 0;
    this.onMeasure({
      at: new Date(now).toISOString(),
      siteId: site.id,
      site: site.name,
      mode: site.mode,
      kaynak: a.source,
      sonuc: outcome,
      toplamMs: now - a.openedAt,
      otomatikMs: a.filledAt && a.sawLoginAt ? a.filledAt - a.sawLoginAt : 0,
      kullaniciMs: userMs,
      kullaniciAdimi: outcome === 'giris' ? (site.userSteps || 0) : 0,
    });
  }

  // Bir sitenin uygulama sayfasına (giriş formu olmayan) ulaşıldı
  _arrived(origin) {
    for (const s of this.sites) {
      if (!s.appOrigins.includes(origin)) continue;
      const st = this.siteState(s.id);
      if (st.lastSubmitAt) { st.lastSubmitAt = 0; this.log(s.name, 'Giriş başarılı'); }
      if (st.attempt) {
        if (!st.attempt.sawLoginAt) this.log(s.name, 'Oturum zaten açık, giriş gerekmedi');
        this._finish(s, st.attempt.sawLoginAt ? 'giris' : 'oturum-hatirlandi');
      }
    }
  }

  async pages() {
    if (!this.context) return [];
    return Promise.all(this.context.pages().map(async (p) => ({ url: p.url(), title: await p.title().catch(() => '') })));
  }

  async close() {
    if (this.context) await this.context.close().catch(() => {});
    this.context = null;
  }

  _attach(page) {
    this.navIds.set(page, 0);
    page.on('framenavigated', (frame) => {
      if (frame === page.mainFrame()) this.navIds.set(page, (this.navIds.get(page) || 0) + 1);
    });
    page.on('load', () => { this._handle(page).catch(() => {}); });
  }

  async _handle(page) {
    const url = page.url();
    let origin = null;
    try { origin = new URL(url).origin; } catch { return; }
    if (!/^https?:/.test(url)) return;

    // Aynı sayfa yüklemesini iki kez işleme
    const navId = this.navIds.get(page) || 0;
    if (this.handled.get(page) === navId) return;
    this.handled.set(page, navId);

    const site = this.sites.find((s) => s.loginPage.match(url));
    let isLogin = false;
    if (site) {
      try { isLogin = await site.loginPage.detect(page); } catch { return; }
    }
    if (!isLogin) { this._arrived(origin); return; }

    const st = this.siteState(site.id);
    if (!st.attempt || Date.now() - st.attempt.openedAt > ATTEMPT_TTL_MS) {
      // Karta tıklanmadan giriş sayfasına düşüldü (ör. oturum süresi doldu)
      st.attempt = { openedAt: Date.now(), source: 'yonlendirme', sawLoginAt: 0, filledAt: 0 };
    }
    if (!st.attempt.sawLoginAt) st.attempt.sawLoginAt = Date.now();

    const creds = this.getCreds();
    if (!creds) {
      await banner(page, 'Tek Kapı kilitli. Portaldan ana şifrenle kilidi aç.', 'warn');
      this.log(site.name, 'Kasa kilitli, doldurulmadı');
      return;
    }
    const missing = site.needs.filter((k) => !creds[k]);
    if (missing.length) {
      await banner(page, 'Bu site için bilgiler eksik. Portalda Ayarlar\'dan ekle.', 'warn');
      this.log(site.name, 'Eksik bilgi: ' + missing.join(', '));
      return;
    }

    // Az önce gönderdik ve yine giriş sayfasındayız: büyük ihtimalle şifre yanlış.
    if (st.lastSubmitAt && Date.now() - st.lastSubmitAt < FAIL_WINDOW_MS) {
      st.failed = true;
      st.lastSubmitAt = 0;
      this.log(site.name, 'Otomatik giriş başarısız. Hesabı kilitletmemek için tekrar denenmeyecek.');
      this._finish(site, 'basarisiz');
    }
    if (st.failed) {
      await banner(page, 'Otomatik giriş daha önce başarısız oldu. Şifreni portalda kontrol et; kendin de giriş yapabilirsin.', 'error');
      return;
    }

    try {
      if (site.mode === 'auto') st.lastSubmitAt = Date.now();
      const res = await site.loginPage.fill(page, creds);
      if (st.attempt && !st.attempt.filledAt) st.attempt.filledAt = Date.now();
      if (!res.submitted) {
        st.lastSubmitAt = 0;
        await banner(page, res.message, 'info');
      }
      st.lastMessage = res.message;
      this.log(site.name, res.message);
    } catch (err) {
      st.lastSubmitAt = 0;
      this.log(site.name, 'Doldurma hatası: ' + err.message.split('\n')[0]);
    }
  }
}

// Sayfanın üstüne küçük bir bilgi şeridi. Sitenin CSP'sine takılmamak için stiller CSSOM ile veriliyor.
async function banner(page, text, tone = 'info') {
  await page.evaluate(([text, tone]) => {
    const colors = { info: ['#0f2a4a', '#ffffff'], warn: ['#8a5a00', '#ffffff'], error: ['#8b1e1e', '#ffffff'] };
    const [bg, fg] = colors[tone] || colors.info;
    let el = document.getElementById('__tek_kapi_banner');
    if (!el) {
      el = document.createElement('div');
      el.id = '__tek_kapi_banner';
      document.documentElement.appendChild(el);
    }
    Object.assign(el.style, {
      position: 'fixed', right: '16px', bottom: '16px', zIndex: '2147483647',
      background: bg, color: fg, font: '500 14px/1.4 system-ui, sans-serif', padding: '10px 16px',
      borderRadius: '10px', boxShadow: '0 6px 24px rgba(0,0,0,.25)', maxWidth: 'min(380px, 80vw)', cursor: 'pointer',
    });
    el.textContent = 'Tek Kapı: ' + text;
    el.title = 'Kapatmak için tıkla';
    el.onclick = () => el.remove();
  }, [text, tone]).catch(() => {});
}

module.exports = { BrowserEngine };
