// Tek Kapı arayüzü
const $ = (s) => document.querySelector(s);
const views = ['viewSetup', 'viewLocked', 'viewHome', 'viewSettings'];
let lastEventId = 0;
let pollTimer = null;

async function api(path, opts = {}) {
  const res = await fetch('/api' + path, {
    method: opts.method || 'GET',
    headers: { 'x-tek-kapi': '1', ...(opts.body ? { 'content-type': 'application/json' } : {}) },
    body: opts.body ? JSON.stringify(opts.body) : undefined,
    credentials: 'same-origin',
  });
  const data = await res.json().catch(() => ({}));
  if (res.status === 401) { await route(); throw new Error('Kilitli'); }
  if (!res.ok) throw new Error(data.error || 'Bir hata oluştu');
  return data;
}

// Kilit ve kurulum ekranı kapı paneli düzeninde (solda panel), ana ekran ve ayarlar üst çubuklu geniş düzende.
function show(id) {
  views.forEach((v) => { $('#' + v).hidden = v !== id; });
  const inside = id === 'viewHome' || id === 'viewSettings';
  $('#topActions').hidden = !inside;
  $('#duzen').classList.toggle('giris-duzeni', !inside);
  $('#sutun').classList.toggle('giris-sutunu', !inside);
  $('#kapiPaneli').hidden = inside;
  $('#icerik').className = inside ? 'icerik icerik-genis' : 'giris-icerik';
  const kasa = $('#kasaDurumu');
  kasa.dataset.durum = inside ? 'acik' : 'kilitli';
  $('#kasaMetni').textContent = inside ? 'Kasa açık' : 'Kasa kilitli';
  const f = $('#' + id).querySelector('input');
  if (f && id !== 'viewHome') f.focus();
}

const getState = () => fetch('/api/state', { headers: { 'x-tek-kapi': '1' } }).then((r) => r.json());

// SSO modu: oturum yoksa (ör. kimlik sunucusundan çıkış yapıldıysa) SSO girişine dön
function ssoCheck(st) {
  const sso = st.sso || {};
  $('#ssoBar').hidden = !(sso.etkin && sso.girisYapildi);
  $('#ssoName').textContent = sso.ad || '';
  if (sso.etkin && !sso.girisYapildi) { window.location.href = '/auth/login'; return false; }
  return true;
}

async function route() {
  const st = await getState();
  clearInterval(pollTimer);
  if (!ssoCheck(st)) return;
  // Kilit ve kurulum ekranlarında da SSO oturumu yoklanır (Back-Channel Logout sonrası girişe dönmek için)
  const ssoPoll = () => { if (st.sso && st.sso.etkin) pollTimer = setInterval(async () => { try { ssoCheck(await getState()); } catch {} }, 3000); };
  if (!st.vaultExists) { show('viewSetup'); return ssoPoll(); }
  if (!st.unlocked) { show('viewLocked'); return ssoPoll(); }
  await renderHome();
  show('viewHome');
  pollTimer = setInterval(() => { refreshEvents(); }, 2000);
}

const GLYPHS = { obis: 'OB', posta: 'EP', uzem: 'UZ', sinav: 'US', yemek: 'YM' };
// Yarı otomatik sistemlerde kullanıcıda kalan adım
const ASSIST = { obis: 'CAPTCHA sende', yemek: 'Giriş Yap ve 2FA sende' };

function el(tag, cls, text) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (text != null) e.textContent = text;
  return e;
}

async function renderHome() {
  const sites = await api('/sites');
  const list = $('#tiles');
  list.innerHTML = '';
  $('#setupNotice').hidden = sites.every((s) => s.ready);
  for (const s of sites) {
    const li = el('li', 'sistem');
    const glyph = el('span', 'uygulama-simgesi sistem-simgesi', GLYPHS[s.id] || s.name.slice(0, 2));
    glyph.setAttribute('aria-hidden', 'true');
    const info = el('div', 'sistem-bilgisi');
    // Alan adı dar ekranda yalnızca noktalardan sonra kırılsın
    const host = el('p', 'ipucu mono sistem-alani');
    s.host.split('.').forEach((parca, i, hepsi) => {
      host.append(parca + (i < hepsi.length - 1 ? '.' : ''));
      if (i < hepsi.length - 1) host.appendChild(document.createElement('wbr'));
    });
    info.append(el('p', 'sistem-adi', s.name), host);
    const tags = el('div', 'sistem-etiketleri');
    const tag = el('span', s.mode === 'auto' ? 'etiket' : 'etiket etiket-notr',
      s.mode === 'auto' ? 'Otomatik' : (ASSIST[s.id] || 'Yarı otomatik'));
    if (s.mode === 'assist' && s.assistNote) tag.title = s.assistNote;
    tags.appendChild(tag);
    if (!s.ready) tags.appendChild(el('span', 'etiket etiket-tehlike', 'Bilgi eksik'));
    else if (s.failed) tags.appendChild(el('span', 'etiket etiket-tehlike', 'Giriş başarısız, şifreni kontrol et'));
    const b = el('button', 'dugme dugme-ana');
    b.type = 'button';
    b.append('Aç', el('span', 'gorunmez', ': ' + s.name));
    b.disabled = !s.ready;
    b.title = s.ready ? `${s.name} sitesini aç` : 'Önce Ayarlar\'dan bilgilerini ekle';
    b.addEventListener('click', async () => {
      b.disabled = true;
      try { await api('/open/' + s.id, { method: 'POST' }); }
      catch (e) { alertInline(e.message); }
      finally { setTimeout(() => { b.disabled = !s.ready; }, 1200); }
    });
    li.append(glyph, info, tags, b);
    list.appendChild(li);
  }
  await refreshEvents(true);
  await renderMetrics();
}

const fmtSec = (ms) => (ms == null ? '–' : (ms / 1000).toFixed(1).replace('.', ',') + ' sn');

async function renderMetrics() {
  let data;
  try { data = await api('/metrics/summary'); } catch { return; }
  const tbody = $('#metricsTable tbody');
  tbody.innerHTML = '';
  const heads = ['Sistem', 'Giriş', 'Ort. giriş süresi', 'Bunun senin payın', 'Oturum hatırlandı', 'Başarısız'];
  for (const s of data.sites) {
    const tr = document.createElement('tr');
    const cells = [
      s.name,
      String(s.giris),
      fmtSec(s.ortGirisMs),
      s.mode === 'assist' ? fmtSec(s.ortKullaniciMs) : 'otomatik',
      s.oturumHatirlandi ? `${s.oturumHatirlandi} kez, ort. ${fmtSec(s.ortHatirlandiMs)}` : '0',
      String(s.basarisiz),
    ];
    cells.forEach((v, i) => {
      const td = el(i === 0 ? 'th' : 'td', i === 0 ? 'yigin-baslik' : 'sayi', v);
      if (i === 0) td.scope = 'row';
      else td.dataset.etiket = heads[i];
      if (v === '–' || v === '0' || v === 'otomatik') td.classList.add('soluk');
      tr.appendChild(td);
    });
    tbody.appendChild(tr);
  }
}

function bant(sel, msg) {
  const b = $(sel);
  b.querySelector('p').textContent = msg;
  b.hidden = false;
  clearTimeout(b._zaman);
  b._zaman = setTimeout(() => { b.hidden = true; b.querySelector('p').textContent = ''; }, 6000);
}

function infoInline(msg) { bant('#homeInfo', msg); }

function alertInline(msg) { bant('#homeError', msg); }

async function refreshEvents(reset = false) {
  if (reset) { lastEventId = 0; $('#events').innerHTML = ''; }
  let evs = [];
  try { evs = await api('/events?since=' + lastEventId); } catch { return; }
  const list = $('#events');
  for (const e of evs) {
    lastEventId = Math.max(lastEventId, e.id);
    const tr = document.createElement('tr');
    const t = el('td');
    const time = el('time', null, new Date(e.at).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }));
    time.dateTime = new Date(e.at).toISOString();
    t.appendChild(time);
    tr.append(t, el('td', 'kaynak', e.source), el('td', 'yigin-baslik', e.text));
    list.prepend(tr);
    if (!reset && /başarısız|güncellendi/i.test(e.text)) setTimeout(renderHome, 0);
    else if (!reset && /ölçüldü/.test(e.text)) setTimeout(renderMetrics, 0);
  }
  list.querySelectorAll('.bos-satir').forEach((x) => x.remove());
  while (list.children.length > 12) list.lastChild.remove();
  if (!list.children.length) {
    const tr = el('tr', 'bos-satir');
    const td = el('td', 'bos', 'Henüz bir işlem yok.');
    td.colSpan = 3;
    tr.appendChild(td);
    list.appendChild(tr);
  }
}

async function openSettings() {
  const c = await api('/credentials');
  const f = $('#formCreds');
  f.reset();
  f.ogrNo.value = c.ogrNo;
  f.ogrMail.value = c.ogrMail;
  f.querySelectorAll('[data-has]').forEach((p) => {
    const has = c.has[p.dataset.has];
    p.textContent = has ? 'Kayıtlı' : (p.dataset.has === 'ssoPass' ? 'Ayrı SSO şifresi kayıtlı değil' : 'Henüz kayıtlı değil');
    p.classList.toggle('ok', has);
  });
  f.querySelector('.error').textContent = '';
  show('viewSettings');
}

function bindForm(form, handler) {
  form.addEventListener('submit', async (ev) => {
    ev.preventDefault();
    const err = form.querySelector('.error');
    const btn = form.querySelector('[type=submit]');
    err.textContent = '';
    btn.disabled = true;
    try { await handler(form); }
    catch (e) { err.textContent = e.message; }
    finally { btn.disabled = false; }
  });
}

bindForm($('#formSetup'), async (f) => {
  if (f.master.value.length < 6) throw new Error('Ana şifre en az 6 karakter olmalı');
  if (f.master.value !== f.master2.value) throw new Error('Şifreler aynı değil');
  await api('/setup', { method: 'POST', body: { master: f.master.value } });
  f.reset();
  await route();
  await openSettings();
});

bindForm($('#formUnlock'), async (f) => {
  await api('/unlock', { method: 'POST', body: { master: f.master.value } });
  f.reset();
  await route();
});

bindForm($('#formCreds'), async (f) => {
  const body = { ogrNo: f.ogrNo.value, ogrMail: f.ogrMail.value };
  for (const k of ['obisPass', 'mailPass', 'ssoPass']) if (f[k].value) body[k] = f[k].value;
  await api('/credentials', { method: 'PUT', body });
  f.reset();
  await renderHome();
  show('viewHome');
});

$('#btnSettings').addEventListener('click', openSettings);
$('#btnNoticeSettings').addEventListener('click', openSettings);
$('#btnBack').addEventListener('click', async () => { await renderHome(); show('viewHome'); });
$('#btnLogoutAll').addEventListener('click', async () => {
  const btn = $('#btnLogoutAll');
  btn.disabled = true;
  try {
    const r = await api('/logout-all', { method: 'POST' });
    infoInline(r.tabsClosed || r.cookiesCleared
      ? `Tüm oturumlar kapatıldı: ${r.tabsClosed} sekme, ${r.cookiesCleared} oturum çerezi.`
      : 'Açık bir oturum yoktu.');
    if (!$('#viewHome').hidden) await renderHome();
  } catch (e) { alertInline(e.message); }
  finally { btn.disabled = false; }
});
$('#btnLock').addEventListener('click', async () => { await api('/lock', { method: 'POST' }); await route(); });
$('#btnSsoLogout').addEventListener('click', async () => {
  const btn = $('#btnSsoLogout');
  btn.disabled = true;
  try {
    const r = await fetch('/auth/logout', { method: 'POST', headers: { 'x-tek-kapi': '1' }, credentials: 'same-origin' }).then((x) => x.json());
    window.location.href = r.url || '/';
  } catch (e) { alertInline(e.message); btn.disabled = false; }
});

route();
