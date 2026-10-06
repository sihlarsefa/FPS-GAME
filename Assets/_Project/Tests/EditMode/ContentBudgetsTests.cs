using NUnit.Framework;
using Project.Infrastructure.Rendering.Perf;
using Project.Infrastructure.World;
using Project.Infrastructure.World.Lighting;
using Project.Infrastructure.World.Lobby;
using UnityEngine;

namespace Project.Tests
{
    public class ContentBudgetsTests
    {
        [Test]
        public void Budgets_AreMonotonicAcrossTiers()
        {
            for (var t = 1; t <= 3; t++)
            {
                Assert.IsTrue(ContentBudgets.PoiLights(t) >= ContentBudgets.PoiLights(t - 1));
                Assert.IsTrue(ContentBudgets.Probes(t) >= ContentBudgets.Probes(t - 1));
                Assert.IsTrue(ContentBudgets.FrontlineItems(t) >= ContentBudgets.FrontlineItems(t - 1));
                Assert.IsTrue(ContentBudgets.LobbyParticles(t) >= ContentBudgets.LobbyParticles(t - 1));
            }
        }

        [Test]
        public void PoiLights_RespectBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                Assert.IsTrue(PoiLightPlanner.MaxLights(t) <= ContentBudgets.PoiLights(t));
                Assert.IsTrue(PoiLightPlanner.MaxShadowCasters(t) <= ContentBudgets.PoiShadowedLights(t));
                var all = new System.Collections.Generic.List<PoiLightSpec>();
                for (var i = 0; i < 10; i++)
                    all.AddRange(PoiLightPlanner.Plan(PoiSiteType.Village, new Vector3(i * 50f, 0f, 0f), 60f, i));
                var res = PoiLightPlanner.ApplyBudget(all, Vector3.zero, t);
                var shadows = 0;
                foreach (var s in res) if (s.Shadow) shadows++;
                Assert.IsTrue(res.Count <= ContentBudgets.PoiLights(t));
                Assert.IsTrue(shadows <= ContentBudgets.PoiShadowedLights(t));
            }
        }

        [Test]
        public void Probes_RespectBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                var all = new System.Collections.Generic.List<ProbeSpec>();
                for (var i = 0; i < 60; i++) all.Add(ReflectionProbePlanner.ForPoi(new Vector3(i * 30f, 0f, 0f), 20f));
                Assert.IsTrue(ReflectionProbePlanner.MaxProbes(t) <= ContentBudgets.Probes(t));
                Assert.IsTrue(ReflectionProbePlanner.ApplyBudget(all, Vector3.zero, t).Count <= ContentBudgets.Probes(t));
            }
        }

        [Test]
        public void Interior_RespectsBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                Assert.IsTrue(InteriorWearPlan.MaxShafts(t) <= ContentBudgets.InteriorShafts(t));
                // En kotu oda: 50 m2, yikik, 4 duvar x 7 m, 3 pencere.
                var total = InteriorWearPlan.MaxPiecesPerRoom(t)
                    + 4 * InteriorWearPlan.BulletHoles(7f, t, true)
                    + 3 * InteriorWearPlan.Shards(t, true)
                    + InteriorWearPlan.Debris(50f, t, true)
                    + InteriorWearPlan.SoldierItems(50f, t, true);
                Assert.IsTrue(total <= ContentBudgets.InteriorRoomRenderers(t));
            }
        }

        [Test]
        public void Frontline_RespectsBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                var p = FrontlinePlan.Create(new Vector2(0f, 0f), new Vector2(300f, 0f), 11, t);
                var n = p.Sandbags.Count + p.Foxholes.Count + p.Hedgehogs.Count + p.Craters.Count + p.Logs.Count;
                foreach (var w in p.WireLines) n += w.Count;
                Assert.IsTrue(n <= ContentBudgets.FrontlineItems(t));
            }
        }

        [Test]
        public void Roadside_ConstantsRespectBudget()
        {
            var prev = 0;
            for (var t = 0; t <= 3; t++)
            {
                var total = RoadsidePlan.TotalFor(t);
                Assert.IsTrue(total <= ContentBudgets.RoadsideProps(t));
                Assert.Greater(total, prev);
                prev = total;
            }
            Assert.IsTrue(RoadsidePlan.TotalFor(3) == RoadsidePlan.MaxPoles + RoadsidePlan.MaxKmStones + RoadsidePlan.MaxGuardRuns + RoadsidePlan.MaxSigns + RoadsidePlan.MaxWrecks);
            Assert.IsTrue(RoadsidePlan.TotalFor(0) < RoadsidePlan.TotalFor(3) * 0.5f);
        }

        [Test]
        public void Trees_RespectBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                Assert.IsTrue(TreeScatter.VarietyCountForTier(t) <= ContentBudgets.TreeVarietyInstances(t));
                Assert.IsTrue(TreeScatter.TargetCountForTier(t) <= ContentBudgets.TreeInstances(t));
            }
        }

        [Test]
        public void Lobby_RespectsBudget()
        {
            for (var t = 0; t <= 3; t++)
            {
                Assert.IsTrue(LobbyCinematicMath.LampCount(t) <= ContentBudgets.LobbyLights(t));
                Assert.IsTrue((LobbyCinematicMath.LampShadows(t) ? LobbyCinematicMath.LampCount(t) : 0) <= ContentBudgets.LobbyShadowedLights(t));
                Assert.IsTrue(LobbyCinematicMath.FogParticles(t) <= ContentBudgets.LobbyFogParticles(t));
                Assert.IsTrue(LobbyCinematicMath.EmberParticles(t) <= ContentBudgets.LobbyEmberParticles(t));
                Assert.IsTrue(LobbyCinematicMath.FogParticles(t) + LobbyCinematicMath.EmberParticles(t) <= ContentBudgets.LobbyParticles(t));
            }
        }
    }
}
