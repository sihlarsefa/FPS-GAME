import { escape, slug } from './markdown.mjs';

export const CATEGORY_TR = {
  Pistol: 'Tabanca',
  Smg: 'Hafif Makineli',
  AssaultRifle: 'Piyade Tüfeği',
  Sniper: 'Keskin Nişancı',
  Shotgun: 'Pompalı',
  Melee: 'Yakın Dövüş',
  Dmr: 'Nişancı Tüfeği',
  Lmg: 'Makineli Tüfek',
};

export const AMMO_TR = {
  Mm9: '9 mm',
  Mm556: '5.56 mm',
  Mm762: '7.62 mm',
  Gauge12: '12 Kalibre',
};

export const RANK_CATEGORY = index => {
  if (index >= 12) return 'Subay';
  if (index >= 6) return 'Astsubay';
  if (index >= 4) return 'Uzman Erbaş';
  return 'Er/Erbaş';
};

export const ROLE_TACTICS = {
  Leader: 'Komuta, rota ve emir tekerleği; orta zırh ile önde değil, görünürde liderlik.',
  Marksman: 'Yüksek zemin ve flanş; JNG ile tek vuruş, SAR 109T ile CQB yedek.',
  MachineGunner: 'Bastırma ateşi ve dar geçit kontrolü; ağır çanta, yavaş dönüş.',
  Medic: 'Tim gerisinde iyileştirme zinciri; sargı→ilk yardım→çanta önceliği.',
  Radioman: 'Topçu çağrısı ve iletişim; hayatta kalmak stratejik üstünlük.',
  Grenadier: 'Bina/köşe açma; parça bomba stoku ile CQB baskısı.',
  Rifleman: 'Esnek piyade; MPT-76 veya MPT-55 varyantı, kanat ve yağma.',
};

export const CONTROLS = [
  { group: 'Hareket', rows: [
    ['W A S D', 'İleri / sol / geri / sağ'],
    ['Sol Shift', 'Koş'],
    ['Space', 'Zıpla'],
    ['Sol Ctrl (basılı)', 'Eğil (tut)'],
    ['C', 'Eğil (aç/kapa)'],
    ['Z', 'Yüzüstü'],
    ['Q / E', 'Yan eğilme'],
  ]},
  { group: 'Çatışma', rows: [
    ['Sol tık', 'Ateş'],
    ['Sağ tık', 'Nişan (ADS)'],
    ['R', 'Şarjör değiştir'],
    ['1–4', 'Silah yuvası'],
    ['Fare tekerleği', 'Silah değiştir'],
    ['B', 'Ateş modu'],
    ['X', 'Silahı indir'],
    ['G', 'El bombası'],
    ['T', 'Sis bombası'],
    ['H', 'İyileş'],
    ['J', 'Boost'],
    ['F', 'Etkileşim / yağma'],
  ]},
  { group: 'Komuta ve destek', rows: [
    ['F1', 'Emir: Takip'],
    ['F2', 'Emir: Mevzi'],
    ['F3', 'Emir: Taarruz'],
    ['F4', 'Emir: Toplan'],
    ['V', 'Topçu çağrısı (telsizci / komutan)'],
  ]},
  { group: 'Arayüz', rows: [
    ['Tab / I', 'Envanter'],
    ['M', 'Harita'],
    ['Esc', 'Duraklat / menü'],
    ['Caps Lock', 'UI yardımcısı (basılı)'],
  ]},
];

export function navItems(base) {
  const b = base || '';
  return [
    { href: `${b}index.html`, label: 'Ana Sayfa' },
    { href: `${b}baslangic.html`, label: 'Yeni Başlayanlar' },
    { href: `${b}silahlar/index.html`, label: 'Silahlar' },
    { href: `${b}rutbeler/index.html`, label: 'Rütbeler' },
    { href: `${b}gorevler/index.html`, label: 'Görevler' },
    { href: `${b}lokasyonlar/index.html`, label: 'Lokasyonlar' },
    { href: `${b}kontroller.html`, label: 'Kontroller' },
    { href: `${b}rehber/index.html`, label: 'GDD' },
    { href: `${b}karsilastir.html`, label: 'Karşılaştır' },
    { href: `${b}harita.html`, label: 'Harita' },
    { href: `${b}yama-notlari.html`, label: 'Yama Notları' },
  ];
}

export function layout({ title, description, base = '', body, breadcrumbs = [], lang = 'tr', extraHead = '' }) {
  const crumbs = breadcrumbs.length
    ? `<nav class="crumbs" aria-label="Sayfa yolu"><ol>${breadcrumbs.map((c, i) => {
        const last = i === breadcrumbs.length - 1;
        return `<li>${last || !c.href ? `<span aria-current="page">${escape(c.label)}</span>` : `<a href="${escape(c.href)}">${escape(c.label)}</a>`}</li>`;
      }).join('')}</ol></nav>`
    : '';
  const nav = navItems(base).map(n => `<a href="${escape(n.href)}">${escape(n.label)}</a>`).join('');
  return `<!DOCTYPE html>
<html lang="${escape(lang)}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${escape(title)} · HAREKÂT Kılavuz</title>
<meta name="description" content="${escape(description || title)}">
<link rel="stylesheet" href="${escape(base)}assets/style.css">
${extraHead}
</head>
<body>
<a class="skip" href="#icerik">İçeriğe geç</a>
<header class="site-header">
  <div class="brand">
    <a href="${escape(base)}index.html" class="logo">HAREKÂT</a>
    <span class="tag">Oyuncu Kılavuzu</span>
  </div>
  <form class="search" role="search" data-search-form>
    <label class="sr-only" for="q">Ara</label>
    <input id="q" name="q" type="search" placeholder="Silah, rütbe, lokasyon…" autocomplete="off" data-search-input>
    <button type="submit">Ara</button>
    <div class="search-results" data-search-results hidden></div>
  </form>
  <button type="button" class="lang-toggle" data-lang-toggle aria-label="Dil değiştir">EN</button>
</header>
<nav class="site-nav" aria-label="Ana menü">${nav}</nav>
<main id="icerik" class="content">
${crumbs}
${body}
</main>
<footer class="site-footer">
  <p>HAREKÂT — harekât tatbikatı kılavuzu. Mavi / Kırmızı kuvvetler. Veriler GDD ve kataloglardan üretilir.</p>
  <p lang="en" class="en-note">Player field guide generated from design docs and Unity catalogs.</p>
</footer>
<script src="${escape(base)}assets/app.js" type="module"></script>
</body>
</html>`;
}

export function cardGrid(items) {
  return `<div class="card-grid">${items.map(i => `
    <a class="card" href="${escape(i.href)}">
      ${i.image ? `<img src="${escape(i.image)}" alt="" width="48" height="48" loading="lazy">` : ''}
      <strong>${escape(i.title)}</strong>
      ${i.meta ? `<span class="meta">${escape(i.meta)}</span>` : ''}
      ${i.text ? `<p>${escape(i.text)}</p>` : ''}
    </a>`).join('')}</div>`;
}

export function statsTable(rows) {
  return `<div class="table-scroll"><table><tbody>${rows.map(([k, v]) =>
    `<tr><th scope="row">${escape(k)}</th><td>${typeof v === 'string' && v.includes('<') ? v : escape(String(v))}</td></tr>`
  ).join('')}</tbody></table></div>`;
}

export function dataTable(headers, rows) {
  return `<div class="table-scroll"><table>
<thead><tr>${headers.map(h => `<th scope="col">${escape(h)}</th>`).join('')}</tr></thead>
<tbody>${rows.map(r => `<tr>${r.map(c => `<td>${c}</td>`).join('')}</tr>`).join('')}</tbody>
</table></div>`;
}

export function weaponSlug(id) {
  return slug(id);
}

export function fmtNum(n, digits = 2) {
  if (!Number.isFinite(n)) return '—';
  const v = Number(n);
  return Number.isInteger(v) ? String(v) : v.toFixed(digits).replace(/\.?0+$/, '');
}
