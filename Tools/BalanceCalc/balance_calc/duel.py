"""1v1 düello simülatörü ve kazanma oranı ısı haritası verisi."""

from __future__ import annotations

import math
import random
from dataclasses import dataclass
from typing import Optional

from .damage import ArmorState, MAX_HEALTH, apply_shot, make_armor
from .monte_carlo import hit_probability, _sample_pellets_hit
from .parse_catalogs import ArmorDef, Weapon


@dataclass
class DuelCell:
    weapon_a: str
    weapon_b: str
    distance_m: float
    armor_level: int
    win_rate_a: float
    mean_ttk_a: float
    mean_ttk_b: float
    trials: int


def _fire_tick(
    weapon: Weapon,
    zone: str,
    distance: float,
    armor: Optional[ArmorState],
    bloom: float,
    rng: random.Random,
) -> tuple[float, float, Optional[ArmorState]]:
    """Tek atış hasarı döner; bloom güncellenmiş."""
    p = hit_probability(weapon, distance, zone, aiming=True, bloom=bloom)
    pellets = _sample_pellets_hit(weapon, p, rng)
    dmg = 0.0
    if pellets > 0:
        if armor and armor.is_broken:
            armor = None
        hit = apply_shot(weapon, zone, distance, armor, pellets_hit=pellets)
        dmg = hit.damage
    bloom = min(weapon.max_bloom, bloom + weapon.bloom_per_shot)
    return dmg, bloom, armor


def simulate_duel(
    wa: Weapon,
    wb: Weapon,
    distance: float,
    vest_a: Optional[ArmorState],
    helmet_a: Optional[ArmorState],
    vest_b: Optional[ArmorState],
    helmet_b: Optional[ArmorState],
    zone: str = "body",
    trials: int = 200,
    seed: int = 7,
    max_time: float = 30.0,
) -> DuelCell:
    """
    İki oyuncu aynı anda ateş eder. Gövde bölgesi varsayılan.
    Kazanan: karşı tarafın HP'sini önce 0'a indiren.
    """
    rng = random.Random(seed)
    wins_a = 0
    ttk_a_sum = 0.0
    ttk_b_sum = 0.0
    finite_a = 0
    finite_b = 0

    for t in range(trials):
        # Her denemede bağımsız zırh kopyası
        arm_a = (helmet_a if zone == "head" else vest_a)
        arm_b = (helmet_b if zone == "head" else vest_b)
        arm_a = arm_a.copy() if arm_a else None
        arm_b = arm_b.copy() if arm_b else None

        hp_a = MAX_HEALTH
        hp_b = MAX_HEALTH
        next_a = 0.0
        next_b = 0.0
        bloom_a = 0.0
        bloom_b = 0.0
        ammo_a = wa.magazine_size
        ammo_b = wb.magazine_size
        time = 0.0
        reload_until_a = -1.0
        reload_until_b = -1.0

        while time < max_time and hp_a > 0 and hp_b > 0:
            # En yakın olay
            candidates = []
            if reload_until_a < 0 and ammo_a > 0:
                candidates.append(("a", next_a))
            elif reload_until_a >= 0:
                candidates.append(("ra", reload_until_a))
            if reload_until_b < 0 and ammo_b > 0:
                candidates.append(("b", next_b))
            elif reload_until_b >= 0:
                candidates.append(("rb", reload_until_b))
            if not candidates:
                break
            kind, t_event = min(candidates, key=lambda x: x[1])
            time = t_event

            if kind == "ra":
                ammo_a = wa.magazine_size
                reload_until_a = -1.0
                next_a = time
                bloom_a = 0.0
                continue
            if kind == "rb":
                ammo_b = wb.magazine_size
                reload_until_b = -1.0
                next_b = time
                bloom_b = 0.0
                continue

            if kind == "a":
                dmg, bloom_a, arm_b = _fire_tick(wa, zone, distance, arm_b, bloom_a, rng)
                hp_b -= dmg
                ammo_a -= 1
                if ammo_a <= 0:
                    reload_until_a = time + wa.reload_duration_seconds
                else:
                    next_a = time + wa.fire_interval_seconds
            else:
                dmg, bloom_b, arm_a = _fire_tick(wb, zone, distance, arm_a, bloom_b, rng)
                hp_a -= dmg
                ammo_b -= 1
                if ammo_b <= 0:
                    reload_until_b = time + wb.reload_duration_seconds
                else:
                    next_b = time + wb.fire_interval_seconds

        if hp_b <= 0 and hp_a > 0:
            wins_a += 1
            ttk_a_sum += time
            finite_a += 1
        elif hp_a <= 0 and hp_b > 0:
            ttk_b_sum += time
            finite_b += 1
        elif hp_a <= 0 and hp_b <= 0:
            # Berabere — yarı yarıya
            wins_a += 0.5
            ttk_a_sum += time
            ttk_b_sum += time
            finite_a += 1
            finite_b += 1

    return DuelCell(
        weapon_a=wa.display_name,
        weapon_b=wb.display_name,
        distance_m=distance,
        armor_level=vest_a.level if vest_a else 0,
        win_rate_a=round(wins_a / trials, 4),
        mean_ttk_a=round(ttk_a_sum / finite_a, 4) if finite_a else float("inf"),
        mean_ttk_b=round(ttk_b_sum / finite_b, 4) if finite_b else float("inf"),
        trials=trials,
    )


def duel_heatmap(
    weapons: list[Weapon],
    vests: list[ArmorDef],
    helmets: list[ArmorDef],
    distances: tuple[int, ...] = (10, 50, 100, 200),
    armor_level: int = 2,
    trials: int = 120,
) -> list[DuelCell]:
    vest = make_armor(vests, armor_level)
    cells: list[DuelCell] = []
    for i, wa in enumerate(weapons):
        for wb in weapons[i + 1 :]:
            for d in distances:
                cells.append(
                    simulate_duel(
                        wa, wb, float(d),
                        vest.copy() if vest else None,
                        None,
                        vest.copy() if vest else None,
                        None,
                        zone="body",
                        trials=trials,
                        seed=hash((wa.weapon_id, wb.weapon_id, d)) % (2**31),
                    )
                )
    return cells
