#!/usr/bin/env python3
"""Hızlı duman testleri — katalog parse + TTK tutarlılığı."""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from balance_calc.damage import distance_factor, make_armor, simulate_ttk
from balance_calc.parse_catalogs import load_catalogs


class BalanceSmokeTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        repo = ROOT.parents[1]
        cls.cat = load_catalogs(repo)
        cls.weapons = {w.weapon_id: w for w in cls.cat["weapons"]}
        cls.vests = cls.cat["vests"]
        cls.helmets = cls.cat["helmets"]

    def test_parses_ten_weapons(self):
        self.assertEqual(len(self.cat["weapons"]), 10)
        self.assertEqual(len(self.vests), 3)
        self.assertEqual(len(self.helmets), 3)

    def test_mpt55_rpm(self):
        w = self.weapons["Mpt55"]
        self.assertAlmostEqual(w.rpm, 750.0, places=1)

    def test_falloff_plateau_then_min(self):
        w = self.weapons["Sar9"]
        self.assertEqual(distance_factor(w, 10), 1.0)
        self.assertEqual(distance_factor(w, 70), w.min_damage_factor)

    def test_jng_headshot_one_tap_no_armor(self):
        w = self.weapons["Jng90"]
        r = simulate_ttk(w, "head", 50, None, None)
        self.assertEqual(r.shots_to_kill, 1)
        self.assertGreaterEqual(r.first_shot_damage, 100)

    def test_armor_reduces_damage(self):
        w = self.weapons["Mpt55"]
        bare = simulate_ttk(w, "body", 50, None, None)
        vest = make_armor(self.vests, 3)
        armored = simulate_ttk(w, "body", 50, vest, None)
        self.assertGreater(armored.shots_to_kill, bare.shots_to_kill)
        self.assertLess(armored.first_shot_damage, bare.first_shot_damage)

    def test_escort_pellets_close(self):
        w = self.weapons["Escort"]
        r = simulate_ttk(w, "body", 10, None, None)
        self.assertEqual(w.pellet_count, 9)
        self.assertEqual(r.shots_to_kill, 1)


if __name__ == "__main__":
    unittest.main()
