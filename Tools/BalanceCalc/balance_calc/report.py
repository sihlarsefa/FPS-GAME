"""CSV, Markdown ve HTML rapor üretimi."""

from __future__ import annotations

import csv
import json
import math
from pathlib import Path
from typing import Any, Iterable

from .damage import TtkResult
from .duel import DuelCell
from .monte_carlo import MonteCarloResult
from .parse_catalogs import Weapon
from .recommendations import CATEGORY_TR, Recommendation


def write_csv(rows: list[TtkResult], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "weapon_id", "display_name", "category", "distance_m", "zone",
        "armor_level", "helmet_level", "shots_to_kill", "ttk_seconds",
        "first_shot_damage", "damage_per_shot", "rpm", "dps_effective", "notes",
    ]
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow({k: getattr(r, k) for k in fields})


def write_monte_carlo_csv(rows: list[MonteCarloResult], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "weapon_id", "display_name", "distance_m", "zone", "armor_level",
        "helmet_level", "trials", "mean_hit_chance", "mean_expected_ttk",
        "p50_ttk", "p90_ttk", "mean_shots_fired",
    ]
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow({k: getattr(r, k) for k in fields})


def write_duel_csv(cells: list[DuelCell], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = [
        "weapon_a", "weapon_b", "distance_m", "armor_level",
        "win_rate_a", "mean_ttk_a", "mean_ttk_b", "trials",
    ]
    with path.open("w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for c in cells:
            w.writerow({k: getattr(c, k) for k in fields})


def _md_escape(s: str) -> str:
    return s.replace("|", "\\|")


def write_markdown(
    weapons: list[Weapon],
    rows: list[TtkResult],
    recs: list[Recommendation],
    mc: list[MonteCarloResult],
    duels: list[DuelCell],
    path: Path,
    what_if_samples: list[dict[str, Any]] | None = None,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines: list[str] = []
    lines.append("# HAREKÂT Silah Denge Raporu")
    lines.append("")
    lines.append("Kaynak: `WeaponCatalog.cs` + `ItemCatalog.cs` + `DamageCalculator.cs` formülleri.")
    lines.append("")
    lines.append("## Varsayımlar")
    lines.append("")
    lines.append("- Maksimum can: **100**")
    lines.append("- Mesafe düşüşü: `FalloffStart`→`FalloffEnd` doğrusal, min = `MinDamageFactor`")
    lines.append("- Zırh: `hasar × (1 − DamageReduction)`; emilen kadar dayanıklılık düşer")
    lines.append("- Gövde → yelek; kafa → kask")
    lines.append("- Pompalı: tüm saçmalar isabet (deterministik TTK); Monte Carlo'da sapma var")
    lines.append("- TTK = `(vuruş − 1) × fireInterval` (ilk atış t=0)")
    lines.append("")

    lines.append("## Silah özeti")
    lines.append("")
    lines.append("| Silah | Kategori | Hasar | RPM | Şarjör | Falloff | HS× | Saçma |")
    lines.append("|---|---|---:|---:|---:|---|---:|---:|")
    for w in weapons:
        cat = CATEGORY_TR.get(w.category, w.category)
        fo = f"{w.falloff_start:.0f}–{w.falloff_end:.0f} m (×{w.min_damage_factor})"
        lines.append(
            f"| {_md_escape(w.display_name)} | {cat} | {w.damage:g} | {w.rpm:.0f} | "
            f"{w.magazine_size} | {fo} | {w.headshot_multiplier:g} | {w.pellet_count} |"
        )
    lines.append("")

    # Özet tablo: 50m gövde Sv.2
    lines.append("## Özet TTK — 50 m gövde, Zırh Sv.2 / Kask Sv.2")
    lines.append("")
    lines.append("| Silah | Vuruş | TTK (s) | İlk hasar | DPS |")
    lines.append("|---|---:|---:|---:|---:|")
    subset = [
        r for r in rows
        if r.distance_m == 50 and r.zone == "body" and r.armor_level == 2 and r.helmet_level == 2
    ]
    subset.sort(key=lambda x: x.ttk_seconds)
    for r in subset:
        lines.append(
            f"| {_md_escape(r.display_name)} | {r.shots_to_kill} | {r.ttk_seconds:.3f} | "
            f"{r.first_shot_damage:.1f} | {r.dps_effective:.1f} |"
        )
    lines.append("")

    lines.append("## Mesafe karşılaştırması — gövde, zırh yok")
    lines.append("")
    header = "| Silah | " + " | ".join(f"{d} m" for d in (10, 50, 100, 200, 300)) + " |"
    lines.append(header)
    lines.append("|---|" + "|".join(["---:"] * 5) + "|")
    for w in weapons:
        cells = []
        for d in (10, 50, 100, 200, 300):
            hit = next(
                (
                    r for r in rows
                    if r.weapon_id == w.weapon_id and r.distance_m == d
                    and r.zone == "body" and r.armor_level == 0 and r.helmet_level == 0
                ),
                None,
            )
            cells.append(f"{hit.ttk_seconds:.2f}s/{hit.shots_to_kill}v" if hit else "-")
        lines.append(f"| {_md_escape(w.display_name)} | " + " | ".join(cells) + " |")
    lines.append("")

    lines.append("## Denge önerileri")
    lines.append("")
    if not recs:
        lines.append("_Anomali bulunamadı._")
    else:
        for rec in recs:
            badge = {"kritik": "🔴", "uyarı": "🟡", "bilgi": "🔵"}.get(rec.severity, "•")
            lines.append(f"### {badge} {rec.title} — {rec.weapon}")
            lines.append("")
            lines.append(f"**Kategori:** {rec.category} · **Seviye:** {rec.severity}")
            lines.append("")
            lines.append(f"**Gerekçe:** {rec.rationale}")
            lines.append("")
            lines.append(f"**Öneri:** {rec.suggestion}")
            lines.append("")

    if mc:
        lines.append("## Monte Carlo (Sv.2 zırh, ADS)")
        lines.append("")
        lines.append("| Silah | Mesafe | Bölge | İsabet | E[TTK] | P50 | P90 |")
        lines.append("|---|---:|---|---:|---:|---:|---:|")
        for m in mc:
            if m.distance_m not in (50, 100, 200):
                continue
            if m.zone != "body":
                continue
            lines.append(
                f"| {_md_escape(m.display_name)} | {m.distance_m:g} | gövde | "
                f"{m.mean_hit_chance:.0%} | {m.mean_expected_ttk:.3f} | "
                f"{m.p50_ttk:.3f} | {m.p90_ttk:.3f} |"
            )
        lines.append("")

    if duels:
        lines.append("## 1v1 düello örnekleri (Sv.2 gövde)")
        lines.append("")
        lines.append("| A | B | Mesafe | A kazanma |")
        lines.append("|---|---|---:|---:|")
        # En dengesiz 15
        ranked = sorted(duels, key=lambda c: abs(c.win_rate_a - 0.5), reverse=True)[:15]
        for c in ranked:
            lines.append(
                f"| {_md_escape(c.weapon_a)} | {_md_escape(c.weapon_b)} | "
                f"{c.distance_m:g} | {c.win_rate_a:.0%} |"
            )
        lines.append("")

    if what_if_samples:
        lines.append("## Ne olur? örnekleri")
        lines.append("")
        for s in what_if_samples:
            lines.append(
                f"- **{s['weapon']}** `{s['overrides']}` → "
                f"TTK {s['baseline']['ttk']:.3f}s → {s['modified']['ttk']:.3f}s "
                f"(Δ {s['delta_ttk']:+.3f}s, vuruş {s['delta_shots']:+d})"
            )
        lines.append("")

    lines.append("---")
    lines.append("*Tools/BalanceCalc ile üretildi.*")
    path.write_text("\n".join(lines), encoding="utf-8")


def _chart_ttk_50(rows: list[TtkResult]) -> tuple[list[str], list[float]]:
    subset = [
        r for r in rows
        if r.distance_m == 50 and r.zone == "body" and r.armor_level == 2 and r.helmet_level == 2
    ]
    subset.sort(key=lambda x: x.ttk_seconds)
    return [r.display_name for r in subset], [r.ttk_seconds for r in subset]


def write_html_report(
    weapons: list[Weapon],
    rows: list[TtkResult],
    recs: list[Recommendation],
    mc: list[MonteCarloResult],
    duels: list[DuelCell],
    path: Path,
) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    labels, values = _chart_ttk_50(rows)

    # Isı haritası: MPT-55 vs diğerleri @ çeşitli mesafeler
    pivot_weapon = "MPT-55"
    heat_weapons = sorted({c.weapon_b for c in duels if c.weapon_a == pivot_weapon} |
                          {c.weapon_a for c in duels if c.weapon_b == pivot_weapon})
    distances = sorted({c.distance_m for c in duels})
    heat: dict[tuple[str, float], float] = {}
    for c in duels:
        if c.weapon_a == pivot_weapon:
            heat[(c.weapon_b, c.distance_m)] = c.win_rate_a
        elif c.weapon_b == pivot_weapon:
            heat[(c.weapon_a, c.distance_m)] = 1.0 - c.win_rate_a

    heat_rows_html = []
    for name in heat_weapons:
        cells = []
        for d in distances:
            wr = heat.get((name, d), 0.5)
            # Kırmızı = MPT kazanır, mavi = rakip
            r = int(220 * wr)
            b = int(220 * (1 - wr))
            cells.append(
                f'<td style="background:rgb({r},40,{b});color:#fff;text-align:center;'
                f'font-variant-numeric:tabular-nums">{wr:.0%}</td>'
            )
        heat_rows_html.append(f"<tr><th>{name}</th>{''.join(cells)}</tr>")

    rec_html = []
    for rec in recs:
        color = {"kritik": "#c0392b", "uyarı": "#d68910", "bilgi": "#2980b9"}.get(rec.severity, "#333")
        rec_html.append(
            f'<article class="rec" style="border-left:4px solid {color}">'
            f"<h3>{rec.title} — {rec.weapon}</h3>"
            f"<p class='meta'>{rec.category} · {rec.severity}</p>"
            f"<p><strong>Gerekçe:</strong> {rec.rationale}</p>"
            f"<p><strong>Öneri:</strong> {rec.suggestion}</p></article>"
        )

    mc_body = [m for m in mc if m.zone == "body"]
    mc_labels = [f"{m.display_name}@{m.distance_m:g}" for m in mc_body]
    mc_vals = [m.mean_expected_ttk if math.isfinite(m.mean_expected_ttk) else 0 for m in mc_body]

    html = f"""<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>HAREKÂT Denge Raporu</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
<style>
:root {{
  --bg: #0f1410; --panel: #1a221c; --text: #e8efe6; --muted: #8a9a88;
  --accent: #c45c26; --line: #2c3a30;
}}
* {{ box-sizing: border-box; }}
body {{
  margin: 0; font-family: "Segoe UI", system-ui, sans-serif;
  background: radial-gradient(ellipse at top, #1a2a1e, var(--bg));
  color: var(--text); line-height: 1.5; padding: 2rem;
}}
h1,h2,h3 {{ font-weight: 650; letter-spacing: 0.02em; }}
h1 {{ color: var(--accent); margin-top: 0; }}
.panel {{
  background: var(--panel); border: 1px solid var(--line);
  border-radius: 8px; padding: 1.25rem; margin: 1.25rem 0;
}}
.grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 1.25rem; }}
@media (max-width: 900px) {{ .grid {{ grid-template-columns: 1fr; }} }}
table {{ width: 100%; border-collapse: collapse; font-size: 0.9rem; }}
th, td {{ padding: 0.4rem 0.55rem; border-bottom: 1px solid var(--line); }}
th {{ text-align: left; color: var(--muted); font-weight: 600; }}
.rec {{ background: #141c16; padding: 0.9rem 1rem; margin: 0.75rem 0; border-radius: 4px; }}
.rec .meta {{ color: var(--muted); font-size: 0.85rem; }}
.chart-wrap {{ position: relative; height: 360px; }}
.note {{ color: var(--muted); font-size: 0.9rem; }}
</style>
</head>
<body>
<h1>HAREKÂT — Silah Denge Raporu</h1>
<p class="note">Deterministik TTK + Monte Carlo + 1v1 düello. Can=100 · DamageCalculator formülleri.</p>

<div class="grid">
  <section class="panel">
    <h2>50 m gövde TTK (Zırh Sv.2)</h2>
    <div class="chart-wrap"><canvas id="ttkChart"></canvas></div>
  </section>
  <section class="panel">
    <h2>Monte Carlo beklenen TTK (gövde)</h2>
    <div class="chart-wrap"><canvas id="mcChart"></canvas></div>
  </section>
</div>

<section class="panel">
  <h2>1v1 ısı haritası — {pivot_weapon} kazanma oranı (Sv.2 gövde)</h2>
  <p class="note">Kırmızı: {pivot_weapon} üstün · Mavi: rakip üstün</p>
  <div style="overflow-x:auto">
  <table>
    <thead><tr><th>Rakip</th>{''.join(f'<th>{d:g} m</th>' for d in distances)}</tr></thead>
    <tbody>{''.join(heat_rows_html)}</tbody>
  </table>
  </div>
</section>

<section class="panel">
  <h2>Denge önerileri</h2>
  {''.join(rec_html) if rec_html else '<p>Anomali yok.</p>'}
</section>

<script>
const ttkLabels = {json.dumps(labels, ensure_ascii=False)};
const ttkValues = {json.dumps(values)};
const mcLabels = {json.dumps(mc_labels, ensure_ascii=False)};
const mcValues = {json.dumps(mc_vals)};

new Chart(document.getElementById('ttkChart'), {{
  type: 'bar',
  data: {{
    labels: ttkLabels,
    datasets: [{{
      label: 'TTK (s)',
      data: ttkValues,
      backgroundColor: '#c45c26aa',
      borderColor: '#c45c26',
      borderWidth: 1
    }}]
  }},
  options: {{
    indexAxis: 'y',
    responsive: true,
    maintainAspectRatio: false,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{
      x: {{ ticks: {{ color: '#8a9a88' }}, grid: {{ color: '#2c3a30' }} }},
      y: {{ ticks: {{ color: '#e8efe6' }}, grid: {{ display: false }} }}
    }}
  }}
}});

new Chart(document.getElementById('mcChart'), {{
  type: 'bar',
  data: {{
    labels: mcLabels,
    datasets: [{{
      label: 'E[TTK] (s)',
      data: mcValues,
      backgroundColor: '#3d7a55aa',
      borderColor: '#3d7a55',
      borderWidth: 1
    }}]
  }},
  options: {{
    responsive: true,
    maintainAspectRatio: false,
    plugins: {{ legend: {{ display: false }} }},
    scales: {{
      x: {{ ticks: {{ color: '#8a9a88', maxRotation: 60, minRotation: 40 }}, grid: {{ display: false }} }},
      y: {{ ticks: {{ color: '#8a9a88' }}, grid: {{ color: '#2c3a30' }} }}
    }}
  }}
}});
</script>
</body>
</html>
"""
    path.write_text(html, encoding="utf-8")


def catalog_json(weapons: list[Weapon], vests, helmets) -> dict[str, Any]:
    return {
        "maxHealth": 100,
        "weapons": [w.to_dict() for w in weapons],
        "vests": [v.to_dict() for v in vests],
        "helmets": [h.to_dict() for h in helmets],
        "distances": [10, 50, 100, 200, 300],
    }
