import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { build } from '../build.mjs';

const wikiRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
const dist = join(wikiRoot, 'dist');

function walkHtml(dir, acc = []) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walkHtml(full, acc);
    else if (name.endsWith('.html')) acc.push(full);
  }
  return acc;
}

function extractHrefs(html) {
  const out = [];
  const re = /(?:href|src)=["']([^"'#]+)["']/gi;
  let m;
  while ((m = re.exec(html))) {
    const url = m[1];
    if (/^(https?:|mailto:|data:)/i.test(url)) continue;
    out.push(url);
  }
  return out;
}

function resolveLocal(fromFile, href) {
  if (href.startsWith('/')) return join(dist, href.replace(/^\//, ''));
  return join(dirname(fromFile), href);
}

test('build produces required pages and assets', async () => {
  const result = await build();
  assert.ok(result.pages.length >= 40, `expected many pages, got ${result.pages.length}`);
  assert.equal(result.weapons.length, 10);
  assert.equal(result.ranks.length, 19);
  assert.equal(result.roles.length, 7);
  assert.ok(result.locations >= 10);

  const required = [
    'index.html',
    'baslangic.html',
    'kontroller.html',
    'karsilastir.html',
    'harita.html',
    'yama-notlari.html',
    'web.config',
    'silahlar/index.html',
    'silahlar/mpt76.html',
    'rutbeler/index.html',
    'rutbeler/rank-0.html',
    'gorevler/index.html',
    'gorevler/leader.html',
    'lokasyonlar/index.html',
    'lokasyonlar/kuzgun-koyu.html',
    'rehber/index.html',
    'rehber/05-silahlar.html',
    'en/index.html',
    'assets/search-index.json',
    'assets/pages.json',
    'assets/style.css',
    'assets/app.js',
    'assets/maps/kuzgun-vadisi-pafta.svg',
  ];
  for (const rel of required) {
    assert.ok(existsSync(join(dist, rel)), `missing ${rel}`);
  }
});

test('no broken internal links or missing declared pages', async () => {
  if (!existsSync(join(dist, 'index.html'))) await build();
  const pages = JSON.parse(readFileSync(join(dist, 'assets/pages.json'), 'utf8'));
  for (const rel of pages) {
    assert.ok(existsSync(join(dist, rel)), `declared page missing: ${rel}`);
  }

  const htmlFiles = walkHtml(dist);
  const broken = [];
  for (const file of htmlFiles) {
    const html = readFileSync(file, 'utf8');
    for (const href of extractHrefs(html)) {
      const clean = href.split('?')[0];
      if (!clean || clean.startsWith('#')) continue;
      const target = resolveLocal(file, clean);
      if (!existsSync(target)) {
        broken.push(`${relative(dist, file)} → ${href}`);
      }
    }
  }
  assert.deepEqual(broken, [], `broken links:\n${broken.slice(0, 30).join('\n')}`);
});

test('search index covers weapons and locations', () => {
  const index = JSON.parse(readFileSync(join(dist, 'assets/search-index.json'), 'utf8'));
  assert.ok(index.some(x => x.type === 'silah' && /MPT-76/i.test(x.title)));
  assert.ok(index.some(x => x.type === 'lokasyon'));
  assert.ok(index.some(x => x.type === 'rütbe'));
  assert.ok(index.every(x => x.url && x.title));
});
