"""Sekme / sapma dahil Monte Carlo isabet ve beklenen TTK."""

from __future__ import annotations

import math
import random
from dataclasses import dataclass
from typing import Optional

from .damage import ArmorState, MAX_HEALTH, apply_shot, make_armor
from .parse_catalogs import ArmorDef, Weapon

# Hedef siluet (metre): gövde ~50 cm genişlik × 60 cm yükseklik; kafa ~20 cm
TORSO_HALF_WIDTH = 0.25
TORSO_HALF_HEIGHT = 0.30
HEAD_HALF_WIDTH = 0.10
HEAD_HALF_HEIGHT = 0.12


@dataclass
class MonteCarloResult:
    weapon_id: str
    display_name: str
    distance_m: float
    zone: str
    armor_level: int
    helmet_level: int
    trials: int
    mean_hit_chance: float
    mean_expected_ttk: float
    p50_ttk: float
    p90_ttk: float
    mean_shots_fired: float


def _angular_half_size(half_metres: float, distance: float) -> float:
    """Yarı boyutun açısal yarıçapı (derece)."""
    d = max(distance, 0.5)
    return math.degrees(math.atan(half_metres / d))


def hit_probability(
    weapon: Weapon,
    distance: float,
    zone: str,
    aiming: bool = True,
    bloom: float = 0.0,
) -> float:
    """
    Basit model: sapma konisi yarı açısı vs hedef açısal yarıçap.
    İsabet ≈ clamp( (hedef_yarı / spread_yarı)^2 , 0, 1 )
    """
    base = weapon.ads_spread if aiming else weapon.hip_spread
    bloom_factor = 0.5 if aiming else 1.0
    spread = base + bloom * bloom_factor
    if weapon.pellet_count > 1:
        spread = math.hypot(spread, weapon.pellet_spread * 0.5)

    if zone == "head":
        ang = min(
            _angular_half_size(HEAD_HALF_WIDTH, distance),
            _angular_half_size(HEAD_HALF_HEIGHT, distance),
        )
    else:
        ang = math.sqrt(
            _angular_half_size(TORSO_HALF_WIDTH, distance)
            * _angular_half_size(TORSO_HALF_HEIGHT, distance)
        )

    if spread <= 1e-6:
        return 1.0
    ratio = ang / spread
    # Koni içi düzgün dağılım: P(r < ang) = (ang/spread)^2
    return max(0.0, min(1.0, ratio * ratio))


def _sample_pellets_hit(weapon: Weapon, p_hit: float, rng: random.Random) -> int:
    if weapon.pellet_count <= 1:
        return 1 if rng.random() < p_hit else 0
    # Her saçma bağımsız; pellet_spread ek sapma → biraz düşük isabet
    pellet_p = p_hit * 0.85 if weapon.pellet_spread > 0 else p_hit
    return sum(1 for _ in range(weapon.pellet_count) if rng.random() < pellet_p)


def simulate_expected_ttk(
    weapon: Weapon,
    zone: str,
    distance: float,
    vest: Optional[ArmorState],
    helmet: Optional[ArmorState],
    trials: int = 400,
    seed: int = 42,
    max_shots: int = 120,
) -> MonteCarloResult:
    rng = random.Random(seed)
    p_base = hit_probability(weapon, distance, zone, aiming=True, bloom=0.0)

    ttks: list[float] = []
    shots_list: list[int] = []
    hit_rates: list[float] = []

    for t in range(trials):
        armor = (helmet if zone == "head" else vest)
        armor = armor.copy() if armor else None
        hp = MAX_HEALTH
        bloom = 0.0
        time_s = 0.0
        shots = 0
        hits = 0

        while hp > 0 and shots < max_shots:
            p = hit_probability(weapon, distance, zone, aiming=True, bloom=bloom)
            pellets = _sample_pellets_hit(weapon, p, rng)
            if pellets > 0:
                if armor and armor.is_broken:
                    armor = None
                hit = apply_shot(weapon, zone, distance, armor, pellets_hit=pellets)
                hp -= hit.damage
                hits += 1
            shots += 1
            bloom = min(weapon.max_bloom, bloom + weapon.bloom_per_shot)
            # Bloom decay between shots
            bloom = max(0.0, bloom - 4.0 * weapon.fire_interval_seconds * 0.5)
            if hp > 0:
                time_s += weapon.fire_interval_seconds
            # Reload if magazine empty
            if shots % max(1, weapon.magazine_size) == 0 and hp > 0:
                time_s += weapon.reload_duration_seconds
                bloom = 0.0

        ttks.append(time_s if hp <= 0 else float("inf"))
        shots_list.append(shots)
        hit_rates.append(hits / shots if shots else 0.0)

    finite = [x for x in ttks if math.isfinite(x)]
    finite.sort()

    def pct(xs: list[float], p: float) -> float:
        if not xs:
            return float("inf")
        i = min(len(xs) - 1, int(round((p / 100.0) * (len(xs) - 1))))
        return xs[i]

    return MonteCarloResult(
        weapon_id=weapon.weapon_id,
        display_name=weapon.display_name,
        distance_m=distance,
        zone=zone,
        armor_level=vest.level if vest else 0,
        helmet_level=helmet.level if helmet else 0,
        trials=trials,
        mean_hit_chance=round(sum(hit_rates) / len(hit_rates), 4),
        mean_expected_ttk=round(sum(finite) / len(finite), 4) if finite else float("inf"),
        p50_ttk=round(pct(finite, 50), 4),
        p90_ttk=round(pct(finite, 90), 4),
        mean_shots_fired=round(sum(shots_list) / len(shots_list), 2),
    )


def run_monte_carlo_suite(
    weapons: list[Weapon],
    vests: list[ArmorDef],
    helmets: list[ArmorDef],
    distances: tuple[int, ...] = (50, 100, 200),
    armor_level: int = 2,
    helmet_level: int = 2,
    trials: int = 300,
) -> list[MonteCarloResult]:
    vest = make_armor(vests, armor_level)
    helmet = make_armor(helmets, helmet_level)
    out: list[MonteCarloResult] = []
    for w in weapons:
        for d in distances:
            for zone in ("body", "head"):
                out.append(
                    simulate_expected_ttk(
                        w, zone, float(d),
                        vest.copy() if vest else None,
                        helmet.copy() if helmet else None,
                        trials=trials,
                    )
                )
    return out
