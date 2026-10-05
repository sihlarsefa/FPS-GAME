#!/usr/bin/env node
/**
 * layout.json doğrulayıcı — lokasyon çakışması, yol eğimi, harita sınırları.
 * Kullanım: node validate.mjs [AyazGecidi|MaviLiman|…]
 * Argümansız: Design/Maps/v2 altındaki tüm layout.json dosyaları.
 */
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.dirname(fileURLToPath(import.meta.url));
const KIND_MAX = 9;
const TIER_MAX = 3;
const GRADE_LIMIT = 0.25;

function dist(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function sampleHeights(layout) {
  /** Tasarım tahmini: lokasyon TargetHeight örnekleri, IDS benzeri basit alan. */
  const locs = layout.Locations.filter(l => typeof l.TargetHeight === 'number' && l.TargetHeight >= 0);
  return (x, z) => {
    if (locs.length === 0) return layout.WaterLevel + 20;
    let num = 0;
    let den = 0;
    for (const l of locs) {
      const d2 = (x - l.Center.x) ** 2 + (z - l.Center.y) ** 2 + 1;
      const w = 1 / d2;
      num += w * l.TargetHeight;
      den += w;
    }
    return Math.min(layout.MaxHeight - 1, Math.max(layout.WaterLevel + 1, num / den));
  };
}

function roadGrade(road, heightAt) {
  let worst = 0;
  let worstSeg = null;
  const pts = road.Points;
  for (let i = 1; i < pts.length; i++) {
    const a = pts[i - 1];
    const b = pts[i];
    const run = dist(a, b);
    if (run < 0.5) continue;
    const rise = Math.abs(heightAt(b.x, b.y) - heightAt(a.x, a.y));
    const g = rise / run;
    if (g > worst) {
      worst = g;
      worstSeg = { from: a, to: b, run, rise, grade: g };
    }
  }
  return { worst, worstSeg };
}

function validateLayout(layout, label) {
  const errors = [];
  const warnings = [];

  for (const key of ['HalfSize', 'MaxHeight', 'WaterLevel', 'Locations', 'Roads', 'Lakes', 'Rivers']) {
    if (!(key in layout)) errors.push(`eksik alan: ${key}`);
  }
  if (errors.length) return { label, errors, warnings, ok: false };

  const half = layout.HalfSize;
  if (!(half > 0)) errors.push(`HalfSize geçersiz: ${half}`);
  if (!(layout.MaxHeight > layout.WaterLevel)) {
    errors.push(`MaxHeight (${layout.MaxHeight}) WaterLevel (${layout.WaterLevel}) üstünde olmalı`);
  }

  const locs = layout.Locations ?? [];
  if (locs.length < 8) warnings.push(`lokasyon sayısı düşük: ${locs.length}`);

  for (let i = 0; i < locs.length; i++) {
    const a = locs[i];
    if (!a.Name) errors.push(`Locations[${i}]: Name yok`);
    if (a.Kind < 0 || a.Kind > KIND_MAX) errors.push(`${a.Name}: Kind ${a.Kind} dışı`);
    if (a.Tier < 0 || a.Tier > TIER_MAX) errors.push(`${a.Name}: Tier ${a.Tier} dışı`);
    if (!(a.Radius > 0)) errors.push(`${a.Name}: Radius geçersiz`);
    if (!a.Center || typeof a.Center.x !== 'number' || typeof a.Center.y !== 'number') {
      errors.push(`${a.Name}: Center {x,y} (dünya XZ) gerekli`);
      continue;
    }
    const margin = a.Radius;
    if (Math.abs(a.Center.x) + margin > half || Math.abs(a.Center.y) + margin > half) {
      errors.push(`${a.Name}: harita sınırını aşıyor (HalfSize ${half})`);
    }
    if (a.TargetHeight > layout.MaxHeight) {
      errors.push(`${a.Name}: TargetHeight MaxHeight üstünde`);
    }
    if (a.TargetHeight >= 0 && a.TargetHeight < layout.WaterLevel + 1) {
      warnings.push(`${a.Name}: TargetHeight suya çok yakın`);
    }
  }

  for (let i = 0; i < locs.length; i++) {
    for (let j = i + 1; j < locs.length; j++) {
      const a = locs[i];
      const b = locs[j];
      if (!a.Center || !b.Center) continue;
      const d = dist(a.Center, b.Center);
      const minSep = a.Radius + b.Radius;
      if (d < minSep * 0.55) {
        errors.push(`çakışma: ${a.Name} ↔ ${b.Name} (d=${d.toFixed(1)} < ${(minSep * 0.55).toFixed(1)})`);
      } else if (d < minSep * 0.85) {
        warnings.push(`yakın lokasyon: ${a.Name} ↔ ${b.Name} (d=${d.toFixed(1)})`);
      }
    }
  }

  const military = locs.filter(l => l.Tier === 3);
  if (military.length !== 1) {
    warnings.push(`Military yağma çekirdeği sayısı ${military.length} (hedef 1)`);
  }

  const heightAt = sampleHeights(layout);
  for (const road of layout.Roads ?? []) {
    if (!road.Points?.length) {
      errors.push(`yol ${road.Name ?? '?'}: Points boş`);
      continue;
    }
    for (const p of road.Points) {
      if (Math.abs(p.x) > half + 2 || Math.abs(p.y) > half + 2) {
        errors.push(`yol ${road.Name}: nokta sınır dışı (${p.x}, ${p.y})`);
        break;
      }
    }
    const { worst, worstSeg } = roadGrade(road, heightAt);
    if (worst > GRADE_LIMIT) {
      warnings.push(
        `yol eğimi tahmini yüksek: ${road.Name} grade=${(worst * 100).toFixed(1)}%` +
          (worstSeg ? ` @ (${worstSeg.from.x.toFixed(0)},${worstSeg.from.y.toFixed(0)})→(${worstSeg.to.x.toFixed(0)},${worstSeg.to.y.toFixed(0)})` : '')
      );
    }
  }

  for (const lake of layout.Lakes ?? []) {
    if (!lake.Center || !(lake.Radius > 0)) {
      errors.push('göl: Center/Radius geçersiz');
      continue;
    }
    if (Math.abs(lake.Center.x) > half * 1.4 || Math.abs(lake.Center.y) > half * 1.4) {
      warnings.push(`göl merkezi harita dışı kesişim (tasarım denizi olabilir): (${lake.Center.x}, ${lake.Center.y})`);
    }
  }

  for (const river of layout.Rivers ?? []) {
    if (!river.Points?.length) continue;
    for (const p of river.Points) {
      if (Math.abs(p.x) > half + 5 || Math.abs(p.y) > half + 5) {
        warnings.push(`dere noktası sınır dışı: (${p.x}, ${p.y})`);
        break;
      }
    }
  }

  return { label, errors, warnings, ok: errors.length === 0 };
}

async function findLayouts(filter) {
  const entries = await readdir(root, { withFileTypes: true });
  const dirs = entries.filter(e => e.isDirectory()).map(e => e.name);
  const targets = filter ? dirs.filter(d => d === filter) : dirs;
  const out = [];
  for (const d of targets) {
    const p = path.join(root, d, 'layout.json');
    try {
      const raw = await readFile(p, 'utf8');
      out.push({ dir: d, layout: JSON.parse(raw) });
    } catch {
      /* layout yoksa atla (ör. yalnızca konsept klasörü) */
    }
  }
  return out;
}

const filter = process.argv[2];
const layouts = await findLayouts(filter);
if (!layouts.length) {
  console.error(filter ? `layout bulunamadı: ${filter}` : 'hiç layout.json yok — önce npm run build');
  process.exit(2);
}

let failed = 0;
for (const { dir, layout } of layouts) {
  const r = validateLayout(layout, dir);
  const mark = r.ok ? 'OK' : 'FAIL';
  console.log(`\n[${mark}] ${r.label}  HalfSize=${layout.HalfSize}  locs=${layout.Locations?.length}  roads=${layout.Roads?.length}`);
  for (const e of r.errors) console.log(`  ERROR  ${e}`);
  for (const w of r.warnings) console.log(`  WARN   ${w}`);
  if (!r.ok) failed++;
}

console.log(`\nÖzet: ${layouts.length - failed}/${layouts.length} geçti.`);
process.exit(failed ? 1 : 0);
