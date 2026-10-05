import { readFileSync, existsSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
import assert from 'node:assert/strict';
import { createSession, canModerate } from '../js/api/session.js';
import { ENDPOINTS, endpointUrl, LOBBY_PATH } from '../js/api/endpoints.js';
import { createClient, ApiError } from '../js/api/client.js';
import { checkRoutes } from './contracts.mjs';

const root = new URL('..', import.meta.url);

function read(rel) {
  return readFileSync(new URL(rel, root), 'utf8');
}

// Static presence
for (const f of [
  'index.html', 'css/main.css', 'web.config',
  'js/app.js', 'js/api.js', 'js/mock-data.js', 'js/pages.js', 'js/lobby.js', 'js/push.js', 'js/util.js',
  'js/api/client.js', 'js/api/endpoints.js', 'js/api/session.js', 'js/api/types.js',
  'i18n/tr.json', 'i18n/en.json',
]) {
  assert.ok(existsSync(new URL(f, root)), `missing ${f}`);
  readFileSync(new URL(f, root));
}

const mock = await import(pathToFileURL(new URL('js/mock-data.js', root).pathname).href);
assert.equal(mock.WEAPONS.length, 10);
assert.equal(mock.RANKS.length, 19);
assert.ok(mock.LOCATIONS.some((l) => l.name === 'Kuzgun Köyü'));

const auth = mock.mockAuth('TestUser', 'secret1');
assert.equal(auth.player.username, 'TestUser');
assert.ok(auth.accessToken.startsWith('mock-jwt-'));
assert.ok(auth.refreshToken);
assert.ok(Date.parse(auth.expiresAt) > Date.now());

const admin = mock.mockAuthResponse('admin');
assert.equal(admin.player.role, 'Admin');
assert.ok(canModerate(admin.player));
assert.ok(!canModerate({ role: 'Player' }));

// Session
const mem = { store: {}, getItem(k) { return this.store[k] ?? null; }, setItem(k, v) { this.store[k] = String(v); }, removeItem(k) { delete this.store[k]; } };
const session = createSession(mem);
assert.equal(session.get(), null);
session.set(auth);
assert.equal(session.get().accessToken, auth.accessToken);
session.clear();
assert.equal(session.get(), null);

// endpointUrl
assert.equal(endpointUrl(ENDPOINTS.player, { username: 'A B' }), '/players/A%20B');
assert.equal(endpointUrl(ENDPOINTS.match, { id: 'abc' }, { x: 1 }), '/matches/abc?x=1');
assert.equal(LOBBY_PATH, '/hubs/lobby');
assert.ok(Object.keys(ENDPOINTS).length >= 50);

// Client retry + JWT (mock fetch)
let calls = 0;
const fetchImpl = async (url, opts) => {
  calls++;
  if (String(url).includes('/auth/login')) {
    return {
      ok: true, status: 200,
      headers: { get: () => null },
      json: async () => auth,
    };
  }
  if (String(url).includes('/players/me')) {
    const hdr = opts.headers.Authorization || '';
    if (!hdr.includes('Bearer')) {
      return { ok: false, status: 401, headers: { get: () => null }, json: async () => ({ detail: 'no' }), text: async () => 'no' };
    }
    return { ok: true, status: 200, headers: { get: () => null }, json: async () => auth.player };
  }
  if (String(url).includes('/auth/refresh')) {
    return {
      ok: true, status: 200, headers: { get: () => null },
      json: async () => ({ ...auth, accessToken: 'mock-jwt-refreshed', expiresAt: new Date(Date.now() + 3600_000).toISOString() }),
    };
  }
  if (String(url).includes('/health') && calls < 2) {
    return { ok: false, status: 503, headers: { get: () => null }, json: async () => ({}), text: async () => '' };
  }
  return { ok: true, status: 200, headers: { get: () => null }, json: async () => ({ status: 'Healthy' }) };
};

const s2 = createSession(mem);
let unauthorized = 0;
const client = createClient({
  base: 'http://test',
  session: s2,
  fetchImpl,
  onUnauthorized: () => { unauthorized++; },
  retryDelay: async () => {},
});
await client.login({ username: 'TestUser', password: 'secret1' });
const me = await client.me();
assert.equal(me.username, 'TestUser');

// Expired token → refresh
s2.set({ ...auth, expiresAt: new Date(Date.now() - 1000).toISOString() });
const me2 = await client.me();
assert.equal(me2.username, 'TestUser');
assert.equal(s2.get().accessToken, 'mock-jwt-refreshed');

// util
const util = await import(pathToFileURL(new URL('js/util.js', root).pathname).href);
assert.equal(util.esc('<b>'), '&lt;b&gt;');
assert.deepEqual(util.validateAuthForm({ username: 'ab', password: '123' }, 'login'), ['username', 'password']);
assert.deepEqual(util.validateAuthForm({ username: 'abc', password: '123456', email: 'a@b.co' }, 'register'), []);
const [sx] = util.worldToSvg(0, 0);
assert.equal(sx, 500);

// i18n files
const tr = JSON.parse(read('i18n/tr.json'));
const en = JSON.parse(read('i18n/en.json'));
assert.ok(tr.nav_admin && en.nav_admin);
assert.deepEqual(Object.keys(tr).sort(), Object.keys(en).sort());

// web.config essentials
const cfg = read('web.config');
assert.ok(cfg.includes('ApiProxy'));
assert.ok(cfg.includes('Content-Security-Policy'));
assert.ok(cfg.includes('Strict-Transport-Security'));
assert.ok(cfg.includes('SpaFallback'));

// contracts (backend route parity)
checkRoutes();

// pages export surface
const pages = await import(pathToFileURL(new URL('js/pages.js', root).pathname).href);
for (const fn of [
  'renderHome', 'renderLogin', 'renderProfile', 'renderSquad', 'renderMatchmaking',
  'renderLeaderboards', 'renderMatch', 'renderAchievements', 'renderAdmin',
  'renderCompare', 'renderArchive', 'renderDownload', 'renderRequirements',
  'renderTeams', 'renderPatches', 'renderPatchPost',
]) {
  assert.equal(typeof pages[fn], 'function', fn);
}

// C3-7 content
const ach = JSON.parse(read('content/data/achievements.json'));
assert.ok(ach.items.length >= 50, 'achievements catalog < 50');
const teams = JSON.parse(read('content/data/teams.json'));
assert.equal(teams.teams.length, 8);
for (const tm of teams.teams) {
  assert.ok(existsSync(new URL(tm.emblem, root)), `missing emblem ${tm.emblem}`);
}
assert.ok(existsSync(new URL('content/data/download.json', root)));
assert.ok(existsSync(new URL('content/data/sysreq.json', root)));
assert.ok(existsSync(new URL('scripts/patchnotes-build.mjs', root)));

const { buildPatchnotes } = await import(pathToFileURL(new URL('scripts/patchnotes-build.mjs', root).pathname).href);
const notes = buildPatchnotes();
assert.ok(notes.length >= 4, 'patchnotes md count');
assert.ok(existsSync(new URL('content/patchnotes/rss.xml', root)), 'rss.xml');
assert.ok(existsSync(new URL('content/patchnotes/index.json', root)), 'patchnotes index');

// web.config preserved essentials (already asserted above)
assert.ok(cfg.includes('ApiProxy') && cfg.includes('SpaFallback'));

console.log(`test ok — endpoints ${Object.keys(ENDPOINTS).length}, weapons ${mock.WEAPONS.length}, ranks ${mock.RANKS.length}, i18n keys ${Object.keys(tr).length}, achievements ${ach.items.length}, teams ${teams.teams.length}, patches ${notes.length}`);
