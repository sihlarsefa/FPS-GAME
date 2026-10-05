#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""HAREKÂT Localization — çoğul / placeholder doğrulama.

Kullanım:
  python3 Localization/scripts/validate_placeholders.py
  python3 Localization/scripts/validate_placeholders.py --csv Localization/strings.csv

Çıkış kodu: 0 = temiz, 1 = hata, 2 = yalnızca uyarı ( --strict ile uyarılar da 1 ).
"""

from __future__ import annotations

import argparse
import csv
import re
import sys
from collections import defaultdict
from pathlib import Path

PLACEHOLDER_RE = re.compile(r"\{(\d+)(?::[^}]*)?\}|\{([A-Za-z_][A-Za-z0-9_]*)(?::[^}]*)?\}")
# Smart Format / ICU-ish plural markers we accept as intentional
PLURAL_HINT_RE = re.compile(r":plural:|:choice:|plural\(|#:|few|many|other", re.I)

LANGS = ("tr", "en", "de", "az", "ar")
BUTTON_KEY_HINTS = (".btn.", "order.name.", "settings.btn", "pause.btn", "menu.dialog.")
BUTTON_SOFT_LIMIT = 18
TITLE_SOFT_LIMIT = 56


def extract_placeholders(text: str) -> set[str]:
    found: set[str] = set()
    for m in PLACEHOLDER_RE.finditer(text or ""):
        if m.group(1) is not None:
            found.add(m.group(1))
        elif m.group(2) is not None:
            found.add(m.group(2))
    return found


def has_unmatched_braces(text: str) -> bool:
    if not text:
        return False
    # ignore escaped {{ }}
    t = text.replace("{{", "").replace("}}", "")
    return t.count("{") != t.count("}")


def load_rows(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as f:
        reader = csv.DictReader(f)
        required = {"key", "tr", "en", "source_file"}
        if not required.issubset(set(reader.fieldnames or [])):
            raise SystemExit(f"CSV eksik sütun. Beklenen en az: {sorted(required)}; gelen: {reader.fieldnames}")
        return list(reader)


def validate(rows: list[dict[str, str]], strict: bool) -> int:
    errors: list[str] = []
    warnings: list[str] = []
    keys_seen: dict[str, int] = {}

    for i, row in enumerate(rows, start=2):
        key = (row.get("key") or "").strip()
        if not key:
            errors.append(f"L{i}: boş key")
            continue
        if key in keys_seen:
            errors.append(f"L{i}: yinelenen key '{key}' (ilk L{keys_seen[key]})")
        else:
            keys_seen[key] = i

        # Present languages (tr/en required; de/az/ar if column exists)
        langs_present = [L for L in LANGS if L in row]
        for L in ("tr", "en"):
            if not (row.get(L) or "").strip():
                errors.append(f"L{i} [{key}]: '{L}' boş")

        ref = extract_placeholders(row.get("tr") or "")
        for L in langs_present:
            text = row.get(L) or ""
            if not text.strip() and L in ("de", "az", "ar"):
                warnings.append(f"L{i} [{key}]: '{L}' boş (uzatma sütunu)")
                continue
            if has_unmatched_braces(text):
                errors.append(f"L{i} [{key}/{L}]: dengesiz '{{' '}}'")
            got = extract_placeholders(text)
            if got != ref:
                errors.append(
                    f"L{i} [{key}/{L}]: placeholder uyuşmazlığı — TR={sorted(ref)} vs {L.upper()}={sorted(got)}"
                )

        # Length soft checks vs TR
        tr = row.get("tr") or ""
        tr_len = len(tr)
        for L in langs_present:
            text = row.get(L) or ""
            if not text or L == "tr" or tr_len == 0:
                continue
            ratio = len(text) / tr_len
            is_button = any(h in key for h in BUTTON_KEY_HINTS)
            if is_button and len(text) > BUTTON_SOFT_LIMIT:
                warnings.append(
                    f"L{i} [{key}/{L}]: düğme metni {len(text)} karakter > {BUTTON_SOFT_LIMIT} — UI taşma riski"
                )
            if ("match.msg" in key or key.endswith(".title") or "training.msg" in key) and len(text) > TITLE_SOFT_LIMIT:
                warnings.append(
                    f"L{i} [{key}/{L}]: başlık/mesaj {len(text)} karakter > {TITLE_SOFT_LIMIT}"
                )
            if ratio >= 1.5:
                warnings.append(
                    f"L{i} [{key}/{L}]: TR'ye göre {ratio:.2f}x uzun ({tr_len}→{len(text)})"
                )

        # Plural hint consistency: if one locale mentions plural syntax, all with placeholders should
        plural_langs = [L for L in langs_present if PLURAL_HINT_RE.search(row.get(L) or "")]
        if plural_langs and set(plural_langs) != set(langs_present):
            missing = sorted(set(langs_present) - set(plural_langs))
            warnings.append(
                f"L{i} [{key}]: çoğul sözdizimi yalnızca {plural_langs} dil(ler)inde; eksik: {missing}"
            )

    # Summary: placeholder inventory
    ph_keys = [k for k, row in ((r["key"], r) for r in rows if r.get("key")) if extract_placeholders(row.get("tr") or "")]
    print(f"Satır: {len(rows)} | Benzersiz key: {len(keys_seen)} | Placeholder'lı: {len(ph_keys)}")
    print(f"Hata: {len(errors)} | Uyarı: {len(warnings)}")

    for msg in errors:
        print(f"ERROR: {msg}")
    for msg in warnings:
        print(f"WARN:  {msg}")

    if errors:
        return 1
    if warnings and strict:
        return 1
    if warnings:
        return 2
    return 0


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description="HAREKÂT strings.csv placeholder/çoğul doğrulayıcı")
    parser.add_argument("--csv", type=Path, default=root / "strings.csv")
    parser.add_argument("--strict", action="store_true", help="Uyarıları da başarısız say")
    args = parser.parse_args()
    if not args.csv.is_file():
        print(f"CSV bulunamadı: {args.csv}", file=sys.stderr)
        return 1
    rows = load_rows(args.csv)
    return validate(rows, args.strict)


if __name__ == "__main__":
    sys.exit(main())
