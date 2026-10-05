/**
 * HAREKÂT API yardımcıları — k6 senaryoları için ortak modül.
 */
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:5080';
const SERVER_KEY = __ENV.SERVER_KEY || 'dev-server-key';
const REGION = __ENV.REGION || 'tr-ist';
const PASSWORD = __ENV.PASSWORD || 'LoadTest!123';

export function register(vu, iter) {
  const username = `k6_${vu}_${iter}_${Date.now()}`;
  const payload = JSON.stringify({
    username,
    email: `${username}@loadtest.harekat.local`,
    password: PASSWORD,
  });
  const res = http.post(`${BASE}/auth/register`, payload, {
    headers: { 'Content-Type': 'application/json' },
    tags: { name: 'register' },
  });
  check(res, { 'register 2xx': (r) => r.status >= 200 && r.status < 300 });
  return { username, body: safeJson(res) };
}

export function login(username) {
  const res = http.post(
    `${BASE}/auth/login`,
    JSON.stringify({ username, password: PASSWORD }),
    { headers: { 'Content-Type': 'application/json' }, tags: { name: 'login' } }
  );
  check(res, { 'login 2xx': (r) => r.status >= 200 && r.status < 300 });
  return safeJson(res);
}

export function me(token) {
  const res = http.get(`${BASE}/players/me`, {
    headers: auth(token),
    tags: { name: 'players_me' },
  });
  check(res, { 'me 2xx': (r) => r.status >= 200 && r.status < 300 });
  return safeJson(res);
}

export function createSquad(token, name) {
  const res = http.post(
    `${BASE}/squads`,
    JSON.stringify({ name }),
    { headers: auth(token), tags: { name: 'squad_create' } }
  );
  check(res, { 'squad 2xx': (r) => r.status >= 200 && r.status < 300 });
  return safeJson(res);
}

export function joinSquad(token, squadId, playerId) {
  const res = http.post(
    `${BASE}/squads/${squadId}/join`,
    JSON.stringify({ playerId }),
    { headers: auth(token), tags: { name: 'squad_join' } }
  );
  check(res, { 'join ok|4xx': (r) => r.status < 500 });
  return res;
}

export function enqueue(token, squadId) {
  const res = http.post(
    `${BASE}/matchmaking/queue`,
    JSON.stringify({ squadId, region: REGION }),
    { headers: auth(token), tags: { name: 'matchmaking_queue' } }
  );
  check(res, { 'queue 2xx': (r) => r.status >= 200 && r.status < 300 });
  return safeJson(res);
}

export function matchResult(matchId, squadId, playerId) {
  const payload = JSON.stringify({
    matchId,
    region: REGION,
    teams: [
      {
        squadId,
        placement: 1 + Math.floor(Math.random() * 6),
        won: Math.random() < 0.15,
        players: [
          {
            playerId,
            kills: Math.floor(Math.random() * 10),
            deaths: Math.floor(Math.random() * 5),
            headshots: Math.floor(Math.random() * 4),
            assists: Math.floor(Math.random() * 3),
          },
        ],
      },
    ],
  });
  const res = http.post(`${BASE}/matches/${matchId}/result`, payload, {
    headers: {
      'Content-Type': 'application/json',
      'X-Server-Key': SERVER_KEY,
    },
    tags: { name: 'match_result' },
  });
  check(res, { 'result 2xx': (r) => r.status >= 200 && r.status < 300 });
  return res;
}

/** Tam oyuncu senaryosu: kayıt → giriş → tim → kuyruk → maç sonucu */
export function fullPlayerFlow(vu, iter) {
  const reg = register(vu, iter);
  const username = reg.username;
  let auth = reg.body;
  if (!auth || !auth.accessToken) {
    auth = login(username);
  } else {
    auth = login(username);
  }
  if (!auth || !auth.accessToken) {
    return;
  }
  me(auth.accessToken);
  const squad = createSquad(auth.accessToken, `Tim-k6-${vu}`);
  if (!squad || !squad.id) {
    return;
  }
  const queue = enqueue(auth.accessToken, squad.id);
  const matchId = (queue && (queue.matchId || queue.ticketId)) || `m-k6-${vu}-${iter}`;
  matchResult(matchId, squad.id, auth.playerId || 'unknown');
}

function auth(token) {
  return {
    'Content-Type': 'application/json',
    Authorization: `Bearer ${token}`,
  };
}

function safeJson(res) {
  try {
    return res.json();
  } catch (_) {
    return null;
  }
}
