/** @param {unknown} s */
export function esc(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));
}

export function formatNumber(n, lang = 'tr') {
  return Number(n || 0).toLocaleString(lang === 'en' ? 'en-US' : 'tr-TR');
}

export function xpProgress(experience, rank) {
  const band = 1600;
  const base = Math.max(0, (rank || 0) * band);
  const next = base + band;
  const pct = Math.min(100, Math.round(((experience - base) / band) * 100));
  return { base, next, pct: Number.isFinite(pct) ? Math.max(0, pct) : 0 };
}

/** World coords (−512…512) → SVG 0…1000 (north = +Z up). */
export function worldToSvg(x, z, half = 512, size = 1000) {
  return [((x + half) / (half * 2)) * size, ((half - z) / (half * 2)) * size];
}

export function validateAuthForm({ username, email, password }, mode = 'login') {
  const errors = [];
  if (!username || String(username).trim().length < 3) errors.push('username');
  if (String(password || '').length < 6) errors.push('password');
  if (mode === 'register') {
    if (!email || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(String(email))) errors.push('email');
  }
  return errors;
}

export function parseHash() {
  const hash = location.hash.replace(/^#/, '') || '/';
  const clean = hash.startsWith('/') ? hash : `/${hash}`;
  const parts = clean.split('/').filter(Boolean);
  const query = {};
  const q = clean.includes('?') ? clean.slice(clean.indexOf('?') + 1) : '';
  new URLSearchParams(q).forEach((v, k) => { query[k] = v; });
  return { parts: parts.map((p) => p.split('?')[0]), query, path: '/' + (parts[0]?.split('?')[0] || '') };
}
