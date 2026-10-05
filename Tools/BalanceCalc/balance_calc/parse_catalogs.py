"""WeaponCatalog.cs ve ItemCatalog.cs ayrıştırıcı (salt okunur)."""

from __future__ import annotations

import re
from dataclasses import dataclass, field, asdict
from pathlib import Path
from typing import Any


@dataclass
class Weapon:
    weapon_id: str
    category: str
    display_name: str
    damage: float
    magazine_size: int
    fire_interval_seconds: float
    reload_duration_seconds: float
    range_m: float
    headshot_multiplier: float
    ammo_type: str = "Mm9"
    limb_multiplier: float = 0.8
    muzzle_velocity: float = 700.0
    recoil_vertical: float = 0.6
    recoil_horizontal: float = 0.35
    hip_spread: float = 3.0
    ads_spread: float = 0.4
    bloom_per_shot: float = 0.35
    max_bloom: float = 3.0
    pellet_count: int = 1
    pellet_spread: float = 0.0
    ads_zoom: float = 1.3
    has_scope: bool = False
    falloff_start: float = 60.0
    falloff_end: float = 250.0
    min_damage_factor: float = 0.6
    weight: float = 4.0
    equip_seconds: float = 0.6
    is_bolt_action: bool = False
    burst_count: int = 3
    fire_modes: list[str] = field(default_factory=lambda: ["Single"])

    @property
    def rpm(self) -> float:
        if self.fire_interval_seconds <= 0:
            return 0.0
        return 60.0 / self.fire_interval_seconds

    def to_dict(self) -> dict[str, Any]:
        d = asdict(self)
        d["rpm"] = round(self.rpm, 2)
        return d


@dataclass
class ArmorDef:
    item_id: str
    display_name: str
    category: str  # Armor | Helmet
    level: int
    durability: float
    damage_reduction: float

    def to_dict(self) -> dict[str, Any]:
        return asdict(self)


_INTERVAL_RPM = re.compile(r"IntervalFromRpm\((\d+(?:\.\d+)?)f?\)")
_FLOAT = r"(-?\d+(?:\.\d+)?)f?"
_INT = r"(\d+)"


def _num(s: str) -> float:
    return float(s)


def _parse_fire_interval(raw: str) -> float:
    raw = raw.strip()
    m = _INTERVAL_RPM.search(raw)
    if m:
        return 60.0 / float(m.group(1))
    return float(raw.rstrip("f").strip())


def _extract_block_props(block: str) -> dict[str, str]:
    props: dict[str, str] = {}
    # DisplayName = "..."
    for m in re.finditer(r'DisplayName\s*=\s*"([^"]+)"', block):
        props["display_name"] = m.group(1)
    for key, pat in [
        ("ammo_type", r"AmmoType\s*=\s*AmmoType\.(\w+)"),
        ("limb_multiplier", rf"LimbMultiplier\s*=\s*{_FLOAT}"),
        ("muzzle_velocity", rf"MuzzleVelocity\s*=\s*{_FLOAT}"),
        ("recoil_vertical", rf"RecoilVertical\s*=\s*{_FLOAT}"),
        ("recoil_horizontal", rf"RecoilHorizontal\s*=\s*{_FLOAT}"),
        ("hip_spread", rf"HipSpread\s*=\s*{_FLOAT}"),
        ("ads_spread", rf"AdsSpread\s*=\s*{_FLOAT}"),
        ("bloom_per_shot", rf"BloomPerShot\s*=\s*{_FLOAT}"),
        ("max_bloom", rf"MaxBloom\s*=\s*{_FLOAT}"),
        ("pellet_count", rf"PelletCount\s*=\s*{_INT}"),
        ("pellet_spread", rf"PelletSpread\s*=\s*{_FLOAT}"),
        ("ads_zoom", rf"AdsZoom\s*=\s*{_FLOAT}"),
        ("has_scope", r"HasScope\s*=\s*(true|false)"),
        ("falloff_start", rf"FalloffStart\s*=\s*{_FLOAT}"),
        ("falloff_end", rf"FalloffEnd\s*=\s*{_FLOAT}"),
        ("min_damage_factor", rf"MinDamageFactor\s*=\s*{_FLOAT}"),
        ("weight", rf"Weight\s*=\s*{_FLOAT}"),
        ("equip_seconds", rf"EquipSeconds\s*=\s*{_FLOAT}"),
        ("is_bolt_action", r"IsBoltAction\s*=\s*(true|false)"),
        ("burst_count", rf"BurstCount\s*=\s*{_INT}"),
    ]:
        m = re.search(pat, block, re.IGNORECASE)
        if m:
            props[key] = m.group(1)

    modes = re.findall(r"FireMode\.(\w+)", block)
    if modes:
        props["fire_modes"] = modes
    return props


def parse_weapon_catalog(path: Path | str) -> list[Weapon]:
    text = Path(path).read_text(encoding="utf-8")
    weapons: list[Weapon] = []

    # new WeaponDefinitionData(WeaponIds.XXX, WeaponCategory.YYY, damage: ..., ...)
    ctor = re.compile(
        r"new\s+WeaponDefinitionData\(\s*WeaponIds\.(\w+)\s*,\s*WeaponCategory\.(\w+)\s*,\s*"
        rf"damage:\s*{_FLOAT}\s*,\s*magazineSize:\s*{_INT}\s*,\s*"
        r"fireIntervalSeconds:\s*([^,]+?)\s*,\s*"
        rf"reloadDurationSeconds:\s*{_FLOAT}\s*,\s*"
        rf"range:\s*{_FLOAT}\s*,\s*headshotMultiplier:\s*{_FLOAT}\s*\)\s*"
        r"\{(.*?)\n\s*\}",
        re.DOTALL,
    )

    for m in ctor.finditer(text):
        wid, cat = m.group(1), m.group(2)
        damage = _num(m.group(3))
        mag = int(m.group(4))
        interval = _parse_fire_interval(m.group(5))
        reload = _num(m.group(6))
        range_m = _num(m.group(7))
        hs = _num(m.group(8))
        props = _extract_block_props(m.group(9))

        def f(key: str, default: float) -> float:
            return float(props[key]) if key in props else default

        def i(key: str, default: int) -> int:
            return int(props[key]) if key in props else default

        def b(key: str, default: bool = False) -> bool:
            if key not in props:
                return default
            return props[key].lower() == "true"

        weapons.append(
            Weapon(
                weapon_id=wid,
                category=cat,
                display_name=props.get("display_name", wid),
                damage=damage,
                magazine_size=mag,
                fire_interval_seconds=interval,
                reload_duration_seconds=reload,
                range_m=range_m,
                headshot_multiplier=hs,
                ammo_type=props.get("ammo_type", "Mm9"),
                limb_multiplier=f("limb_multiplier", 0.8),
                muzzle_velocity=f("muzzle_velocity", 700.0),
                recoil_vertical=f("recoil_vertical", 0.6),
                recoil_horizontal=f("recoil_horizontal", 0.35),
                hip_spread=f("hip_spread", 3.0),
                ads_spread=f("ads_spread", 0.4),
                bloom_per_shot=f("bloom_per_shot", 0.35),
                max_bloom=f("max_bloom", 3.0),
                pellet_count=i("pellet_count", 1),
                pellet_spread=f("pellet_spread", 0.0),
                ads_zoom=f("ads_zoom", 1.3),
                has_scope=b("has_scope"),
                falloff_start=f("falloff_start", 60.0),
                falloff_end=f("falloff_end", 250.0),
                min_damage_factor=f("min_damage_factor", 0.6),
                weight=f("weight", 4.0),
                equip_seconds=f("equip_seconds", 0.6),
                is_bolt_action=b("is_bolt_action"),
                burst_count=i("burst_count", 3),
                fire_modes=props.get("fire_modes", ["Single"]),
            )
        )
    return weapons


def parse_item_catalog(path: Path | str) -> tuple[list[ArmorDef], list[ArmorDef]]:
    text = Path(path).read_text(encoding="utf-8")
    vests: list[ArmorDef] = []
    helmets: list[ArmorDef] = []

    # AddArmor(ItemIds.Vest1, "Çelik Yelek (Sv.1)", ItemCategory.Armor, 1, 200f, 0.30f);
    pat = re.compile(
        rf'AddArmor\(\s*ItemIds\.(\w+)\s*,\s*"([^"]+)"\s*,\s*ItemCategory\.(\w+)\s*,\s*'
        rf"{_INT}\s*,\s*{_FLOAT}\s*,\s*{_FLOAT}\s*\)"
    )
    for m in pat.finditer(text):
        item = ArmorDef(
            item_id=m.group(1),
            display_name=m.group(2),
            category=m.group(3),
            level=int(m.group(4)),
            durability=_num(m.group(5)),
            damage_reduction=_num(m.group(6)),
        )
        if item.category == "Helmet":
            helmets.append(item)
        else:
            vests.append(item)

    vests.sort(key=lambda a: a.level)
    helmets.sort(key=lambda a: a.level)
    return vests, helmets


def default_catalog_paths(repo_root: Path) -> tuple[Path, Path]:
    base = repo_root / "Assets" / "_Project" / "Scripts" / "Application" / "Catalogs"
    return base / "WeaponCatalog.cs", base / "ItemCatalog.cs"


def load_catalogs(repo_root: Path | None = None) -> dict[str, Any]:
    root = repo_root or Path(__file__).resolve().parents[3]
    wpath, ipath = default_catalog_paths(root)
    weapons = parse_weapon_catalog(wpath)
    vests, helmets = parse_item_catalog(ipath)
    return {
        "weapons": weapons,
        "vests": vests,
        "helmets": helmets,
        "paths": {"weapon": str(wpath), "item": str(ipath)},
        "max_health": 100.0,
    }
