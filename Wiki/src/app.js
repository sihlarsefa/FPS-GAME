const basePath = () => {
  const scripts = [...document.querySelectorAll('script[src*="app.js"]')];
  const src = scripts.at(-1)?.getAttribute('src') || 'assets/app.js';
  return src.replace(/assets\/app\.js$/, '');
};

const BASE = basePath();

async function loadIndex() {
  const res = await fetch(`${BASE}assets/search-index.json`);
  if (!res.ok) throw new Error('search index missing');
  return res.json();
}

function normalize(s) {
  return String(s || '')
    .toLocaleLowerCase('tr')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

function search(index, query) {
  const q = normalize(query).trim();
  if (q.length < 2) return [];
  const terms = q.split(/\s+/).filter(Boolean);
  return index
    .map(item => {
      const hay = normalize([item.title, item.type, item.text, ...(item.keywords || [])].join(' '));
      let score = 0;
      for (const t of terms) {
        if (!hay.includes(t)) return null;
        score += hay.startsWith(t) ? 5 : 1;
        if (normalize(item.title).includes(t)) score += 3;
      }
      return { item, score };
    })
    .filter(Boolean)
    .sort((a, b) => b.score - a.score || a.item.title.localeCompare(b.item.title, 'tr'))
    .slice(0, 12)
    .map(x => x.item);
}

function resolveUrl(url) {
  if (!url) return '#';
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('/')) return `${BASE}${url.replace(/^\//, '')}`;
  return url;
}

function bindSearch(index) {
  const form = document.querySelector('[data-search-form]');
  const input = document.querySelector('[data-search-input]');
  const box = document.querySelector('[data-search-results]');
  if (!form || !input || !box) return;

  let active = -1;
  let current = [];

  const render = items => {
    current = items;
    active = -1;
    if (!items.length) {
      box.hidden = true;
      box.innerHTML = '';
      return;
    }
    box.hidden = false;
    box.innerHTML = items.map((it, i) =>
      `<a href="${resolveUrl(it.url)}" role="option" data-i="${i}"><span class="type">${it.type}</span>${it.title}</a>`
    ).join('');
  };

  const go = url => { window.location.href = resolveUrl(url); };

  input.addEventListener('input', () => render(search(index, input.value)));
  input.addEventListener('keydown', e => {
    if (box.hidden || !current.length) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      active = (active + 1) % current.length;
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      active = (active - 1 + current.length) % current.length;
    } else if (e.key === 'Enter' && active >= 0) {
      e.preventDefault();
      go(current[active].url);
      return;
    } else if (e.key === 'Escape') {
      box.hidden = true;
      return;
    } else return;
    [...box.querySelectorAll('a')].forEach((a, i) => a.setAttribute('aria-selected', i === active ? 'true' : 'false'));
  });

  form.addEventListener('submit', e => {
    e.preventDefault();
    const items = search(index, input.value);
    if (items[0]) go(items[0].url);
  });

  document.addEventListener('click', e => {
    if (!form.contains(e.target)) box.hidden = true;
  });
}

function bindLang() {
  const btn = document.querySelector('[data-lang-toggle]');
  if (!btn) return;
  const apply = lang => {
    document.body.classList.toggle('lang-en', lang === 'en');
    btn.textContent = lang === 'en' ? 'TR' : 'EN';
    localStorage.setItem('harekat-wiki-lang', lang);
  };
  apply(localStorage.getItem('harekat-wiki-lang') || 'tr');
  btn.addEventListener('click', () => {
    const next = document.body.classList.contains('lang-en') ? 'tr' : 'en';
    apply(next);
    if (next === 'en' && !location.pathname.includes('/en/')) {
      const en = `${BASE}en/index.html`;
      // soft hint only — stay on page; EN landing available via nav search
      btn.title = `English overview: ${en}`;
    }
  });
}

function bindCompare() {
  const dataEl = document.getElementById('weapon-data');
  const aSel = document.querySelector('[data-compare-a]');
  const bSel = document.querySelector('[data-compare-b]');
  const out = document.querySelector('[data-compare-table]');
  if (!dataEl || !aSel || !bSel || !out) return;

  const weapons = JSON.parse(dataEl.textContent);
  const params = new URLSearchParams(location.search);
  const fill = (sel, preferred) => {
    sel.innerHTML = weapons.map(w =>
      `<option value="${w.id}" ${w.id === preferred ? 'selected' : ''}>${w.name}</option>`
    ).join('');
  };
  fill(aSel, params.get('a') || weapons[0]?.id);
  fill(bSel, params.get('b') || weapons[1]?.id || weapons[0]?.id);

  const fields = [
    ['Sınıf', 'category', null],
    ['Hasar', 'damage', 'higher'],
    ['Şarjör', 'magazine', 'higher'],
    ['RPM', 'rpm', 'higher'],
    ['Doldurma (sn)', 'reload', 'lower'],
    ['Menzil (m)', 'range', 'higher'],
    ['Mermi', 'ammo', null],
    ['Kafa ×', 'hs', 'higher'],
    ['Düşüş aralığı', 'falloff', null],
    ['Min hasar faktörü', 'minFactor', 'higher'],
  ];

  const render = () => {
    const a = weapons.find(w => w.id === aSel.value);
    const b = weapons.find(w => w.id === bSel.value);
    if (!a || !b) return;
    const rows = fields.map(([label, key, prefer]) => {
      const av = a[key];
      const bv = b[key];
      let ac = '';
      let bc = '';
      if (prefer && typeof av === 'number' && typeof bv === 'number' && av !== bv) {
        const aWins = prefer === 'higher' ? av > bv : av < bv;
        ac = aWins ? 'compare-better' : 'compare-worse';
        bc = aWins ? 'compare-worse' : 'compare-better';
      }
      return `<tr><th scope="row">${label}</th><td class="${ac}">${av}</td><td class="${bc}">${bv}</td></tr>`;
    }).join('');
    out.innerHTML = `<div class="table-scroll"><table>
      <thead><tr><th>Özellik</th><th>${a.name}</th><th>${b.name}</th></tr></thead>
      <tbody>${rows}</tbody></table></div>
      <p><a href="silahlar/${a.id.toLowerCase()}.html">${a.name} sayfası</a> ·
      <a href="silahlar/${b.id.toLowerCase()}.html">${b.name} sayfası</a></p>`;
  };

  aSel.addEventListener('change', render);
  bSel.addEventListener('change', render);
  render();
}

function bindMapLayers() {
  const object = document.querySelector('[data-map-object]');
  const toolbar = document.querySelector('[data-map-toolbar]');
  if (!object || !toolbar) return;

  const apply = () => {
    const doc = object.contentDocument;
    if (!doc) return;
    const svg = doc.querySelector('svg');
    if (!svg) return;
    const checks = [...toolbar.querySelectorAll('[data-layer]')];
    for (const input of checks) {
      const layer = input.dataset.layer;
      const nodes = doc.querySelectorAll(`[data-layer="${layer}"], .layer-${layer}, #${layer}, #layer-${layer}`);
      // Heuristic: toggle groups whose id/class contains the keyword
      const fuzzy = doc.querySelectorAll(`g[id*="${layer}" i], g[class*="${layer}" i], text`);
      const targets = nodes.length ? nodes : (layer === 'labels' ? doc.querySelectorAll('text') : fuzzy);
      for (const n of targets) {
        if (layer === 'labels' && n.tagName?.toLowerCase() !== 'text' && !n.querySelector?.('text')) continue;
        n.style.display = input.checked ? '' : 'none';
      }
      if (layer === 'grid') {
        for (const n of doc.querySelectorAll('g[id*="grid" i], line[stroke-dasharray], .grid')) {
          n.style.display = input.checked ? '' : 'none';
        }
      }
      if (layer === 'roads') {
        for (const n of doc.querySelectorAll('g[id*="road" i], g[id*="yol" i], path[id*="road" i]')) {
          n.style.display = input.checked ? '' : 'none';
        }
      }
      if (layer === 'water') {
        for (const n of doc.querySelectorAll('g[id*="water" i], g[id*="su" i], g[id*="lake" i], g[id*="river" i]')) {
          n.style.display = input.checked ? '' : 'none';
        }
      }
    }
  };

  object.addEventListener('load', apply);
  toolbar.addEventListener('change', apply);
  // Same-origin object may already be loaded
  setTimeout(apply, 300);
}

async function main() {
  bindLang();
  bindCompare();
  bindMapLayers();
  try {
    const index = await loadIndex();
    bindSearch(index);
  } catch (err) {
    console.warn('Wiki search unavailable', err);
  }
}

main();
