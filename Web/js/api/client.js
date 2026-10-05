import { ENDPOINTS, endpointUrl } from './endpoints.js';

export class ApiError extends Error {
  constructor(message, status = 0) { super(message); this.name = 'ApiError'; this.status = status; }
}
/** @param {{base:string,telemetryBase?:string,session:ReturnType<import('./session.js').createSession>,fetchImpl?:typeof fetch,onUnauthorized?:Function,retryDelay?:Function}} options */
export function createClient({ base, telemetryBase = '/telemetry', session, fetchImpl = globalThis.fetch, onUnauthorized = () => {}, retryDelay = (ms) => new Promise(r => setTimeout(r, ms)) }) {
  let refreshing = null;
  const expire = () => { session.clear(); onUnauthorized(); };
  async function transport(endpoint, options = {}) {
    const { params, query, body, signal, headers = {}, token } = options;
    const url = (endpoint.service === 'telemetry' ? telemetryBase : base).replace(/\/$/, '') + endpointUrl(endpoint, params, query);
    const requestHeaders = { Accept: endpoint.format === 'blob' ? 'image/png' : 'application/json', ...headers };
    if (body !== undefined) requestHeaders['Content-Type'] = 'application/json';
    if (token) requestHeaders.Authorization = `Bearer ${token}`;
    for (let attempt = 0; attempt < 3; attempt++) {
      signal?.throwIfAborted();
      const timeout = AbortSignal.timeout(12000);
      const requestSignal = signal ? AbortSignal.any([signal, timeout]) : timeout;
      let response;
      try {
        response = await fetchImpl(url, { method: endpoint.method, headers: requestHeaders, body: body === undefined ? undefined : JSON.stringify(body), signal: requestSignal, cache: 'no-store', credentials: 'same-origin' });
      } catch (error) {
        if (signal?.aborted) throw error;
        if (endpoint.method === 'GET' && attempt < 2) { await retryDelay(250 * 2 ** attempt); continue; }
        throw new ApiError(error.name === 'TimeoutError' ? 'Request timed out / İstek zaman aşımına uğradı.' : 'Connection unavailable / Sunucuya erişilemiyor.');
      }
      if (endpoint.method === 'GET' && [429, 502, 503, 504].includes(response.status) && attempt < 2) {
        const seconds = Number(response.headers.get('Retry-After'));
        await retryDelay(Number.isFinite(seconds) && seconds > 0 ? Math.min(seconds * 1000, 3000) : 250 * 2 ** attempt);
        continue;
      }
      if (!response.ok) {
        let detail;
        try { const problem = await response.json(); detail = problem.detail || problem.message || problem.title || problem.error?.message || (typeof problem.error === 'string' ? problem.error : null); } catch { /* do not reflect HTML errors */ }
        throw new ApiError(typeof detail === 'string' ? detail.slice(0, 500) : `HTTP ${response.status}`, response.status);
      }
      if (response.status === 204) return null;
      if (endpoint.format === 'blob') return response.blob();
      if (endpoint.format === 'text') return response.text();
      return response.json();
    }
  }
  async function accessToken() {
    const current = session.get();
    if (!current) throw new ApiError('Sign in required / Oturum açmalısınız.', 401);
    if (Date.parse(current.expiresAt) > Date.now() + 30000) return current.accessToken;
    if (!refreshing) {
      const revision = session.revision();
      refreshing = transport(ENDPOINTS.refresh, { body: { refreshToken: current.refreshToken } }).then(next => {
        if (revision !== session.revision()) throw new ApiError('Session changed / Oturum değişti.', 401);
        session.set(next); return next.accessToken;
      }).catch(error => { if (revision === session.revision()) expire(); throw error; }).finally(() => { refreshing = null; });
    }
    return refreshing;
  }
  /** All endpoints in endpoints.js are callable. Auth is only attached to protected main-API calls.
   * @param {keyof typeof ENDPOINTS} name
   * @param {{params?:Object,query?:Object,body?:Object,headers?:Object,signal?:AbortSignal}} options
   * @returns {Promise<any>}
   */
  async function call(name, options = {}) {
    const endpoint = ENDPOINTS[name];
    if (!endpoint) throw new TypeError(`Unknown endpoint: ${name}`);
    const revision = session.revision();
    try {
      const token = endpoint.auth ? await accessToken() : undefined;
      return await transport(endpoint, { ...options, token });
    } catch (error) {
      // A rejected access token ends the session. Only proactive expiration refreshes tokens.
      if (error.status === 401 && endpoint.auth && session.get() && revision <= session.revision()) expire();
      throw error;
    }
  }
  return { call, accessToken,
    /** @param {import('./types.js').LoginRequest} body @returns {Promise<import('./types.js').AuthResponse>} */
    async login(body) { const value = await call('login', { body }); session.set(value); return value; },
    /** @param {import('./types.js').RegisterRequest} body @returns {Promise<import('./types.js').AuthResponse>} */
    async register(body) { const value = await call('register', { body }); session.set(value); return value; },
    /** @returns {Promise<import('./types.js').PlayerDto>} */
    me: () => call('me'),
    /** @returns {Promise<import('./types.js').SquadDto>} */
    mySquad: () => call('mySquad'),
    async logout() { try { if (session.get()) await call('logout'); } finally { expire(); } },
  };
}
