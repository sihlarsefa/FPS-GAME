import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  parseWeapons, parseRanks, parseItems, parseRoles, verifyBalance, parseCsv,
} from '../lib/catalogs.mjs';
import { markdown, slug } from '../lib/markdown.mjs';

const root = join(dirname(fileURLToPath(import.meta.url)), '../..');

test('parseWeapons reads WeaponCatalog', () => {
  const weapons = parseWeapons(readFileSync(join(root, 'Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs'), 'utf8'));
  assert.equal(weapons.length, 10);
  assert.ok(weapons.every(w => w.display_name && w.damage > 0 && w.rpm > 0));
});

test('verifyBalance matches BalanceCalc snapshot', () => {
  const weapons = parseWeapons(readFileSync(join(root, 'Assets/_Project/Scripts/Application/Catalogs/WeaponCatalog.cs'), 'utf8'));
  const snap = JSON.parse(readFileSync(join(root, 'Tools/BalanceCalc/out/catalog.json'), 'utf8'));
  verifyBalance(weapons, snap);
});

test('parseRanks / roles / items', () => {
  const ranks = parseRanks(readFileSync(join(root, 'Assets/_Project/Scripts/Application/Catalogs/RankCatalog.cs'), 'utf8'));
  const roles = parseRoles(readFileSync(join(root, 'Assets/_Project/Scripts/Application/Catalogs/LoadoutCatalog.cs'), 'utf8'));
  const items = parseItems(readFileSync(join(root, 'Assets/_Project/Scripts/Application/Catalogs/ItemCatalog.cs'), 'utf8'));
  assert.equal(ranks.length, 19);
  assert.equal(roles.length, 7);
  assert.ok(items.length >= 10);
});

test('parseCsv reads ttk', () => {
  const rows = parseCsv(readFileSync(join(root, 'Tools/BalanceCalc/out/ttk.csv'), 'utf8'));
  assert.ok(rows.length > 100);
  assert.ok(rows[0].weapon_id);
});

test('markdown escapes raw HTML and keeps links', () => {
  const html = markdown('Merhaba **dünya** ve [MPT](Mpt76) <script>', url => (url === 'Mpt76' ? '/silahlar/mpt76.html' : null));
  assert.match(html, /<strong>dünya<\/strong>/);
  assert.match(html, /href="\/silahlar\/mpt76\.html"/);
  assert.match(html, /&lt;script&gt;/);
  assert.equal(slug('MPT-76'), 'mpt-76');
});
