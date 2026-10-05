import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { languages, parseCsv, scanCSharp, diffCatalog, validate } from './lib/core.mjs';
import { fontCoverage, legacyRuntimeLikelyGaps } from './lib/font.mjs';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const loc = path.join(root, 'Localization'), reports = path.join(loc, 'reports/v2');
const command = process.argv[2] || 'help';
const option = name => { const i = process.argv.indexOf(name); if (i >= 0 && (!process.argv[i + 1] || process.argv[i + 1].startsWith('--'))) throw new Error(`Missing ${name} value`); return i < 0 ? undefined : process.argv[i + 1]; };
const read = p => fs.readFile(p, 'utf8');
const json = async (p, value) => { await fs.mkdir(path.dirname(p), { recursive: true }); await fs.writeFile(p, JSON.stringify(value, null, 2) + '\n'); };
async function walk(dir) { const out = []; for (const d of await fs.readdir(dir, { withFileTypes: true })) { if (d.isSymbolicLink()) continue; const p = path.join(dir, d.name); if (d.isDirectory()) out.push(...await walk(p)); else if (d.isFile() && p.endsWith('.cs')) out.push(p); } return out.sort(); }
async function scan(rows) { const entries = []; for (const file of await walk(path.join(root, 'Assets'))) entries.push(...scanCSharp(await read(file), path.relative(root, file).split(path.sep).join('/'), rows)); return entries; }
async function validation(rows) { const result = validate(rows); await json(path.join(reports, 'validation.json'), result); if (result.errors.length) throw new Error(result.errors.join('\n')); return result; }
async function exports(rows) {
  await validation(rows);
  for (const lang of languages) { const table = Object.fromEntries(rows.map(r => [r.key, r[lang]])); await json(path.join(loc, `unity/strings_${lang}.json`), table); await json(path.join(loc, `tables/${lang}.json`), table); }
  await json(path.join(loc, 'unity/manifest.json'), { schemaVersion: 2, languages, count: rows.length, source: 'Localization/strings.csv', rtl: ['ar'], format: 'composite/named placeholders; adapter must preserve formatting', translationStatus: 'Phase 1 translations retained; human review required before release' });
}
async function extraction(rows) {
  const entries = await scan(rows); let previous = [];
  try { previous = JSON.parse(await read(path.join(reports, 'extracted.json'))).entries; } catch(e) { if (e.code !== 'ENOENT') throw e; }
  await json(path.join(reports, 'diff.json'), diffCatalog(entries, rows, previous));
  await json(path.join(reports, 'extracted.json'), { schemaVersion: 2, source: 'Assets/**/*.cs (read only)', candidateCount: entries.length, entries });
  const escape = s => String(s).replace(/\|/g, '\\|').replace(/[\r\n]/g, ' ');
  const lines = ['# Unity metin geçiş listesi v2', '', 'Otomatik aday envanteri. `mapped` mevcut CSV eşleşmesi; `review` anahtar önerisidir, çeviri değildir. Interpolated ifadeler elle `{0}` vb. argümanlara ayrılmalı. Aynı metnin birden fazla anahtarı varsa bağlama göre seçim yapılır. Bu dosya kaynak kodunu değiştirmez.', '', '| Dosya:satır | Durum | Anahtar / aday | String |', '|---|---|---|---|', ...entries.map(e => `| ${escape(e.file)}:${e.line} | ${e.status}${e.interpolated ? ' / interpolation' : ''} | ${escape(e.keys.join(', ') || e.proposedKey)} | ${escape(e.text)} |`)];
  await fs.writeFile(path.join(loc, 'UNITY_STRING_MAP_V2.md'), lines.join('\n') + '\n');
  return entries;
}
async function fonts(rows) {
  const characters = [...new Set(rows.flatMap(row => languages.flatMap(l => [...row[l]])))].filter(c => !/\s/.test(c)).sort((a,b) => a.codePointAt(0) - b.codePointAt(0));
  const font = option('--font');
  const heuristic = legacyRuntimeLikelyGaps(characters);
  let verified = null;
  if (font) verified = { font: path.resolve(font), status: 'verified-file', ...fontCoverage(await fs.readFile(path.resolve(font)), characters) };
  const result = {
    font: font ? path.resolve(font) : 'LegacyRuntime.ttf (Unity built-in; binary not shipped in Hub install)',
    status: font ? 'verified-file' : 'heuristic-legacy-runtime',
    checked: font ? verified.checked : characters.length,
    missing: font ? verified.missing : heuristic.likelyMissing,
    covered: font ? verified.covered : characters.length - heuristic.likelyMissingCount,
    legacyRuntimeHeuristic: heuristic,
    proxyNote: 'Pass --font path/to.ttf for exact cmap. Unity 6 Hub install does not expose LegacyRuntime.ttf as a standalone file.',
    required: characters.map(c => ({ char: c, codepoint: `U+${c.codePointAt(0).toString(16).toUpperCase().padStart(4, '0')}` }))
  };
  await json(path.join(reports, 'font-coverage.json'), result); return result;
}
try {
  if (command === 'help') console.log('LocTool: extract | diff | validate [--strict] | export | font [--font exact.ttf] | build');
  else {
    const rows = parseCsv(await read(path.join(loc, 'strings.csv')));
    if (command === 'extract' || command === 'diff') { const entries = await extraction(rows); console.log(`${entries.length} C# candidates; ${entries.filter(e => !e.keys.length).length} need review. See Localization/reports/v2.`); }
    else if (command === 'validate') { const r = await validation(rows); console.log(`${r.rows} keys, 0 errors, ${r.warnings.length} layout warnings.`); if (process.argv.includes('--strict') && r.warnings.length) process.exitCode = 1; }
    else if (command === 'export') { await exports(rows); console.log(`Exported ${rows.length} keys × ${languages.length} languages.`); }
    else if (command === 'font') { const r = await fonts(rows); console.log(`${r.status}; ${r.checked} characters checked; missing=${r.missing?.length ?? 'unknown'}`); }
    else if (command === 'build') { await exports(rows); const entries = await extraction(rows); const r = await fonts(rows); console.log(`Built ${rows.length} keys × 5 languages; ${entries.length} source candidates; font ${r.status}.`); }
    else throw new Error(`Unknown command: ${command}`);
  }
} catch (error) { console.error(error.message); process.exitCode = 1; }
