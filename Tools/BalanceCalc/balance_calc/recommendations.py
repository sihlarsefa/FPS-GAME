"""Otomatik denge önerileri ve gerekçeleri."""

from __future__ import annotations

from collections import defaultdict
from dataclasses import dataclass
from statistics import median
from typing import Any

from .damage import TtkResult
from .parse_catalogs import Weapon


@dataclass
class Recommendation:
    severity: str  # kritik | uyarı | bilgi
    weapon: str
    category: str
    title: str
    rationale: str
    suggestion: str


CATEGORY_TR = {
    "Pistol": "Tabanca",
    "Smg": "Hafif Makineli",
    "AssaultRifle": "Piyade Tüfeği",
    "Dmr": "Nişancı Tüfeği",
    "Sniper": "Keskin Nişancı",
    "Lmg": "Makineli Tüfek",
    "Shotgun": "Pompalı",
    "Melee": "Yakın Dövüş",
}


def _filter_rows(
    rows: list[TtkResult],
    distance: float = 50.0,
    zone: str = "body",
    armor: int = 2,
    helmet: int = 2,
) -> list[TtkResult]:
    return [
        r
        for r in rows
        if abs(r.distance_m - distance) < 0.01
        and r.zone == zone
        and r.armor_level == armor
        and r.helmet_level == helmet
    ]


def generate_recommendations(
    weapons: list[Weapon],
    rows: list[TtkResult],
) -> list[Recommendation]:
    recs: list[Recommendation] = []
    by_cat: dict[str, list[TtkResult]] = defaultdict(list)

    baseline = _filter_rows(rows, 50, "body", 2, 2)
    by_id = {r.weapon_id: r for r in baseline}
    wpn_by_id = {w.weapon_id: w for w in weapons}

    for r in baseline:
        by_cat[r.category].append(r)

    # Genel sıralama (tüm silahlar, 50 m gövde Sv.2)
    ranked = sorted(baseline, key=lambda x: x.ttk_seconds)
    if ranked:
        fastest, slowest = ranked[0], ranked[-1]
        med_all = median([x.ttk_seconds for x in ranked])
        if fastest.ttk_seconds < med_all * 0.55 and fastest.category not in ("Lmg",):
            recs.append(
                Recommendation(
                    severity="uyarı",
                    weapon=fastest.display_name,
                    category=CATEGORY_TR.get(fastest.category, fastest.category),
                    title="Genel listede en düşük TTK",
                    rationale=(
                        f"{fastest.display_name} 50 m gövde Sv.2 TTK={fastest.ttk_seconds:.3f}s "
                        f"({fastest.shots_to_kill} vuruş); tüm silah medyanı {med_all:.3f}s."
                    ),
                    suggestion=(
                        "Rolüne göre kabul edilebilir olabilir (LMG/AR). "
                        "Diğer orta menzil silahlarla farkı %30+ ise hasar veya RPM’yi kısın."
                    ),
                )
            )
        if slowest.ttk_seconds > med_all * 2.0 and slowest.category == "Shotgun":
            recs.append(
                Recommendation(
                    severity="bilgi",
                    weapon=slowest.display_name,
                    category="Pompalı",
                    title="50 m’de pompalı yavaş (beklenen)",
                    rationale=(
                        f"{slowest.display_name} TTK={slowest.ttk_seconds:.3f}s — "
                        f"falloff 8–40 m tasarımı nedeniyle 50 m’de zayıf."
                    ),
                    suggestion="Yakın dövüş rolü korunuyor; CQB mesafesinde (≤15 m) kontrol edin.",
                )
            )

    # Kategori içi sapma (eşikler biraz gevşek)
    for cat, items in by_cat.items():
        if len(items) < 2:
            continue
        ttks = [x.ttk_seconds for x in items]
        med = median(ttks)
        if med <= 0:
            continue
        for x in items:
            ratio = x.ttk_seconds / med
            cat_tr = CATEGORY_TR.get(cat, cat)
            if ratio < 0.85:
                recs.append(
                    Recommendation(
                        severity="kritik" if ratio < 0.7 else "uyarı",
                        weapon=x.display_name,
                        category=cat_tr,
                        title="Kategori içinde güçlü",
                        rationale=(
                            f"{x.display_name} 50 m gövde / Sv.2 zırhta TTK={x.ttk_seconds:.3f}s "
                            f"({x.shots_to_kill} vuruş); kategori medyanı {med:.3f}s "
                            f"(oran {ratio:.2f})."
                        ),
                        suggestion=(
                            "Hasarı %5–10 düşürün, FalloffStart’ı yaklaştırın veya "
                            "FireInterval’ı artırın (RPM düşür)."
                        ),
                    )
                )
            elif ratio > 1.2:
                recs.append(
                    Recommendation(
                        severity="uyarı",
                        weapon=x.display_name,
                        category=cat_tr,
                        title="Kategori içinde zayıf",
                        rationale=(
                            f"{x.display_name} TTK={x.ttk_seconds:.3f}s, kategori medyanı {med:.3f}s "
                            f"(oran {ratio:.2f})."
                        ),
                        suggestion=(
                            "Hasarı %5–10 artırın, MinDamageFactor yükseltin veya "
                            "şarjör/yeniden doldurma süresini iyileştirin."
                        ),
                    )
                )

    # AR ailesi: 5.56 vs 7.62 farkı
    ars = by_cat.get("AssaultRifle", [])
    if len(ars) >= 2:
        slow_ar = max(ars, key=lambda x: x.ttk_seconds)
        fast_ar = min(ars, key=lambda x: x.ttk_seconds)
        if slow_ar.ttk_seconds > fast_ar.ttk_seconds * 1.15:
            recs.append(
                Recommendation(
                    severity="bilgi",
                    weapon=slow_ar.display_name,
                    category="Piyade Tüfeği",
                    title="AR ailesi TTK ayrışması",
                    rationale=(
                        f"En hızlı AR {fast_ar.display_name} ({fast_ar.ttk_seconds:.3f}s) vs "
                        f"{slow_ar.display_name} ({slow_ar.ttk_seconds:.3f}s). "
                        f"7.62’ler daha az vuruş, 5.56 daha yüksek RPM ile telafi ediyor."
                    ),
                    suggestion=(
                        "MPT-55’in vuruş sayısını 1 düşürmek için hasarı ~18–20 bandına "
                        "yaklaştırmayın; mevcut ayrım rol farkını koruyor. İsterseniz "
                        "MPT-55 MinDamageFactor’ü 0.70 yaparak uzak mesafeyi güçlendirin."
                    ),
                )
            )

    # Uzak mesafe anomali: 10m vs 300m gövde zırhsız
    close = {r.weapon_id: r for r in _filter_rows(rows, 10, "body", 0, 0)}
    far = {r.weapon_id: r for r in _filter_rows(rows, 300, "body", 0, 0)}
    mid = {r.weapon_id: r for r in _filter_rows(rows, 50, "body", 0, 0)}
    for w in weapons:
        c, f = close.get(w.weapon_id), far.get(w.weapon_id)
        if not c or not f:
            continue
        # Pompalı uzakta zayıf olmalı
        if w.category == "Shotgun" and f.shots_to_kill >= max(3, c.shots_to_kill * 2):
            recs.append(
                Recommendation(
                    severity="bilgi",
                    weapon=w.display_name,
                    category=CATEGORY_TR.get(w.category, w.category),
                    title="Pompalı mesafe düşüşü sağlıklı",
                    rationale=(
                        f"10 m: {c.shots_to_kill} vuruş / {c.ttk_seconds:.2f}s → "
                        f"300 m: {f.shots_to_kill} vuruş / {f.ttk_seconds:.2f}s "
                        f"(ilk hasar {c.first_shot_damage:.0f} → {f.first_shot_damage:.0f})."
                    ),
                    suggestion="Yakın muharebe rolü korunuyor; değişiklik gerekmez.",
                )
            )
        elif w.category in ("AssaultRifle", "Dmr", "Lmg"):
            base_ttk = c.ttk_seconds if c.ttk_seconds > 0 else w.fire_interval_seconds
            if f.ttk_seconds > base_ttk * 2.2:
                recs.append(
                    Recommendation(
                        severity="uyarı",
                        weapon=w.display_name,
                        category=CATEGORY_TR.get(w.category, w.category),
                        title="Uzun mesafede aşırı cezalı",
                        rationale=(
                            f"TTK 10 m={c.ttk_seconds:.3f}s → 300 m={f.ttk_seconds:.3f}s "
                            f"(×{f.ttk_seconds / max(base_ttk, 1e-6):.1f})."
                        ),
                        suggestion="FalloffEnd’i uzatın veya MinDamageFactor’ü 0.05–0.1 artırın.",
                    )
                )
            else:
                recs.append(
                    Recommendation(
                        severity="bilgi",
                        weapon=w.display_name,
                        category=CATEGORY_TR.get(w.category, w.category),
                        title="Menzil düşüşü makul",
                        rationale=(
                            f"10 m {c.shots_to_kill}v/{c.ttk_seconds:.2f}s → "
                            f"300 m {f.shots_to_kill}v/{f.ttk_seconds:.2f}s "
                            f"(hasar {c.first_shot_damage:.1f} → {f.first_shot_damage:.1f})."
                        ),
                        suggestion="Mevcut falloff eğrisi orta menzil rolüyle uyumlu.",
                    )
                )

    # Tabanca vs SMG CQB
    pistols = [by_id[w.weapon_id] for w in weapons if w.category == "Pistol" and w.weapon_id in by_id]
    smgs = [by_id[w.weapon_id] for w in weapons if w.category == "Smg" and w.weapon_id in by_id]
    if pistols and smgs:
        best_p = min(pistols, key=lambda x: x.ttk_seconds)
        best_s = min(smgs, key=lambda x: x.ttk_seconds)
        if best_s.ttk_seconds < best_p.ttk_seconds:
            recs.append(
                Recommendation(
                    severity="bilgi",
                    weapon=best_s.display_name,
                    category="Hafif Makineli",
                    title="SMG tabancadan hızlı (beklenen)",
                    rationale=(
                        f"{best_s.display_name} TTK {best_s.ttk_seconds:.3f}s < "
                        f"{best_p.display_name} {best_p.ttk_seconds:.3f}s (50 m Sv.2)."
                    ),
                    suggestion="Tabancayı yan silah olarak tutun; SMG CQB birincil kalmalı.",
                )
            )

    # Tek atış kafa (zırhsız) kontrolü
    head0 = {r.weapon_id: r for r in _filter_rows(rows, 50, "head", 0, 0)}
    for w in weapons:
        r = head0.get(w.weapon_id)
        if not r:
            continue
        if r.shots_to_kill == 1 and w.category not in ("Sniper", "Dmr", "Shotgun"):
            recs.append(
                Recommendation(
                    severity="uyarı",
                    weapon=w.display_name,
                    category=CATEGORY_TR.get(w.category, w.category),
                    title="50 m zırhsız tek kafa",
                    rationale=(
                        f"İlk kafa hasarı {r.first_shot_damage:.1f} ≥ 100; "
                        f"kategori {CATEGORY_TR.get(w.category, w.category)} için agresif."
                    ),
                    suggestion="HeadshotMultiplier’ı 0.1–0.2 düşürün veya taban hasarı azaltın.",
                )
            )
        if w.category == "Sniper":
            if r.shots_to_kill >= 3:
                recs.append(
                    Recommendation(
                        severity="kritik",
                        weapon=w.display_name,
                        category="Keskin Nişancı",
                        title="Keskin nişancı kafası yetersiz",
                        rationale=f"{r.shots_to_kill} kafa vuruşu gerekiyor (beklenen 1).",
                        suggestion="Hasar veya HeadshotMultiplier artırın.",
                    )
                )
            elif r.shots_to_kill == 1:
                body = mid.get(w.weapon_id)
                recs.append(
                    Recommendation(
                        severity="bilgi",
                        weapon=w.display_name,
                        category="Keskin Nişancı",
                        title="Tek kafa — keskin nişancı rolü",
                        rationale=(
                            f"Zırhsız kafa tek vuruş ({r.first_shot_damage:.0f} hasar). "
                            + (
                                f"Gövde {body.shots_to_kill} vuruş / {body.ttk_seconds:.2f}s."
                                if body
                                else ""
                            )
                        ),
                        suggestion="Bolt-action temposu (1.4 s) ile dengelenmiş; Sv.3 kaskta 2. vuruşa çıkıyor mu kontrol edin.",
                    )
                )

    # Kask Sv.3 vs zırhsız kafa
    head3 = {r.weapon_id: r for r in _filter_rows(rows, 50, "head", 0, 3)}
    for w in weapons:
        a = head0.get(w.weapon_id)
        b = head3.get(w.weapon_id)
        if not a or not b:
            continue
        if b.shots_to_kill > a.shots_to_kill:
            recs.append(
                Recommendation(
                    severity="bilgi",
                    weapon=w.display_name,
                    category=CATEGORY_TR.get(w.category, w.category),
                    title="Sv.3 kask vuruş ekliyor",
                    rationale=(
                        f"Zırhsız kafa {a.shots_to_kill} vuruş → Sv.3 kask {b.shots_to_kill} vuruş "
                        f"(TTK {a.ttk_seconds:.2f}s → {b.ttk_seconds:.2f}s, "
                        f"hasar {a.first_shot_damage:.0f} → {b.first_shot_damage:.0f})."
                    ),
                    suggestion="Kask ilerleme ödülü çalışıyor; Sv.3’ü end-game hedefi olarak tutun.",
                )
            )

    # JNG gövde 2-shot vs KNT
    jng = by_id.get("Jng90")
    knt = by_id.get("Knt76")
    if jng and knt and jng.ttk_seconds > knt.ttk_seconds * 1.3:
        recs.append(
            Recommendation(
                severity="uyarı",
                weapon="JNG-90",
                category="Keskin Nişancı",
                title="Gövde TTK’da DMR’dan yavaş",
                rationale=(
                    f"JNG-90 gövde TTK {jng.ttk_seconds:.3f}s vs KNT-76 {knt.ttk_seconds:.3f}s "
                    f"(bolt-action aralığı {wpn_by_id['Jng90'].fire_interval_seconds}s)."
                ),
                suggestion=(
                    "Kasıtlıysa sorun yok (kafa odaklı). Gövde rekabeti isteniyorsa "
                    "hasarı 95–100’e çıkarıp 2-shot’ı Sv.2’de koruyun."
                ),
            )
        )

    # Tekrarları silah+başlık ile sadeleştir
    seen: set[tuple[str, str]] = set()
    unique: list[Recommendation] = []
    for r in recs:
        key = (r.weapon, r.title)
        if key in seen:
            continue
        seen.add(key)
        unique.append(r)

    order = {"kritik": 0, "uyarı": 1, "bilgi": 2}
    unique.sort(key=lambda r: (order.get(r.severity, 9), r.weapon))
    return unique


def what_if_delta(
    weapon: Weapon,
    overrides: dict[str, Any],
    vests: list,
    helmets: list,
    distance: float = 50.0,
    zone: str = "body",
    armor_level: int = 2,
    helmet_level: int = 2,
) -> dict[str, Any]:
    """Değer değiştirip TTK farkını hesapla."""
    from copy import deepcopy
    from .damage import simulate_ttk, make_armor

    base_w = deepcopy(weapon)
    mod_w = deepcopy(weapon)
    for k, v in overrides.items():
        if hasattr(mod_w, k):
            setattr(mod_w, k, type(getattr(mod_w, k))(v))

    vest = make_armor(vests, armor_level)
    helmet = make_armor(helmets, helmet_level)
    base = simulate_ttk(base_w, zone, distance, vest, helmet)
    vest2 = make_armor(vests, armor_level)
    helmet2 = make_armor(helmets, helmet_level)
    mod = simulate_ttk(mod_w, zone, distance, vest2, helmet2)

    return {
        "weapon": weapon.display_name,
        "overrides": overrides,
        "baseline": {
            "ttk": base.ttk_seconds,
            "shots": base.shots_to_kill,
            "first_damage": base.first_shot_damage,
        },
        "modified": {
            "ttk": mod.ttk_seconds,
            "shots": mod.shots_to_kill,
            "first_damage": mod.first_shot_damage,
        },
        "delta_ttk": round(mod.ttk_seconds - base.ttk_seconds, 4),
        "delta_shots": mod.shots_to_kill - base.shots_to_kill,
    }
