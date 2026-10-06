using System.Collections.Generic;
using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class HudLayoutTests
    {
        [Test]
        public void Compass_Place_InsideAndEdge()
        {
            var p = CompassLayout.Place(1, 20f, 720f, 4f);
            Assert.AreEqual(80f, p.X, 0.01f);
            Assert.IsFalse(p.Offscreen);
            var e = CompassLayout.Place(2, 170f, 720f, 4f);
            Assert.IsTrue(e.Offscreen);
            Assert.AreEqual(360f, e.X, 0.01f);
            Assert.AreEqual(CompassLayout.OffscreenAlpha, e.Alpha, 0.001f);
            var l = CompassLayout.Place(3, -170f, 720f, 4f);
            Assert.AreEqual(-360f, l.X, 0.01f);
        }

        [Test]
        public void Compass_EdgeFadeAndCenterAlpha()
        {
            Assert.AreEqual(1f, CompassLayout.Place(1, 0f, 720f, 4f).Alpha, 0.001f);
            var near = CompassLayout.Place(1, 85f, 720f, 4f);
            Assert.Less(near.Alpha, 1f);
            Assert.Greater(near.Alpha, CompassLayout.OffscreenAlpha);
        }

        [Test]
        public void Compass_DistanceScale_Monotonic()
        {
            Assert.AreEqual(1.25f, CompassLayout.DistanceScale(5f), 0.001f);
            Assert.AreEqual(0.7f, CompassLayout.DistanceScale(500f), 0.001f);
            Assert.Greater(CompassLayout.DistanceScale(50f), CompassLayout.DistanceScale(150f));
            Assert.AreEqual(1f, CompassLayout.DistanceScale(-3f), 0.001f);
        }

        [Test]
        public void Compass_Declutter_KeepsGapAndBounds()
        {
            var list = new List<CompassLayout.Placed>();
            for (var i = 0; i < 4; i++)
                list.Add(new CompassLayout.Placed { Id = i, X = 350f + i, Alpha = 1f, Scale = 1f });
            CompassLayout.Declutter(list, 12f, 720f);
            for (var i = 1; i < list.Count; i++)
                Assert.IsTrue(list[i].X - list[i - 1].X >= 12f - 0.01f);
            Assert.IsTrue(list[list.Count - 1].X <= 360.01f);
        }

        [Test]
        public void Compass_CenterReadout_SnapsToCardinal()
        {
            Assert.AreEqual("K", CompassLayout.CenterReadout(358f));
            Assert.AreEqual("D", CompassLayout.CenterReadout(93f));
            Assert.AreEqual("247°", CompassLayout.CenterReadout(247f));
            Assert.AreEqual("KB", CompassLayout.CenterReadout(316f));
        }

        [Test]
        public void Compass_TickHeight_GrowsNearCenter()
        {
            Assert.Greater(CompassLayout.TickHeight(45, 0f), CompassLayout.TickHeight(45, 90f));
            Assert.AreEqual(12f, CompassLayout.TickHeight(90, 120f), 0.01f);
        }

        [Test]
        public void Ammo_Tiers()
        {
            Assert.AreEqual(AmmoTier.Empty, AmmoPipLayout.Tier(0, 30));
            Assert.AreEqual(AmmoTier.Critical, AmmoPipLayout.Tier(3, 30));
            Assert.AreEqual(AmmoTier.Low, AmmoPipLayout.Tier(8, 30));
            Assert.AreEqual(AmmoTier.Normal, AmmoPipLayout.Tier(20, 30));
            Assert.AreEqual(AmmoTier.Normal, AmmoPipLayout.Tier(5, 0));
        }

        [Test]
        public void Ammo_Pips_CapAndScale()
        {
            var a = AmmoPipLayout.Compute(10, 30);
            Assert.AreEqual(30, a.Count);
            Assert.AreEqual(10, a.Filled);
            var b = AmmoPipLayout.Compute(100, 100);
            Assert.AreEqual(1, b.RoundsPerPip + 0 - b.RoundsPerPip + 1);
            Assert.IsTrue(b.Count <= AmmoPipLayout.MaxPips);
            Assert.AreEqual(b.Count, b.Filled);
            var c = AmmoPipLayout.Compute(1, 100);
            Assert.AreEqual(1, c.Filled);
        }

        [Test]
        public void Ammo_PipWidthAndReserve()
        {
            Assert.AreEqual(0f, AmmoPipLayout.PipX(0, 5f, 2f, 4f, 5), 0.001f);
            Assert.AreEqual(5f * 7f + 4f, AmmoPipLayout.PipX(5, 5f, 2f, 4f, 5), 0.001f);
            Assert.AreEqual(0f, AmmoPipLayout.TotalWidth(0, 5f, 2f, 4f, 5), 0.001f);
            Assert.AreEqual(3, AmmoPipLayout.ReserveMagazines(70, 30));
            Assert.AreEqual(6, AmmoPipLayout.ReserveMagazines(900, 30));
            Assert.AreEqual(0, AmmoPipLayout.ReserveMagazines(0, 30));
            Assert.IsTrue(AmmoPipLayout.ShowReloadHint(AmmoTier.Empty, 30, false));
            Assert.IsFalse(AmmoPipLayout.ShowReloadHint(AmmoTier.Empty, 30, true));
            Assert.IsFalse(AmmoPipLayout.ShowReloadHint(AmmoTier.Normal, 30, false));
        }

        [Test]
        public void Damage_MergesNearbyAndCaps()
        {
            var arcs = new List<DamageArcLayout.Arc>();
            DamageArcLayout.Add(arcs, 10f, 20f);
            DamageArcLayout.Add(arcs, 15f, 20f);
            Assert.AreEqual(1, arcs.Count);
            Assert.Greater(arcs[0].Angle, 10f);
            Assert.Less(arcs[0].Angle, 15.01f);
            DamageArcLayout.Add(arcs, 180f, 20f);
            Assert.AreEqual(2, arcs.Count);
            for (var i = 0; i < 20; i++)
                DamageArcLayout.Add(arcs, 40f * i + 30f, 10f);
            Assert.IsTrue(arcs.Count <= DamageArcLayout.MaxArcs);
        }

        [Test]
        public void Damage_WrapAroundMerge()
        {
            var arcs = new List<DamageArcLayout.Arc>();
            DamageArcLayout.Add(arcs, 175f, 15f);
            DamageArcLayout.Add(arcs, -175f, 15f);
            Assert.AreEqual(1, arcs.Count);
        }

        [Test]
        public void Damage_TickExpiresAndIgnoresInvalid()
        {
            var arcs = new List<DamageArcLayout.Arc>();
            DamageArcLayout.Add(arcs, 0f, 0f);
            DamageArcLayout.Add(arcs, float.NaN, 10f);
            Assert.AreEqual(0, arcs.Count);
            DamageArcLayout.Add(arcs, 0f, 30f);
            DamageArcLayout.Tick(arcs, 1f);
            Assert.AreEqual(1, arcs.Count);
            DamageArcLayout.Tick(arcs, 5f);
            Assert.AreEqual(0, arcs.Count);
        }

        [Test]
        public void Damage_WidthAndRing()
        {
            Assert.AreEqual(DamageArcLayout.MinWidthDeg, DamageArcLayout.WidthForDamage(0f), 0.01f);
            Assert.AreEqual(DamageArcLayout.MaxWidthDeg, DamageArcLayout.WidthForDamage(500f), 0.01f);
            var v = DamageArcLayout.RingPosition(90f, 100f);
            Assert.AreEqual(100f, v.x, 0.01f);
            Assert.AreEqual(0f, v.y, 0.01f);
        }

        [Test]
        public void VitalBar_GhostHoldsThenDrains()
        {
            var m = new VitalBarModel();
            m.Tick(1f, 0.016f);
            m.Tick(0.5f, 0.016f);
            Assert.AreEqual(0.5f, m.Value, 0.001f);
            Assert.AreEqual(1f, m.Ghost, 0.001f);
            m.Tick(0.5f, 0.3f);
            Assert.AreEqual(1f, m.Ghost, 0.001f);
            for (var i = 0; i < 100; i++)
                m.Tick(0.5f, 0.1f);
            Assert.AreEqual(0.5f, m.Ghost, 0.001f);
        }

        [Test]
        public void VitalBar_HealCatchesUp_LowPulse_Segments()
        {
            var m = new VitalBarModel();
            m.Tick(0.2f, 0.016f);
            Assert.IsTrue(m.IsLow);
            Assert.Greater(m.LowPulse(0.125f), 0.9f);
            m.Tick(0.9f, 0.016f);
            Assert.AreEqual(0.9f, m.Ghost, 0.001f);
            Assert.IsFalse(m.IsLow);
            Assert.AreEqual(0f, m.LowPulse(0.1f), 0.001f);
            Assert.AreEqual(5, VitalBarModel.FilledSegments(0.5f, 10));
            Assert.AreEqual(6, VitalBarModel.FilledSegments(0.51f, 10));
            Assert.AreEqual(0, VitalBarModel.FilledSegments(0f, 10));
            Assert.AreEqual(10, VitalBarModel.FilledSegments(2f, 10));
        }

        [Test]
        public void Contrast_BlackWhiteIs21()
        {
            Assert.AreEqual(21f, HudContrast.Ratio(Color.white, Color.black), 0.05f);
            Assert.AreEqual(1f, HudContrast.Ratio(Color.gray, Color.gray), 0.001f);
        }

        [Test]
        public void Contrast_ShadowAndReadable()
        {
            var lowRatioShadow = HudContrast.ShadowAlpha(new Color(0.7f, 0.7f, 0.65f), HudContrast.WorstCaseBackground, HudContrast.CriticalRatio);
            var whiteShadow = HudContrast.ShadowAlpha(Color.black, Color.white, HudContrast.TextRatio);
            Assert.Greater(lowRatioShadow, whiteShadow);
            Assert.IsTrue(lowRatioShadow <= 0.95f);
            var fixedColor = HudContrast.EnsureReadable(new Color(0.9f, 0.9f, 0.9f, 0.5f), Color.white, 4.5f);
            Assert.IsTrue(HudContrast.Ratio(fixedColor, Color.white) >= 4.5f);
            Assert.AreEqual(0.5f, fixedColor.a, 0.001f);
            Assert.AreEqual(1f, HudContrast.ShadowOffset(12f).x, 0.001f);
            Assert.AreEqual(-2f, HudContrast.ShadowOffset(40f).y, 0.001f);
        }

        [Test]
        public void Contrast_MinFontScales()
        {
            Assert.AreEqual(14, HudContrast.MinFont(1080f));
            Assert.AreEqual(28, HudContrast.MinFont(4000f));
            Assert.AreEqual(10, HudContrast.MinFont(100f));
        }

        [Test]
        public void Squad_SortLocalCommanderThenState()
        {
            var rows = new List<SquadListLayout.Row>
            {
                new SquadListLayout.Row { Slot = 4, State = SquadRowState.Dead },
                new SquadListLayout.Row { Slot = 3, State = SquadRowState.Alive },
                new SquadListLayout.Row { Slot = 2, State = SquadRowState.Downed },
                new SquadListLayout.Row { Slot = 1, State = SquadRowState.Alive, IsCommander = true },
                new SquadListLayout.Row { Slot = 0, State = SquadRowState.Alive, IsLocal = true },
            };
            SquadListLayout.Sort(rows);
            Assert.AreEqual(0, rows[0].Slot);
            Assert.AreEqual(1, rows[1].Slot);
            Assert.AreEqual(3, rows[2].Slot);
            Assert.AreEqual(2, rows[3].Slot);
            Assert.AreEqual(4, rows[4].Slot);
        }

        [Test]
        public void Squad_SortCapsRows_Geometry_Labels()
        {
            var rows = new List<SquadListLayout.Row>();
            for (var i = 0; i < 14; i++)
                rows.Add(new SquadListLayout.Row { Slot = i });
            SquadListLayout.Sort(rows);
            Assert.AreEqual(SquadListLayout.MaxRows, rows.Count);
            Assert.AreEqual(0f, SquadListLayout.RowY(0), 0.001f);
            Assert.AreEqual(-29f, SquadListLayout.RowY(1), 0.001f);
            Assert.AreEqual(0f, SquadListLayout.PanelHeight(0), 0.001f);
            Assert.AreEqual(26f * 2f + 3f, SquadListLayout.PanelHeight(2), 0.001f);
            Assert.AreEqual("", SquadListLayout.DistanceLabel(0.2f));
            Assert.AreEqual("120m", SquadListLayout.DistanceLabel(118f));
            Assert.AreEqual("1.5km", SquadListLayout.DistanceLabel(1500f));
            Assert.Less(SquadListLayout.RowAlpha(SquadRowState.Dead, 0f), SquadListLayout.RowAlpha(SquadRowState.Alive, 0f));
        }
    }
}
