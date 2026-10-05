#!/usr/bin/env bash
# Basit maliyet / kapasite simülasyonu (stdout + CSV)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="${ROOT}/docs/cost-simulation.csv"

python3 - <<PY
import math
from pathlib import Path

out = Path("${OUT}")
rows = ["ccu,matches,servers,ondemand_nodes,spot_nodes,monthly_compute_usd,night_savings_usd"]
print("=== HAREKÂT maliyet simülasyonu ===")
for ccu in (500, 1000, 2500, 5000, 10000):
    matches = math.ceil(ccu / 50)
    servers = matches + 5
    nodes = math.ceil(servers / 2)
    spot = (nodes * 70) // 100
    ondemand = nodes - spot
    if ondemand < 1:
        ondemand, spot = 1, max(0, nodes - 1)
    day = ondemand * 0.134 * 730 + spot * 0.040 * 730
    savings = round(day * 0.25, 2)
    monthly = round(day - savings, 2)
    rows.append(f"{ccu},{matches},{servers},{ondemand},{spot},{monthly},{savings}")
    print(
        f"CCU={ccu:<6} maç={matches:<5} sunucu={servers:<5} "
        f"nodes(od/spot)={ondemand}/{spot}  aylık≈\${monthly}  gece tasarruf≈\${savings}"
    )
out.write_text("\n".join(rows) + "\n")
print(f"CSV: {out}")
PY
