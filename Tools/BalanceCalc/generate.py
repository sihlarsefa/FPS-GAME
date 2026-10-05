#!/usr/bin/env python3
"""HAREKÂT BalanceCalc — katalogdan TTK/CSV/MD/HTML + etkileşimli UI üretir."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from balance_calc.damage import compute_matrix
from balance_calc.duel import duel_heatmap
from balance_calc.monte_carlo import run_monte_carlo_suite
from balance_calc.parse_catalogs import load_catalogs
from balance_calc.recommendations import generate_recommendations, what_if_delta
from balance_calc.report import (
    catalog_json,
    write_csv,
    write_duel_csv,
    write_html_report,
    write_markdown,
    write_monte_carlo_csv,
)


def find_repo_root() -> Path:
    here = Path(__file__).resolve().parent
    for p in [here, *here.parents]:
        if (p / "Assets" / "_Project" / "Scripts" / "Application" / "Catalogs" / "WeaponCatalog.cs").exists():
            return p
    return here.parents[1]


def build_interactive_ui(data: dict, out_path: Path, template_path: Path) -> None:
    tpl = template_path.read_text(encoding="utf-8")
    payload = json.dumps(data, ensure_ascii=False, indent=None)
    if "__CATALOG_PLACEHOLDER__" not in tpl:
        raise SystemExit("Şablonda __CATALOG_PLACEHOLDER__ bulunamadı.")
    html = tpl.replace("__CATALOG_PLACEHOLDER__", payload)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(html, encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="HAREKÂT silah denge hesaplayıcısı")
    parser.add_argument("--repo", type=Path, default=None, help="Repo kökü (otomatik bulunur)")
    parser.add_argument("--out", type=Path, default=ROOT / "out", help="Çıktı klasörü")
    parser.add_argument("--mc-trials", type=int, default=250, help="Monte Carlo deneme sayısı")
    parser.add_argument("--duel-trials", type=int, default=100, help="Düello deneme / hücre")
    parser.add_argument("--skip-duel", action="store_true", help="1v1 simülasyonunu atla")
    parser.add_argument("--skip-mc", action="store_true", help="Monte Carlo'yu atla")
    args = parser.parse_args()

    repo = args.repo or find_repo_root()
    out: Path = args.out
    out.mkdir(parents=True, exist_ok=True)

    print(f"Repo: {repo}")
    cat = load_catalogs(repo)
    weapons, vests, helmets = cat["weapons"], cat["vests"], cat["helmets"]
    print(f"Silah: {len(weapons)} · Yelek: {len(vests)} · Kask: {len(helmets)}")

    if not weapons:
        print("HATA: Silah ayrıştırılamadı.", file=sys.stderr)
        return 1

    print("TTK matrisi hesaplanıyor…")
    rows = compute_matrix(weapons, vests, helmets)
    write_csv(rows, out / "ttk.csv")
    print(f"  → {out / 'ttk.csv'} ({len(rows)} satır)")

    recs = generate_recommendations(weapons, rows)
    print(f"Öneri: {len(recs)}")

    mc: list = []
    if not args.skip_mc:
        print("Monte Carlo…")
        mc = run_monte_carlo_suite(
            weapons, vests, helmets, trials=args.mc_trials
        )
        write_monte_carlo_csv(mc, out / "monte_carlo.csv")
        print(f"  → {out / 'monte_carlo.csv'}")

    duels: list = []
    if not args.skip_duel:
        print("1v1 düello ısı haritası…")
        duels = duel_heatmap(
            weapons, vests, helmets, trials=args.duel_trials
        )
        write_duel_csv(duels, out / "duel_heatmap.csv")
        print(f"  → {out / 'duel_heatmap.csv'} ({len(duels)} hücre)")

    # Ne olur? örnekleri
    samples = []
    by_id = {w.weapon_id: w for w in weapons}
    if "Mpt55" in by_id:
        samples.append(
            what_if_delta(by_id["Mpt55"], {"damage": 24.0}, vests, helmets)
        )
    if "G3" in by_id:
        samples.append(
            what_if_delta(by_id["G3"], {"fire_interval_seconds": 0.12}, vests, helmets)
        )
    if "Escort" in by_id:
        samples.append(
            what_if_delta(by_id["Escort"], {"min_damage_factor": 0.35}, vests, helmets, distance=50)
        )

    write_markdown(
        weapons, rows, recs, mc, duels, out / "denge_raporu.md", what_if_samples=samples
    )
    print(f"  → {out / 'denge_raporu.md'}")

    write_html_report(weapons, rows, recs, mc, duels, out / "denge_raporu.html")
    print(f"  → {out / 'denge_raporu.html'}")

    # Katalog JSON + etkileşimli UI
    data = catalog_json(weapons, vests, helmets)
    (out / "catalog.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    ui_path = ROOT / "ui" / "balance.html"
    build_interactive_ui(data, ui_path, ROOT / "ui" / "balance.template.html")
    # Ayrıca out/ altına kopya
    build_interactive_ui(data, out / "balance_ui.html", ROOT / "ui" / "balance.template.html")
    print(f"  → {ui_path}")
    print(f"  → {out / 'balance_ui.html'}")

    # Özet öneri JSON
    (out / "oneriler.json").write_text(
        json.dumps(
            [
                {
                    "severity": r.severity,
                    "weapon": r.weapon,
                    "category": r.category,
                    "title": r.title,
                    "rationale": r.rationale,
                    "suggestion": r.suggestion,
                }
                for r in recs
            ],
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    print("\nTamam.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
