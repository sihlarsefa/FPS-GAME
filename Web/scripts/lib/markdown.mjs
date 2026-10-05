// HAREKÂT Web — bağımlılıksız, güvenli Markdown → HTML dönüştürücü.
// Yama notları, haberler, basın kiti ve hukuki metinler bu modülü kullanır.
//
// Desteklenen: başlıklar (#…####), paragraflar, satır sonu (iki boşluk), - / * / 1. listeleri,
// > alıntı, --- yatay çizgi, ``` kod bloğu, | boru | tabloları |, **kalın**, *italik*, `kod`,
// [bağlantı](url). Girdi HTML'i her zaman kaçışlanır; javascript:/data: bağlantıları düşürülür.

const TR_FOLD = {
  ç: 'c', ğ: 'g', ı: 'i', İ: 'i', ö: 'o', ş: 's', ü: 'u', â: 'a', î: 'i', û: 'u',
  Ç: 'c', Ğ: 'g', Ö: 'o', Ş: 's', Ü: 'u', Â: 'a', Î: 'i', Û: 'u',
};

/** HTML kaçışı (metin ve öznitelik için güvenli). */
export function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, (c) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;',
  }[c]));
}

/** Türkçe karakterleri katlayan ASCII kebab-case kimlik. */
export function slugify(text) {
  return String(text ?? '')
    .replace(/[çğıİöşüâîûÇĞÖŞÜÂÎÛ]/g, (c) => TR_FOLD[c] || c)
    .toLowerCase()
    .replace(/<[^>]+>/g, '')
    .replace(/&[a-z#0-9]+;/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64) || 'bolum';
}

/** Bağlantı hedefi güvenli mi? (http/https/mailto, sayfa içi #, göreli yol) */
export function isSafeUrl(url) {
  const u = String(url ?? '').trim();
  if (!u) return false;
  if (/^(https?:|mailto:)/i.test(u)) return true;
  if (/^[a-z][a-z0-9+.-]*:/i.test(u)) return false; // javascript:, data:, vbscript: …
  return !u.startsWith('//');
}

/**
 * Basit YAML benzeri frontmatter. `anahtar: değer`, `anahtar: [a, b]` desteklenir.
 * @returns {{ meta: Record<string, string|string[]>, body: string }}
 */
export function parseFrontmatter(raw) {
  const text = String(raw ?? '').replace(/^﻿/, '').replace(/\r\n/g, '\n');
  if (!text.startsWith('---\n')) return { meta: {}, body: text };
  const end = text.indexOf('\n---', 3);
  if (end < 0) return { meta: {}, body: text };
  const block = text.slice(4, end);
  const body = text.slice(end + 4).replace(/^[^\n]*\n/, '');
  const meta = {};
  for (const line of block.split('\n')) {
    const m = /^([A-Za-z_][\w-]*)\s*:\s*(.*)$/.exec(line.trim());
    if (!m) continue;
    let value = m[2].trim();
    if (/^\[.*\]$/.test(value)) {
      meta[m[1]] = value.slice(1, -1).split(',').map((v) => unquote(v.trim())).filter(Boolean);
    } else {
      meta[m[1]] = unquote(value);
    }
  }
  return { meta, body };
}

function unquote(v) {
  return v.replace(/^(["'])(.*)\1$/, '$2');
}

/** Satır içi biçimlendirme. Önce kaçışlar, sonra işaretleri uygular; `kod` korunur. */
export function inline(text, opts = {}) {
  const parts = String(text ?? '').split(/(`[^`]+`)/g);
  return parts.map((part) => {
    if (/^`[^`]+`$/.test(part)) return `<code>${escapeHtml(part.slice(1, -1))}</code>`;
    let s = escapeHtml(part);
    s = s.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, label, href) => {
      const raw = href.replace(/&amp;/g, '&');
      if (!isSafeUrl(raw)) return label;
      const external = /^https?:/i.test(raw);
      const rel = external ? ' rel="noopener noreferrer" target="_blank"' : '';
      const resolved = opts.resolveLink ? opts.resolveLink(raw) : raw;
      return `<a href="${escapeHtml(resolved)}"${rel}>${label}</a>`;
    });
    s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
    s = s.replace(/(^|[^*\w])\*([^*\s][^*]*?)\*(?!\*)/g, '$1<em>$2</em>');
    return s;
  }).join('');
}

function splitRow(line) {
  let row = line.trim();
  if (row.startsWith('|')) row = row.slice(1);
  if (row.endsWith('|')) row = row.slice(0, -1);
  return row.split('|').map((c) => c.trim());
}

const isTableSep = (line) => /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(line);

/**
 * Markdown → HTML.
 * @param {string} md
 * @param {{ headingIds?: boolean, resolveLink?: (href:string)=>string }} [opts]
 * @returns {{ html: string, toc: Array<{level:number,id:string,text:string}>, title: string|null }}
 */
export function renderMarkdown(md, opts = {}) {
  const lines = String(md ?? '').replace(/\r\n/g, '\n').split('\n');
  const out = [];
  const toc = [];
  const usedIds = new Set();
  let title = null;
  let para = [];
  let list = null; // { type: 'ul'|'ol', items: [] }
  let quote = [];

  const uniqueId = (text) => {
    const base = slugify(text);
    let id = base;
    let n = 2;
    while (usedIds.has(id)) id = `${base}-${n++}`;
    usedIds.add(id);
    return id;
  };
  const flushPara = () => {
    if (!para.length) return;
    const html = para.map((l, i) => {
      const hard = / {2,}$/.test(l) && i < para.length - 1;
      return inline(l.trim(), opts) + (hard ? '<br>' : '');
    }).join('\n');
    out.push(`<p>${html}</p>`);
    para = [];
  };
  const flushList = () => {
    if (!list) return;
    out.push(`<${list.type}>${list.items.map((i) => `<li>${inline(i, opts)}</li>`).join('')}</${list.type}>`);
    list = null;
  };
  const flushQuote = () => {
    if (!quote.length) return;
    const inner = renderMarkdown(quote.join('\n'), { ...opts, headingIds: false }).html;
    out.push(`<blockquote>${inner}</blockquote>`);
    quote = [];
  };
  const flushAll = () => { flushPara(); flushList(); flushQuote(); };

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    if (/^```/.test(line)) {
      flushAll();
      const code = [];
      i++;
      while (i < lines.length && !/^```/.test(lines[i])) code.push(lines[i++]);
      out.push(`<pre><code>${escapeHtml(code.join('\n'))}</code></pre>`);
      continue;
    }

    const heading = /^(#{1,4})\s+(.+?)\s*#*\s*$/.exec(line);
    if (heading) {
      flushAll();
      const level = heading[1].length;
      const text = heading[2];
      if (level === 1 && title === null) title = text.replace(/[*`]/g, '');
      const html = inline(text, opts);
      if (opts.headingIds !== false && level >= 2) {
        const id = uniqueId(text);
        if (level <= 3) toc.push({ level, id, text: text.replace(/[*`]/g, '') });
        out.push(`<h${level} id="${id}">${html}</h${level}>`);
      } else {
        out.push(`<h${level}>${html}</h${level}>`);
      }
      continue;
    }

    if (/^\s*(-{3,}|\*{3,}|_{3,})\s*$/.test(line)) {
      flushAll();
      out.push('<hr>');
      continue;
    }

    if (/^\s*\|/.test(line) && i + 1 < lines.length && isTableSep(lines[i + 1])) {
      flushAll();
      const head = splitRow(line);
      i += 2;
      const rows = [];
      while (i < lines.length && /^\s*\|/.test(lines[i])) rows.push(splitRow(lines[i++]));
      i--;
      out.push(`<div class="table-wrap"><table><thead><tr>${head.map((h) => `<th scope="col">${inline(h, opts)}</th>`).join('')}</tr></thead><tbody>${
        rows.map((r) => `<tr>${head.map((_, c) => `<td>${inline(r[c] ?? '', opts)}</td>`).join('')}</tr>`).join('')
      }</tbody></table></div>`);
      continue;
    }

    const q = /^>\s?(.*)$/.exec(line);
    if (q) {
      flushPara(); flushList();
      quote.push(q[1]);
      continue;
    } else if (quote.length) {
      flushQuote();
    }

    const ul = /^\s*[-*]\s+(.*)$/.exec(line);
    const ol = /^\s*\d+[.)]\s+(.*)$/.exec(line);
    if (ul || ol) {
      flushPara();
      const type = ul ? 'ul' : 'ol';
      if (list && list.type !== type) flushList();
      if (!list) list = { type, items: [] };
      list.items.push((ul || ol)[1].trim());
      continue;
    }

    if (!line.trim()) {
      flushAll();
      continue;
    }

    if (list && /^\s{2,}\S/.test(line)) {
      list.items[list.items.length - 1] += ` ${line.trim()}`;
      continue;
    }

    flushList();
    para.push(line.replace(/\s+$/, (m) => (m.length >= 2 ? '  ' : '')));
  }
  flushAll();
  return { html: out.join('\n'), toc, title };
}

/** HTML'den düz metin (özet / RSS açıklaması için). */
export function stripHtml(html) {
  return String(html ?? '')
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/\s+/g, ' ')
    .trim();
}

/** Cümle sınırında kısaltma. */
export function excerpt(text, max = 220) {
  const s = String(text ?? '').trim();
  if (s.length <= max) return s;
  const cut = s.slice(0, max);
  const at = Math.max(cut.lastIndexOf('. '), cut.lastIndexOf(' '));
  return `${cut.slice(0, at > max * 0.6 ? at : max).trim()}…`;
}

/** Semver karşılaştırma (yalnızca MAJOR.MINOR.PATCH[-pre]); a<b → negatif. */
export function compareVersions(a, b) {
  const parse = (v) => {
    const m = /^(\d+)\.(\d+)\.(\d+)(?:-([\w.]+))?$/.exec(String(v).trim());
    if (!m) return null;
    return { nums: [Number(m[1]), Number(m[2]), Number(m[3])], pre: m[4] || null };
  };
  const pa = parse(a);
  const pb = parse(b);
  if (!pa || !pb) return String(a).localeCompare(String(b));
  for (let i = 0; i < 3; i++) if (pa.nums[i] !== pb.nums[i]) return pa.nums[i] - pb.nums[i];
  if (pa.pre === pb.pre) return 0;
  if (!pa.pre) return 1;
  if (!pb.pre) return -1;
  return pa.pre.localeCompare(pb.pre, 'en', { numeric: true });
}

export const SEMVER_RE = /^\d+\.\d+\.\d+(?:-[\w.]+)?$/;
export const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
