import { CONFIG } from './config.js';
import {
  LEADERBOARD, SERVERS, SUSPECTS, mockAuth, mockRegister, session, defaultProfile,
} from './mock-data.js';

async function request(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
  if (session.token) headers.Authorization = `Bearer ${session.token}`;
  const res = await fetch(`${CONFIG.apiBase}${path}`, { ...options, headers });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
  if (res.status === 204) return null;
  return res.json();
}

export const api = {
  async health() {
    if (CONFIG.useMock) return { status: 'mock', ok: true };
    return request('/health');
  },

  async register(body) {
    if (CONFIG.useMock) return mockRegister(body.username, body.password);
    return request('/auth/register', { method: 'POST', body: JSON.stringify(body) });
  },

  async login(body) {
    if (CONFIG.useMock) return mockAuth(body.username, body.password);
    return request('/auth/login', { method: 'POST', body: JSON.stringify(body) });
  },

  async me() {
    if (CONFIG.useMock) return session.player || defaultProfile();
    return request('/players/me');
  },

  async leaderboards() {
    if (CONFIG.useMock) return { items: LEADERBOARD };
    return request('/leaderboards');
  },

  async createSquad(name) {
    if (CONFIG.useMock) {
      session.squad = {
        id: 'sq-' + Date.now(),
        name: name || 'Tim-1',
        inviteCode: Math.random().toString(36).slice(2, 8).toUpperCase(),
        members: [{ name: session.player?.displayName || 'Komutan', role: 'Tim Komutanı' }],
      };
      return session.squad;
    }
    return request('/squads', { method: 'POST', body: JSON.stringify({ name }) });
  },

  async joinSquad(code) {
    if (CONFIG.useMock) {
      session.squad = {
        id: 'sq-join',
        name: 'Katılan Tim',
        inviteCode: code,
        members: [
          { name: 'Komutan', role: 'Tim Komutanı' },
          { name: session.player?.displayName || 'Piyade', role: 'Piyade' },
        ],
      };
      return session.squad;
    }
    return request('/squads/join', { method: 'POST', body: JSON.stringify({ code }) });
  },

  async queue() {
    if (CONFIG.useMock) return { status: 'queued', etaSeconds: 45, region: 'İstanbul' };
    return request('/matchmaking/queue', { method: 'POST', body: '{}' });
  },

  async servers() {
    if (CONFIG.useMock) return { items: SERVERS };
    return request('/servers');
  },

  async suspects() {
    if (CONFIG.useMock) return { items: SUSPECTS };
    return request('/admin/suspects');
  },
};
