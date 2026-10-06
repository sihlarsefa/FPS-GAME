#if UNITY_EDITOR
using NUnit.Framework;
using Project.Presentation.Benchmark;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class AaaBenchmarkTests
    {
        private static BenchmarkShotPath.Anchors Anchors() => new BenchmarkShotPath.Anchors
        {
            Player = new Vector3(0f, 2f, -50f),
            PlayerForward = Vector3.forward,
            Soldier = new Vector3(20f, 2f, -10f),
            Vehicle = new Vector3(-35f, 2f, 12f),
            HouseDoor = new Vector3(30f, 2f, -5f),
            HouseInside = new Vector3(30f, 2f, 2f),
            Forest = new Vector3(-40f, 2f, 40f),
            Targets = new Vector3(-10f, 2f, -26f)
        };

        [Test]
        public void Path_HasSixShots_AndPositiveDuration()
        {
            var shots = BenchmarkShotPath.Build(Anchors());
            Assert.AreEqual(BenchmarkShotPath.ShotCount, shots.Count);
            Assert.IsTrue(BenchmarkShotPath.TotalDuration(shots) > 30f);
        }

        [Test]
        public void Path_Evaluate_VisitsEveryShotInOrder()
        {
            var shots = BenchmarkShotPath.Build(Anchors());
            var last = -1;
            for (var t = 0f; t < BenchmarkShotPath.TotalDuration(shots); t += 0.25f)
            {
                var pose = BenchmarkShotPath.Evaluate(shots, t);
                Assert.IsTrue(pose.ShotIndex >= last);
                last = pose.ShotIndex;
            }

            Assert.AreEqual(shots.Count - 1, last);
        }

        [Test]
        public void Path_Evaluate_LoopsAndHandlesNegative()
        {
            var shots = BenchmarkShotPath.Build(Anchors());
            var total = BenchmarkShotPath.TotalDuration(shots);
            Assert.AreEqual(BenchmarkShotPath.Evaluate(shots, 1f).ShotIndex, BenchmarkShotPath.Evaluate(shots, total + 1f).ShotIndex);
            Assert.AreEqual(shots.Count - 1, BenchmarkShotPath.Evaluate(shots, -0.1f).ShotIndex);
        }

        [Test]
        public void Path_WeaponShots_UsePlayerCamera()
        {
            var shots = BenchmarkShotPath.Build(Anchors());
            Assert.IsTrue(shots[0].PlayerCamera && shots[0].Reload);
            Assert.IsTrue(shots[5].PlayerCamera && shots[5].Fire);
            Assert.IsFalse(shots[1].PlayerCamera);
        }

        [Test]
        public void Path_Evaluate_EmptyIsSafe()
        {
            var pose = BenchmarkShotPath.Evaluate(null, 3f);
            Assert.AreEqual(0, pose.ShotIndex);
        }

        [Test]
        public void Burst_TogglesWithinSecond()
        {
            Assert.IsTrue(BenchmarkShotPath.BurstOn(0.1f));
            Assert.IsFalse(BenchmarkShotPath.BurstOn(0.9f));
        }

        [Test]
        public void Screenshot_NameIsPngAndSafe()
        {
            var name = BenchmarkShotPath.ScreenshotName(new System.DateTime(2026, 10, 6, 12, 30, 5, System.DateTimeKind.Utc), 7);
            Assert.AreEqual("aaa_20261006_123005_007.png", name);
        }

        [Test]
        public void Layout_WeightsSumToOne_AndZonesDominate()
        {
            var w = new float[BenchmarkLayout.LayerCount];
            var pts = new[] { new Vector2(0f, 0f), new Vector2(18f, -32f), new Vector2(48f, 42f), new Vector2(-42f, 46f), new Vector2(-70f, -70f) };
            for (var i = 0; i < pts.Length; i++)
            {
                BenchmarkLayout.Weights(pts[i].x, pts[i].y, 5f, w);
                var sum = 0f;
                for (var l = 0; l < w.Length; l++)
                    sum += w[l];
                Assert.IsTrue(Mathf.Abs(sum - 1f) < 1e-3f);
            }

            BenchmarkLayout.Weights(BenchmarkLayout.MudZone.x, BenchmarkLayout.MudZone.y, 2f, w);
            Assert.IsTrue(w[BenchmarkLayout.LMud] > 0.45f);
            BenchmarkLayout.Weights(BenchmarkLayout.RockZone.x, BenchmarkLayout.RockZone.y, 2f, w);
            Assert.IsTrue(w[BenchmarkLayout.LRock] > 0.5f);
            BenchmarkLayout.Weights(0f, 0f, 45f, w);
            Assert.IsTrue(w[BenchmarkLayout.LRock] > 0.5f);
        }

        [Test]
        public void Layout_PadsAreFlat_AndTreesAvoidPads()
        {
            var spawn = BenchmarkLayout.SpawnPad;
            Assert.AreEqual(BenchmarkLayout.BaseHeight, BenchmarkLayout.HeightAt(spawn.x, spawn.y), 1e-3f);
            var house = BenchmarkLayout.HousePad;
            Assert.AreEqual(0f, BenchmarkLayout.TreeDensity(house.x, house.y), 1e-5f);
            Assert.IsTrue(BenchmarkLayout.TreeDensity(BenchmarkLayout.ForestZone.x, BenchmarkLayout.ForestZone.y) > 0.8f);
        }

        [Test]
        public void Layout_RockZoneIsAHill()
        {
            var rock = BenchmarkLayout.RockZone;
            Assert.IsTrue(BenchmarkLayout.HeightAt(rock.x, rock.y) > BenchmarkLayout.BaseHeight + 4f);
            Assert.IsTrue(BenchmarkLayout.HeightAt(rock.x, rock.y) < BenchmarkLayout.TerrainHeight);
        }

        [Test]
        public void Layout_ZoneClassification()
        {
            Assert.AreEqual(BenchmarkZone.Forest, BenchmarkLayout.ZoneAt(BenchmarkLayout.ForestZone.x, BenchmarkLayout.ForestZone.y));
            Assert.AreEqual(BenchmarkZone.Mud, BenchmarkLayout.ZoneAt(BenchmarkLayout.MudZone.x, BenchmarkLayout.MudZone.y));
            Assert.AreEqual(BenchmarkZone.Rock, BenchmarkLayout.ZoneAt(BenchmarkLayout.RockZone.x, BenchmarkLayout.RockZone.y));
            Assert.AreEqual(BenchmarkZone.Pad, BenchmarkLayout.ZoneAt(BenchmarkLayout.SpawnPad.x, BenchmarkLayout.SpawnPad.y));
        }

        [Test]
        public void Installer_NameMatching()
        {
            Assert.IsTrue(AaaBenchmarkFeatureInstaller.NameMatches("SsaoTuner"));
            Assert.IsTrue(AaaBenchmarkFeatureInstaller.NameMatches("ContactShadowsFeature"));
            Assert.IsFalse(AaaBenchmarkFeatureInstaller.NameMatches("FootstepEmitter"));
            Assert.IsFalse(AaaBenchmarkFeatureInstaller.NameMatches(null));
            Assert.IsTrue(AaaBenchmarkFeatureInstaller.NamespaceExcluded("Project.Infrastructure.Audio"));
            Assert.IsFalse(AaaBenchmarkFeatureInstaller.NamespaceExcluded("Project.Infrastructure.Rendering"));
        }
    }
}
#endif
