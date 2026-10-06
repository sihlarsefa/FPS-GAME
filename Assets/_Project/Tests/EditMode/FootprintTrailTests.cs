#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Vfx;

namespace Project.Tests.EditMode
{
    public sealed class FootprintTrailTests
    {
        [Test]
        public void OnlySnowAndDirtTrack()
        {
            Assert.IsTrue(FootprintRules.IsTrackSurface(SurfaceKind.Snow));
            Assert.IsTrue(FootprintRules.IsTrackSurface(SurfaceKind.Dirt));
            Assert.IsFalse(FootprintRules.IsTrackSurface(SurfaceKind.Concrete));
            Assert.IsFalse(FootprintRules.IsTrackSurface(SurfaceKind.Water));
        }

        [Test]
        public void Capacity_TierLimited_Max256()
        {
            Assert.AreEqual(256, FootprintRules.CapacityFor(VfxTier.High));
            Assert.Less(FootprintRules.CapacityFor(VfxTier.Low), FootprintRules.CapacityFor(VfxTier.Medium));
            Assert.IsFalse(FootprintRules.VehicleTracksEnabled(VfxTier.Low));
            Assert.IsTrue(FootprintRules.VehicleTracksEnabled(VfxTier.High));
        }

        [Test]
        public void Spacing_StepsEveryStride_AndRejectsJunk()
        {
            var acc = 0f;
            var steps = 0;
            for (var i = 0; i < 100; i++)
                if (FootprintRules.Advance(ref acc, 0.1f, FootprintRules.StepSpacing)) steps++;
            Assert.AreEqual(11, steps); // 10 m / 0.85
            Assert.IsFalse(FootprintRules.Advance(ref acc, -1f, 0.85f));
            Assert.IsFalse(FootprintRules.Advance(ref acc, float.NaN, 0.85f));
            acc = 0f;
            Assert.IsTrue(FootprintRules.Advance(ref acc, 50f, 0.85f));
            Assert.AreEqual(0f, acc);
        }

        [Test]
        public void Feet_Alternate_Sides()
        {
            Assert.Less(FootprintRules.SideOffset(true), 0f);
            Assert.Greater(FootprintRules.SideOffset(false), 0f);
        }

        [Test]
        public void Alpha_FullThenFadesToZeroAt60s()
        {
            Assert.AreEqual(1f, FootprintRules.AlphaAt(0f));
            Assert.AreEqual(1f, FootprintRules.AlphaAt(40f));
            Assert.AreEqual(0.5f, FootprintRules.AlphaAt(50f), 1e-4f);
            Assert.AreEqual(0f, FootprintRules.AlphaAt(60f));
            Assert.AreEqual(0f, FootprintRules.AlphaAt(500f));
        }

        [Test]
        public void Ledger_RingOverwritesOldest_ActiveNeverExceedsCap()
        {
            var l = new FootprintLedger(4);
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(i % 4, l.NextSlot);
                Assert.AreEqual(i % 4, l.Add(i));
            }

            Assert.AreEqual(4, l.Active);
            Assert.AreEqual(1f, l.Age(1, 10f), 1e-4f); // slot1 doğum 9
            l.Release(1);
            Assert.AreEqual(3, l.Active);
            Assert.IsFalse(l.IsActive(1));
            l.Release(1);
            Assert.AreEqual(3, l.Active);
            l.Clear();
            Assert.AreEqual(0, l.Active);
        }
    }
}
#endif
