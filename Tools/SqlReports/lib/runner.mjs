import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildSamplePayload } from './sample-data.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const ROOT = path.resolve(__dirname, '..');

function num(row, ...keys) {
  for (const k of keys) {
    if (row && row[k] != null && row[k] !== '') return Number(row[k]);
  }
  return 0;
}

function pickMetric(rows, metric) {
  const hit = (rows ?? []).find((r) => String(r.Metric ?? r.metric).toLowerCase() === metric);
  return hit ? num(hit, 'Value', 'value') : 0;
}

/** Çoklu result set: mssql recordsets dizisi. */
export async function loadSqlFile(rel) {
  return fs.readFile(path.join(ROOT, rel), 'utf8');
}

export async function tryConnect(connectionString) {
  if (!connectionString) return null;
  try {
    const sql = await import('mssql');
    const pool = await sql.default.connect(connectionString);
    return { sql: sql.default, pool };
  } catch (err) {
    console.warn('[SqlReports] Bağlantı başarısız, örnek veriye düşülüyor:', err.message);
    return null;
  }
}

async function queryAll(pool, sqlText) {
  const req = pool.request();
  const result = await req.query(sqlText);
  const sets = result.recordsets?.length ? result.recordsets : [result.recordset ?? []];
  return sets;
}

function mapBuckets(rows, labelKey, valueKey) {
  return (rows ?? []).map((r) => ({
    label: String(r[labelKey] ?? r.label ?? '?'),
    value: num(r, valueKey, 'value', 'MatchCount', 'TicketCount', 'PlayerCount', 'TeamSlots', 'ActivePlayers')
  }));
}

/**
 * Canlı sorgulardan KPI payload üretir; eksik telemetri tablolarında örnek seriye düşer.
 */
export async function buildLivePayload(pool) {
  const sample = buildSamplePayload();
  const payload = {
    ...sample,
    mode: 'live',
    generatedAt: new Date().toISOString(),
    connection: 'HAREKAT_SQL_CONNECTION',
    series: { ...sample.series },
    tables: { ...sample.tables },
    kpis: { ...sample.kpis }
  };

  try {
    const activeSql = await loadSqlFile('scripts/01_active_players.sql');
    const [metrics, trend] = await queryAll(pool, activeSql);
    payload.kpis.dau = pickMetric(metrics, 'dau') || payload.kpis.dau;
    payload.kpis.wau = pickMetric(metrics, 'wau') || payload.kpis.wau;
    payload.kpis.mau = pickMetric(metrics, 'mau') || payload.kpis.mau;
    payload.kpis.onlineNow = pickMetric(metrics, 'online_now') || payload.kpis.onlineNow;
    if (trend?.length) {
      payload.series.dauTrend = trend.slice(-7).map((r) => ({
        label: String(r.ActivityDate).slice(5, 10),
        value: num(r, 'ActivePlayers')
      }));
    }
  } catch (e) {
    console.warn('[SqlReports] 01_active_players:', e.message);
  }

  try {
    const retSql = await loadSqlFile('scripts/02_retention.sql');
    const sets = await queryAll(pool, retSql);
    const summary = sets[1]?.[0];
    if (summary) {
      payload.kpis.retentionD1 = num(summary, 'OverallPctD1');
      payload.kpis.retentionD7 = num(summary, 'OverallPctD7');
      payload.kpis.retentionD30 = num(summary, 'OverallPctD30');
    }
  } catch (e) {
    console.warn('[SqlReports] 02_retention:', e.message);
  }

  try {
    const durSql = await loadSqlFile('scripts/03_match_duration.sql');
    const [buckets, summary] = await queryAll(pool, durSql);
    if (buckets?.length) payload.series.matchDuration = mapBuckets(buckets, 'Bucket', 'MatchCount');
    if (summary?.[0]) payload.kpis.avgMatchDurationMin = +(num(summary[0], 'AvgDurationSec') / 60).toFixed(1);
  } catch (e) {
    console.warn('[SqlReports] 03_match_duration:', e.message);
  }

  try {
    const wSql = await loadSqlFile('scripts/04_weapon_usage.sql');
    const [rows] = await queryAll(pool, wSql);
    if (rows?.length && !rows[0].Status) {
      payload.series.weapons = rows.slice(0, 10).map((r) => ({
        label: String(r.WeaponId),
        value: num(r, 'KillCount')
      }));
    }
  } catch (e) {
    console.warn('[SqlReports] 04_weapon_usage:', e.message);
  }

  try {
    const sSql = await loadSqlFile('scripts/05_squad_placement.sql');
    const [top, hist] = await queryAll(pool, sSql);
    if (top?.length) {
      payload.tables.topSquads = top.slice(0, 10).map((r) => ({
        squad: r.SquadName,
        matches: num(r, 'MatchesPlayed'),
        winPct: num(r, 'WinRatePct'),
        avgPlace: num(r, 'AvgPlacement')
      }));
    }
    if (hist?.length) {
      payload.series.placement = hist.map((r) => ({
        label: String(r.Placement),
        value: num(r, 'TeamSlots')
      }));
    }
  } catch (e) {
    console.warn('[SqlReports] 05_squad_placement:', e.message);
  }

  try {
    const rSql = await loadSqlFile('scripts/06_rank_distribution.sql');
    const [ranks] = await queryAll(pool, rSql);
    if (ranks?.length) {
      const groups = [
        { label: 'Er', codes: [0] },
        { label: 'Onbaşı', codes: [1] },
        { label: 'Çavuş', codes: [2, 3] },
        { label: 'Uzman', codes: [4, 5] },
        { label: 'Astsubay', codes: [6, 7, 8, 9, 10, 11] },
        { label: 'Subay', codes: [12, 13, 14, 15] },
        { label: 'Üst subay', codes: [16, 17, 18] }
      ];
      payload.series.ranks = groups.map((g) => ({
        label: g.label,
        value: ranks.filter((r) => g.codes.includes(num(r, 'RankCode'))).reduce((a, r) => a + num(r, 'PlayerCount'), 0)
      }));
    }
  } catch (e) {
    console.warn('[SqlReports] 06_rank_distribution:', e.message);
  }

  try {
    const mSql = await loadSqlFile('scripts/07_matchmaking_wait.sql');
    const [buckets, byRegion] = await queryAll(pool, mSql);
    if (buckets?.length) payload.series.waitBuckets = mapBuckets(buckets, 'Bucket', 'TicketCount');
    if (byRegion?.length) {
      const avg = byRegion.reduce((a, r) => a + num(r, 'AvgWaitSec'), 0) / byRegion.length;
      payload.kpis.avgMatchWaitSec = Math.round(avg);
    }
  } catch (e) {
    console.warn('[SqlReports] 07_matchmaking_wait:', e.message);
  }

  try {
    const gSql = await loadSqlFile('scripts/08_server_occupancy.sql');
    const [, fleet] = await queryAll(pool, gSql);
    if (fleet?.length) {
      payload.series.servers = fleet.map((r) => ({
        label: String(r.Region),
        value: num(r, 'FleetOccupancyPct')
      }));
      const totalP = fleet.reduce((a, r) => a + num(r, 'TotalPlayers'), 0);
      const totalC = fleet.reduce((a, r) => a + num(r, 'TotalCapacity'), 0);
      if (totalC > 0) payload.kpis.fleetOccupancyPct = +((100 * totalP) / totalC).toFixed(1);
    }
  } catch (e) {
    console.warn('[SqlReports] 08_server_occupancy:', e.message);
  }

  try {
    const cSql = await loadSqlFile('scripts/09_cheat_suspicion.sql');
    const sets = await queryAll(pool, cSql);
    const last = sets[sets.length - 1] ?? [];
    if (last.length && !last[0].Status) {
      payload.series.suspicionTrend = last.slice(-7).map((r) => ({
        label: String(r.DayUtc).slice(5, 10),
        value: num(r, 'SuspectCount', 'Reports')
      }));
      payload.kpis.suspectReports7d = last.slice(-7).reduce((a, r) => a + num(r, 'SuspectCount', 'Reports'), 0);
    }
  } catch (e) {
    console.warn('[SqlReports] 09_cheat_suspicion:', e.message);
  }

  return payload;
}

export async function resolvePayload(connectionString) {
  const conn = await tryConnect(connectionString);
  if (!conn) return buildSamplePayload();
  try {
    const payload = await buildLivePayload(conn.pool);
    await conn.pool.close();
    return payload;
  } catch (err) {
    console.warn('[SqlReports] Canlı rapor hatası:', err.message);
    try { await conn.pool.close(); } catch { /* ignore */ }
    return buildSamplePayload();
  }
}
