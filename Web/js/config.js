function storageGet(key) {
  try {
    if (typeof process !== 'undefined' && process.versions?.node) return null;
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function storageSet(key, value) {
  try {
    if (typeof process !== 'undefined' && process.versions?.node) return;
    localStorage.setItem(key, value);
  } catch { /* ignore */ }
}

function defaultApiBase() {
  const stored = storageGet('harekat_api');
  if (stored) return stored;
  try {
    if (typeof location !== 'undefined' && (location.hostname === 'localhost' || location.hostname === '127.0.0.1')) {
      return 'http://localhost:5080';
    }
  } catch { /* node */ }
  return '/api';
}

function defaultTelemetryBase() {
  const stored = storageGet('harekat_telemetry');
  if (stored) return stored;
  try {
    if (typeof location !== 'undefined' && (location.hostname === 'localhost' || location.hostname === '127.0.0.1')) {
      return 'http://localhost:5081';
    }
  } catch { /* node */ }
  return '/telemetry';
}

export const CONFIG = {
  apiBase: defaultApiBase(),
  telemetryBase: defaultTelemetryBase(),
  lobbyPath: '/hubs/lobby',
  useMock: (storageGet('harekat_mock') ?? 'true') !== 'false',
  lang: storageGet('harekat_lang') || 'tr',
  theme: storageGet('harekat_theme') || 'dark',
  pollMs: 4000,
};

export function setLang(lang) {
  CONFIG.lang = lang;
  storageSet('harekat_lang', lang);
}

export function setTheme(theme) {
  CONFIG.theme = theme === 'light' ? 'light' : 'dark';
  storageSet('harekat_theme', CONFIG.theme);
  try { document.documentElement.dataset.theme = CONFIG.theme; } catch { /* node */ }
}

export function setMock(useMock) {
  CONFIG.useMock = useMock;
  storageSet('harekat_mock', String(useMock));
}

export function applyTheme() {
  try { document.documentElement.dataset.theme = CONFIG.theme; } catch { /* node */ }
}
