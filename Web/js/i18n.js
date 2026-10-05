import { CONFIG, setLang } from './config.js';

const cache = { tr: null, en: null };
let ready = null;

export async function loadI18n(lang = CONFIG.lang) {
  if (cache[lang]) return cache[lang];
  const res = await fetch(`i18n/${lang}.json`, { cache: 'no-cache' });
  if (!res.ok) throw new Error(`i18n load failed: ${lang}`);
  cache[lang] = await res.json();
  return cache[lang];
}

export async function ensureI18n() {
  if (!ready) {
    ready = Promise.all([loadI18n('tr'), loadI18n('en')]).catch(() => {
      // Offline / file:// fallback embedded subset
      cache.tr = cache.tr || { tagline: 'Tim Tatbikatı', home_cta: 'Tim Kur', home_secondary: 'Sıralamalara Bak', mock_note: 'Mock modu açık' };
      cache.en = cache.en || { tagline: 'Squad Exercise', home_cta: 'Create Squad', home_secondary: 'View Leaderboards', mock_note: 'Mock mode on' };
    });
  }
  await ready;
}

export function t(key) {
  return cache[CONFIG.lang]?.[key] ?? cache.tr?.[key] ?? key;
}

export function applyI18n(root = document) {
  root.querySelectorAll('[data-i18n]').forEach((el) => {
    const key = el.getAttribute('data-i18n');
    el.textContent = t(key);
  });
  root.querySelectorAll('[data-i18n-placeholder]').forEach((el) => {
    el.setAttribute('placeholder', t(el.getAttribute('data-i18n-placeholder')));
  });
  root.querySelectorAll('[data-i18n-aria]').forEach((el) => {
    el.setAttribute('aria-label', t(el.getAttribute('data-i18n-aria')));
  });
}

export async function toggleLang() {
  await ensureI18n();
  setLang(CONFIG.lang === 'tr' ? 'en' : 'tr');
  document.documentElement.lang = CONFIG.lang;
  applyI18n();
  return CONFIG.lang;
}

/** Sync dict injection for tests (no fetch). */
export function injectDict(lang, dict) {
  cache[lang] = dict;
}
