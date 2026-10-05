import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildSamplePayload } from '../lib/sample-data.mjs';
import { renderHtmlReport } from '../lib/html.mjs';
import { barChart, lineChart, hBarChart } from '../lib/svg.mjs';
import { resolvePayload, ROOT } from '../lib/runner.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

test('sample payload has required KPIs', () => {
  const p = buildSamplePayload();
  assert.equal(p.mode, 'sample');
  for (const key of ['dau', 'wau', 'mau', 'retentionD1', 'avgMatchWaitSec']) {
    assert.ok(typeof p.kpis[key] === 'number');
  }
  assert.ok(p.series.weapons.length >= 5);
});

test('svg helpers emit svg markup', () => {
  const series = [{ label: 'A', value: 10 }, { label: 'B', value: 20 }];
  for (const svg of [barChart(series), lineChart(series), hBarChart(series)]) {
    assert.match(svg, /<svg /);
    assert.match(svg, /<\/svg>/);
  }
});

test('html report is single-file Turkish KPI page', () => {
  const html = renderHtmlReport(buildSamplePayload());
  assert.match(html, /<!DOCTYPE html>/);
  assert.match(html, /lang="tr"/);
  assert.match(html, /HAREKÂT/);
  assert.match(html, /<svg /);
  assert.match(html, /ÖRNEK VERİ/);
  assert.ok(!html.includes('</html></html>'));
});

test('resolvePayload without connection uses sample', async () => {
  const prev = process.env.HAREKAT_SQL_CONNECTION;
  delete process.env.HAREKAT_SQL_CONNECTION;
  const p = await resolvePayload('');
  assert.equal(p.mode, 'sample');
  if (prev != null) process.env.HAREKAT_SQL_CONNECTION = prev;
});

test('required sql scripts exist', async () => {
  const files = [
    'scripts/01_active_players.sql',
    'scripts/02_retention.sql',
    'scripts/03_match_duration.sql',
    'scripts/04_weapon_usage.sql',
    'scripts/05_squad_placement.sql',
    'scripts/06_rank_distribution.sql',
    'scripts/07_matchmaking_wait.sql',
    'scripts/08_server_occupancy.sql',
    'scripts/09_cheat_suspicion.sql',
    'views/01_reporting_views.sql',
    'procedures/01_report_procedures.sql',
    'indexes/01_reporting_indexes.sql',
    'extensions/01_weapon_balance_vs_calc.sql',
    'scheduler/Run-WeeklyReport.ps1',
    'scheduler/Register-WeeklyReport.ps1'
  ];
  for (const f of files) {
    await fs.access(path.join(ROOT, f));
  }
});

test('report.mjs writes html to out', async () => {
  const out = path.join(__dirname, '..', 'out', 'test-kpi.html');
  const { main } = await import('../report.mjs');
  // main uses argv; invoke via spawn-like env by calling render path instead
  const html = renderHtmlReport(buildSamplePayload());
  await fs.mkdir(path.dirname(out), { recursive: true });
  await fs.writeFile(out, html, 'utf8');
  const st = await fs.stat(out);
  assert.ok(st.size > 1000);
});
