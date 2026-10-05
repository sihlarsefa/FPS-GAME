// Tasarım / pazarlama kaynaklarını Web içeriğine aynalar (salt-okunur kaynaklar → Web/ kopyaları).
//
//   Design/Teams (C3-4)        → assets/teams/*.svg, assets/ranks/rank-N.svg, content/data/teams.json
//   Design/Progression (C3-6)  → content/data/achievements.json
//   Marketing/brand+screenshots → assets/press/**.svg
//   Marketing/press            → content/data/presskit.json (TR değerleri), content/press/bulten.{tr,en}.md
//   Marketing/LiveOps          → content/data/sysreq.json (TR değerleri + Marketing'in TR/EN özet metni)
//   Localization/strings.csv   → rütbe İngilizce adları;  Backend RankCatalog → rütbe XP eşikleri
//
// TR değerleri kaynaktan gelir; EN çeviriler Web JSON'unda korunur, TR değişirse uyarı verilir.
// Kaynak klasör yoksa (ör. yalnız Web/ dağıtıldığında) o bölüm atlanır.
//
//   node scripts/sync-design.mjs           → yazar
//   node scripts/sync-design.mjs --check   → yalnız karşılaştırır, fark varsa çıkış kodu 1
import { readFileSync, writeFileSync, existsSync, mkdirSync, readdirSync, unlinkSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { slugify } from './lib/markdown.mjs';

const WEB = join(dirname(fileURLToPath(import.meta.url)), '..');
const REPO = join(WEB, '..');

const ACH_CATEGORIES = {
  combat: ['Muharebe', 'Combat'],
  weapon: ['Silah', 'Weapons'],
  career: ['Kariyer', 'Career'],
  survival: ['Hayatta Kalma', 'Survival'],
  squad: ['Tim', 'Squad'],
  support: ['Destek', 'Support'],
  vehicle: ['Araç', 'Vehicles'],
  social: ['Sosyal', 'Social'],
  season: ['Sezon', 'Season'],
  cosmetic: ['Kozmetik', 'Cosmetics'],
  tutorial: ['Eğitim', 'Tutorial'],
};

const RANK_CATEGORIES = {
  enlisted: ['Er / Erbaş', 'Enlisted'],
  specialist: ['Uzman Erbaş', 'Specialist'],
  nco: ['Astsubay', 'NCO'],
  officer: ['Subay', 'Officer'],
};

const SYSREQ_KEYS = {
  'İşletim sistemi': ['os', 'Operating system'],
  'İşlemci': ['cpu', 'Processor'],
  'Bellek': ['ram', 'Memory'],
  'Ekran kartı': ['gpu', 'Graphics'],
  'DirectX': ['directx', 'DirectX'],
  'Depolama': ['storage', 'Storage'],
  'Ağ': ['network', 'Network'],
  'Ek': ['extra', 'Additional'],
  'Ses': ['audio', 'Audio'],
};

// ———————————————————————————————————————— yardımcılar

const rel = (p) => p.slice(REPO.length + 1).replace(/\\/g, '/');
const stable = (v) => `${JSON.stringify(v, null, 2)}\n`;

function readText(path) {
  return existsSync(path) ? readFileSync(path, 'utf8').replace(/\r\n/g, '\n') : null;
}

function readJson(path, warnings) {
  const text = readText(path);
  if (text == null) return null;
  try {
    return JSON.parse(text);
  } catch (err) {
    warnings.push(`${rel(path)} okunamadı (${err.message}); bu bölüm atlandı`);
    return null;
  }
}

/** SVG güvenlik denetimi: betik, olay öznitelikleri, dış referans yok. */
export function svgProblems(svg) {
  const problems = [];
  if (!/<svg[\s>]/i.test(svg)) problems.push('svg kök öğesi yok');
  if (/<script/i.test(svg)) problems.push('<script> içeriyor');
  if (/\son[a-z]+\s*=/i.test(svg)) problems.push('olay özniteliği (on*) içeriyor');
  if (/javascript:/i.test(svg)) problems.push('javascript: bağlantısı içeriyor');
  if (/(?:xlink:)?href\s*=\s*["'](?!#)/i.test(svg)) problems.push('dış href içeriyor');
  if (/<foreignObject/i.test(svg)) problems.push('<foreignObject> içeriyor');
  return problems;
}

const cleanCell = (s) => String(s ?? '').replace(/\*\*/g, '').replace(/`/g, '').replace(/\s+$/, '').trim();

/** `## Başlık` bölümünün gövdesi (bir sonraki ## başlığına kadar). */
function section(md, headingRe) {
  const lines = md.split('\n');
  const start = lines.findIndex((l) => /^##\s+/.test(l) && headingRe.test(l.replace(/^##\s+/, '')));
  if (start < 0) return null;
  const out = [];
  for (let i = start + 1; i < lines.length; i++) {
    if (/^##\s+/.test(lines[i])) break;
    out.push(lines[i]);
  }
  return out.join('\n');
}

/** İlk boru tablosunun gövde satırları (başlık ve ayraç hariç). */
function tableRows(text) {
  if (!text) return [];
  const lines = text.split('\n').filter((l) => /^\s*\|/.test(l));
  return lines.slice(2).map((l) => {
    let row = l.trim();
    if (row.startsWith('|')) row = row.slice(1);
    if (row.endsWith('|')) row = row.slice(0, -1);
    return row.split('|').map(cleanCell);
  });
}

function bullets(text) {
  if (!text) return [];
  return text.split('\n').filter((l) => /^\s*-\s+/.test(l)).map((l) => cleanCell(l.replace(/^\s*-\s+/, '')));
}

function boldLine(md, labelRe) {
  for (const line of md.split('\n')) {
    const m = /^\*\*(.+?)\*\*\s*(.*)$/.exec(line.trim());
    if (m && labelRe.test(m[1])) return cleanCell(m[2]);
  }
  return null;
}

function parseCsv(text) {
  const rows = [];
  let row = [];
  let cell = '';
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"' && text[i + 1] === '"') { cell += '"'; i++; } else if (c === '"') quoted = false; else cell += c;
    } else if (c === '"') quoted = true;
    else if (c === ',') { row.push(cell); cell = ''; } else if (c === '\n') { row.push(cell); rows.push(row); row = []; cell = ''; } else if (c !== '\r') cell += c;
  }
  if (cell || row.length) { row.push(cell); rows.push(row); }
  return rows;
}

// ———————————————————————————————————————— planlayıcılar
// Her planlayıcı { files: Map<hedefYol, içerik>, warnings: [] } doldurur; hiçbiri doğrudan yazmaz.

function planTeams(plan) {
  const dir = join(REPO, 'Design', 'Teams');
  const teamsSrc = readJson(join(dir, 'teams.json'), plan.warnings);
  const ranksSrc = readJson(join(dir, 'ranks.json'), plan.warnings);
  if (!teamsSrc || !ranksSrc) {
    plan.skipped.push('Design/Teams');
    return;
  }
  const current = readJson(join(WEB, 'content', 'data', 'teams.json'), []) || {};

  const teams = [];
  for (const t of teamsSrc.teams || []) {
    const svgPath = join(dir, 'emblems', `${t.id}.svg`);
    const svg = readText(svgPath);
    if (svg == null) {
      plan.errors.push(`amblem yok: ${rel(svgPath)}`);
      continue;
    }
    const bad = svgProblems(svg);
    if (bad.length) plan.errors.push(`${rel(svgPath)}: ${bad.join(', ')}`);
    plan.files.set(join(WEB, 'assets', 'teams', `${t.id}.svg`), svg);
    teams.push({
      id: t.id,
      name_tr: t.name_tr,
      name_en: t.name_en,
      primary: t.primary,
      secondary: t.secondary,
      accent: t.accent,
      armband: t.armband,
      slogan_tr: t.slogan_tr,
      slogan_en: t.slogan_en,
      story_tr: t.story_tr,
      story_en: t.story_en,
      emblem: `assets/teams/${t.id}.svg`,
    });
  }
  plan.prune.push({ dir: join(WEB, 'assets', 'teams'), keep: new Set(teams.map((t) => `${t.id}.svg`)), ext: '.svg' });

  // Rütbe İngilizce adları (Localization) ve XP eşikleri (Backend RankCatalog)
  const enNames = {};
  const csv = readText(join(REPO, 'Localization', 'strings.csv'));
  if (csv) {
    for (const r of parseCsv(csv).slice(1)) if (/^rank\.[a-z_]+$/.test(r[0] || '')) enNames[r[0]] = r[2];
  } else plan.warnings.push('Localization/strings.csv yok; rütbe EN adları TR ile aynı kalır');
  const xp = {};
  const rankCs = readText(join(REPO, 'Backend', 'Harekat.Domain', 'Catalogs', 'RankCatalog.cs'));
  if (rankCs) {
    for (const m of rankCs.matchAll(/\[MilitaryRank\.(\w+)\]\s*=\s*([\d_]+)/g)) xp[m[1]] = Number(m[2].replace(/_/g, ''));
  } else plan.warnings.push('Backend RankCatalog.cs yok; rütbe XP eşikleri boş kalır');

  const ranks = [];
  for (const r of ranksSrc.ranks || []) {
    const svgPath = join(dir, r.file);
    const svg = readText(svgPath);
    if (svg == null) {
      plan.errors.push(`rütbe nişanı yok: ${rel(svgPath)}`);
      continue;
    }
    const bad = svgProblems(svg);
    if (bad.length) plan.errors.push(`${rel(svgPath)}: ${bad.join(', ')}`);
    plan.files.set(join(WEB, 'assets', 'ranks', `rank-${r.index}.svg`), svg);
    const locKey = `rank.${basename(r.file, '.svg').replace(/-/g, '_')}`;
    ranks.push({
      index: r.index,
      enum: r.enum,
      name_tr: r.name_tr,
      name_en: enNames[locKey] || r.name_tr,
      category: r.category,
      xp: xp[r.enum] ?? null,
      insignia: `assets/ranks/rank-${r.index}.svg`,
    });
  }
  ranks.sort((a, b) => a.index - b.index);
  ranks.forEach((r, i) => {
    if (r.index !== i) plan.errors.push(`ranks.json sırası MilitaryRank ile uyuşmuyor (beklenen ${i}, bulunan ${r.index})`);
  });

  plan.files.set(join(WEB, 'content', 'data', 'teams.json'), stable({
    schema: 'harekat.web.teams.v2',
    source: 'Design/Teams (C3-4) · rütbe EN adları Localization/strings.csv · XP eşikleri Backend RankCatalog',
    disclaimer_tr: current.disclaimer_tr || 'Kurgu tim kimlikleri — resmi örgüt armaları kopyalanmaz; hilal-yıldız esinli özgün amblemler.',
    disclaimer_en: current.disclaimer_en || 'Fictional squad identities — no official insignia copies; original crescent-star inspired emblems.',
    rankCategories: Object.fromEntries(Object.entries(RANK_CATEGORIES).map(([k, [tr, en]]) => [k, { tr, en }])),
    teams,
    ranks,
  }));
}

function planAchievements(plan) {
  const src = readJson(join(REPO, 'Design', 'Progression', 'achievements.json'), plan.warnings);
  if (!src) {
    plan.skipped.push('Design/Progression');
    return;
  }
  const list = src.achievements || src.items || [];
  const ids = new Set();
  const items = list.map((a) => {
    if (ids.has(a.id)) plan.errors.push(`yinelenen başarım kimliği: ${a.id}`);
    ids.add(a.id);
    if (!a.title || !a.description || !a.metric || !(a.target > 0)) plan.errors.push(`eksik alanlı başarım: ${a.id}`);
    if (a.category && !ACH_CATEGORIES[a.category]) plan.warnings.push(`bilinmeyen başarım kategorisi '${a.category}' (${a.id})`);
    return {
      id: a.id,
      title_tr: a.title,
      title_en: a.titleEn || a.title,
      desc_tr: a.description,
      desc_en: a.descriptionEn || a.description,
      category: a.category || 'career',
      metric: a.metric,
      target: a.target,
      xp: a.xpReward ?? 0,
      icon: a.iconIdea || null,
      event: a.conditionEvent || null,
      condition: a.conditionNotes || null,
    };
  });
  if (src.count != null && src.count !== items.length) {
    plan.warnings.push(`Design/Progression/achievements.json count=${src.count} fakat ${items.length} kayıt var`);
  }
  const order = Object.keys(ACH_CATEGORIES);
  const cats = [...new Set(items.map((i) => i.category))]
    .sort((a, b) => (order.indexOf(a) + 1 || 99) - (order.indexOf(b) + 1 || 99));
  plan.files.set(join(WEB, 'content', 'data', 'achievements.json'), stable({
    schema: 'harekat.web.achievements.v2',
    source: 'Design/Progression/achievements.json (C3-6)',
    count: items.length,
    categories: cats.map((id) => ({
      id,
      label_tr: ACH_CATEGORIES[id]?.[0] || id,
      label_en: ACH_CATEGORIES[id]?.[1] || id,
      count: items.filter((i) => i.category === id).length,
    })),
    items,
  }));
}

/** TR alanlarını kaynaktan alır, EN'i mevcut JSON'dan korur; TR değiştiyse uyarır. */
function keepEn(plan, label, fresh, old, fields) {
  const out = { ...fresh };
  for (const [trField, enField] of fields) {
    const prevEn = old?.[enField];
    if (old && old[trField] !== fresh[trField] && prevEn) {
      plan.warnings.push(`${label}: TR metni değişti → '${enField}' çevirisini gözden geçir ("${fresh[trField]}")`);
    }
    out[enField] = prevEn || fresh[trField];
  }
  return out;
}

function planSysreq(plan) {
  const mdPath = join(REPO, 'Marketing', 'LiveOps', 'sistem_gereksinimleri_windows.md');
  const md = readText(mdPath);
  if (md == null) {
    plan.skipped.push('Marketing/LiveOps');
    return;
  }
  const current = readJson(join(WEB, 'content', 'data', 'sysreq.json'), plan.warnings) || {};
  const oldTier = (id) => (current.tiers || []).find((t) => t.id === id);

  const tierDefs = [
    { id: 'minimum', re: /^Minimum/i, label_en: 'Minimum', target: /Hedef deneyim \(min\)/i },
    { id: 'recommended', re: /^Önerilen/i, label_en: 'Recommended', target: /Hedef deneyim \(önerilen\)/i },
    { id: 'highEnd', re: /^Üst düzey/i, label_en: 'High-end / streaming (optional)', target: null },
  ];
  const tiers = [];
  for (const def of tierDefs) {
    const headingLine = md.split('\n').find((l) => /^##\s+/.test(l) && def.re.test(l.replace(/^##\s+/, '')));
    const body = section(md, def.re);
    if (!body) {
      plan.errors.push(`${rel(mdPath)}: '${def.id}' bölümü bulunamadı`);
      continue;
    }
    const prev = oldTier(def.id);
    const rows = tableRows(body).map(([labelTr, valueTr]) => {
      const [key, labelEn] = SYSREQ_KEYS[labelTr] || [slugify(labelTr), labelTr];
      const old = (prev?.rows || []).find((r) => r.key === key);
      return keepEn(plan, `sysreq.${def.id}.${key}`, { key, label_tr: labelTr, label_en: labelEn, value_tr: valueTr }, old, [['value_tr', 'value_en']]);
    });
    const tier = {
      id: def.id,
      label_tr: cleanCell(headingLine.replace(/^##\s+/, '')),
      label_en: def.label_en,
      rows,
    };
    if (def.target) {
      const targetTr = boldLine(md, def.target);
      if (targetTr) Object.assign(tier, keepEn(plan, `sysreq.${def.id}.target`, { target_tr: targetTr }, prev, [['target_tr', 'target_en']]));
    }
    tiers.push(tier);
  }

  const matrixRows = tableRows(section(md, /^Çözünürlük/i));
  const oldMatrix = current.resolutionMatrix?.rows || [];
  const matrix = matrixRows.map((cells, i) => {
    const old = oldMatrix[i];
    const same = old && JSON.stringify(old.tr) === JSON.stringify(cells);
    if (old && !same) plan.warnings.push(`sysreq.resolutionMatrix[${i}]: TR değişti → EN satırını gözden geçir`);
    return { tr: cells, en: old?.en && old.en.length === cells.length ? old.en : cells };
  });

  const notesTr = bullets(section(md, /^Bilinen kısıtlar/i));
  const notesEnOld = current.notes_en || [];
  if (notesEnOld.length !== notesTr.length || JSON.stringify(current.notes_tr) !== JSON.stringify(notesTr)) {
    if (current.notes_tr) plan.warnings.push('sysreq.notes: TR maddeleri değişti → notes_en dizisini gözden geçir');
  }

  plan.files.set(join(WEB, 'content', 'data', 'sysreq.json'), stable({
    schema: 'harekat.web.sysreq.v2',
    source: 'Marketing/LiveOps/sistem_gereksinimleri_windows.md',
    platform: 'Windows',
    status_tr: current.status_tr || 'Taslak değerler; build ölçümleriyle güncellenir.',
    status_en: current.status_en || 'Draft values; updated with build measurements.',
    summary_tr: boldLine(md, /^TR:$/) || current.summary_tr || '',
    summary_en: boldLine(md, /^EN:$/) || current.summary_en || '',
    tiers,
    resolutionMatrix: {
      columns_tr: current.resolutionMatrix?.columns_tr || ['Hedef', 'Çözünürlük', 'Preset', 'Donanım bandı'],
      columns_en: current.resolutionMatrix?.columns_en || ['Goal', 'Resolution', 'Preset', 'Hardware tier'],
      rows: matrix,
    },
    notes_tr: notesTr,
    notes_en: notesEnOld.length === notesTr.length ? notesEnOld : notesTr,
  }));
}

/** İç depo yollarını içeren "Kaynaklar / Assets" bölümünü bültenden çıkarır. */
export function publicPressRelease(md, lang) {
  const lines = md.replace(/\r\n/g, '\n').split('\n');
  const out = [];
  let skipping = false;
  for (const line of lines) {
    if (/^###\s+(Kaynaklar|Assets|Resources)\s*$/i.test(line)) { skipping = true; continue; }
    if (skipping && /^#{1,3}\s+/.test(line)) skipping = false;
    if (!skipping) out.push(line);
  }
  const front = lang === 'en'
    ? '---\ntitle: Press release — HAREKÂT\nsource: Marketing/press/press_release_en.md\n---\n\n'
    : '---\ntitle: Basın bülteni — HAREKÂT\nsource: Marketing/press/press_release_tr.md\n---\n\n';
  return `${front}${out.join('\n').replace(/\n{3,}/g, '\n\n').trim()}\n`;
}

function planPress(plan) {
  const brandDir = join(REPO, 'Marketing', 'brand');
  const shotsDir = join(REPO, 'Marketing', 'screenshots');
  const pressDir = join(REPO, 'Marketing', 'press');
  if (!existsSync(brandDir) || !existsSync(pressDir)) {
    plan.skipped.push('Marketing/brand|press');
    return;
  }
  const current = readJson(join(WEB, 'content', 'data', 'presskit.json'), plan.warnings) || {};
  const brandMd = readText(join(brandDir, 'BRAND.md')) || '';
  const factMd = readText(join(pressDir, 'fact_sheet.md')) || '';

  // Logolar
  const fileUsage = Object.fromEntries(tableRows(section(brandMd, /^Dosyalar/i)).map(([f, u]) => [f, u]));
  const logos = [];
  for (const f of readdirSync(brandDir).filter((n) => n.endsWith('.svg')).sort()) {
    const svg = readText(join(brandDir, f));
    const bad = svgProblems(svg);
    if (bad.length) { plan.errors.push(`Marketing/brand/${f}: ${bad.join(', ')}`); continue; }
    plan.files.set(join(WEB, 'assets', 'press', 'brand', f), svg);
    const old = (current.logos || []).find((l) => l.file === `assets/press/brand/${f}`);
    logos.push(keepEn(plan, `presskit.logos.${f}`, {
      file: `assets/press/brand/${f}`,
      usage_tr: fileUsage[f] || f,
    }, old, [['usage_tr', 'usage_en']]));
  }
  plan.prune.push({ dir: join(WEB, 'assets', 'press', 'brand'), keep: new Set(logos.map((l) => basename(l.file))), ext: '.svg' });

  // Steam kapsül görselleri
  const capsules = [];
  if (existsSync(shotsDir)) {
    for (const f of readdirSync(shotsDir).filter((n) => /^capsule_.+\.svg$/.test(n)).sort()) {
      const svg = readText(join(shotsDir, f));
      const bad = svgProblems(svg);
      if (bad.length) { plan.errors.push(`Marketing/screenshots/${f}: ${bad.join(', ')}`); continue; }
      plan.files.set(join(WEB, 'assets', 'press', 'capsules', f), svg);
      const size = /_(\d+)x(\d+)\.svg$/.exec(f);
      capsules.push({
        file: `assets/press/capsules/${f}`,
        name: f.replace(/^capsule_/, '').replace(/_\d+x\d+\.svg$/, ''),
        width: size ? Number(size[1]) : null,
        height: size ? Number(size[2]) : null,
      });
    }
    plan.prune.push({ dir: join(WEB, 'assets', 'press', 'capsules'), keep: new Set(capsules.map((c) => basename(c.file))), ext: '.svg' });
  }

  // Bilgi notu (fact sheet)
  const factRows = tableRows(factMd).map(([labelTr, valueTr]) => {
    const key = slugify(labelTr);
    const old = (current.factSheet || []).find((r) => r.key === key);
    return keepEn(plan, `presskit.factSheet.${key}`, { key, label_tr: labelTr, value_tr: valueTr }, old, [['label_tr', 'label_en'], ['value_tr', 'value_en']]);
  });

  // Renkler
  const colors = tableRows(section(brandMd, /^Renkler/i)).map(([nameTr, hex, usageTr]) => {
    const key = slugify(nameTr);
    const old = (current.colors || []).find((c) => c.key === key);
    return keepEn(plan, `presskit.colors.${key}`, { key, name_tr: nameTr, hex, usage_tr: usageTr }, old, [['name_tr', 'name_en'], ['usage_tr', 'usage_en']]);
  });

  const rulesTr = [...bullets(section(brandMd, /^Amblem kuralı/i)), ...bullets(section(brandMd, /^Yazım/i))];
  const rulesEnOld = current.rules_en || [];
  if (current.rules_tr && JSON.stringify(current.rules_tr) !== JSON.stringify(rulesTr)) {
    plan.warnings.push('presskit.rules: TR kuralları değişti → rules_en dizisini gözden geçir');
  }

  plan.files.set(join(WEB, 'content', 'data', 'presskit.json'), stable({
    schema: 'harekat.web.presskit.v1',
    source: 'Marketing/press/fact_sheet.md · Marketing/brand/BRAND.md · Marketing/screenshots',
    contact: current.contact || { email: 'press@harekat.example', placeholder: true },
    oneLiner_tr: boldLine(factMd, /Tek cümle/i) || current.oneLiner_tr || '',
    oneLiner_en: boldLine(factMd, /One-liner/i) || current.oneLiner_en || '',
    factSheet: factRows,
    colors,
    rules_tr: rulesTr,
    rules_en: rulesEnOld.length === rulesTr.length ? rulesEnOld : rulesTr,
    logos,
    capsules,
    releases: [
      { lang: 'tr', page: 'content/press/bulten.tr.json' },
      { lang: 'en', page: 'content/press/bulten.en.json' },
    ],
  }));

  for (const lang of ['tr', 'en']) {
    const src = readText(join(pressDir, `press_release_${lang}.md`));
    if (src == null) { plan.warnings.push(`Marketing/press/press_release_${lang}.md yok`); continue; }
    plan.files.set(join(WEB, 'content', 'press', `bulten.${lang}.md`), publicPressRelease(src, lang));
  }
}

// ———————————————————————————————————————— çalıştırıcı

/**
 * @param {{ write?: boolean }} [opts]
 * @returns {{ drift: string[], written: string[], removed: string[], warnings: string[], errors: string[], skipped: string[] }}
 */
export function syncDesign({ write = true } = {}) {
  const plan = { files: new Map(), prune: [], warnings: [], errors: [], skipped: [] };
  planTeams(plan);
  planAchievements(plan);
  planSysreq(plan);
  planPress(plan);

  const drift = [];
  const written = [];
  const removed = [];
  if (plan.errors.length) return { drift, written, removed, ...plan, files: undefined };

  for (const [target, content] of plan.files) {
    const existing = existsSync(target) ? readFileSync(target, 'utf8') : null;
    if (existing === content) continue;
    drift.push(rel(target));
    if (write) {
      mkdirSync(dirname(target), { recursive: true });
      writeFileSync(target, content);
      written.push(rel(target));
    }
  }
  for (const { dir, keep, ext } of plan.prune) {
    if (!existsSync(dir)) continue;
    for (const f of readdirSync(dir)) {
      if (!f.endsWith(ext) || keep.has(f)) continue;
      drift.push(`${rel(join(dir, f))} (fazla)`);
      if (write) {
        unlinkSync(join(dir, f));
        removed.push(rel(join(dir, f)));
      }
    }
  }
  return { drift, written, removed, warnings: plan.warnings, errors: plan.errors, skipped: plan.skipped };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const check = process.argv.includes('--check');
  const res = syncDesign({ write: !check });
  for (const s of res.skipped) console.log(`atlandı (kaynak yok): ${s}`);
  for (const w of res.warnings) console.warn(`uyarı: ${w}`);
  if (res.errors.length) {
    for (const e of res.errors) console.error(`hata: ${e}`);
    process.exit(1);
  }
  if (check) {
    if (res.drift.length) {
      console.error(`sync:check — ${res.drift.length} dosya kaynakla uyumsuz:\n  ${res.drift.join('\n  ')}\n→ npm run sync`);
      process.exit(1);
    }
    console.log('sync:check ok — Web kopyaları Design/Marketing kaynaklarıyla aynı');
  } else {
    console.log(`sync ok — ${res.written.length} yazıldı, ${res.removed.length} silindi`);
  }
}
