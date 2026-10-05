# HAREKÂT Web Portalı (HTML / CSS / JS)

React yok — **vanilla HTML + CSS + ES modules**. Askeri koyu tema, vurgu rengi `#E30A17`.

## Çalıştırma

```bash
cd Web
npm run start
# tarayıcı: http://localhost:5173
```

Veya herhangi bir statik sunucu ile `Web/` klasörünü aç.

## Mock / API

Varsayılan: **mock açık** (`localStorage.harekat_mock` ≠ `false`).

```js
localStorage.setItem('harekat_mock', 'false');
localStorage.setItem('harekat_api', 'http://localhost:5080');
location.reload();
```

Endpoint'ler Cursor backend ile uyumlu: `/auth/register`, `/auth/login`, `/players/me`, `/squads`, `/matchmaking/queue`, `/leaderboards`, `/health`.

## Sayfalar

| Hash | İçerik |
|------|--------|
| `#/` | Ana sayfa |
| `#/leaderboards` | Sıralamalar |
| `#/profile` | Giriş/kayıt, rütbe, XP, kariyer |
| `#/squad` | Tim kur / katıl / kuyruk |
| `#/arsenal` | Silah kataloğu |
| `#/map` | Kuzgun Vadisi SVG pafta |
| `#/patches` | Yama notları |
| `#/news` | Haberler & rehberler |
| `#/season` | Sezon / başarımlar / nişanlar |
| `#/admin` | Sunucu filosu, şüpheliler |
| `#/player/:name` | Paylaşılabilir oyuncu kartı |
| `#/match/:id` | Maç detayı |

## Komutlar

```bash
npm run lint
npm run test
npm run build   # → Web/dist
```

## PWA

`manifest.webmanifest` + `sw.js` ile çevrimdışı önbellek.

## i18n

TR/EN — sağ üst dil düğmesi (`harekat_lang`).
