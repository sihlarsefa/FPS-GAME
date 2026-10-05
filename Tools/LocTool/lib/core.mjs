import { createHash } from 'node:crypto';
export const languages = ['tr', 'en', 'de', 'az', 'ar'];
export function parseCsv(text) {
  const rows = []; let row = [], field = '', quoted = false;
  text = text.replace(/^\uFEFF/, '');
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"') { if (quoted && text[i + 1] === '"') { field += '"'; i++; } else if (quoted || !field) quoted = !quoted; else throw new Error('Invalid CSV quote'); }
    else if (c === ',' && !quoted) { row.push(field); field = ''; }
    else if (c === '\n' && !quoted) { row.push(field.replace(/\r$/, '')); if (row.some(Boolean)) rows.push(row); row = []; field = ''; }
    else field += c;
  }
  if (quoted) throw new Error('Unclosed CSV quote');
  if (field || row.length) { row.push(field.replace(/\r$/, '')); rows.push(row); }
  const header = rows.shift() || [];
  if (new Set(header).size !== header.length) throw new Error('Duplicate CSV column');
  return rows.map((r, i) => { if (r.length !== header.length) throw new Error(`CSV row ${i + 2}: column count`); return Object.fromEntries(header.map((h, j) => [h, r[j]])); });
}
export const hash = (text) => createHash('sha256').update(text).digest('hex').slice(0, 12);
export const normalize = (text) => text.normalize('NFC').replace(/\s+/g, ' ').trim();
export function placeholders(text) {
  const tokens = []; let invalid = false;
  for (let i = 0; i < text.length; i++) {
    if ((text[i] === '{' || text[i] === '}') && text[i + 1] === text[i]) { i++; continue; }
    if (text[i] === '}') { invalid = true; continue; }
    if (text[i] !== '{') continue;
    const end = text.indexOf('}', i + 1);
    if (end < 0) { invalid = true; break; }
    const part = text.slice(i + 1, end);
    const m = /^(\d+|[A-Za-z_]\w*)(?:,\s*-?\d+)?(?::[^{}]+)?$/.exec(part);
    if (!m) invalid = true; else tokens.push(m[1]);
    i = end;
  }
  return { tokens: tokens.sort(), invalid };
}
export function validate(rows) {
  const errors = [], warnings = [], keys = new Set();
  for (const row of rows) {
    if (!/^[a-z][a-z0-9_]*(?:\.[a-z0-9_]+)+$/.test(row.key || '')) errors.push(`${row.key}: invalid key`);
    if (keys.has(row.key)) errors.push(`${row.key}: duplicate key`); keys.add(row.key);
    const base = placeholders(row.tr || '');
    for (const lang of languages) {
      const value = row[lang] || '', p = placeholders(value);
      if (!value.trim()) errors.push(`${row.key}/${lang}: missing translation`);
      if (p.invalid) errors.push(`${row.key}/${lang}: malformed placeholder`);
      if (JSON.stringify(p.tokens) !== JSON.stringify(base.tokens)) errors.push(`${row.key}/${lang}: placeholder count/name mismatch`);
      if (/\p{Cc}/u.test(value.replace(/[\n\r\t]/g, ''))) errors.push(`${row.key}/${lang}: control character`);
      const length = [...value].length, limit = /btn|order\.name/.test(row.key) ? 18 : /title/.test(row.key) ? 56 : 160;
      if (length > limit || (lang !== 'tr' && length > Math.max(12, [...(row.tr || '')].length * 1.5))) warnings.push({ key: row.key, lang, length, limit, reason: 'layout-review' });
    }
  }
  return { rows: rows.length, errors, warnings };
}
function decode(text, verbatim) {
  if (verbatim) return text.replace(/""/g, '"');
  return text.replace(/\\(u[0-9a-fA-F]{4}|U[0-9a-fA-F]{8}|x[0-9a-fA-F]{1,4}|[\\"'0abfnrtv])/g, (_, e) => {
    if (/^[uUx]/.test(e)) return String.fromCodePoint(parseInt(e.slice(1), 16));
    return ({'0':'\0',a:'\x07',b:'\b',f:'\f',n:'\n',r:'\r',t:'\t',v:'\x0b'})[e] ?? e;
  });
}
// Lexical scan: comments and character literals are excluded; interpolation stays a review candidate.
export function scanCSharp(source, file, knownRows = []) {
  const known = new Map();
  for (const row of knownRows) { const k = normalize(row.tr); if (!known.has(k)) known.set(k, []); known.get(k).push(row.key); }
  const result = []; let i = 0;
  while (i < source.length) {
    if (source.startsWith('//', i)) { i = source.indexOf('\n', i); if (i < 0) break; continue; }
    if (source.startsWith('/*', i)) { const end = source.indexOf('*/', i + 2); if (end < 0) break; i = end + 2; continue; }
    if (source[i] === "'") { i++; while (i < source.length) { if (source[i] === '\\') i += 2; else if (source[i++] === "'") break; } continue; }
    const start = i;
    const prefix = /^(?:\$@|@\$|@|\$)?"/.exec(source.slice(i));
    if (!prefix) { i++; continue; }
    const verbatim = prefix[0].includes('@'), interpolated = prefix[0].includes('$');
    i += prefix[0].length;
    const quoteStart = i - 1, rawCount = /^"{3,}/.exec(source.slice(quoteStart))?.[0].length;
    let raw = '', literalEnd;
    if (rawCount) {
      i = quoteStart + rawCount; literalEnd = source.indexOf('"'.repeat(rawCount), i);
      if (literalEnd < 0) throw new Error(`${file}: unclosed raw literal`);
      raw = source.slice(i, literalEnd); i = literalEnd + rawCount;
    } else {
      let depth = 0;
      while (i < source.length) {
        const c = source[i];
        if (interpolated && c === '{' && source[i + 1] !== '{') depth++;
        else if (interpolated && c === '}' && depth) depth--;
        if (depth > 0 && c === '"') { // expression strings such as $"{x.ToString(\"N0\")} kişi"
          raw += source[i++];
          while (i < source.length) { const a = source[i++]; raw += a; if (a === '\\') raw += source[i++]; else if (a === '"') break; }
          continue;
        }
        if (c === '"' && depth === 0) {
          if (verbatim && source[i + 1] === '"') { raw += '""'; i += 2; continue; }
          i++; break;
        }
        if (c === '\\' && !verbatim) { raw += source.slice(i, i + 2); i += 2; continue; }
        if (interpolated && (c === '{' || c === '}') && source[i + 1] === c && !depth) { raw += c + c; i += 2; continue; }
        raw += c; i++;
      }
    }
    const text = rawCount ? raw : decode(raw, verbatim), norm = normalize(text);
    const candidates = known.get(norm) || [];
    const context = source.slice(Math.max(0, source.lastIndexOf('\n', start) + 1), source.indexOf('\n', i) < 0 ? source.length : source.indexOf('\n', i)).trim();
    if (/NameRoster\.cs$/.test(file) || /\b(?:Debug\.(?:Log\w*)|Tooltip|Header)\s*\(/.test(context)) continue;
    if (!candidates.length && !/[çğıöşüÇĞİÖŞÜ]/.test(text) && !/\b(?:Silah|Mermi|Tim|Takip|Toplan|Tamam|Iptal|Zirh|Hasar|Can|Kask|Oyuncu|Devam|Kaydet)\b/i.test(text)) continue;
    if (!norm) continue;
    const segment = file.split('/').at(-1).replace(/\.cs$/, '').replace(/([a-z])([A-Z])/g, '$1_$2').toLowerCase();
    result.push({ id: hash(`${file}\0${text}`), file, line: source.slice(0, start).split('\n').length, text, interpolated, raw: !!rawCount, keys: candidates, proposedKey: candidates[0] || `ui.${segment}.${hash(text).slice(0,8)}`, status: candidates.length ? 'mapped' : 'review', context });
  }
  return result;
}
export function diffCatalog(entries, rows, previous = []) {
  const textSet = new Set(entries.map(e => normalize(e.text))), before = new Set(previous.map(e => e.id));
  const current = new Set(entries.map(e => e.id));
  return {
    newTexts: entries.filter(e => !e.keys.length),
    addedSinceSnapshot: previous.length ? entries.filter(e => !before.has(e.id)) : [],
    removedSinceSnapshot: previous.filter(e => !current.has(e.id)),
    unreferencedKeys: rows.filter(r => !textSet.has(normalize(r.tr))).map(r => r.key),
    note: 'Unreferenced keys may be composite/interpolated or used outside C#. Review before deletion. Candidates never change translations automatically.'
  };
}
