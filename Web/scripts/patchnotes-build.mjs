// Yama notları: content/patchnotes/<sürüm>.md → JSON (SPA), statik HTML sayfaları ve RSS 2.0.
//
// Frontmatter (zorunlu: version, date; isteğe bağlı: title, summary, tags):
//   ---
//   version: 0.4.1
//   date: 2026-10-05
//   title: 0.4.1 — Portal: basın kiti
//   tags: [portal, istemci]
//   ---
// Dosya adı sürümle aynı olmalı (0.4.1.md). Sıralama semver'e göredir (0.10.0 > 0.9.0).
import { readdirSync, readFileSync, writeFileSync, mkdirSync, existsSync, unlinkSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import {
  renderMarkdown, parseFrontmatter, stripHtml, excerpt, escapeHtml, compareVersions, SEMVER_RE, DATE_RE,
} from './lib/markdown.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const notesDir = join(root, 'content', 'patchnotes');

export const TAG_LABELS = {
  istemci: { tr: 'İstemci', en: 'Client' },
  portal: { tr: 'Portal', en: 'Portal' },
  sunucu: { tr: 'Sunucu', en: 'Server' },
  denge: { tr: 'Denge', en: 'Balance' },
  duzeltme: { tr: 'Düzeltme', en: 'Fixes' },
  harita: { tr: 'Harita', en: 'Map' },
};

/** Site kök adresi: HAREKAT_SITE_URL > content/data/site.json > yer tutucu. */
export function siteBaseUrl() {
  const env = typeof process !== 'undefined' ? process.env.HAREKAT_SITE_URL : '';
  if (env) return env.replace(/\/+$/, '');
  try {
    const site = JSON.parse(readFileSync(join(root, 'content', 'data', 'site.json'), 'utf8'));
    if (site.baseUrl) return String(site.baseUrl).replace(/\/+$/, '');
  } catch { /* yer tutucu */ }
  return 'https://harekat.example';
}

export function escXml(s) {
  return String(s ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

const cdata = (s) => `<![CDATA[${String(s ?? '').replace(/]]>/g, ']]]]><![CDATA[>')}]]>`;

/** Tek bir md dosyasını ayrıştırır ve doğrular. */
export function parseNote(fileName, raw) {
  const slug = basename(fileName, '.md');
  const errors = [];
  const { meta, body } = parseFrontmatter(raw);
  const version = String(meta.version || '');
  const date = String(meta.date || '');
  if (!SEMVER_RE.test(version)) errors.push(`${fileName}: 'version' semver olmalı (ör. 0.4.1), bulunan: '${version}'`);
  if (version && slug !== version) errors.push(`${fileName}: dosya adı sürümle aynı olmalı (${version}.md)`);
  if (!DATE_RE.test(date) || Number.isNaN(Date.parse(`${date}T00:00:00Z`))) {
    errors.push(`${fileName}: 'date' YYYY-AA-GG olmalı, bulunan: '${date}'`);
  }
  const tags = Array.isArray(meta.tags) ? meta.tags : (meta.tags ? [String(meta.tags)] : []);
  for (const tag of tags) {
    if (!TAG_LABELS[tag]) errors.push(`${fileName}: bilinmeyen etiket '${tag}' (izinli: ${Object.keys(TAG_LABELS).join(', ')})`);
  }
  const rendered = renderMarkdown(body);
  const title = String(meta.title || rendered.title || slug);
  // Sayfa başlığı ayrıca gösterildiği için gövdedeki ilk H1 düşürülür.
  const html = rendered.html.replace(/^<h1>[\s\S]*?<\/h1>\n?/, '');
  if (!stripHtml(html)) errors.push(`${fileName}: gövde boş`);
  const summary = String(meta.summary || excerpt(stripHtml(html), 220));
  return {
    errors,
    post: { id: slug, slug, version, title, date, tags, summary, html, toc: rendered.toc },
  };
}

function shell({ title, description, canonical, body, depth = '../../' }) {
  return `<!DOCTYPE html>
<html lang="tr" data-theme="dark">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <meta name="theme-color" content="#0B0F14" />
  <title>${escapeHtml(title)}</title>
  <meta name="description" content="${escapeHtml(description)}" />
  <link rel="canonical" href="${escapeHtml(canonical)}" />
  <link rel="icon" href="${depth}assets/icons/favicon.svg" type="image/svg+xml" />
  <link rel="stylesheet" href="${depth}css/main.css" />
  <link rel="alternate" type="application/rss+xml" title="HAREKÂT Yama Notları" href="rss.xml" />
</head>
<body class="static-page">
  <main id="main" class="main">
${body}
  </main>
  <footer class="footer"><p>HAREKÂT © Tatbikat — Mavi / Kırmızı kuvvetler. Kurgusal içerik; gerçek örgüt adı kullanılmaz.</p></footer>
</body>
</html>
`;
}

function tagPills(tags) {
  return tags.map((t) => `<span class="pill">${escapeHtml(TAG_LABELS[t]?.tr || t)}</span>`).join(' ');
}

export function renderStaticPost(post, site) {
  return shell({
    title: `${post.title} — HAREKÂT Yama Notları`,
    description: post.summary,
    canonical: `${site}/content/patchnotes/${post.slug}.html`,
    body: `    <p class="muted"><a href="../../#/patches/${encodeURIComponent(post.slug)}">← Portal</a> · <a href="index.html">Tüm yama notları</a> · <a href="rss.xml">RSS</a></p>
    <article class="card section">
      <h1>${escapeHtml(post.title)}</h1>
      <p class="muted"><time datetime="${post.date}">${post.date}</time> · ${escapeHtml(post.version)} ${tagPills(post.tags)}</p>
      <div class="prose">${post.html}</div>
    </article>`,
  });
}

export function renderStaticIndex(posts, site) {
  const items = posts.map((p) => `      <li class="card">
        <h2><a href="${encodeURIComponent(p.slug)}.html">${escapeHtml(p.title)}</a></h2>
        <p class="muted"><time datetime="${p.date}">${p.date}</time> ${tagPills(p.tags)}</p>
        <p>${escapeHtml(p.summary)}</p>
      </li>`).join('\n');
  return shell({
    title: 'Yama Notları — HAREKÂT',
    description: 'HAREKÂT istemci, portal ve sunucu yama notları.',
    canonical: `${site}/content/patchnotes/index.html`,
    body: `    <p class="muted"><a href="../../#/patches">← Portal</a> · <a href="rss.xml">RSS beslemesi</a></p>
    <h1>Yama Notları</h1>
    <ul class="static-list">
${items}
    </ul>`,
  });
}

export function renderRss(posts, site, builtAt = new Date()) {
  const items = posts.map((p) => `
    <item>
      <title>${escXml(p.title)}</title>
      <link>${escXml(`${site}/content/patchnotes/${encodeURIComponent(p.slug)}.html`)}</link>
      <guid isPermaLink="false">harekat-patch-${escXml(p.version)}</guid>
      <pubDate>${new Date(`${p.date}T12:00:00Z`).toUTCString()}</pubDate>
${p.tags.map((t) => `      <category>${escXml(TAG_LABELS[t]?.tr || t)}</category>\n`).join('')}      <description>${escXml(p.summary)}</description>
      <content:encoded>${cdata(p.html)}</content:encoded>
    </item>`).join('');
  const last = posts[0] ? new Date(`${posts[0].date}T12:00:00Z`) : builtAt;
  return `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0" xmlns:atom="http://www.w3.org/2005/Atom" xmlns:content="http://purl.org/rss/1.0/modules/content/">
  <channel>
    <title>HAREKÂT Yama Notları</title>
    <link>${escXml(`${site}/content/patchnotes/index.html`)}</link>
    <atom:link href="${escXml(`${site}/content/patchnotes/rss.xml`)}" rel="self" type="application/rss+xml" />
    <description>HAREKÂT istemci, portal ve sunucu yama notları</description>
    <language>tr</language>
    <lastBuildDate>${last.toUTCString()}</lastBuildDate>
    <generator>harekat-web patchnotes-build</generator>${items}
  </channel>
</rss>
`;
}

/** Tüm notları üretir; hata varsa hepsini listeleyip fırlatır. */
export function buildPatchnotes({ dir = notesDir, site = siteBaseUrl(), write = true } = {}) {
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  const files = readdirSync(dir).filter((f) => f.endsWith('.md'));
  const errors = [];
  const posts = [];
  for (const f of files) {
    const { errors: errs, post } = parseNote(f, readFileSync(join(dir, f), 'utf8'));
    errors.push(...errs);
    posts.push(post);
  }
  const seen = new Set();
  for (const p of posts) {
    if (seen.has(p.version)) errors.push(`yinelenen sürüm: ${p.version}`);
    seen.add(p.version);
  }
  if (errors.length) throw new Error(`patchnotes doğrulama hatası:\n - ${errors.join('\n - ')}`);

  posts.sort((a, b) => compareVersions(b.version, a.version) || b.date.localeCompare(a.date));
  if (!write) return posts;

  const keep = new Set(['index.json', 'index.html', 'rss.xml']);
  for (const p of posts) {
    writeFileSync(join(dir, `${p.slug}.json`), `${JSON.stringify(p, null, 2)}\n`);
    writeFileSync(join(dir, `${p.slug}.html`), renderStaticPost(p, site));
    keep.add(`${p.slug}.json`).add(`${p.slug}.html`).add(`${p.slug}.md`);
  }
  // Kaynağı silinmiş notların eski çıktıları kalmasın.
  for (const f of readdirSync(dir)) {
    if (!keep.has(f) && /\.(json|html)$/.test(f)) unlinkSync(join(dir, f));
  }
  const index = posts.map(({ id, slug, version, title, date, tags, summary }) => ({
    id, slug, version, title, date, tags, summary,
  }));
  writeFileSync(join(dir, 'index.json'), `${JSON.stringify(index, null, 2)}\n`);
  writeFileSync(join(dir, 'index.html'), renderStaticIndex(posts, site));
  writeFileSync(join(dir, 'rss.xml'), renderRss(posts, site));
  return posts;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const posts = buildPatchnotes();
  console.log(`patchnotes build ok — ${posts.length} not + statik HTML + rss.xml (${siteBaseUrl()})`);
}
