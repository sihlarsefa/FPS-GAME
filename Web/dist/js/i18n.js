import { CONFIG, setLang } from './config.js';

const dict = {
  tr: {
    tagline: 'Tim Tatbikatı',
    home_cta: 'Tim Kur',
    home_secondary: 'Sıralamalara Bak',
    mock_note: 'Mock modu açık — backend yokken sahte veri kullanılır.',
  },
  en: {
    tagline: 'Squad Exercise',
    home_cta: 'Create Squad',
    home_secondary: 'View Leaderboards',
    mock_note: 'Mock mode on — fake data while backend is offline.',
  },
};

export function t(key) {
  return dict[CONFIG.lang]?.[key] ?? dict.tr[key] ?? key;
}

export function applyI18n(root = document) {
  root.querySelectorAll('[data-i18n]').forEach((el) => {
    const key = el.getAttribute('data-i18n');
    el.textContent = t(key);
  });
}

export function toggleLang() {
  setLang(CONFIG.lang === 'tr' ? 'en' : 'tr');
  applyI18n();
  return CONFIG.lang;
}
