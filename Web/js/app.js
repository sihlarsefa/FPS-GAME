import { CONFIG } from './config.js';
import { api } from './api.js';
import { applyI18n, toggleLang, t } from './i18n.js';
import * as pages from './pages.js';

const main = document.getElementById('main');
const mockBadge = document.getElementById('mockBadge');
const apiStatus = document.getElementById('apiStatus');
const langToggle = document.getElementById('langToggle');

function pathParts() {
  const hash = location.hash.replace(/^#/, '') || '/';
  const clean = hash.startsWith('/') ? hash : `/${hash}`;
  return clean.split('/').filter(Boolean);
}

function setActiveNav() {
  const route = '/' + (pathParts()[0] || '');
  document.querySelectorAll('.nav a').forEach((a) => {
    const r = a.getAttribute('data-route');
    a.classList.toggle('active', r === route || (r === '/' && route === '/'));
  });
}

async function route() {
  const parts = pathParts();
  const head = parts[0] || '';
  setActiveNav();
  let html;
  try {
    if (!head) html = await pages.renderHome();
    else if (head === 'leaderboards') html = await pages.renderLeaderboards();
    else if (head === 'profile') html = await pages.renderProfile();
    else if (head === 'squad') html = await pages.renderSquad();
    else if (head === 'arsenal') html = await pages.renderArsenal();
    else if (head === 'map') html = await pages.renderMap();
    else if (head === 'patches') html = await pages.renderPatches();
    else if (head === 'news') html = await pages.renderNews();
    else if (head === 'season') html = await pages.renderSeason();
    else if (head === 'admin') html = await pages.renderAdmin();
    else if (head === 'player') html = await pages.renderPlayer(decodeURIComponent(parts[1] || 'Oyuncu'));
    else if (head === 'match') html = await pages.renderMatch(parts[1] || 'demo');
    else html = await pages.renderNotFound();
  } catch (err) {
    html = `<div class="alert error">Hata: ${String(err.message || err)}</div>`;
  }
  main.innerHTML = html;
  applyI18n(main);
  bindPageEvents();
  main.focus({ preventScroll: true });
}

function bindPageEvents() {
  const authForm = document.getElementById('authForm');
  if (authForm) {
    authForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(authForm);
      const body = { username: fd.get('username'), password: fd.get('password') };
      const action = e.submitter?.dataset?.action || 'login';
      const msg = document.getElementById('authMsg');
      try {
        const res = action === 'register' ? await api.register(body) : await api.login(body);
        msg.textContent = `Hoş geldin, ${res.player.displayName}`;
        route();
      } catch (err) {
        msg.textContent = err.message;
      }
    });
  }

  const createSquad = document.getElementById('createSquadForm');
  if (createSquad) {
    createSquad.addEventListener('submit', async (e) => {
      e.preventDefault();
      const name = new FormData(createSquad).get('name');
      await api.createSquad(String(name));
      document.getElementById('squadMsg').textContent = 'Tim kuruldu.';
      route();
    });
  }

  const joinSquad = document.getElementById('joinSquadForm');
  if (joinSquad) {
    joinSquad.addEventListener('submit', async (e) => {
      e.preventDefault();
      const code = new FormData(joinSquad).get('code');
      await api.joinSquad(String(code));
      document.getElementById('squadMsg').textContent = 'Time katıldın.';
      route();
    });
  }

  const queueBtn = document.getElementById('queueBtn');
  if (queueBtn) {
    queueBtn.addEventListener('click', async () => {
      const q = await api.queue();
      document.getElementById('squadMsg').textContent = `Kuyruk: ${q.status} · ETA ${q.etaSeconds}s · ${q.region}`;
    });
  }

  const banForm = document.getElementById('banForm');
  if (banForm) {
    banForm.addEventListener('submit', (e) => {
      e.preventDefault();
      const name = new FormData(banForm).get('name');
      document.getElementById('banMsg').textContent = `${name} yasaklandı (mock).`;
    });
  }
}

async function boot() {
  mockBadge.hidden = !CONFIG.useMock;
  langToggle.textContent = CONFIG.lang === 'tr' ? 'EN' : 'TR';
  langToggle.addEventListener('click', () => {
    const lang = toggleLang();
    langToggle.textContent = lang === 'tr' ? 'EN' : 'TR';
    route();
  });

  try {
    const h = await api.health();
    apiStatus.textContent = CONFIG.useMock ? `API: mock (${t('mock_note')})` : `API: ${h.status || 'ok'}`;
  } catch {
    apiStatus.textContent = 'API: erişilemiyor — mock önerilir (localStorage harekat_mock=true)';
  }

  applyI18n();
  window.addEventListener('hashchange', route);
  if (!location.hash) location.hash = '#/';
  else route();

  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(() => {});
  }
}

boot();
