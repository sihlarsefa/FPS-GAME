#!/usr/bin/env python3
"""cases.json → Senaryolar/*.md + senaryolar.csv. Oyun testi çalıştırmaz."""
from __future__ import annotations
import csv, json
from collections import Counter, OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Senaryolar"
CASES = ROOT / "cases.json"

SLUG = {
    "Hareket": "01_Hareket",
    "Silahlar": "02_Silahlar",
    "Envanter": "03_Envanter",
    "İyileşme": "04_Iyilesme",
    "Zırh": "05_Zirh",
    "İntikal": "06_Intikal",
    "Komuta ve topçu": "07_Komuta_Topcu",
    "Bölge ve maç": "08_Bolge_Mac",
    "Yapay zekâ": "09_Yapay_Zeka",
    "HUD ve harita": "10_HUD_Harita",
    "Menüler ayarlar kariyer": "11_Menuler_Ayarlar",
    "Atış Poligonu": "12_Atis_Poligonu",
    "Performans": "13_Performans",
    "Online ve dayanıklılık": "14_Online",
    "Backend uçları": "15_Backend",
    "Erişilebilirlik": "16_Erisilebilirlik",
    "Windows matrisi": "17_Windows",
    "Denge": "18_Denge",
    "Sürülebilir Kirpi": "19_Surulebilir_Kirpi",
    "Geliştirici konsolu": "20_Gelistirici_Konsolu",
    "Online paneller": "21_Online_Paneller",
    "Windows sunucu": "22_Windows_Sunucu",
    "İçerik override": "23_Icerik_Override",
}

def main() -> None:
    cases = json.loads(CASES.read_text(encoding="utf-8"))
    OUT.mkdir(parents=True, exist_ok=True)
    fields = ["id", "module", "title", "precondition", "steps", "expected", "priority", "source", "basis", "status"]
    with (OUT / "senaryolar.csv").open("w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        for r in cases:
            row = {k: r.get(k, "") for k in fields}
            row["steps"] = " | ".join(r["steps"]) if isinstance(r["steps"], list) else r["steps"]
            w.writerow(row)

    by_mod: OrderedDict[str, list] = OrderedDict()
    for r in cases:
        by_mod.setdefault(r["module"], []).append(r)

    index = [
        "# HAREKÂT — Manuel Test Senaryoları",
        "",
        f"**Toplam:** {len(cases)} senaryo · **Kaynak:** `QA/cases.json` · **CSV:** [senaryolar.csv](senaryolar.csv)",
        "",
        "| # | Modül | Senaryo | Dosya |",
        "|---|-------|---------|-------|",
    ]
    for i, (mod, rows) in enumerate(by_mod.items(), 1):
        fname = SLUG.get(mod, f"{i:02d}_{mod}") + ".md"
        index.append(f"| {i} | {mod} | {len(rows)} | [{fname}]({fname}) |")
        pri = Counter(r["priority"] for r in rows)
        lines = [
            f"# {mod}",
            "",
            f"Senaryo sayısı: **{len(rows)}** · Öncelik: "
            + ", ".join(f"{k}={v}" for k, v in sorted(pri.items())),
            "",
            "Her senaryo: ID, ön koşul, adımlar, beklenen sonuç, öncelik.",
            "",
        ]
        for r in rows:
            steps = r["steps"] if isinstance(r["steps"], list) else [r["steps"]]
            step_md = "\n".join(f"{n}. {s}" for n, s in enumerate(steps, 1))
            lines += [
                f'## {r["id"]} — {r["title"]}',
                "",
                f'- **Öncelik:** {r["priority"]}',
                f'- **Kaynak:** `{r["source"]}`',
                f'- **Dayanak:** {r["basis"]}',
                f'- **Durum:** {r["status"]}',
                "",
                "### Ön koşul",
                "",
                r["precondition"],
                "",
                "### Adımlar",
                "",
                step_md,
                "",
                "### Beklenen sonuç",
                "",
                r["expected"],
                "",
                "---",
                "",
            ]
        (OUT / fname).write_text("\n".join(lines), encoding="utf-8")

    index += ["", "## Öncelik özeti", ""]
    for k, v in sorted(Counter(r["priority"] for r in cases).items()):
        index.append(f"- **{k}:** {v}")
    index += [
        "",
        "## Kullanım",
        "",
        "1. Excel için `senaryolar.csv` (UTF-8 BOM).",
        "2. Modül MD dosyalarında çalıştırın.",
        "3. Yenileme: `python3 QA/scripts/seed_cases.py` sonra `python3 QA/scripts/export_scenarios.py`.",
        "",
    ]
    (OUT / "README.md").write_text("\n".join(index), encoding="utf-8")
    print(f"{len(cases)} senaryo → {OUT}")

if __name__ == "__main__":
    main()
