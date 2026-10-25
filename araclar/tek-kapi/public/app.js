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

function show(id) {
  views.forEach((v) => { $('#' + v).hidden = v !== id; });
  const inside = id === 'viewHome' || id === 'viewSettings';
  $('#topActions').hidden = !inside;
  const f = $('#' + id).querySelector('input');
  if (f && id !== 'viewHome') f.focus();
}

async function route() {
  const st = await fetch('/api/state', { headers: { 'x-tek-kapi': '1' } }).then((r) => r.json());
  clearInterval(pollTimer);
  if (!st.vaultExists) return show('viewSetup');
  if (!st.unlocked) return show('viewLocked');
  await renderHome();
  show('viewHome');
  pollTimer = setInterval(() => { refreshEvents(); }, 2000);
}

const GLYPHS = { obis: 'OB', posta: '@', uzem: 'UZ', sinav: 'US', yemek: 'YM' };

async function renderHome() {
  const sites = await api('/sites');
  const grid = $('#tiles');
  grid.innerHTML = '';
  $('#setupNotice').hidden = sites.every((s) => s.ready);
  for (const s of sites) {
    const b = document.createElement('button');
    b.className = 'tile';
    b.disabled = !s.ready;
    b.title = s.ready ? `${s.name} sitesini aç` : 'Önce Ayarlar\'dan bilgilerini ekle';
    b.innerHTML = `
      <div class="tile-head">
        <span class="glyph" aria-hidden="true"></span>
        <div><div class="tile-name"></div><div class="tile-host"></div></div>
      </div>
      <p class="tile-desc"></p>
      <div class="chips"></div>
      <p class="tile-note"></p>`;
    b.querySelector('.glyph').textContent = GLYPHS[s.id] || s.name.slice(0, 2);
    b.querySelector('.tile-name').textContent = s.name;
    b.querySelector('.tile-host').textContent = s.host;
    b.querySelector('.tile-desc').textContent = s.desc;
    const chips = b.querySelector('.chips');
    const chip = (cls, txt) => { const c = document.createElement('span'); c.className = 'chip ' + cls; c.textContent = txt; chips.appendChild(c); };
    if (!s.ready) chip('missing', 'Bilgi eksik');
    else if (s.failed) chip('failed', 'Giriş başarısız, şifreni kontrol et');
    else chip(s.mode === 'auto' ? 'auto' : 'assist', s.mode === 'auto' ? 'Otomatik giriş' : 'Yarı otomatik');
    b.querySelector('.tile-note').textContent = s.mode === 'assist' ? s.assistNote : '';
    b.addEventListener('click', async () => {
      b.disabled = true;
      try { await api('/open/' + s.id, { method: 'POST' }); }
      catch (e) { alertInline(e.message); }
      finally { setTimeout(() => { b.disabled = !s.ready; }, 1200); }
    });
    grid.appendChild(b);
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
  for (const s of data.sites) {
    const tr = document.createElement('tr');
    const cells = [
      s.name,
      String(s.giris),
      fmtSec(s.ortGirisMs),
      s.mode === 'assist' ? fmtSec(s.ortKullaniciMs) : 'otomatik',
      s.oturumHatirlandi ? `${s.oturumHatirlandi} kez · ${fmtSec(s.ortHatirlandiMs)}` : '0',
      String(s.basarisiz),
    ];
    cells.forEach((v, i) => {
      const td = document.createElement('td');
      td.textContent = v;
      if (i > 0) td.className = 'num';
      if (v === '–' || v === '0' || v === 'otomatik') td.classList.add('muted');
      tr.appendChild(td);
    });
    tbody.appendChild(tr);
  }
}

function infoInline(msg) {
  $('#homeInfo').textContent = msg;
  setTimeout(() => { $('#homeInfo').textContent = ''; }, 6000);
}

function alertInline(msg) {
  $('#homeError').textContent = msg;
  setTimeout(() => { $('#homeError').textContent = ''; }, 6000);
}

async function refreshEvents(reset = false) {
  if (reset) { lastEventId = 0; $('#events').innerHTML = ''; }
  let evs = [];
  try { evs = await api('/events?since=' + lastEventId); } catch { return; }
  const list = $('#events');
  for (const e of evs) {
    lastEventId = Math.max(lastEventId, e.id);
    const li = document.createElement('li');
    const t = document.createElement('time');
    t.textContent = new Date(e.at).toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    const src = document.createElement('span'); src.className = 'src'; src.textContent = e.source;
    const txt = document.createElement('span'); txt.textContent = e.text;
    li.append(t, src, txt);
    list.prepend(li);
    if (!reset && /başarısız|güncellendi/i.test(e.text)) setTimeout(renderHome, 0);
    else if (!reset && /ölçüldü/.test(e.text)) setTimeout(renderMetrics, 0);
  }
  while (list.children.length > 12) list.lastChild.remove();
  if (!list.children.length) {
    const li = document.createElement('li'); li.className = 'empty'; li.textContent = 'Henüz bir işlem yok.'; list.appendChild(li);
  } else {
    list.querySelectorAll('.empty').forEach((x) => x.remove());
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
    p.textContent = has ? 'Kayıtlı ✓' : (p.dataset.has === 'ssoPass' ? 'Ayrı SSO şifresi kayıtlı değil' : 'Henüz kayıtlı değil');
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

route();
