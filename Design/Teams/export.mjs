#!/usr/bin/env node
/**
 * HAREKÂT — Tim amblem + rütbe SVG → PNG dışa aktarım
 * Bağımlılık yok (npm paketi kullanmaz). Sistem araçları:
 *   1) rsvg-convert (librsvg)
 *   2) inkscape
 *   3) magick / convert (ImageMagick) — SVG desteği kurulumuna bağlı
 *
 * Kullanım:
 *   node Design/Teams/export.mjs
 *   node Design/Teams/export.mjs --sizes 64,128,256,512
 *   node Design/Teams/export.mjs --only emblems
 *   node Design/Teams/export.mjs --only ranks
 *
 * PNG mümkün değilse talimat yazdırır; SVG kaynaklar kalır.
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(__dirname, 'export');
const EMBLEMS = path.join(__dirname, 'emblems');
const RANKS = path.join(__dirname, 'ranks');

function parseArgs(argv) {
  const opts = { sizes: [64, 128, 256, 512], only: 'all' };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--sizes' && argv[i + 1]) {
      opts.sizes = argv[++i].split(',').map((s) => parseInt(s.trim(), 10)).filter(Boolean);
    } else if (a === '--only' && argv[i + 1]) {
      opts.only = argv[++i];
    } else if (a === '--help' || a === '-h') {
      opts.help = true;
    }
  }
  return opts;
}

function which(cmd) {
  const r = spawnSync(process.platform === 'win32' ? 'where' : 'which', [cmd], {
    encoding: 'utf8',
  });
  return r.status === 0 ? (r.stdout.trim().split(/\r?\n/)[0] || null) : null;
}

function listSvg(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir).filter((f) => f.endsWith('.svg')).sort();
}

function ensureDir(d) {
  fs.mkdirSync(d, { recursive: true });
}

function exportWithRsvg(bin, src, dest, size) {
  const r = spawnSync(bin, ['-w', String(size), '-h', String(size), '-o', dest, src], {
    encoding: 'utf8',
  });
  return r.status === 0;
}

function exportWithInkscape(bin, src, dest, size) {
  const r = spawnSync(
    bin,
    [src, `--export-filename=${dest}`, `--export-width=${size}`, `--export-height=${size}`],
    { encoding: 'utf8' },
  );
  return r.status === 0;
}

function exportWithMagick(bin, src, dest, size) {
  const r = spawnSync(bin, ['-background', 'none', '-resize', `${size}x${size}`, src, dest], {
    encoding: 'utf8',
  });
  return r.status === 0;
}

function printManual(toolHint) {
  console.log(`
PNG dışa aktarım aracı bulunamadı.

Kurulum (macOS örneği):
  brew install librsvg
  # veya
  brew install inkscape

Sonra tekrar:
  node Design/Teams/export.mjs

Elle (rsvg-convert):
  rsvg-convert -w 256 -h 256 -o out.png Design/Teams/emblems/kartal.svg

Elle (Inkscape CLI):
  inkscape Design/Teams/emblems/kartal.svg --export-filename=out.png --export-width=256 --export-height=256

${toolHint}
SVG kaynaklar Design/Teams/emblems ve Design/Teams/ranks altında kalmaya devam eder.
`);
}

function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.help) {
    console.log('Usage: node Design/Teams/export.mjs [--sizes 64,128,256,512] [--only emblems|ranks|all]');
    process.exit(0);
  }

  const rsvg = which('rsvg-convert');
  const inkscape = which('inkscape');
  const magick = which('magick') || which('convert');

  let exporter = null;
  let name = '';
  if (rsvg) {
    exporter = (src, dest, size) => exportWithRsvg(rsvg, src, dest, size);
    name = `rsvg-convert (${rsvg})`;
  } else if (inkscape) {
    exporter = (src, dest, size) => exportWithInkscape(inkscape, src, dest, size);
    name = `inkscape (${inkscape})`;
  } else if (magick) {
    exporter = (src, dest, size) => exportWithMagick(magick, src, dest, size);
    name = `ImageMagick (${magick})`;
  }

  const jobs = [];
  if (opts.only === 'all' || opts.only === 'emblems') {
    for (const f of listSvg(EMBLEMS)) jobs.push({ src: path.join(EMBLEMS, f), base: f.replace(/\.svg$/, '') });
  }
  if (opts.only === 'all' || opts.only === 'ranks') {
    for (const f of listSvg(RANKS)) jobs.push({ src: path.join(RANKS, f), base: f.replace(/\.svg$/, '') });
  }

  if (!exporter) {
    printManual(`Beklenen dosya sayısı: ${jobs.length} SVG × ${opts.sizes.length} boyut.`);
    process.exit(2);
  }

  console.log(`Araç: ${name}`);
  console.log(`Boyutlar: ${opts.sizes.join(', ')}`);
  console.log(`Dosya: ${jobs.length} SVG`);

  let ok = 0;
  let fail = 0;
  for (const size of opts.sizes) {
    const dir = path.join(OUT, String(size));
    ensureDir(dir);
    for (const job of jobs) {
      const dest = path.join(dir, `${job.base}.png`);
      if (exporter(job.src, dest, size)) {
        ok++;
        process.stdout.write('.');
      } else {
        fail++;
        process.stdout.write('x');
        console.error(`\nHata: ${job.src} → ${dest}`);
      }
    }
  }
  console.log(`\nTamam: ${ok} PNG, hata: ${fail}`);
  console.log(`Çıktı: ${OUT}/{${opts.sizes.join(',')}}/`);
  process.exit(fail ? 1 : 0);
}

main();
