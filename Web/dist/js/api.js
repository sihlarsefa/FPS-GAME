import { CONFIG } from './config.js';
import { createClient, ApiError } from './api/client.js';
import { createSession, canModerate } from './api/session.js';
import { ENDPOINTS, LOBBY_PATH } from './api/endpoints.js';
import {
  LEADERBOARD, SERVERS, SUSPECTS, REPORTS, ACHIEVEMENTS, MATCH_DEMO, NEWS,
  RANKS, WEAPONS, mockAuthResponse, mockState, syncLegacySession, defaultPlayer,
} from './mock-data.js';

const storage = typeof sessionStorage !== 'undefined' ? sessionStorage : undefined;
export const authSession = createSession(storage);

function onUnauthorized() {
  syncLegacySession(null);
  try { window.dispatchEvent(new CustomEvent('harekat:auth', { detail: null })); } catch { /* node */ }
}

const live = createClient({
  base: CONFIG.apiBase,
  telemetryBase: CONFIG.telemetryBase,
  session: authSession,
  onUnauthorized,
});

async function mockCall(name, options = {}) {
  const delay = (ms) => new Promise((r) => setTimeout(r, ms));
  await delay(40);
  const body = options.body || {};
  const params = options.params || {};
  const query = options.query || {};

  switch (name) {
    case 'health':
      return { status: 'Healthy', service: 'Harekat.Api', mock: true };
    case 'metrics':
      return '# HELP mock\nmock 1\n';
    case 'register': {
      const auth = mockAuthResponse(body.username);
      authSession.set(auth);
      syncLegacySession(auth);
      return auth;
    }
    case 'login': {
      const auth = mockAuthResponse(body.username);
      authSession.set(auth);
      syncLegacySession(auth);
      return auth;
    }
    case 'refresh': {
      const cur = authSession.get();
      if (!cur) throw new ApiError('No session', 401);
      const next = { ...cur, expiresAt: new Date(Date.now() + 3600_000).toISOString() };
      authSession.set(next);
      return next;
    }
    case 'logout':
      authSession.clear();
      mockState.player = null;
      mockState.squad = null;
      syncLegacySession(null);
      return null;
    case 'me': {
      const cur = authSession.get();
      if (!cur) throw new ApiError('Unauthorized', 401);
      return cur.player || mockState.player || defaultPlayer();
    }
    case 'player': {
      const hit = LEADERBOARD.find((e) => e.username.toLowerCase() === String(params.username || '').toLowerCase());
      if (!hit) {
        const p = defaultPlayer(params.username || 'Oyuncu');
        return p;
      }
      return {
        ...defaultPlayer(hit.username),
        id: hit.playerId,
        rank: hit.militaryRank,
        eloRating: hit.elo,
        seasonXp: hit.value,
        stats: { ...defaultPlayer().stats, experience: hit.value, kills: Math.round(hit.value / 50), wins: Math.round(hit.value / 1200), rank: hit.militaryRank },
      };
    }
    case 'createSquad': {
      mockState.squad = {
        id: 'sq-' + Date.now(),
        name: body.name || 'Tim-1',
        leaderId: authSession.get()?.player?.id || 'p-local',
        memberIds: [authSession.get()?.player?.id || 'p-local'],
        readyMemberIds: [],
        inviteCode: Math.random().toString(36).slice(2, 8).toUpperCase(),
        region: body.region || 'tr',
        allReady: false,
        openSlots: 9,
        members: [{ id: authSession.get()?.player?.id || 'p-local', name: authSession.get()?.player?.username || 'Komutan', ready: false }],
      };
      return mockState.squad;
    }
    case 'joinSquad': {
      mockState.squad = {
        id: 'sq-join',
        name: 'Katılan Tim',
        leaderId: 'p-leader',
        memberIds: ['p-leader', authSession.get()?.player?.id || 'p-local'],
        readyMemberIds: ['p-leader'],
        inviteCode: body.inviteCode || body.code || 'ABC123',
        region: 'tr',
        allReady: false,
        openSlots: 8,
        members: [
          { id: 'p-leader', name: 'Komutan', ready: true },
          { id: authSession.get()?.player?.id || 'p-local', name: authSession.get()?.player?.username || 'Piyade', ready: false },
        ],
      };
      return mockState.squad;
    }
    case 'mySquad':
    case 'squad':
      if (!mockState.squad) throw new ApiError('No squad', 404);
      return mockState.squad;
    case 'ready': {
      if (!mockState.squad) throw new ApiError('No squad', 404);
      const pid = authSession.get()?.player?.id || 'p-local';
      const ready = body.ready !== false && body.ready !== 'false';
      const set = new Set(mockState.squad.readyMemberIds || []);
      if (ready) set.add(pid); else set.delete(pid);
      mockState.squad.readyMemberIds = [...set];
      mockState.squad.allReady = mockState.squad.readyMemberIds.length >= mockState.squad.memberIds.length;
      mockState.squad.members = (mockState.squad.members || []).map((m) => ({
        ...m, ready: mockState.squad.readyMemberIds.includes(m.id),
      }));
      return { squadId: mockState.squad.id, playerId: pid, isReady: ready, allReady: mockState.squad.allReady };
    }
    case 'leaveSquad':
      mockState.squad = null;
      return null;
    case 'queue': {
      mockState.ticket = {
        ticketId: 't-' + Date.now(),
        status: 'Queued',
        region: body.region || 'tr',
        enqueuedAt: new Date().toISOString(),
        etaSeconds: 35 + Math.floor(Math.random() * 40),
        elapsedSeconds: 0,
      };
      return mockState.ticket;
    }
    case 'cancelQueue':
      mockState.ticket = null;
      return null;
    case 'ticket': {
      if (!mockState.ticket) throw new ApiError('Ticket not found', 404);
      const elapsed = Math.floor((Date.now() - Date.parse(mockState.ticket.enqueuedAt)) / 1000);
      mockState.ticket.elapsedSeconds = elapsed;
      mockState.ticket.etaSeconds = Math.max(5, (mockState.ticket.etaSeconds || 40) - 1);
      if (elapsed > 20) {
        mockState.ticket.status = 'Matched';
        mockState.ticket.matchId = 'demo';
      }
      return mockState.ticket;
    }
    case 'match': {
      const id = params.id || 'demo';
      return { ...MATCH_DEMO, id };
    }
    case 'servers':
      return SERVERS;
    case 'leaderboards': {
      const take = Number(query.take) || 10;
      const page = Number(query.page) || 1;
      const start = (page - 1) * take;
      const items = LEADERBOARD.slice(start, start + take);
      return { items, page, take, total: LEADERBOARD.length };
    }
    case 'seasonLeaderboard': {
      const take = Number(query.take) || 10;
      const season = Number(query.season) || 1;
      return {
        items: LEADERBOARD.slice(0, take).map((e) => ({ ...e, value: Math.round(e.value * (season === 1 ? 1 : 0.7)) })),
        season,
        take,
        total: LEADERBOARD.length,
      };
    }
    case 'activeSeason':
      return { number: 1, name: 'Vadi Tatbikatı', startsAt: '2026-09-01T00:00:00Z', endsAt: '2026-12-01T00:00:00Z', isActive: true };
    case 'seasonArchive': {
      const number = Number(params.number) || 0;
      return LEADERBOARD.slice(0, 5).map((e, i) => ({
        seasonNumber: number,
        playerId: e.playerId,
        username: e.username,
        seasonXp: Math.round(e.value * 0.6),
        placement: i + 1,
        rewardBadge: i === 0 ? 'gold' : i < 3 ? 'silver' : 'bronze',
      }));
    }
    case 'achievements':
      return ACHIEVEMENTS;
    case 'cosmetics':
      return [
        { id: 'camo_olive', name: 'Zeytin Kamuflaj', slot: 'Camo', owned: true, equipped: true },
        { id: 'beret_red', name: 'Kırmızı Bere', slot: 'Beret', owned: true, equipped: true },
      ];
    case 'friends':
      return mockState.friends;
    case 'friendRequest':
      mockState.friends.push({
        id: 'f-' + Date.now(), otherPlayerId: body.targetPlayerId, otherUsername: 'Arkadaş',
        status: 'Pending', otherOnline: false,
      });
      return mockState.friends.at(-1);
    case 'reportPlayer':
      REPORTS.unshift({
        id: 'r-' + Date.now(), reportedPlayerId: body.reportedPlayerId, reporterId: 'me',
        reason: body.reason, matchId: body.matchId, createdAt: new Date().toISOString(), status: 'Open',
      });
      return { ok: true };
    case 'ban':
      return { ok: true, playerId: body.playerId, reason: body.reason };
    case 'mute':
      return { ok: true, playerId: body.playerId, durationHours: body.durationHours };
    case 'reports':
      return REPORTS;
    case 'telemetryHealth':
      return { status: 'Healthy', service: 'Telemetry', mock: true };
    case 'suspects':
      return SUSPECTS.filter((s) => s.totalScore >= (Number(query.minScore) || 0));
    case 'reviewQueue':
      return SUSPECTS.map((s, i) => ({
        id: 'rq-' + i, playerId: s.playerId, matchId: s.matchId, riskScore: s.totalScore,
        enqueuedAt: s.generatedAt, status: 'Pending', notes: s.findings[0]?.detail || null,
      }));
    case 'playerRisk':
      return {
        playerId: params.playerId,
        points: [
          { timestamp: '2026-10-01T10:00:00Z', score: 20, matchId: 'm1', reason: null },
          { timestamp: '2026-10-03T10:00:00Z', score: 55, matchId: 'm2', reason: 'aim' },
          { timestamp: '2026-10-05T10:00:00Z', score: 78, matchId: 'm3', reason: 'wall' },
        ],
      };
    case 'telemetryPerf':
      return { ingestPerSec: 120, queueMs: 14, queueDepth: 3 };
    case 'heatmap':
      return {
        matchId: params.matchId,
        cellSizeMeters: 50,
        gridWidth: 20,
        cells: MATCH_DEMO.deaths.map((d, i) => ({
          gridX: Math.floor((d.x + 512) / 50), gridZ: Math.floor((d.z + 512) / 50),
          deaths: 1, landings: i < MATCH_DEMO.landings.length ? 1 : 0,
        })),
      };
    case 'weaponBalance':
      return {
        scope: 'mock',
        weapons: WEAPONS.slice(0, 5).map((w) => ({
          weaponId: w.id, kills: 100 + w.dmg, deaths: 80, kd: 1.2,
          averageKillDistance: w.range / 3, averageTtkMs: 400, hitRate: 0.32,
          shots: 1000, hits: 320, ttkPercentiles: [280, 400, 620],
        })),
      };
    default:
      if (ENDPOINTS[name]) return { ok: true, mock: true, endpoint: name };
      throw new TypeError(`Unknown mock endpoint: ${name}`);
  }
}

async function call(name, options = {}) {
  if (CONFIG.useMock) return mockCall(name, options);
  return live.call(name, options);
}

if (authSession.get()) syncLegacySession(authSession.get());

export const api = {
  ENDPOINTS,
  LOBBY_PATH,
  session: authSession,
  canModerate,
  call,
  ApiError,

  health: () => call('health'),
  telemetryHealth: () => call('telemetryHealth'),

  async login(body) {
    const value = CONFIG.useMock ? await mockCall('login', { body }) : await live.login(body);
    syncLegacySession(value);
    try { window.dispatchEvent(new CustomEvent('harekat:auth', { detail: value.player })); } catch { /* node */ }
    return value;
  },
  async register(body) {
    const value = CONFIG.useMock ? await mockCall('register', { body }) : await live.register(body);
    syncLegacySession(value);
    try { window.dispatchEvent(new CustomEvent('harekat:auth', { detail: value.player })); } catch { /* node */ }
    return value;
  },
  async logout() {
    if (CONFIG.useMock) await mockCall('logout');
    else await live.logout();
    try { window.dispatchEvent(new CustomEvent('harekat:auth', { detail: null })); } catch { /* node */ }
  },
  me: () => call('me'),
  player: (username) => call('player', { params: { username } }),

  createSquad: (name, region) => call('createSquad', { body: { name, region: region || 'tr' } }),
  joinSquad: (inviteCode) => call('joinSquad', { body: { inviteCode } }),
  mySquad: () => call('mySquad'),
  squad: (id) => call('squad', { params: { id } }),
  ready: (ready = true) => call('ready', { body: { ready }, query: { ready } }),
  leaveSquad: () => call('leaveSquad'),

  queue: (region, maxPingMs) => call('queue', { body: { region, maxPingMs } }),
  cancelQueue: () => call('cancelQueue'),
  ticket: (id) => call('ticket', { params: { id } }),
  match: (id) => call('match', { params: { id } }),

  leaderboards: (query = {}) => call('leaderboards', { query }),
  seasonLeaderboard: (query = {}) => call('seasonLeaderboard', { query }),
  activeSeason: () => call('activeSeason'),
  seasonArchive: (number) => call('seasonArchive', { params: { number } }),

  achievements: () => call('achievements'),
  cosmetics: () => call('cosmetics'),
  equipCosmetic: (cosmeticId) => call('equipCosmetic', { body: { cosmeticId } }),

  friends: () => call('friends'),
  friendRequest: (targetPlayerId) => call('friendRequest', { body: { targetPlayerId } }),

  servers: () => call('servers'),
  reports: () => call('reports'),
  ban: (body) => call('ban', { body }),
  mute: (body) => call('mute', { body }),
  reportPlayer: (body) => call('reportPlayer', { body }),

  suspects: (minScore) => call('suspects', { query: { minScore } }),
  reviewQueue: () => call('reviewQueue'),
  playerRisk: (playerId) => call('playerRisk', { params: { playerId } }),
  telemetryPerf: () => call('telemetryPerf'),
  heatmap: (matchId) => call('heatmap', { params: { matchId } }),
  weaponBalance: () => call('weaponBalance'),

  /** Convenience for UI */

  currentPlayer() {
    return authSession.get()?.player || null;
  },
  isAuthed() {
    return !!authSession.get();
  },
  rankName(index) {
    return RANKS[index] || RANKS[0];
  },
  news: () => Promise.resolve(NEWS),
};

export { RANKS, WEAPONS };
