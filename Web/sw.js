const CACHE = 'harekat-web-v2';
const ASSETS = [
  './',
  './index.html',
  './css/main.css',
  './js/app.js',
  './js/api.js',
  './js/config.js',
  './js/i18n.js',
  './js/mock-data.js',
  './js/pages.js',
  './js/lobby.js',
  './js/push.js',
  './js/util.js',
  './i18n/tr.json',
  './i18n/en.json',
  './manifest.webmanifest',
];

self.addEventListener('install', (e) => {
  e.waitUntil(caches.open(CACHE).then((c) => c.addAll(ASSETS)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (e) => {
  e.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))).then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (e) => {
  if (e.request.method !== 'GET') return;
  const url = new URL(e.request.url);
  if (url.pathname.startsWith('/api') || url.pathname.startsWith('/telemetry')) return;
  e.respondWith(
    caches.match(e.request).then((cached) => cached || fetch(e.request).then((res) => {
      const copy = res.clone();
      caches.open(CACHE).then((c) => c.put(e.request, copy));
      return res;
    }).catch(() => cached)),
  );
});

self.addEventListener('push', (e) => {
  let data = { title: 'HAREKÂT', body: 'Tim daveti' };
  try { data = { ...data, ...JSON.parse(e.data?.text() || '{}') }; } catch { /* ignore */ }
  e.waitUntil(self.registration.showNotification(data.title, {
    body: data.body,
    icon: './assets/icons/favicon.svg',
    data: data.url || './#/squad',
  }));
});

self.addEventListener('notificationclick', (e) => {
  e.notification.close();
  const target = e.notification.data || './#/squad';
  e.waitUntil(clients.openWindow(target));
});
