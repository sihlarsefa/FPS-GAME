#!/usr/bin/env node
/**
 * HAREKÂT MSSQL analitik KPI raporu.
 * Bağlantı: HAREKAT_SQL_CONNECTION veya MSSQL_CONNECTION
 * Bağlantı yoksa / hata olursa örnek veriyle tek HTML üretir.
 *
 * Kullanım:
 *   npm install
 *   npm run report
 *   node report.mjs --out out/kpi.html
 */
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderHtmlReport } from './lib/html.mjs';
import { resolvePayload, ROOT } from './lib/runner.mjs';

function parseArgs(argv) {
  const out = { out: path.join(ROOT, 'out', 'kpi-report.html') };
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--out' && argv[i + 1]) {
      out.out = path.resolve(argv[++i]);
    }
  }
  return out;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const connectionString =
    process.env.HAREKAT_SQL_CONNECTION ||
    process.env.MSSQL_CONNECTION ||
    process.env.SQL_CONNECTION_STRING ||
    '';

  const payload = await resolvePayload(connectionString);
  const html = renderHtmlReport(payload);

  await fs.mkdir(path.dirname(args.out), { recursive: true });
  await fs.writeFile(args.out, html, 'utf8');

  console.log(`Rapor yazıldı: ${args.out}`);
  console.log(`Mod: ${payload.mode} · KPI DAU=${payload.kpis.dau} WAU=${payload.kpis.wau} MAU=${payload.kpis.mau}`);
}

const isMain = process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
if (isMain) {
  main().catch((err) => {
    console.error(err);
    process.exitCode = 1;
  });
}

export { main };
