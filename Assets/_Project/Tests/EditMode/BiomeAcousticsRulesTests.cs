#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;

namespace Project.Tests.EditMode
{
    public class BiomeAcousticsRulesTests
    {
        [Test]
        public void Classify_UsesIndoorFirst()
        {
            Assert.AreEqual(SpaceKind.Indoor, BiomeAcousticsRules.Classify(0.8f, 1f, 0f, 0f));
            Assert.AreEqual(SpaceKind.Forest, BiomeAcousticsRules.Classify(0f, 0.3f, 0.8f, 0f));
            Assert.AreEqual(SpaceKind.VillageStreet, BiomeAcousticsRules.Classify(0f, 0.5f, 0.1f, 0.6f));
            Assert.AreEqual(SpaceKind.OpenValley, BiomeAcousticsRules.Classify(0f, 1f, 0.1f, 0.1f));
        }

        [Test]
        public void Outdoor_ValleyLongSparse_ForestShortSoft()
        {
            var v = BiomeAcousticsRules.Outdoor(SpaceKind.OpenValley);
            var f = BiomeAcousticsRules.Outdoor(SpaceKind.Forest);
            var s = BiomeAcousticsRules.Outdoor(SpaceKind.VillageStreet);
            Assert.Greater(v.DecayTime, s.DecayTime);
            Assert.Greater(s.DecayTime, f.DecayTime);
            Assert.Greater(v.EchoSpacing, s.EchoSpacing);
            Assert.Greater(f.HfDamping, v.HfDamping);
        }

        [Test]
        public void Indoor_BiggerRoomLongerDecay()
        {
            var small = BiomeAcousticsRules.Indoor(3f, 3f, 2.5f);
            var hall = BiomeAcousticsRules.Indoor(20f, 25f, 8f);
            Assert.Greater(hall.DecayTime, small.DecayTime);
            Assert.Greater(hall.PreDelay, small.PreDelay - 0.0001f);
            Assert.AreEqual(SpaceKind.Indoor, small.Kind);
            Assert.Greater(BiomeAcousticsRules.Indoor(0f, 0f, 0f).DecayTime, 0f);
        }

        [Test]
        public void Blend_Endpoints()
        {
            var a = BiomeAcousticsRules.Outdoor(SpaceKind.Forest);
            var b = BiomeAcousticsRules.Outdoor(SpaceKind.OpenValley);
            Assert.AreEqual(a.DecayTime, BiomeAcousticsRules.Blend(a, b, 0f).DecayTime, 0.0001f);
            Assert.AreEqual(b.DecayTime, BiomeAcousticsRules.Blend(a, b, 2f).DecayTime, 0.0001f);
        }

        [Test]
        public void Spread_BedsWide_ThreatsPoint()
        {
            Assert.AreEqual(180f, BiomeAcousticsRules.SpreadDegrees(SpatialRole.AmbientBed, 0f));
            Assert.AreEqual(0f, BiomeAcousticsRules.SpreadDegrees(SpatialRole.Threat, 10f));
            Assert.AreEqual(0f, BiomeAcousticsRules.SpreadDegrees(SpatialRole.Gunshot, 100f));
            Assert.Less(BiomeAcousticsRules.SpreadDegrees(SpatialRole.DistantBattle, 900f), 40f);
            Assert.Greater(BiomeAcousticsRules.SpatialBlend(SpatialRole.Threat), BiomeAcousticsRules.SpatialBlend(SpatialRole.AmbientBed));
        }

        [Test]
        public void FarBattle_RangeAndMatchGate()
        {
            Assert.IsTrue(BiomeAcousticsRules.ShouldPlayFarBattle(true, true, 650f));
            Assert.IsFalse(BiomeAcousticsRules.ShouldPlayFarBattle(false, true, 650f));
            Assert.IsFalse(BiomeAcousticsRules.ShouldPlayFarBattle(true, false, 650f));
            Assert.IsFalse(BiomeAcousticsRules.ShouldPlayFarBattle(true, true, 399f));
            Assert.IsFalse(BiomeAcousticsRules.ShouldPlayFarBattle(true, true, 901f));
            Assert.IsFalse(BiomeAcousticsRules.ShouldPlayFarBattle(true, true, float.NaN));
        }

        [Test]
        public void FarBattle_LowpassFallsWithRange_VolumeFalls()
        {
            Assert.Greater(BiomeAcousticsRules.FarBattleLowpassHz(400f, false), BiomeAcousticsRules.FarBattleLowpassHz(900f, false));
            Assert.Less(BiomeAcousticsRules.FarBattleLowpassHz(500f, true), BiomeAcousticsRules.FarBattleLowpassHz(500f, false));
            Assert.Greater(BiomeAcousticsRules.FarBattleVolume(400f, false), BiomeAcousticsRules.FarBattleVolume(900f, false));
            Assert.AreEqual(0f, BiomeAcousticsRules.FarBattleVolume(1200f, false));
        }
    }
}
#endif
