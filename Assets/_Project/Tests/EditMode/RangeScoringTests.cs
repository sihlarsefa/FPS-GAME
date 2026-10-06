using System.Collections.Generic;
using NUnit.Framework;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Presentation.Training;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RangeScoringTests
    {
        private sealed class MemStore : ISettingsStore
        {
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Saves;
            public bool HasKey(string key) => Ints.ContainsKey(key);
            public float GetFloat(string key, float fallback) => fallback;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) { }
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void LaneLateral_IsSymmetricAndOrdered()
        {
            Assert.AreEqual(0f, RangeScoring.LaneLateral(2), 0.001f);
            Assert.AreEqual(-RangeScoring.LaneLateral(0), RangeScoring.LaneLateral(4), 0.001f);
            Assert.Less(RangeScoring.LaneLateral(0), RangeScoring.LaneLateral(1));
            Assert.AreEqual("ŞERİT 3", RangeScoring.LaneLabel(2));
        }

        [Test]
        public void MarkerDistances_Cover100To600()
        {
            Assert.AreEqual(100f, RangeScoring.LaneMarkerDistances[0]);
            Assert.AreEqual(600f, RangeScoring.LaneMarkerDistances[RangeScoring.LaneMarkerDistances.Length - 1]);
        }

        [Test]
        public void Zone_HeadshotOverridesBodyPart()
        {
            Assert.AreEqual(HitZone.Head, RangeScoring.ZoneOf(BodyPart.Torso, true));
            Assert.AreEqual(HitZone.Head, RangeScoring.ZoneOf(BodyPart.Head, false));
            Assert.AreEqual(HitZone.Limb, RangeScoring.ZoneOf(BodyPart.Leg, false));
            Assert.AreEqual(HitZone.Torso, RangeScoring.ZoneOf(BodyPart.Torso, false));
        }

        [Test]
        public void ReactionFactor_DecreasesWithTime()
        {
            Assert.AreEqual(1.5f, RangeScoring.ReactionFactor(0.1f), 0.001f);
            Assert.AreEqual(0.5f, RangeScoring.ReactionFactor(5f), 0.001f);
            Assert.Greater(RangeScoring.ReactionFactor(0.8f), RangeScoring.ReactionFactor(1.4f));
            Assert.Greater(RangeScoring.PopUpScore(HitZone.Head, 0.3f), RangeScoring.PopUpScore(HitZone.Torso, 0.3f));
            Assert.AreEqual(150, RangeScoring.PopUpScore(HitZone.Head, 0.2f));
        }

        [Test]
        public void PopUpSchedule_IsDeterministicAndOrdered()
        {
            var a = RangeScoring.BuildPopUpSchedule(7);
            var b = RangeScoring.BuildPopUpSchedule(7);
            Assert.AreEqual(RangeScoring.ReactionTargets, a.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Lane, b[i].Lane);
                Assert.AreEqual(a[i].Distance, b[i].Distance);
                Assert.That(a[i].Lane, Is.InRange(0, RangeScoring.LaneCount - 1));
                if (i > 0) Assert.Greater(a[i].SpawnTime, a[i - 1].SpawnTime);
            }
        }

        [Test]
        public void ShootHouse_HasThreatsEveryRoomAndHostages()
        {
            for (var seed = 1; seed < 40; seed++)
            {
                var layout = RangeScoring.BuildShootHouse(seed, 5);
                Assert.AreEqual(5, layout.Rooms.Count);
                Assert.AreEqual(3, layout.Rows);
                Assert.AreEqual(layout.Targets.Count, layout.ThreatCount + layout.HostageCount);
                for (var r = 0; r < 5; r++)
                {
                    var threats = 0;
                    foreach (var t in layout.Targets)
                        if (t.Room == r && !t.Hostage) threats++;
                    Assert.GreaterOrEqual(threats, 1);
                }
                foreach (var t in layout.Targets)
                {
                    Assert.GreaterOrEqual(t.Z, 0f);
                    Assert.LessOrEqual(t.Z, layout.Length);
                }
            }
        }

        [Test]
        public void ShootHouse_ClampsRoomCountAndIsDeterministic()
        {
            Assert.AreEqual(2, RangeScoring.BuildShootHouse(1, 0).Rooms.Count);
            Assert.AreEqual(8, RangeScoring.BuildShootHouse(1, 99).Rooms.Count);
            var a = RangeScoring.BuildShootHouse(5, 6);
            var b = RangeScoring.BuildShootHouse(5, 6);
            Assert.AreEqual(a.Targets.Count, b.Targets.Count);
            Assert.AreEqual(a.Targets[0].X, b.Targets[0].X);
        }

        [Test]
        public void HouseStars_Tiers()
        {
            var par = RangeScoring.ParSeconds(4);
            Assert.AreEqual(3, RangeScoring.HouseStars(true, 0, par - 1f, par));
            Assert.AreEqual(2, RangeScoring.HouseStars(true, 1, par - 1f, par));
            Assert.AreEqual(2, RangeScoring.HouseStars(true, 0, par * 1.4f, par));
            Assert.AreEqual(1, RangeScoring.HouseStars(true, 3, par * 3f, par));
            Assert.AreEqual(0, RangeScoring.HouseStars(false, 0, 1f, par));
            Assert.AreEqual("**-", RangeScoring.StarText(2));
        }

        [Test]
        public void HouseScore_HostagePenaltyAndFloor()
        {
            Assert.AreEqual(5 * 100 + 250 + 50, RangeScoring.HouseScore(5, 0, 40f, 50f, true));
            Assert.AreEqual(0, RangeScoring.HouseScore(1, 5, 10f, 50f, false));
            Assert.Less(RangeScoring.HouseScore(5, 2, 40f, 50f, true), RangeScoring.HouseScore(5, 0, 40f, 50f, true));
        }

        [Test]
        public void PitScore_NearestBinWinsAndOutsideIsZero()
        {
            Assert.AreEqual(100, RangeScoring.PitScore(40f, 0f));
            Assert.AreEqual(70, RangeScoring.PitScore(30f, 0f));
            Assert.AreEqual(0, RangeScoring.PitScore(10f, 0f));
            Assert.Less(RangeScoring.PitScore(40f, 3f), 100);
            Assert.Greater(RangeScoring.PitScore(40f, 3f), 0);
        }

        [Test]
        public void RailScore_FasterIsWorth_More()
            => Assert.Greater(RangeScoring.RailScore(HitZone.Torso, 6f), RangeScoring.RailScore(HitZone.Torso, 2f));

        [Test]
        public void RecordRun_TracksBestScoreTimeAndStars()
        {
            var store = new MemStore();
            RangeScoring.RecordRun(store, RangeStation.ShootHouse, 500, 40000, 2, out var s1, out var t1);
            Assert.IsTrue(s1);
            Assert.IsTrue(t1);
            RangeScoring.RecordRun(store, RangeStation.ShootHouse, 300, 55000, 1, out var s2, out var t2);
            Assert.IsFalse(s2);
            Assert.IsFalse(t2);
            RangeScoring.RecordRun(store, RangeStation.ShootHouse, 700, 35000, 3, out var s3, out var t3);
            Assert.IsTrue(s3 && t3);
            Assert.AreEqual(700, RangeScoring.GetBestScore(store, RangeStation.ShootHouse));
            Assert.AreEqual(35000, RangeScoring.GetBestTimeMs(store, RangeStation.ShootHouse));
            Assert.AreEqual(3, RangeScoring.GetBestStars(store, RangeStation.ShootHouse));
            Assert.AreEqual(3, RangeScoring.GetRuns(store, RangeStation.ShootHouse));
            Assert.AreEqual(3, store.Saves);
        }

        [Test]
        public void RecordRun_NoTimeKeepsOldTime_NullStoreSafe()
        {
            var store = new MemStore();
            RangeScoring.RecordRun(store, RangeStation.VehiclePad, 100, 30000, 0, out _, out _);
            RangeScoring.RecordRun(store, RangeStation.VehiclePad, 50, 0, 0, out _, out var t);
            Assert.IsFalse(t);
            Assert.AreEqual(30000, RangeScoring.GetBestTimeMs(store, RangeStation.VehiclePad));
            RangeScoring.RecordRun(null, RangeStation.Rails, 1, 1, 1, out var a, out var b);
            Assert.IsFalse(a || b);
            Assert.AreEqual(0, RangeScoring.GetBestScore(null, RangeStation.Rails));
        }

        [Test]
        public void FormatTime_AndKeysAreDistinct()
        {
            Assert.AreEqual("--", RangeScoring.FormatTime(0));
            Assert.AreEqual("1:05.5", RangeScoring.FormatTime(65500));
            Assert.AreNotEqual(RangeScoring.BestScoreKey(RangeStation.Rails), RangeScoring.BestScoreKey(RangeStation.Reaction));
        }

        [Test]
        public void Barks_AllHaveTurkishLinesAndWrapSeed()
        {
            foreach (RangeBark b in System.Enum.GetValues(typeof(RangeBark)))
            {
                Assert.GreaterOrEqual(RangeScoring.BarkVariants(b), 1);
                Assert.IsNotEmpty(RangeScoring.BarkText(b, 0));
                Assert.IsNotEmpty(RangeScoring.BarkText(b, -7));
                Assert.AreEqual(RangeScoring.BarkText(b, 1), RangeScoring.BarkText(b, 1 + RangeScoring.BarkVariants(b)));
            }
        }

        [Test]
        public void DriveCheckpoints_ZigZagForward()
        {
            RangeScoring.DriveCheckpoint(0, out var f0, out var l0);
            RangeScoring.DriveCheckpoint(1, out var f1, out var l1);
            Assert.Greater(f1, f0);
            Assert.AreEqual(-l0, l1, 0.001f);
        }

        [Test]
        public void ReactionRank_Thresholds()
        {
            Assert.AreEqual("S", RangeScoring.ReactionRank(2000, 20));
            Assert.AreEqual("D", RangeScoring.ReactionRank(10, 20));
        }
    }
}
