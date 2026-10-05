#!/usr/bin/env python3
"""Kapasite formülü birim testleri — Deploy katmanı."""
from __future__ import annotations

import math
import sys
import unittest

PLAYERS_PER_MATCH = 50
SERVERS_PER_NODE = 2
BUFFER = 5


def servers_for_ccu(ccu: int) -> int:
    matches = math.ceil(ccu / PLAYERS_PER_MATCH)
    return matches + BUFFER


def nodes_for_servers(servers: int) -> int:
    return math.ceil(servers / SERVERS_PER_NODE)


class CapacityTests(unittest.TestCase):
    def test_500_ccu(self) -> None:
        s = servers_for_ccu(500)
        self.assertEqual(s, 15)
        self.assertEqual(nodes_for_servers(s), 8)

    def test_1000_ccu(self) -> None:
        s = servers_for_ccu(1000)
        self.assertEqual(s, 25)
        self.assertEqual(nodes_for_servers(s), 13)

    def test_10000_ccu(self) -> None:
        s = servers_for_ccu(10_000)
        self.assertEqual(s, 205)
        self.assertEqual(nodes_for_servers(s), 103)

    def test_squad_fits_match(self) -> None:
        squad = 10
        self.assertEqual(40 % squad, 0)
        self.assertEqual(60 % squad, 0)
        self.assertTrue(40 <= PLAYERS_PER_MATCH <= 60)

    def test_spot_ratio_bounds(self) -> None:
        for nodes in (10, 50, 100):
            spot = (nodes * 70) // 100
            ondemand = nodes - spot
            self.assertGreaterEqual(ondemand, 0)
            self.assertLessEqual(spot / nodes, 0.75)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(CapacityTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if result.wasSuccessful() else 1)
