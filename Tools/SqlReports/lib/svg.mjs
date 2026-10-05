/** Inline SVG grafik yardımcıları (bağımlılıksız). */

function esc(s) {
  return String(s)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

export function barChart(series, { width = 560, height = 220, title = '', color = '#2f4a2c' } = {}) {
  const data = series ?? [];
  const max = Math.max(1, ...data.map((d) => Number(d.value) || 0));
  const padL = 48;
  const padB = 36;
  const padT = title ? 28 : 12;
  const padR = 12;
  const plotW = width - padL - padR;
  const plotH = height - padT - padB;
  const n = Math.max(1, data.length);
  const gap = 6;
  const barW = Math.max(8, (plotW - gap * (n - 1)) / n);

  const bars = data.map((d, i) => {
    const v = Number(d.value) || 0;
    const h = (v / max) * plotH;
    const x = padL + i * (barW + gap);
    const y = padT + plotH - h;
    return `<rect x="${x.toFixed(1)}" y="${y.toFixed(1)}" width="${barW.toFixed(1)}" height="${h.toFixed(1)}" fill="${color}" rx="2"/>
      <text x="${(x + barW / 2).toFixed(1)}" y="${height - 12}" text-anchor="middle" font-size="10" fill="#334">${esc(d.label)}</text>
      <text x="${(x + barW / 2).toFixed(1)}" y="${(y - 4).toFixed(1)}" text-anchor="middle" font-size="10" fill="#111">${v}</text>`;
  }).join('\n');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(title || 'bar chart')}">
  <rect width="100%" height="100%" fill="#f7f5ef"/>
  ${title ? `<text x="${padL}" y="18" font-size="13" font-weight="600" fill="#1a1a1a">${esc(title)}</text>` : ''}
  <line x1="${padL}" y1="${padT}" x2="${padL}" y2="${padT + plotH}" stroke="#bbb"/>
  <line x1="${padL}" y1="${padT + plotH}" x2="${width - padR}" y2="${padT + plotH}" stroke="#bbb"/>
  ${bars}
</svg>`;
}

export function lineChart(series, { width = 560, height = 220, title = '', color = '#E30A17' } = {}) {
  const data = series ?? [];
  const vals = data.map((d) => Number(d.value) || 0);
  const max = Math.max(1, ...vals);
  const min = Math.min(0, ...vals);
  const padL = 48;
  const padB = 36;
  const padT = title ? 28 : 12;
  const padR = 12;
  const plotW = width - padL - padR;
  const plotH = height - padT - padB;
  const n = Math.max(1, data.length - 1);

  const pts = data.map((d, i) => {
    const x = padL + (i / n) * plotW;
    const y = padT + plotH - ((Number(d.value) - min) / (max - min || 1)) * plotH;
    return { x, y, label: d.label, value: d.value };
  });

  const path = pts.map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  const dots = pts.map((p) =>
    `<circle cx="${p.x.toFixed(1)}" cy="${p.y.toFixed(1)}" r="3.5" fill="${color}"/>
     <text x="${p.x.toFixed(1)}" y="${height - 12}" text-anchor="middle" font-size="10" fill="#334">${esc(p.label)}</text>`
  ).join('\n');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(title || 'line chart')}">
  <rect width="100%" height="100%" fill="#f7f5ef"/>
  ${title ? `<text x="${padL}" y="18" font-size="13" font-weight="600" fill="#1a1a1a">${esc(title)}</text>` : ''}
  <line x1="${padL}" y1="${padT}" x2="${padL}" y2="${padT + plotH}" stroke="#bbb"/>
  <line x1="${padL}" y1="${padT + plotH}" x2="${width - padR}" y2="${padT + plotH}" stroke="#bbb"/>
  <path d="${path}" fill="none" stroke="${color}" stroke-width="2.5"/>
  ${dots}
</svg>`;
}

export function hBarChart(series, { width = 560, height = 280, title = '', color = '#3d5a80' } = {}) {
  const data = series ?? [];
  const max = Math.max(1, ...data.map((d) => Number(d.value) || 0));
  const padL = 110;
  const padB = 20;
  const padT = title ? 28 : 12;
  const padR = 48;
  const plotW = width - padL - padR;
  const rowH = Math.max(18, (height - padT - padB) / Math.max(1, data.length));

  const rows = data.map((d, i) => {
    const v = Number(d.value) || 0;
    const w = (v / max) * plotW;
    const y = padT + i * rowH;
    return `<text x="${padL - 8}" y="${(y + rowH * 0.65).toFixed(1)}" text-anchor="end" font-size="11" fill="#222">${esc(d.label)}</text>
      <rect x="${padL}" y="${(y + 4).toFixed(1)}" width="${w.toFixed(1)}" height="${(rowH - 8).toFixed(1)}" fill="${color}" rx="2"/>
      <text x="${(padL + w + 6).toFixed(1)}" y="${(y + rowH * 0.65).toFixed(1)}" font-size="11" fill="#111">${v}</text>`;
  }).join('\n');

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" role="img" aria-label="${esc(title || 'horizontal bar')}">
  <rect width="100%" height="100%" fill="#f7f5ef"/>
  ${title ? `<text x="12" y="18" font-size="13" font-weight="600" fill="#1a1a1a">${esc(title)}</text>` : ''}
  ${rows}
</svg>`;
}
