#!/usr/bin/env node
import { mkdirSync, readFileSync, writeFileSync, cpSync, readdirSync, existsSync, rmSync } from 'node:fs';
import { dirname, join, relative, basename, extname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import {
  parseWeapons, parseRanks, parseItems, parseRoles, verifyBalance, parseCsv,
} from './lib/catalogs.mjs';
import { markdown, escape, slug } from './lib/markdown.mjs';
import {
  layout, cardGrid, statsTable, dataTable, CATEGORY_TR, AMMO_TR, RANK_CATEGORY,
  ROLE_TACTICS, CONTROLS, fmtNum, weaponSlug,
} from './lib/html.mjs';

const __dirname = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dirname, '..');
const DIST = join(__dirname, 'dist');
const OUT = DIST;

const paths = {
  weaponCs: join(ROOT, 'Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs'),
  rankCs: join(ROOT, 'Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs'),
  itemCs: join(ROOT, 'Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs'),
  loadoutCs: join(ROOT, 'Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs'),
  catalogJson: join(ROOT, 'Tools/BalanceCalc/out/catalog.json'),
  ttkCsv: join(ROOT, 'Tools/BalanceCalc/out/ttk.csv'),
  gdd: join(ROOT, 'Design/GDD'),
  locs: join(ROOT, 'Design/Maps/KuzgunVadisi/Lokasyonlar'),
  plans: join(ROOT, 'Design/Maps/KuzgunVadisi/YakinPlanlar'),
  pafta: join(ROOT, 'Design/Maps/KuzgunVadisi/kuzgun-vadisi-pafta.svg'),
  taktik: join(ROOT, 'Design/Maps/KuzgunVadisi/taktik-rehber.md'),
  ranksSvg: join(ROOT, 'Web/assets/ranks'),
  styleSrc: join(__dirname, 'src/style.css'),
  appSrc: join(__dirname, 'src/app.js'),
  webConfig: join(__dirname, 'web.config'),
};

function ensureDir(p) {
  mkdirSync(p, { recursive: true });
}

function write(rel, content) {
  const full = join(OUT, rel);
  ensureDir(dirname(full));
  writeFileSync(full, content);
  return rel;
}

function read(p) {
  return readFileSync(p, 'utf8');
}

function listMd(dir) {
  return readdirSync(dir).filter(f => f.endsWith('.md')).sort();
}

function extractWeaponTips(gddText) {
  const tips = new Map();
  const section = /###\s+([^\n]+)\n([\s\S]*?)(?=\n### |\n## |$)/g;
  let m;
  while ((m = section.exec(gddText))) {
    const name = m[1].trim();
    const body = m[2];
    const role = /\*\*Rol:\*\*\s*(.+)/.exec(body)?.[1]?.trim();
    const plus = [...body.matchAll(/\*\*\+\*\*\s*(.+)/g)].map(x => x[1].trim());
    const minus = [...body.matchAll(/\*\*[−\-]\*\*\s*(.+)/g)].map(x => x[1].trim());
    tips.set(name, { role, plus, minus });
  }
  return tips;
}

function resolveWikiLink(url, pageLinks) {
  if (!url) return null;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith('#')) return url;
  const clean = url.replace(/^\.\//, '').replace(/^\.\.\//g, '');
  const base = basename(clean, extname(clean));
  if (pageLinks.has(clean)) return pageLinks.get(clean);
  if (pageLinks.has(base)) return pageLinks.get(base);
  if (pageLinks.has(slug(base))) return pageLinks.get(slug(base));
  if (/\.(svg|png|jpg|jpeg|webp|gif)$/i.test(url)) {
    const name = basename(url);
    if (pageLinks.has(name)) return pageLinks.get(name);
  }
  return null;
}

function relHref(fromRel, toRel) {
  if (!toRel || /^https?:\/\//i.test(toRel) || toRel.startsWith('#')) return toRel;
  const target = toRel.replace(/^\//, '');
  const depth = fromRel.split('/').length - 1;
  return `${depth ? '../'.repeat(depth) : ''}${target}`;
}

function makeLinker(pageLinks, fromRel) {
  return url => {
    const resolved = resolveWikiLink(url, pageLinks);
    if (!resolved) return null;
    if (/^https?:\/\//i.test(resolved) || resolved.startsWith('#')) return resolved;
    return relHref(fromRel, resolved);
  };
}

function ttkForWeapon(rows, weaponId) {
  return rows.filter(r =>
    r.weapon_id === weaponId
    && r.zone === 'body'
    && r.armor_level === '0'
    && r.helmet_level === '0');
}

function buildSearchIndex(entries) {
  return entries.map(e => ({
    title: e.title,
    url: e.url,
    type: e.type,
    text: e.text.slice(0, 400),
    keywords: e.keywords || [],
  }));
}

function patchNotes() {
  return [
    {
      version: '0.2.0-faz2',
      date: '2026-10-05',
      items: [
        'Oyuncu kılavuzu (Wiki) statik site: silah, rütbe, lokasyon ve GDD sayfaları.',
        'BalanceCalc TTK tabloları silah sayfalarına gömüldü.',
        'İstemci araması, silah karşılaştırma ve etkileşimli pafta katmanları.',
      ],
    },
    {
      version: '0.1.0-faz1',
      date: '2026-09-01',
      items: [
        'GDD 21 bölüm, denge hesaplayıcı ve Kuzgun Vadisi paftaları yayımlandı.',
        'Web portalı v1 ve yerelleştirme tabloları.',
      ],
    },
  ];
}

export async function build() {
  if (existsSync(OUT)) rmSync(OUT, { recursive: true, force: true });
  ensureDir(OUT);

  const weapons = parseWeapons(read(paths.weaponCs));
  const ranks = parseRanks(read(paths.rankCs));
  const items = parseItems(read(paths.itemCs));
  const roles = parseRoles(read(paths.loadoutCs));
  const snapshot = JSON.parse(read(paths.catalogJson));
  verifyBalance(weapons, snapshot);
  const ttkRows = parseCsv(read(paths.ttkCsv));
  const tips = extractWeaponTips(read(join(paths.gdd, '05-silahlar.md')));
  const itemById = Object.fromEntries(items.map(i => [i.id, i]));
  const weaponById = Object.fromEntries(weapons.map(w => [w.weapon_id, w]));

  const pageLinks = new Map();
  const search = [];
  const declaredPages = [];

  const register = (rel, title, type, text, keywords = []) => {
    const norm = rel.replace(/\\/g, '/');
    declaredPages.push(norm);
    pageLinks.set(basename(rel, '.html'), norm);
    pageLinks.set(norm, norm);
    search.push({ title, url: `/${norm}`, type, text: text || title, keywords });
  };

  // Assets
  ensureDir(join(OUT, 'assets/maps'));
  ensureDir(join(OUT, 'assets/ranks'));
  ensureDir(join(OUT, 'assets/plans'));
  writeFileSync(join(OUT, 'assets/style.css'), read(paths.styleSrc));
  writeFileSync(join(OUT, 'assets/app.js'), read(paths.appSrc));
  if (existsSync(paths.webConfig)) writeFileSync(join(OUT, 'web.config'), read(paths.webConfig));
  if (existsSync(paths.pafta)) {
    cpSync(paths.pafta, join(OUT, 'assets/maps/kuzgun-vadisi-pafta.svg'));
    pageLinks.set('kuzgun-vadisi-pafta.svg', 'assets/maps/kuzgun-vadisi-pafta.svg');
  }
  for (const f of readdirSync(paths.plans).filter(x => x.endsWith('.svg'))) {
    cpSync(join(paths.plans, f), join(OUT, 'assets/plans', f));
    pageLinks.set(f, `assets/plans/${f}`);
  }
  if (existsSync(paths.ranksSvg)) {
    for (const f of readdirSync(paths.ranksSvg).filter(x => x.endsWith('.svg'))) {
      cpSync(join(paths.ranksSvg, f), join(OUT, 'assets/ranks', f));
      pageLinks.set(f, `assets/ranks/${f}`);
    }
  }

  // Pre-register known pages for link resolution
  for (const w of weapons) pageLinks.set(w.weapon_id, `silahlar/${weaponSlug(w.weapon_id)}.html`);
  for (const r of ranks) pageLinks.set(r.id, `rutbeler/${r.id}.html`);
  for (const role of roles) pageLinks.set(slug(role.id), `gorevler/${slug(role.id)}.html`);
  for (const f of listMd(paths.locs).filter(x => x !== 'README.md')) {
    const id = basename(f, '.md');
    pageLinks.set(id, `lokasyonlar/${id}.html`);
  }
  for (const f of listMd(paths.gdd).filter(x => x !== 'README.md')) {
    const id = basename(f, '.md');
    pageLinks.set(id, `rehber/${id}.html`);
    pageLinks.set(`${id}.md`, `rehber/${id}.html`);
  }

  // —— Home
  const homeBody = `
<section class="hero">
  <p class="eyebrow">Harekât tatbikatı · Mavi / Kırmızı</p>
  <h1>Oyuncu Kılavuzu</h1>
  <p class="lede">Silah istatistikleri, rütbe nişanları, tim görevleri, Kuzgun Vadisi lokasyonları ve GDD bölümleri — kataloglardan üretilmiş statik saha rehberi.</p>
  <p lang="en" class="lede en-note">Field guide generated from GDD and Unity catalogs. Turkish primary, English chrome available.</p>
</section>
<section>
  <h2>Hızlı erişim</h2>
  ${cardGrid([
    { href: 'baslangic.html', title: 'Yeni Başlayanlar', text: 'İlk 10 dakika, eğitim ve temel ipuçları' },
    { href: 'silahlar/index.html', title: 'Silahlar', meta: `${weapons.length} silah`, text: 'Hasar, RPM, TTK ve kullanım notları' },
    { href: 'rutbeler/index.html', title: 'Rütbeler', meta: `${ranks.length} rütbe`, text: 'Nişan, XP eşiği, komuta zinciri' },
    { href: 'gorevler/index.html', title: 'Tim Görevleri', meta: `${roles.length} rol`, text: 'Başlangıç teçhizatı ve taktik rol' },
    { href: 'lokasyonlar/index.html', title: 'Lokasyonlar', text: 'Pafta kesiti ve taktik notlar' },
    { href: 'kontroller.html', title: 'Kontroller', text: 'Tuş haritası ve emirler' },
    { href: 'harita.html', title: 'Kuzgun Vadisi', text: 'Etkileşimli pafta' },
    { href: 'karsilastir.html', title: 'Silah Karşılaştır', text: 'İki silah yan yana' },
  ])}
</section>
<section>
  <h2>Sistem özeti</h2>
  ${statsTable([
    ['Tim boyutu', '10'],
    ['Harita', 'Kuzgun Vadisi (1024×1024 m)'],
    ['İntikal', 'T-70 helikopter veya Kirpi ZPT'],
    ['Zone fazları', '7'],
    ['Topçu bekleme', '150 sn'],
  ])}
</section>`;
  write('index.html', layout({
    title: 'Ana Sayfa',
    description: 'HAREKÂT oyuncu kılavuzu',
    body: homeBody,
    breadcrumbs: [{ label: 'Ana Sayfa' }],
  }));
  register('index.html', 'Ana Sayfa', 'sayfa', 'HAREKÂT oyuncu kılavuzu ana sayfa');

  // —— Beginners
  const tutorialMd = read(join(paths.gdd, '21-ilk-10-dk-tutorial.md'));
  const beginnersBody = `
<h1>Yeni Başlayanlar</h1>
<p class="lede">İlk maça çıkmadan önce hareket, yağma, emir ve zone kavramlarını öğren.</p>
<div class="prose">${markdown(tutorialMd, makeLinker(pageLinks, 'baslangic.html'))}</div>
<section>
  <h2>İlk adımlar</h2>
  <ol class="checklist">
    <li><a href="kontroller.html">Tuş haritasını</a> gözden geçir (WASD, ADS, F1–F4).</li>
    <li><a href="gorevler/index.html">Görevini</a> ve başlangıç teçhizatını öğren.</li>
    <li><a href="lokasyonlar/kuzgun-koyu.html">Kuzgun Köyü</a> gibi yumuşak iniş noktalarını tercih et.</li>
    <li><a href="rehber/04-intikal.html">T-70 / Kirpi</a> farkını bil.</li>
    <li><a href="rehber/07-zone-fazlari.html">Harekât alanı fazlarını</a> takip et.</li>
  </ol>
</section>`;
  write('baslangic.html', layout({
    title: 'Yeni Başlayanlar',
    description: 'İlk 10 dakika ve eğitim rehberi',
    body: beginnersBody,
    breadcrumbs: [{ href: 'index.html', label: 'Ana Sayfa' }, { label: 'Yeni Başlayanlar' }],
  }));
  register('baslangic.html', 'Yeni Başlayanlar', 'rehber', tutorialMd);

  // —— Controls
  const controlsBody = `
<h1>Kontroller</h1>
<p class="lede">Kaynak: <code>UnityInputReader</code> ve <code>SquadCommandInput</code>.</p>
${CONTROLS.map(g => `
<section>
  <h2>${escape(g.group)}</h2>
  ${dataTable(['Tuş', 'İşlev'], g.rows.map(([k, v]) => [`<kbd>${escape(k)}</kbd>`, escape(v)]))}
</section>`).join('')}
<section>
  <h2>Komuta zinciri</h2>
  <p>Komutan ölünce komuta hayattaki en kıdemli üyeye geçer. Ayrıntı: <a href="rehber/03-rutbeler-komuta.html">Rütbeler ve komuta</a>.</p>
</section>
<section>
  <h2>İntikal</h2>
  <ul>
    <li><strong>T-70:</strong> havadan iniş, geniş sektör seçimi — <a href="rehber/04-intikal.html">İntikal</a>.</li>
    <li><strong>Kirpi:</strong> yol ağı üzerinden karadan yaklaşım.</li>
  </ul>
</section>
<section>
  <h2>Topçu desteği</h2>
  <p>V tuşu ile çağrı (telsizci / yetkili). Bekleme 150 sn. <a href="rehber/08-topcu-destegi.html">Topçu rehberi</a>.</p>
</section>`;
  write('kontroller.html', layout({
    title: 'Kontroller',
    description: 'Tuş haritası ve komuta girdileri',
    body: controlsBody,
    breadcrumbs: [{ href: 'index.html', label: 'Ana Sayfa' }, { label: 'Kontroller' }],
  }));
  register('kontroller.html', 'Kontroller', 'rehber', CONTROLS.flatMap(g => g.rows.map(r => r.join(' '))).join(' '));

  // —— Weapons
  const weaponIndexRows = weapons.map(w => [
    `<a href="${weaponSlug(w.weapon_id)}.html">${escape(w.display_name || w.weapon_id)}</a>`,
    escape(CATEGORY_TR[w.category] || w.category),
    fmtNum(w.damage),
    String(w.magazine_size),
    fmtNum(w.rpm, 1),
    escape(AMMO_TR[w.ammo_type] || w.ammo_type || '—'),
  ]);
  write('silahlar/index.html', layout({
    title: 'Silahlar',
    description: 'Silah kataloğu',
    base: '../',
    body: `<h1>Silahlar</h1>
<p class="lede">${weapons.length} silah — değerler <code>WeaponCatalog</code> kaynaklı; TTK <code>BalanceCalc</code> çıktısından.</p>
${dataTable(['Silah', 'Sınıf', 'Hasar', 'Şarjör', 'RPM', 'Mermi'], weaponIndexRows)}
<p><a href="../karsilastir.html">İki silahı karşılaştır →</a></p>`,
    breadcrumbs: [{ href: '../index.html', label: 'Ana Sayfa' }, { label: 'Silahlar' }],
  }));
  register('silahlar/index.html', 'Silahlar', 'indeks', 'silah kataloğu');

  for (const w of weapons) {
    const tip = tips.get(w.display_name) || tips.get(w.weapon_id) || {};
    const ttk = ttkForWeapon(ttkRows, w.weapon_id);
    const ttkTable = ttk.length
      ? dataTable(
        ['Mesafe (m)', 'Vuruş', 'TTK (sn)', 'Hasar/atış', 'DPS'],
        ttk.map(r => [
          escape(r.distance_m),
          escape(r.shots_to_kill),
          escape(r.ttk_seconds),
          escape(r.damage_per_shot),
          escape(r.dps_effective),
        ]),
      )
      : '<p>TTK verisi yok.</p>';
    const body = `
<h1>${escape(w.display_name || w.weapon_id)}</h1>
<p class="meta">${escape(CATEGORY_TR[w.category] || w.category)} · <code>${escape(w.weapon_id)}</code></p>
${statsTable([
  ['Hasar', fmtNum(w.damage)],
  ['Şarjör', w.magazine_size],
  ['RPM', fmtNum(w.rpm, 1)],
  ['Ateş aralığı', `${fmtNum(w.fire_interval_seconds, 3)} sn`],
  ['Doldurma', `${fmtNum(w.reload_duration_seconds, 2)} sn`],
  ['Menzil', `${fmtNum(w.range_m, 0)} m`],
  ['Mermi', AMMO_TR[w.ammo_type] || w.ammo_type || '—'],
  ['Kafa çarpanı', fmtNum(w.headshot_multiplier, 2)],
  ['Menzil düşüşü', `${fmtNum(w.falloff_start, 0)} → ${fmtNum(w.falloff_end, 0)} m`],
  ['Min hasar faktörü', fmtNum(w.min_damage_factor, 2)],
  ['Saçma', w.pellet_count > 1 ? `${w.pellet_count} pellet` : '—'],
])}
<section>
  <h2>Öldürme süresi (zırhsız gövde)</h2>
  <p>BalanceCalc <code>ttk.csv</code> — armor 0, helmet 0, zone=body.</p>
  ${ttkTable}
</section>
<section>
  <h2>Kullanım ipuçları</h2>
  ${tip.role ? `<p><strong>Rol:</strong> ${escape(tip.role)}</p>` : '<p>GDD ipucu eşleşmedi; sınıf rolüne göre kullan.</p>'}
  ${tip.plus?.length ? `<ul class="plus">${tip.plus.map(x => `<li>${escape(x)}</li>`).join('')}</ul>` : ''}
  ${tip.minus?.length ? `<ul class="minus">${tip.minus.map(x => `<li>${escape(x)}</li>`).join('')}</ul>` : ''}
</section>
<p><a href="index.html">← Tüm silahlar</a> · <a href="../karsilastir.html?a=${escape(w.weapon_id)}">Karşılaştır</a></p>`;
    const rel = `silahlar/${weaponSlug(w.weapon_id)}.html`;
    write(rel, layout({
      title: w.display_name || w.weapon_id,
      description: `${w.display_name} istatistikleri`,
      base: '../',
      body,
      breadcrumbs: [
        { href: '../index.html', label: 'Ana Sayfa' },
        { href: 'index.html', label: 'Silahlar' },
        { label: w.display_name || w.weapon_id },
      ],
    }));
    register(rel, w.display_name || w.weapon_id, 'silah', `${w.display_name} ${w.category} ${w.ammo_type}`, [w.weapon_id, CATEGORY_TR[w.category]]);
  }

  // —— Ranks
  write('rutbeler/index.html', layout({
    title: 'Rütbeler',
    description: 'Kariyer rütbeleri',
    base: '../',
    body: `<h1>Rütbeler</h1>
<p class="lede">Kariyer XP eşikleri — <code>RankCatalog</code>.</p>
${cardGrid(ranks.map(r => ({
  href: `${r.id}.html`,
  title: r.name,
  meta: `${r.short} · ${RANK_CATEGORY(r.index)} · ${r.xp.toLocaleString('tr-TR')} XP`,
  image: `../assets/ranks/rank-${r.index}.svg`,
})))}`,
    breadcrumbs: [{ href: '../index.html', label: 'Ana Sayfa' }, { label: 'Rütbeler' }],
  }));
  register('rutbeler/index.html', 'Rütbeler', 'indeks', 'rütbe listesi');

  for (const r of ranks) {
    const next = ranks[r.index + 1];
    const prev = ranks[r.index - 1];
    const body = `
<div class="rank-hero">
  <img src="../assets/ranks/rank-${r.index}.svg" alt="" width="96" height="96">
  <div>
    <h1>${escape(r.name)}</h1>
    <p class="meta">${escape(r.short)} · ${escape(RANK_CATEGORY(r.index))}</p>
  </div>
</div>
${statsTable([
  ['XP eşiği', r.xp.toLocaleString('tr-TR')],
  ['Sıra', `${r.index + 1} / ${ranks.length}`],
  ['Önceki', prev ? `<a href="${prev.id}.html">${escape(prev.name)}</a>` : '—'],
  ['Sonraki', next ? `<a href="${next.id}.html">${escape(next.name)}</a>` : 'En yüksek'],
])}
<section>
  <h2>Açıklama</h2>
  <p>${escape(r.name)} (${escape(r.short)}), ${escape(RANK_CATEGORY(r.index))} sınıfında kariyer rütbesidir.
  ${r.xp === 0 ? 'Başlangıç rütbesidir.' : `Bu rütbeye ulaşmak için toplam ${r.xp.toLocaleString('tr-TR')} tecrübe gerekir.`}
  Maç içi slot rütbesi kariyerden bağımsızdır; komuta kıdem sırasına göre devredilir.</p>
</section>
<p><a href="index.html">← Tüm rütbeler</a> · <a href="../rehber/03-rutbeler-komuta.html">Komuta zinciri</a></p>`;
    const rel = `rutbeler/${r.id}.html`;
    write(rel, layout({
      title: r.name,
      description: `${r.name} rütbesi`,
      base: '../',
      body,
      breadcrumbs: [
        { href: '../index.html', label: 'Ana Sayfa' },
        { href: 'index.html', label: 'Rütbeler' },
        { label: r.name },
      ],
    }));
    register(rel, r.name, 'rütbe', `${r.name} ${r.short} ${RANK_CATEGORY(r.index)} ${r.xp}`, [r.short]);
  }

  // —— Roles
  write('gorevler/index.html', layout({
    title: 'Tim Görevleri',
    description: 'Başlangıç teçhizatı',
    base: '../',
    body: `<h1>Tim Görevleri</h1>
<p class="lede">10 kişilik tim dağılımı — <code>LoadoutCatalog</code>.</p>
${cardGrid(roles.map(role => ({
  href: `${slug(role.id)}.html`,
  title: role.name,
  meta: role.id,
  text: ROLE_TACTICS[role.id] || '',
})))}`,
    breadcrumbs: [{ href: '../index.html', label: 'Ana Sayfa' }, { label: 'Görevler' }],
  }));
  register('gorevler/index.html', 'Tim Görevleri', 'indeks', 'görev teçhizat');

  for (const role of roles) {
    const weaponLinks = role.weapons.map(id => {
      const w = weaponById[id];
      return w
        ? `<a href="../silahlar/${weaponSlug(id)}.html">${escape(w.display_name)}</a>`
        : escape(id);
    }).join(', ');
    const itemRows = role.items.map(it => {
      const name = itemById[it.id]?.name || it.id;
      const alt = it.alternative ? ` / ${itemById[it.alternative]?.name || it.alternative}` : '';
      return [escape(name + alt), String(it.count)];
    });
    const body = `
<h1>${escape(role.name)}</h1>
<p class="meta"><code>${escape(role.id)}</code></p>
<p>${escape(ROLE_TACTICS[role.id] || '')}</p>
${statsTable([
  ['Silahlar', weaponLinks],
  ['Yelek', `Seviye ${role.vest}`],
  ['Miğfer', `Seviye ${role.helmet}`],
  ['Çanta', `Seviye ${role.backpack}`],
])}
<section>
  <h2>Başlangıç eşyaları</h2>
  ${dataTable(['Eşya', 'Adet'], itemRows)}
</section>
<p><a href="index.html">← Tüm görevler</a> · <a href="../rehber/02-tim-yapisi-roller.html">Tim yapısı</a></p>`;
    const rel = `gorevler/${slug(role.id)}.html`;
    write(rel, layout({
      title: role.name,
      description: `${role.name} teçhizatı`,
      base: '../',
      body,
      breadcrumbs: [
        { href: '../index.html', label: 'Ana Sayfa' },
        { href: 'index.html', label: 'Görevler' },
        { label: role.name },
      ],
    }));
    register(rel, role.name, 'görev', `${role.name} ${role.weapons.join(' ')}`, [role.id]);
  }

  // —— Locations
  const locFiles = listMd(paths.locs).filter(f => f !== 'README.md');
  const locCards = [];
  for (const f of locFiles) {
    const id = basename(f, '.md');
    const rel = `lokasyonlar/${id}.html`;
    const md = read(join(paths.locs, f));
    const title = /^#\s+(.+)$/m.exec(md)?.[1]?.trim() || id;
    const grid = /\|\s*Grid\s*\|\s*\*\*([^*]+)\*\*/.exec(md)?.[1]?.trim();
    const plan = `../assets/plans/${id}-yakin-plan.svg`;
    const hasPlan = existsSync(join(paths.plans, `${id}-yakin-plan.svg`));
    const htmlMd = markdown(md, url => {
      if (url.includes('yakin-plan')) return plan;
      return makeLinker(pageLinks, rel)(url);
    });
    const body = `
<h1>${escape(title)}</h1>
${hasPlan ? `<figure class="plan"><img src="${plan}" alt="${escape(title)} yakın plan" loading="lazy"><figcaption>Yakın plan paftası</figcaption></figure>` : ''}
<div class="prose">${htmlMd}</div>
<p><a href="index.html">← Lokasyonlar</a> · <a href="../harita.html">Etkileşimli harita</a></p>`;
    write(rel, layout({
      title,
      description: `${title} taktik notları`,
      base: '../',
      body,
      breadcrumbs: [
        { href: '../index.html', label: 'Ana Sayfa' },
        { href: 'index.html', label: 'Lokasyonlar' },
        { label: title },
      ],
    }));
    register(rel, title, 'lokasyon', md, [grid, id].filter(Boolean));
    locCards.push({ href: `${id}.html`, title, meta: grid || id, text: 'Taktik notlar ve pafta' });
  }
  const taktikMd = existsSync(paths.taktik) ? read(paths.taktik) : '';
  write('lokasyonlar/index.html', layout({
    title: 'Lokasyonlar',
    description: 'Kuzgun Vadisi lokasyonları',
    base: '../',
    body: `<h1>Kuzgun Vadisi — Lokasyonlar</h1>
<p class="lede">${locCards.length} nokta. Genel pafta: <a href="../harita.html">etkileşimli harita</a>.</p>
${cardGrid(locCards)}
${taktikMd ? `<section class="prose"><h2>Taktik rehber</h2>${markdown(taktikMd, makeLinker(pageLinks, 'lokasyonlar/index.html'))}</section>` : ''}`,
    breadcrumbs: [{ href: '../index.html', label: 'Ana Sayfa' }, { label: 'Lokasyonlar' }],
  }));
  register('lokasyonlar/index.html', 'Lokasyonlar', 'indeks', 'Kuzgun Vadisi lokasyonları');

  // —— GDD guides
  const gddFiles = listMd(paths.gdd).filter(f => f !== 'README.md');
  const gddCards = [];
  for (const f of gddFiles) {
    const id = basename(f, '.md');
    const rel = `rehber/${id}.html`;
    const md = read(join(paths.gdd, f));
    const title = /^#\s+(.+)$/m.exec(md)?.[1]?.trim() || id;
    const body = `<h1>${escape(title)}</h1><div class="prose">${markdown(md, makeLinker(pageLinks, rel))}</div>
<p><a href="index.html">← GDD dizini</a></p>`;
    write(rel, layout({
      title,
      description: title,
      base: '../',
      body,
      breadcrumbs: [
        { href: '../index.html', label: 'Ana Sayfa' },
        { href: 'index.html', label: 'GDD' },
        { label: title },
      ],
    }));
    register(rel, title, 'gdd', md, [id]);
    gddCards.push({ href: `${id}.html`, title, meta: id });
  }
  write('rehber/index.html', layout({
    title: 'GDD',
    description: 'Oyun tasarım belgeleri',
    base: '../',
    body: `<h1>Oyun Tasarım Dokümanı</h1>
<p class="lede">Design/GDD kaynaklarından üretilmiş oyuncu sürümü.</p>
${cardGrid(gddCards)}`,
    breadcrumbs: [{ href: '../index.html', label: 'Ana Sayfa' }, { label: 'GDD' }],
  }));
  register('rehber/index.html', 'GDD', 'indeks', 'oyun tasarım belgeleri');

  // —— Compare (UZATMA)
  const compareBody = `
<h1>Silah Karşılaştırma</h1>
<p class="lede">İki silahı yan yana seç. Veriler derleme zamanında gömüldü.</p>
<div class="compare-controls">
  <label>Silah A <select data-compare-a></select></label>
  <label>Silah B <select data-compare-b></select></label>
</div>
<div data-compare-table></div>
<script type="application/json" id="weapon-data">${JSON.stringify(weapons.map(w => ({
    id: w.weapon_id,
    name: w.display_name,
    category: CATEGORY_TR[w.category] || w.category,
    damage: w.damage,
    magazine: w.magazine_size,
    rpm: Math.round(w.rpm * 100) / 100,
    reload: w.reload_duration_seconds,
    range: w.range_m,
    ammo: AMMO_TR[w.ammo_type] || w.ammo_type,
    hs: w.headshot_multiplier,
    falloff: `${w.falloff_start}–${w.falloff_end}`,
    minFactor: w.min_damage_factor,
  })))}</script>`;
  write('karsilastir.html', layout({
    title: 'Silah Karşılaştır',
    description: 'İki silah karşılaştırması',
    body: compareBody,
    breadcrumbs: [{ href: 'index.html', label: 'Ana Sayfa' }, { label: 'Karşılaştır' }],
  }));
  register('karsilastir.html', 'Silah Karşılaştır', 'araç', 'silah karşılaştırma');

  // —— Interactive map (UZATMA)
  const mapBody = `
<h1>Kuzgun Vadisi Haritası</h1>
<p class="lede">Pafta SVG — katmanları aç/kapa. Lokasyon sayfalarına gitmek için listeden seç.</p>
<div class="map-toolbar" data-map-toolbar>
  <label><input type="checkbox" data-layer="grid" checked> Grid</label>
  <label><input type="checkbox" data-layer="labels" checked> Etiketler</label>
  <label><input type="checkbox" data-layer="roads" checked> Yollar</label>
  <label><input type="checkbox" data-layer="water" checked> Su</label>
</div>
<figure class="map-frame">
  <object data="assets/maps/kuzgun-vadisi-pafta.svg" type="image/svg+xml" aria-label="Kuzgun Vadisi paftası" data-map-object></object>
</figure>
<section>
  <h2>Lokasyonlar</h2>
  <ul class="link-list">${locCards.map(c => `<li><a href="lokasyonlar/${escape(c.href)}">${escape(c.title)}</a>${c.meta ? ` <span class="meta">(${escape(c.meta)})</span>` : ''}</li>`).join('')}</ul>
</section>`;
  write('harita.html', layout({
    title: 'Kuzgun Vadisi Haritası',
    description: 'Etkileşimli pafta',
    body: mapBody,
    breadcrumbs: [{ href: 'index.html', label: 'Ana Sayfa' }, { label: 'Harita' }],
  }));
  register('harita.html', 'Kuzgun Vadisi Haritası', 'harita', 'pafta grid yollar su lokasyon');

  // —— Patch notes (UZATMA)
  const patches = patchNotes();
  const patchBody = `
<h1>Yama Notları</h1>
<p class="lede">Saha kılavuzu ve tasarım sürüm özeti.</p>
${patches.map(p => `
<article class="patch">
  <h2>${escape(p.version)} <span class="meta">${escape(p.date)}</span></h2>
  <ul>${p.items.map(i => `<li>${escape(i)}</li>`).join('')}</ul>
</article>`).join('')}`;
  write('yama-notlari.html', layout({
    title: 'Yama Notları',
    description: 'Yama notu arşivi',
    body: patchBody,
    breadcrumbs: [{ href: 'index.html', label: 'Ana Sayfa' }, { label: 'Yama Notları' }],
  }));
  register('yama-notlari.html', 'Yama Notları', 'arsiv', patches.flatMap(p => p.items).join(' '));

  // —— Search index + build info
  const index = buildSearchIndex(search);
  write('assets/search-index.json', JSON.stringify(index, null, 2));
  write('assets/build-info.json', JSON.stringify({
    builtAt: new Date().toISOString(),
    weapons: weapons.length,
    ranks: ranks.length,
    roles: roles.length,
    locations: locCards.length,
    gdd: gddFiles.length,
    pages: declaredPages.length,
  }, null, 2));
  write('assets/pages.json', JSON.stringify(declaredPages.sort(), null, 2));

  // English landing stub (UZATMA TR/EN)
  write('en/index.html', layout({
    title: 'Player Guide',
    description: 'HAREKÂT field guide',
    lang: 'en',
    base: '../',
    body: `<h1>HAREKÂT Field Guide</h1>
<p>Primary content is Turkish. Use the search box and navigation to browse weapons, ranks, roles, and Kuzgun Vadisi locations generated from design catalogs.</p>
<ul>
  <li><a href="../baslangic.html">Beginners (TR)</a></li>
  <li><a href="../silahlar/index.html">Weapons</a></li>
  <li><a href="../rutbeler/index.html">Ranks</a></li>
  <li><a href="../lokasyonlar/index.html">Locations</a></li>
  <li><a href="../harita.html">Map</a></li>
</ul>`,
    breadcrumbs: [{ href: '../index.html', label: 'Home' }, { label: 'EN' }],
  }));
  register('en/index.html', 'English Guide', 'sayfa', 'English field guide landing');

  console.log(`Wiki build OK — ${declaredPages.length} pages → ${relative(ROOT, OUT)}`);
  return { pages: declaredPages, weapons, ranks, roles, locations: locCards.length };
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  build().catch(err => {
    console.error(err);
    process.exit(1);
  });
}
