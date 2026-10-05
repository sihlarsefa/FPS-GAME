import { CONFIG } from './config.js';
import { api } from './api.js';
import { LOBBY_PATH } from './api/endpoints.js';

/**
 * Real-time squad updates without @microsoft/signalr.
 * Prefer WebSocket to /hubs/lobby; fall back to long-polling mySquad.
 */
export function createLobbyWatcher({ onUpdate, onStatus, intervalMs = CONFIG.pollMs } = {}) {
  let closed = false;
  let timer = null;
  let socket = null;
  let mode = 'idle';

  const status = (s) => { mode = s; onStatus?.(s); };

  async function pollOnce() {
    try {
      const squad = await api.mySquad();
      onUpdate?.({ type: 'squad', squad });
      status('polling');
    } catch (err) {
      if (err.status === 404) onUpdate?.({ type: 'squad', squad: null });
      status('polling-error');
    }
  }

  function startPolling() {
    stopSocket();
    status('polling');
    pollOnce();
    timer = setInterval(pollOnce, intervalMs);
  }

  function stopPolling() {
    if (timer) clearInterval(timer);
    timer = null;
  }

  function stopSocket() {
    if (socket) {
      try { socket.close(); } catch { /* ignore */ }
      socket = null;
    }
  }

  function wsUrl() {
    const base = CONFIG.apiBase || location.origin;
    try {
      const u = new URL(base, location.origin);
      u.protocol = u.protocol === 'https:' ? 'wss:' : 'ws:';
      u.pathname = (u.pathname.replace(/\/$/, '') + LOBBY_PATH).replace(/\/+/g, '/');
      u.search = '';
      const token = api.session.get()?.accessToken;
      if (token) u.searchParams.set('access_token', token);
      return u.toString();
    } catch {
      return null;
    }
  }

  function startSocket() {
    if (CONFIG.useMock || typeof WebSocket === 'undefined') {
      startPolling();
      return;
    }
    const url = wsUrl();
    if (!url) {
      startPolling();
      return;
    }
    try {
      socket = new WebSocket(url);
      status('connecting');
      socket.addEventListener('open', () => status('websocket'));
      socket.addEventListener('message', (ev) => {
        try {
          const data = JSON.parse(ev.data);
          onUpdate?.(data);
        } catch {
          onUpdate?.({ type: 'raw', data: ev.data });
        }
      });
      socket.addEventListener('close', () => {
        if (!closed) startPolling();
      });
      socket.addEventListener('error', () => {
        stopSocket();
        if (!closed) startPolling();
      });
    } catch {
      startPolling();
    }
  }

  /** Mock: synthesize ready-state ticks while watching. */
  function startMockPulse() {
    status('mock');
    pollOnce();
    timer = setInterval(pollOnce, intervalMs);
  }

  function start() {
    closed = false;
    if (CONFIG.useMock) startMockPulse();
    else startSocket();
  }

  function stop() {
    closed = true;
    stopPolling();
    stopSocket();
    status('stopped');
  }

  return { start, stop, getMode: () => mode, pollOnce };
}
