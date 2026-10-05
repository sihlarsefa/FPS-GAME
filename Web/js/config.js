function defaultApiBase() {
  const stored = localStorage.getItem('harekat_api');
  if (stored) return stored;
  if (location.hostname === 'localhost' || location.hostname === '127.0.0.1') {
    return 'http://localhost:8080';
  }
  return ''; // same origin — nginx reverse proxy
}

export const CONFIG = {
  apiBase: defaultApiBase(),
  useMock: (localStorage.getItem('harekat_mock') ?? (location.hostname === 'localhost' ? 'true' : 'false')) !== 'false',
  lang: localStorage.getItem('harekat_lang') || 'tr',
};

export function setLang(lang) {
  CONFIG.lang = lang;
  localStorage.setItem('harekat_lang', lang);
}

export function setMock(useMock) {
  CONFIG.useMock = useMock;
  localStorage.setItem('harekat_mock', String(useMock));
}
