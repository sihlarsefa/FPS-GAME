import { CONFIG, applyTheme, setTheme } from './config.js';
import { api } from './api.js';
import { applyI18n, toggleLang, t, ensureI18n } from './i18n.js';
import * as pages from './pages.js';
import { createLobbyWatcher } from './lobby.js';
import { validateAuthForm, parseHash } from './util.js';
import { initPushUi } from './push.js';

const main = document.getElementById('main');
const mockBadge = document.getElementById('mockBadge');
const apiStatus = document.getElementById('apiStatus');
const langToggle = document.getElementById('langToggle');
const themeToggle = document.getElementById('themeToggle');
const adminNav = document.getElementById('navAdmin');

let lobby = null;
let mmTimer = null;
let ticketId = null;

function setActiveNav() {
  const { path } = parseHash();
  document.querySelectorAll('.nav a[data-route]').forEach((a) => {
    const r = a.getAttribute('data-route');
    a.classList.toggle('active', r === path || (r === '/' && path === '/'));
  });
  const me = api.currentPlayer();
  if (adminNav) adminNav.hidden = !api.canModerate(me);
}

async function route() {
  const { parts, query } = parseHash();
  const head = parts[0] || '';
  setActiveNav();
  stopLobby();
  stopMm();
  let html;
  try {
    if (!head) html = await pages.renderHome();
    else if (head === 'login') html = await pages.renderLogin();
    else if (head === 'leaderboards') html = await pages.renderLeaderboards(query);
    else if (head === 'profile') html = await pages.renderProfile();
    else if (head === 'squad') html = await pages.renderSquad();
    else if (head === 'matchmaking') html = await pages.renderMatchmaking();
    else if (head === 'achievements') html = await pages.renderAchievements();
    else if (head === 'download') html = await pages.renderDownload();
    else if (head === 'requirements') html = await pages.renderRequirements();
    else if (head === 'teams') html = await pages.renderTeams();
    else if (head === 'arsenal') html = await pages.renderArsenal();
    else if (head === 'map') html = await pages.renderMap();
    else if (head === 'patches' && parts[1]) html = await pages.renderPatchPost(decodeURIComponent(parts[1]));
    else if (head === 'patches') html = await pages.renderPatches();
    else if (head === 'news' && parts[1]) html = await pages.renderNewsPost(decodeURIComponent(parts[1]));
    else if (head === 'news') html = await pages.renderNews();
    else if (head === 'season') html = await pages.renderSeason();
    else if (head === 'archive') html = await pages.renderArchive(query);
    else if (head === 'compare') html = await pages.renderCompare(query);
    else if (head === 'admin') html = await pages.renderAdmin();
    else if (head === 'player') html = await pages.renderPlayer(decodeURIComponent(parts[1] || 'Oyuncu'));
    else if (head === 'match') html = await pages.renderMatch(parts[1] || 'demo');
    else html = await pages.renderNotFound();
  } catch (err) {
    html = `<div class="alert error">${t('error_generic')}: ${String(err.message || err)}</div>`;
  }
  main.innerHTML = html;
  applyI18n(main);
  bindPageEvents(head);
  main.focus({ preventScroll: true });
}

function stopLobby() {
  lobby?.stop();
  lobby = null;
}

function stopMm() {
  if (mmTimer) clearInterval(mmTimer);
  mmTimer = null;
}

function bindPageEvents(head) {
  const authForm = document.getElementById('authForm');
  if (authForm) {
    const emailField = authForm.querySelector('[data-register-only]');
    authForm.addEventListener('click', (e) => {
      if (e.target?.dataset?.action === 'register' && emailField) emailField.hidden = false;
    });
    authForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(authForm);
      const body = {
        username: String(fd.get('username') || '').trim(),
        password: String(fd.get('password') || ''),
        email: String(fd.get('email') || `${fd.get('username')}@harekat.local`),
        region: 'tr',
      };
      const action = e.submitter?.dataset?.action || 'login';
      const msg = document.getElementById('authMsg');
      const errors = validateAuthForm(body, action);
      if (errors.length) {
        msg.textContent = `Eksik/geçersiz: ${errors.join(', ')}`;
        return;
      }
      try {
        const res = action === 'register' ? await api.register(body) : await api.login(body);
        msg.textContent = `Hoş geldin, ${res.player.username}`;
        location.hash = '#/profile';
      } catch (err) {
        msg.textContent = err.message;
      }
    });
  }

  document.getElementById('logoutBtn')?.addEventListener('click', async () => {
    await api.logout();
    location.hash = '#/login';
  });

  const createSquad = document.getElementById('createSquadForm');
  if (createSquad) {
    createSquad.addEventListener('submit', async (e) => {
      e.preventDefault();
      try {
        await api.createSquad(String(new FormData(createSquad).get('name')));
        document.getElementById('squadMsg').textContent = 'Tim kuruldu.';
        route();
      } catch (err) {
        document.getElementById('squadMsg').textContent = err.message;
      }
    });
  }

  const joinSquad = document.getElementById('joinSquadForm');
  if (joinSquad) {
    joinSquad.addEventListener('submit', async (e) => {
      e.preventDefault();
      try {
        await api.joinSquad(String(new FormData(joinSquad).get('code')));
        document.getElementById('squadMsg').textContent = 'Time katıldın.';
        route();
      } catch (err) {
        document.getElementById('squadMsg').textContent = err.message;
      }
    });
  }

  document.getElementById('readyBtn')?.addEventListener('click', async () => {
    await api.ready(true);
    document.getElementById('squadMsg').textContent = 'Hazır.';
  });
  document.getElementById('unreadyBtn')?.addEventListener('click', async () => {
    await api.ready(false);
    document.getElementById('squadMsg').textContent = 'Hazır değil.';
  });
  document.getElementById('leaveSquadBtn')?.addEventListener('click', async () => {
    await api.leaveSquad();
    route();
  });

  if (head === 'squad' && api.isAuthed()) {
    lobby = createLobbyWatcher({
      onStatus: (s) => {
        const el = document.getElementById('lobbyStatus');
        if (el) el.textContent = s;
      },
      onUpdate: (msg) => {
        if (msg?.squad) {
          const list = document.getElementById('memberList');
          if (!list) return;
          const sq = msg.squad;
          list.innerHTML = Array.from({ length: 10 }, (_, i) => {
            const m = sq.members?.[i] || (sq.memberIds?.[i] ? {
              id: sq.memberIds[i],
              name: String(sq.memberIds[i]).slice(0, 8),
              ready: (sq.readyMemberIds || []).includes(sq.memberIds[i]),
            } : null);
            if (!m) return '<li class="slot empty muted">— boş / bot —</li>';
            return `<li class="slot ${m.ready ? 'ready' : ''}"><span>${m.name || m.id}</span> <span class="pill ${m.ready ? 'ok' : ''}">${m.ready ? 'HAZIR' : '…'}</span></li>`;
          }).join('');
        }
      },
    });
    lobby.start();
  }

  const queueForm = document.getElementById('queueForm');
  if (queueForm) {
    queueForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(queueForm);
      try {
        const q = await api.queue(String(fd.get('region')), Number(fd.get('maxPingMs')) || undefined);
        ticketId = q.ticketId;
        document.getElementById('mmState').textContent = q.status;
        document.getElementById('mmEta').textContent = `${q.etaSeconds ?? '—'}s`;
        document.getElementById('mmStatus').textContent = `Ticket ${q.ticketId}`;
        stopMm();
        mmTimer = setInterval(async () => {
          try {
            const tck = await api.ticket(ticketId);
            document.getElementById('mmState').textContent = tck.status;
            document.getElementById('mmElapsed').textContent = `${tck.elapsedSeconds ?? 0}s`;
            document.getElementById('mmEta').textContent = `${tck.etaSeconds ?? '—'}s`;
            if (tck.status === 'Matched' && tck.matchId) {
              document.getElementById('mmStatus').textContent = `Eşleşti → maç ${tck.matchId}`;
              stopMm();
              location.hash = `#/match/${tck.matchId}`;
            }
          } catch (err) {
            document.getElementById('mmStatus').textContent = err.message;
          }
        }, 2000);
      } catch (err) {
        document.getElementById('mmStatus').textContent = err.message;
      }
    });
  }
  document.getElementById('cancelQueueBtn')?.addEventListener('click', async () => {
    await api.cancelQueue();
    stopMm();
    ticketId = null;
    document.getElementById('mmState').textContent = 'Cancelled';
    document.getElementById('mmStatus').textContent = 'Kuyruk iptal.';
  });

  document.getElementById('lbFilterForm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const season = new FormData(e.target).get('season');
    location.hash = season === '' || season == null ? '#/leaderboards' : `#/leaderboards?season=${season}`;
  });

  document.getElementById('archiveForm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const season = new FormData(e.target).get('season');
    location.hash = `#/archive?season=${season}`;
  });

  document.getElementById('compareForm')?.addEventListener('submit', (e) => {
    e.preventDefault();
    const fd = new FormData(e.target);
    location.hash = `#/compare?a=${encodeURIComponent(String(fd.get('a')))}&b=${encodeURIComponent(String(fd.get('b')))}`;
  });

  const banForm = document.getElementById('banForm');
  if (banForm) {
    banForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(banForm);
      const action = e.submitter?.dataset?.action || 'ban';
      const body = {
        playerId: String(fd.get('playerId')),
        reason: String(fd.get('reason') || ''),
        durationHours: fd.get('durationHours') ? Number(fd.get('durationHours')) : null,
      };
      try {
        if (action === 'mute') await api.mute(body);
        else await api.ban(body);
        document.getElementById('banMsg').textContent = `${action} OK: ${body.playerId}`;
      } catch (err) {
        document.getElementById('banMsg').textContent = err.message;
      }
    });
  }

  document.getElementById('adminSearchForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    const username = String(new FormData(e.target).get('username'));
    try {
      const p = await api.player(username);
      document.getElementById('adminSearchMsg').textContent = `${p.username} · id ${p.id} · role ${p.role} · elo ${p.eloRating}`;
      const idInput = banForm?.querySelector('[name=playerId]');
      if (idInput) idInput.value = p.id;
    } catch (err) {
      document.getElementById('adminSearchMsg').textContent = err.message;
    }
  });

  document.getElementById('copyShaBtn')?.addEventListener('click', async () => {
    const val = document.getElementById('sha256Value')?.textContent?.trim() || '';
    const msg = document.getElementById('copyShaMsg');
    try {
      await navigator.clipboard.writeText(val);
      if (msg) msg.textContent = t('dl_copied');
    } catch {
      if (msg) msg.textContent = val;
    }
  });

  initPushUi(main);
}

async function boot() {
  applyTheme();
  await ensureI18n();
  document.documentElement.lang = CONFIG.lang;
  mockBadge.hidden = !CONFIG.useMock;
  langToggle.textContent = CONFIG.lang === 'tr' ? 'EN' : 'TR';
  if (themeToggle) themeToggle.textContent = CONFIG.theme === 'dark' ? '☀' : '☾';

  langToggle.addEventListener('click', async () => {
    const lang = await toggleLang();
    langToggle.textContent = lang === 'tr' ? 'EN' : 'TR';
    route();
  });

  themeToggle?.addEventListener('click', () => {
    setTheme(CONFIG.theme === 'dark' ? 'light' : 'dark');
    themeToggle.textContent = CONFIG.theme === 'dark' ? '☀' : '☾';
  });

  window.addEventListener('harekat:auth', () => setActiveNav());

  try {
    const h = await api.health();
    apiStatus.textContent = CONFIG.useMock ? `API: mock` : `API: ${h.status || 'ok'}`;
  } catch {
    apiStatus.textContent = 'API: erişilemiyor — mock önerilir';
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
