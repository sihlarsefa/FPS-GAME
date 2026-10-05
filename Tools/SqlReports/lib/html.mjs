import { barChart, lineChart, hBarChart } from './svg.mjs';

function esc(s) {
  return String(s ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function kpiCard(label, value, suffix = '') {
  return `<article class="kpi"><div class="kpi-label">${esc(label)}</div><div class="kpi-value">${esc(value)}${esc(suffix)}</div></article>`;
}

function tableHtml(headers, rows) {
  const th = headers.map((h) => `<th>${esc(h)}</th>`).join('');
  const body = rows.map((r) => `<tr>${r.map((c) => `<td>${esc(c)}</td>`).join('')}</tr>`).join('');
  return `<table><thead><tr>${th}</tr></thead><tbody>${body}</tbody></table>`;
}

/** Tek dosyalık HTML KPI raporu üretir. */
export function renderHtmlReport(payload) {
  const k = payload.kpis;
  const s = payload.series;
  const modeBadge = payload.mode === 'live' ? 'CANLI MSSQL' : 'ÖRNEK VERİ';
  const generated = esc(payload.generatedAt);

  const topSquadRows = (payload.tables?.topSquads ?? []).map((r) => [
    r.squad, r.matches, `${r.winPct}%`, r.avgPlace
  ]);
  const weaponRows = (payload.tables?.weaponCompare ?? []).map((r) => [
    r.weapon, r.expectedTtk, r.actualTtk, r.verdict
  ]);

  return `<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>HAREKÂT — Analitik KPI Raporu</title>
<style>
:root {
  --bg: #e8e4d9;
  --panel: #f7f5ef;
  --ink: #1a1a1a;
  --muted: #5a5a5a;
  --olive: #2f4a2c;
  --red: #E30A17;
  --amber: #c47a12;
  --blue: #3d5a80;
}
* { box-sizing: border-box; }
body {
  margin: 0; font-family: "Segoe UI", Tahoma, sans-serif;
  background: linear-gradient(160deg, #d9d3c3 0%, var(--bg) 40%, #cfd6c4 100%);
  color: var(--ink); line-height: 1.45;
}
header {
  padding: 28px 32px 18px;
  border-bottom: 3px solid var(--red);
  background: rgba(47,74,44,.92); color: #f5f2ea;
}
header h1 { margin: 0 0 6px; font-size: 1.6rem; letter-spacing: .04em; }
header p { margin: 0; opacity: .9; font-size: .95rem; }
.badge {
  display: inline-block; margin-top: 10px; padding: 3px 10px;
  border: 1px solid #f5f2ea; border-radius: 2px; font-size: .75rem; letter-spacing: .08em;
}
main { padding: 24px 32px 48px; max-width: 1200px; margin: 0 auto; }
.grid-kpi {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 12px; margin-bottom: 28px;
}
.kpi {
  background: var(--panel); border: 1px solid #c9c2b0; padding: 14px 12px;
  border-top: 3px solid var(--olive);
}
.kpi-label { font-size: .72rem; text-transform: uppercase; letter-spacing: .06em; color: var(--muted); }
.kpi-value { font-size: 1.45rem; font-weight: 700; margin-top: 4px; }
.section {
  background: var(--panel); border: 1px solid #c9c2b0; padding: 16px 18px; margin-bottom: 18px;
}
.section h2 {
  margin: 0 0 12px; font-size: 1.05rem; color: var(--olive);
  border-bottom: 1px solid #ddd6c6; padding-bottom: 6px;
}
.charts {
  display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px;
}
.chart-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .9rem; }
th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid #ddd6c6; }
th { background: #ebe6d8; font-size: .75rem; text-transform: uppercase; letter-spacing: .04em; }
footer { padding: 16px 32px 32px; color: var(--muted); font-size: .8rem; max-width: 1200px; margin: 0 auto; }
@media print {
  body { background: #fff; }
  header { background: #2f4a2c; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
}
</style>
</head>
<body>
<header>
  <h1>HAREKÂT Analitik KPI</h1>
  <p>Tatbikat operasyon metrikleri · üretilme: ${generated}</p>
  <span class="badge">${esc(modeBadge)}</span>
</header>
<main>
  <section class="grid-kpi" aria-label="Özet KPI">
    ${kpiCard('DAU', k.dau)}
    ${kpiCard('WAU', k.wau)}
    ${kpiCard('MAU', k.mau)}
    ${kpiCard('Çevrimiçi', k.onlineNow)}
    ${kpiCard('D1 tutma', k.retentionD1, '%')}
    ${kpiCard('D7 tutma', k.retentionD7, '%')}
    ${kpiCard('D30 tutma', k.retentionD30, '%')}
    ${kpiCard('Ort. maç', k.avgMatchDurationMin, ' dk')}
    ${kpiCard('Ort. bekleme', k.avgMatchWaitSec, ' sn')}
    ${kpiCard('Filo doluluk', k.fleetOccupancyPct, '%')}
    ${kpiCard('Şüphe (7g)', k.suspectReports7d)}
  </section>

  <section class="section">
    <h2>Aktif oyuncu ve elde tutma</h2>
    <div class="charts">
      <div class="chart-wrap">${lineChart(s.dauTrend, { title: 'Son 7 gün DAU', color: '#E30A17' })}</div>
      <div class="chart-wrap">${barChart([
        { label: 'D1', value: k.retentionD1 },
        { label: 'D7', value: k.retentionD7 },
        { label: 'D30', value: k.retentionD30 }
      ], { title: 'Elde tutma %', color: '#2f4a2c' })}</div>
    </div>
  </section>

  <section class="section">
    <h2>Maç süresi ve eşleştirme</h2>
    <div class="charts">
      <div class="chart-wrap">${barChart(s.matchDuration, { title: 'Maç süresi dağılımı', color: '#c47a12' })}</div>
      <div class="chart-wrap">${barChart(s.waitBuckets, { title: 'Bekleme süresi', color: '#3d5a80' })}</div>
    </div>
  </section>

  <section class="section">
    <h2>Silah öldürmeleri ve rütbe</h2>
    <div class="charts">
      <div class="chart-wrap">${hBarChart(s.weapons, { title: 'Silah öldürme (30g)', color: '#2f4a2c', height: 320 })}</div>
      <div class="chart-wrap">${barChart(s.ranks, { title: 'Rütbe grupları', color: '#5c4033' })}</div>
    </div>
  </section>

  <section class="section">
    <h2>Tim yerleşimi, sunucu, hile şüphesi</h2>
    <div class="charts">
      <div class="chart-wrap">${barChart(s.placement, { title: 'Yerleşim dağılımı', color: '#3d5a80' })}</div>
      <div class="chart-wrap">${barChart(s.servers, { title: 'Bölge doluluk %', color: '#2f4a2c' })}</div>
      <div class="chart-wrap">${lineChart(s.suspicionTrend, { title: 'Şüphe raporları (hafta)', color: '#E30A17' })}</div>
    </div>
  </section>

  <section class="section">
    <h2>En iyi timler (örnek / canlı)</h2>
    ${tableHtml(['Tim', 'Maç', 'Galibiyet %', 'Ort. sıra'], topSquadRows)}
  </section>

  <section class="section">
    <h2>Silah denge: BalanceCalc vs telemetri</h2>
    ${tableHtml(['Silah', 'Beklenen TTK', 'Gerçek TTK', 'Sonuç'], weaponRows)}
  </section>
</main>
<footer>
  HAREKÂT SqlReports · salt okunur T-SQL · Tools/SqlReports · bağlantı: ${esc(payload.connection || 'yok (örnek)')}
</footer>
</body>
</html>`;
}
