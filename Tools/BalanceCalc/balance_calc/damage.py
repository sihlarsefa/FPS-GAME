"""DamageCalculator.cs ile uyumlu hasar / TTK motoru."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Optional

from .parse_catalogs import ArmorDef, Weapon

MAX_HEALTH = 100.0
DISTANCES = (10, 50, 100, 200, 300)
ARMOR_LEVELS = (0, 1, 2, 3)
HIT_ZONES = ("body", "head")


@dataclass
class ArmorState:
    level: int
    damage_reduction: float
    durability: float
    max_durability: float

    @property
    def is_broken(self) -> bool:
        return self.durability <= 0.0

    def wear(self, amount: float) -> None:
        if amount <= 0:
            return
        self.durability = max(0.0, self.durability - amount)

    def copy(self) -> "ArmorState":
        return ArmorState(self.level, self.damage_reduction, self.durability, self.max_durability)


def make_armor(defs: list[ArmorDef], level: int) -> Optional[ArmorState]:
    if level <= 0:
        return None
    for d in defs:
        if d.level == level:
            return ArmorState(d.level, d.damage_reduction, d.durability, d.durability)
    return None


def clamp01(v: float) -> float:
    if v != v:  # NaN
        return 0.0
    return max(0.0, min(1.0, v))


def body_part_multiplier(weapon: Weapon, zone: str) -> float:
    if zone == "head":
        return weapon.headshot_multiplier if weapon.headshot_multiplier >= 0 else 2.0
    if zone in ("arm", "leg", "limb"):
        return weapon.limb_multiplier if weapon.limb_multiplier >= 0 else 0.8
    return 1.0


def distance_factor(weapon: Weapon, distance: float) -> float:
    start = weapon.falloff_start
    end = weapon.falloff_end
    mn = clamp01(weapon.min_damage_factor)
    if distance <= start:
        return 1.0
    if end <= start or distance >= end:
        return mn
    t = (distance - start) / (end - start)
    return 1.0 + (mn - 1.0) * t


def apply_armor(raw: float, armor: Optional[ArmorState], effectiveness: float = 1.0) -> tuple[float, float]:
    if raw != raw or raw <= 0:
        return 0.0, 0.0
    if armor is None or armor.is_broken:
        return raw, 0.0
    reduction = clamp01(armor.damage_reduction) * clamp01(effectiveness)
    if reduction <= 0:
        return raw, 0.0
    absorbed = raw * reduction
    armor.wear(absorbed)
    return raw - absorbed, absorbed


def raw_pellet_damage(weapon: Weapon, zone: str, distance: float) -> float:
    return weapon.damage * body_part_multiplier(weapon, zone) * distance_factor(weapon, distance)


@dataclass
class HitResult:
    damage: float
    absorbed: float
    pellets_hit: int


def apply_shot(
    weapon: Weapon,
    zone: str,
    distance: float,
    armor: Optional[ArmorState],
    pellets_hit: Optional[int] = None,
) -> HitResult:
    """Tek atış: her saçma ayrı hasar + zırh emilimi (DamageCalculator)."""
    n = weapon.pellet_count if pellets_hit is None else max(0, pellets_hit)
    total_dmg = 0.0
    total_abs = 0.0
    raw = raw_pellet_damage(weapon, zone, distance)
    for _ in range(n):
        dmg, absb = apply_armor(raw, armor)
        total_dmg += dmg
        total_abs += absb
    return HitResult(total_dmg, total_abs, n)


@dataclass
class TtkResult:
    weapon_id: str
    display_name: str
    category: str
    distance_m: float
    zone: str
    armor_level: int
    helmet_level: int
    shots_to_kill: int
    ttk_seconds: float
    damage_per_shot: float
    first_shot_damage: float
    rpm: float
    dps_effective: float
    notes: str = ""


def simulate_ttk(
    weapon: Weapon,
    zone: str,
    distance: float,
    vest: Optional[ArmorState],
    helmet: Optional[ArmorState],
    max_health: float = MAX_HEALTH,
    max_shots: int = 200,
) -> TtkResult:
    armor = (helmet if zone == "head" else vest)
    armor = armor.copy() if armor else None

    hp = max_health
    shots = 0
    first_dmg = 0.0
    last_dmg = 0.0

    while hp > 0 and shots < max_shots:
        # Zırh kırılınca sonraki atışlarda etkisiz
        if armor and armor.is_broken:
            armor = None
        hit = apply_shot(weapon, zone, distance, armor)
        if shots == 0:
            first_dmg = hit.damage
        last_dmg = hit.damage
        hp -= hit.damage
        shots += 1
        if hit.damage <= 0:
            break

    ttk = max(0.0, (shots - 1) * weapon.fire_interval_seconds) if shots > 0 else float("inf")
    avg = first_dmg if shots <= 1 else (max_health / shots)  # yaklaşık
    # Daha doğru: toplam hasar / shots
    total_dealt = max_health - min(0.0, hp) if hp <= 0 else first_dmg * shots
    # Yeniden simüle etmeden last_dmg kullan
    dps = (max_health / ttk) if ttk > 0 else (first_dmg / max(weapon.fire_interval_seconds, 1e-6))

    # Gerçek ortalama hasar: first shot + (eğer zırh kırıldıysa değişir)
    # Basit raporlama için first_shot_damage ve shots yeterli
    note = ""
    if weapon.pellet_count > 1:
        note = f"saçma={weapon.pellet_count} (tam isabet varsayımı)"

    return TtkResult(
        weapon_id=weapon.weapon_id,
        display_name=weapon.display_name,
        category=weapon.category,
        distance_m=distance,
        zone=zone,
        armor_level=vest.level if vest else 0,
        helmet_level=helmet.level if helmet else 0,
        shots_to_kill=shots,
        ttk_seconds=round(ttk, 4),
        damage_per_shot=round(last_dmg, 3),
        first_shot_damage=round(first_dmg, 3),
        rpm=round(weapon.rpm, 2),
        dps_effective=round(dps, 3),
        notes=note,
    )


def compute_matrix(
    weapons: list[Weapon],
    vests: list[ArmorDef],
    helmets: list[ArmorDef],
    distances: tuple[int, ...] = DISTANCES,
    armor_levels: tuple[int, ...] = ARMOR_LEVELS,
    zones: tuple[str, ...] = HIT_ZONES,
) -> list[TtkResult]:
    rows: list[TtkResult] = []
    for w in weapons:
        for dist in distances:
            for zone in zones:
                for a_lvl in armor_levels:
                    for h_lvl in armor_levels:
                        # Gövde için kask anlamsız ama matris tam kalsın: zone=body → vest, zone=head → helmet
                        # Raporlama: her kombinasyon; etkili zırh zone'a göre
                        vest = make_armor(vests, a_lvl)
                        helmet = make_armor(helmets, h_lvl)
                        rows.append(simulate_ttk(w, zone, float(dist), vest, helmet))
    return rows
