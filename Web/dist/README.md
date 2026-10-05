# HAREKÂT Web Portalı v2 (HTML / CSS / JS)

React yok — **vanilla HTML + CSS + ES modules**. JWT API katmanı, yönetici paneli, IIS `web.config`.

## Çalıştırma

```bash
cd Web
npm run start
# tarayıcı: http://localhost:5173
```

## Mock / API

Varsayılan: **mock açık** (`localStorage.harekat_mock` ≠ `false`).

```js
localStorage.setItem('harekat_mock', 'false');
localStorage.setItem('harekat_api', 'http://localhost:5080');
localStorage.setItem('harekat_telemetry', 'http://localhost:5081');
location.reload();
```

Typed client: `js/api/` (`endpoints.js`, `client.js`, `session.js`, `types.js`).
Sözleşme kontrolü: `npm run contracts` (Backend route + DTO snapshot).

## Sayfalar

| Hash | İçerik |
|------|--------|
| `#/` | Ana sayfa |
| `#/login` | Giriş / kayıt (form doğrulama) |
| `#/profile` | Rütbe, XP, kariyer, son maçlar, en iyi silah |
| `#/squad` | Tim kur / davet / hazır / WebSocket\|long-poll |
| `#/matchmaking` | Kuyruk, süre, ETA |
| `#/leaderboards` | Sezon filtresi + sayfalama |
| `#/match/:id` | Tim sıralaması, kill feed, SVG iniş/ölüm |
| `#/achievements` | Başarımlar |
| `#/admin` | Rol tabanlı yönetim paneli |
| `#/compare` | Oyuncu karşılaştırma |
| `#/archive` | Sezon arşivi |
| `#/news` | Markdown → statik haber |

## IIS (Windows Server)

1. IIS + **URL Rewrite** + **ARR** kur.
2. `Web/dist` (veya `Web/`) klasörünü site kökü yap.
3. `web.config` ile:
   - `/api/*` → `http://localhost:5080/{path}` (Harekat.Api)
   - `/telemetry/*` → `http://localhost:5081/{path}`
   - `/hubs/*` → lobby WebSocket proxy
   - SPA fallback → `index.html`
   - CSP, HSTS, sıkıştırma, statik önbellek
4. ARR’da “Enable proxy” ve WebSocket desteğini aç.
5. HTTPS bağlayıcı + HSTS (prod).
6. Application pool: **No Managed Code** (statik site).

## Komutlar

```bash
npm run lint
npm run test
npm run build      # → Web/dist (+ news JSON)
npm run contracts
npm run news
```

## i18n / tema / PWA

- TR/EN: `i18n/*.json`, sağ üst dil düğmesi
- Koyu/açık tema: `harekat_theme`
- PWA: `manifest.webmanifest` + `sw.js` (push iskeleti — tim daveti)

## Erişilebilirlik

Skip link, odak halkaları, ARIA progressbar/status, klavye ile gezinme, kontrast tokenları.
