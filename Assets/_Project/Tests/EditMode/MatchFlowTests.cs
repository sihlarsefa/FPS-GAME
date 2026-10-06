using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Match.Flow;
using Project.Core.Domain;

namespace Project.Tests.Match
{
    public sealed class MatchFlowTests
    {
        // --- ZoneDamageRules ---
        [Test]
        public void PercentDpsRoundTrip()
        {
            Assert.AreEqual(3f, ZoneDamageRules.PercentToDps(3f, 100f), 1e-4f);
            Assert.AreEqual(0.4f, ZoneDamageRules.DpsToPercent(0.4f, 100f), 1e-4f);
            Assert.AreEqual(0f, ZoneDamageRules.PercentToDps(-1f, 100f), 1e-6f);
        }

        [Test]
        public void ExposureMultiplierRampsSmoothly()
        {
            Assert.AreEqual(1f, ZoneDamageRules.ExposureMultiplier(0f), 1e-5f);
            Assert.AreEqual(1f, ZoneDamageRules.ExposureMultiplier(1.5f), 1e-5f);
            Assert.AreEqual(1.5f, ZoneDamageRules.ExposureMultiplier(30f), 1e-5f);
            var mid = ZoneDamageRules.ExposureMultiplier(8.25f);
            Assert.AreEqual(1.25f, mid, 1e-3f);
            Assert.Greater(ZoneDamageRules.ExposureMultiplier(10f), ZoneDamageRules.ExposureMultiplier(5f));
        }

        [Test]
        public void GraceSuppressesEarlyDamage()
        {
            Assert.AreEqual(0f, ZoneDamageRules.DamageForTick(5f, 1f, 0f), 1e-6f);
            // Tik 1.0 -> 2.0: yalnızca son 0.5 sn hasar verir.
            var d = ZoneDamageRules.DamageForTick(10f, 1f, 1f);
            Assert.Greater(d, 4.9f);
            Assert.Less(d, 5.1f);
        }

        [Test]
        public void LongerExposureHurtsMore()
        {
            var early = ZoneDamageRules.DamageForTick(4f, 1f, 3f);
            var late = ZoneDamageRules.DamageForTick(4f, 1f, 20f);
            Assert.Greater(late, early);
            Assert.AreEqual(6f, late, 1e-3f);
        }

        [Test]
        public void SecondsToDieIsFiniteAndShorterWithHigherDps()
        {
            var slow = ZoneDamageRules.SecondsToDie(1f, 100f);
            var fast = ZoneDamageRules.SecondsToDie(5f, 100f);
            Assert.Greater(slow, fast);
            Assert.Less(slow, 200f);
            Assert.IsTrue(float.IsPositiveInfinity(ZoneDamageRules.SecondsToDie(0f, 100f)));
        }

        // --- ZoneRotationPlanner ---
        [Test]
        public void AnalyzeProducesOneReportPerPhase()
        {
            var phases = MatchConfig.DefaultZonePhases();
            var reports = ZoneRotationPlanner.Analyze(phases, 740f, 40f, 4.5f);
            Assert.AreEqual(phases.Length, reports.Count);
            Assert.AreEqual(740f, reports[0].StartRadius, 1e-3f);
            Assert.AreEqual(reports[0].EndRadius, reports[1].StartRadius, 1e-3f);
        }

        [Test]
        public void AreaRatioIsSquareOfRadiusRatio()
        {
            var phases = new[] { new ZonePhase(60f, 60f, 500f, 1f) };
            var r = ZoneRotationPlanner.Analyze(phases, 1000f, 0f, 4.5f);
            Assert.AreEqual(0.25f, r[0].AreaRatio, 1e-4f);
            Assert.AreEqual(500f / 60f, r[0].EdgeSpeed, 1e-3f);
        }

        [Test]
        public void FastCollapseIsFlagged()
        {
            var phases = new[] { new ZonePhase(5f, 10f, 100f, 5f) };
            var r = ZoneRotationPlanner.Analyze(phases, 800f, 0f, 4.5f);
            Assert.IsTrue(r[0].TooFast);
            Assert.IsFalse(r[0].TooSlow);
        }

        [Test]
        public void LazyZoneIsFlaggedTooSlow()
        {
            var phases = new[] { new ZonePhase(120f, 200f, 900f, 1f) };
            var r = ZoneRotationPlanner.Analyze(phases, 1000f, 0f, 4.5f);
            Assert.IsTrue(r[0].TooSlow);
            Assert.IsFalse(r[0].TooFast);
        }

        [Test]
        public void WorstRotationSpeedMath()
        {
            Assert.AreEqual(5f, ZoneRotationPlanner.WorstRotationSpeed(500f, 300f, 100f, 60f), 1e-4f);
            Assert.AreEqual(0f, ZoneRotationPlanner.WorstRotationSpeed(100f, 300f, 0f, 60f), 1e-6f);
            Assert.IsTrue(float.IsPositiveInfinity(ZoneRotationPlanner.WorstRotationSpeed(500f, 300f, 0f, 0f)));
        }

        [Test]
        public void WalkingShortfallComputed()
        {
            // 450 m uzakta, 100 sn süre, 4.5 m/sn -> tam yetişir.
            Assert.AreEqual(0f, ZoneRotationPlanner.SecondsOutsideIfWalking(550f, 0f, 0f, 0f, 100f, 100f, 4.5f), 1e-4f);
            // 900 m yol, 100 sn, 3 m/sn -> 300 sn gerekir, 200 sn eksik.
            Assert.AreEqual(200f, ZoneRotationPlanner.SecondsOutsideIfWalking(1000f, 0f, 0f, 0f, 100f, 100f, 3f), 1e-3f);
            Assert.AreEqual(0f, ZoneRotationPlanner.SecondsOutsideIfWalking(10f, 0f, 0f, 0f, 100f, 50f, 3f), 1e-6f);
        }

        [Test]
        public void TotalSecondsSumsAllStages()
        {
            var phases = new[] { new ZonePhase(10f, 20f, 1f, 1f), new ZonePhase(30f, 40f, 0f, 1f) };
            Assert.AreEqual(100f, ZoneRotationPlanner.TotalSeconds(phases), 1e-4f);
        }

        // --- ZonePhasePlanBuilder ---
        [Test]
        public void BuilderRadiiShrinkMonotonicallyAndEndOnFinal()
        {
            var plan = ZonePhasePlanBuilder.Build(740f, 0f, 7, 900f, 1f, 18f, 100f);
            Assert.AreEqual(7, plan.Length);
            var prev = 740f;
            for (var i = 0; i < plan.Length; i++)
            {
                Assert.IsTrue(plan[i].TargetRadius <= prev);
                prev = plan[i].TargetRadius;
            }

            Assert.AreEqual(0f, plan[6].TargetRadius, 1e-4f);
        }

        [Test]
        public void BuilderTotalTimeCloseToRequested()
        {
            var plan = ZonePhasePlanBuilder.Build(740f, 0f, 7, 900f, 1f, 18f, 100f);
            var total = ZoneRotationPlanner.TotalSeconds(plan);
            Assert.Greater(total, 880f);
            Assert.Less(total, 920f);
        }

        [Test]
        public void BuilderDamageRampsUp()
        {
            var plan = ZonePhasePlanBuilder.Build(740f, 0f, 6, 900f, 1f, 15f, 100f);
            Assert.Greater(plan[5].DamagePerSecond, plan[0].DamagePerSecond);
            Assert.AreEqual(1f, plan[0].DamagePerSecond, 0.06f);
            Assert.AreEqual(15f, plan[5].DamagePerSecond, 0.06f);
        }

        [Test]
        public void BuilderSanitizesBadInput()
        {
            var plan = ZonePhasePlanBuilder.Build(-5f, 99999f, 0, -1f, -3f, -2f, 100f);
            Assert.AreEqual(1, plan.Length);
            Assert.IsTrue(plan[0].WaitSeconds >= 0f);
        }

        // --- HotDropModel ---
        private static List<DropPoi> SamplePois() => new List<DropPoi>
        {
            new DropPoi("kale", 0f, 100f, 10f, 2f),
            new DropPoi("koy", 0f, 600f, 5f, 3f),
            new DropPoi("uzak", 0f, 5000f, 20f, 3f)
        };

        [Test]
        public void ExpectedSquadsSumToSquadCountWithinGlide()
        {
            var r = HotDropModel.Evaluate(SamplePois(), -2000f, 0f, 2000f, 0f, 10, 1100f, 450f);
            var sum = r[0].ExpectedSquads + r[1].ExpectedSquads + r[2].ExpectedSquads;
            Assert.AreEqual(10f, sum, 1e-3f);
            Assert.AreEqual(0f, r[2].ExpectedSquads, 1e-6f);
        }

        [Test]
        public void ValuableNearRoutePoiIsHotter()
        {
            var r = HotDropModel.Evaluate(SamplePois(), -2000f, 0f, 2000f, 0f, 10, 1100f, 450f);
            Assert.Greater(r[0].ExpectedSquads, r[1].ExpectedSquads);
            Assert.IsTrue(r[0].Heat >= DropHeat.Hot);
        }

        [Test]
        public void NoPoiInRangeYieldsZeroContention()
        {
            var pois = new List<DropPoi> { new DropPoi("x", 0f, 9000f, 10f, 1f) };
            var r = HotDropModel.Evaluate(pois, -100f, 0f, 100f, 0f, 10, 1100f, 450f);
            Assert.AreEqual(0f, r[0].Contention, 1e-6f);
            Assert.IsTrue(r[0].Heat == DropHeat.Cold);
        }

        [Test]
        public void DistanceToRouteClampsToSegmentEnds()
        {
            Assert.AreEqual(5f, HotDropModel.DistanceToRoute(5f, 5f, 0f, 0f, 10f, 0f), 1e-4f);
            Assert.AreEqual(10f, HotDropModel.DistanceToRoute(-10f, 0f, 0f, 0f, 10f, 0f), 1e-4f);
            Assert.AreEqual(5f, HotDropModel.DistanceToRoute(3f, 4f, 0f, 0f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void HeatClassificationBoundaries()
        {
            Assert.IsTrue(HotDropModel.Classify(0.2f) == DropHeat.Cold);
            Assert.IsTrue(HotDropModel.Classify(0.6f) == DropHeat.Warm);
            Assert.IsTrue(HotDropModel.Classify(1.2f) == DropHeat.Hot);
            Assert.IsTrue(HotDropModel.Classify(3f) == DropHeat.Inferno);
        }

        [Test]
        public void PromoteTierRespectsFloorAndCeiling()
        {
            Assert.AreEqual(0, HotDropModel.PromoteTier(0, DropHeat.Inferno));
            Assert.AreEqual(2, HotDropModel.PromoteTier(1, DropHeat.Hot));
            Assert.AreEqual(3, HotDropModel.PromoteTier(3, DropHeat.Inferno));
            Assert.AreEqual(1, HotDropModel.PromoteTier(1, DropHeat.Warm));
        }

        // --- LootDensityModel ---
        [Test]
        public void NeighbourCountsMatchBruteForce()
        {
            var xs = new List<float> { 0f, 10f, 20f, 100f, 105f, -34f };
            var zs = new List<float> { 0f, 0f, 0f, 100f, 100f, 0f };
            var counts = LootDensityModel.CountNeighbours(xs, zs, 35f);
            for (var i = 0; i < xs.Count; i++)
            {
                var brute = 0;
                for (var j = 0; j < xs.Count; j++)
                {
                    if (i == j) continue;
                    var dx = xs[i] - xs[j];
                    var dz = zs[i] - zs[j];
                    if (dx * dx + dz * dz <= 35f * 35f) brute++;
                }

                Assert.AreEqual(brute, counts[i]);
            }
        }

        [Test]
        public void CrowdedClusterIsThinnedLonelyPointBoosted()
        {
            Assert.AreEqual(LootDensityModel.MinMultiplier, LootDensityModel.ChanceMultiplier(1, 40), 1e-5f);
            Assert.AreEqual(LootDensityModel.MaxMultiplier, LootDensityModel.ChanceMultiplier(3, 0), 1e-5f);
            Assert.AreEqual(1f, LootDensityModel.ChanceMultiplier(1, 3), 1e-5f);
        }

        [Test]
        public void AdjustedChanceStaysInUnitRange()
        {
            Assert.AreEqual(1f, LootDensityModel.AdjustedChance(0.95f, 3, 0), 1e-6f);
            Assert.AreEqual(0f, LootDensityModel.AdjustedChance(float.NaN, 1, 2), 1e-6f);
        }

        [Test]
        public void ExpectedItemsAndBudget()
        {
            var tiers = new List<int> { 1, 1 };
            var neigh = new List<int> { 3, 3 };
            var e = LootDensityModel.ExpectedItems(tiers, neigh, t => 0.5f, 2f);
            Assert.AreEqual(2f, e, 1e-4f);
            Assert.IsTrue(LootDensityModel.MeetsBudget(e, 0, 6f));
            Assert.IsFalse(LootDensityModel.MeetsBudget(e, 10, 6f));
        }

        // --- CoverSpacingRules ---
        [Test]
        public void NoCoverFailsEverywhere()
        {
            var r = CoverSpacingRules.Evaluate(new List<float>(), 100f, EngagementBand.Open);
            Assert.IsFalse(r.Passes);
            Assert.AreEqual(1f, r.ExposedFraction, 1e-5f);
        }

        [Test]
        public void DenseCoverPassesCloseQuarters()
        {
            var cover = new List<float> { 2f, 8f, 14f, 20f, 26f };
            var r = CoverSpacingRules.Evaluate(cover, 28f, EngagementBand.CloseQuarters);
            Assert.IsTrue(r.Passes);
            Assert.AreEqual(6f, r.MaxGap, 1e-4f);
            Assert.AreEqual(0f, r.ExposedFraction, 1e-5f);
        }

        [Test]
        public void WideGapFailsMidBand()
        {
            var cover = new List<float> { 5f, 120f };
            var r = CoverSpacingRules.Evaluate(cover, 125f, EngagementBand.Mid);
            Assert.IsFalse(r.Passes);
            Assert.AreEqual(115f, r.MaxGap, 1e-3f);
        }

        [Test]
        public void ExposedFractionIgnoresReachableMiddle()
        {
            // Boşluk 27.5 m = tam 2*reach: açıkta kalan yok.
            var cover = new List<float> { 0f, 27.5f };
            var r = CoverSpacingRules.Evaluate(cover, 27.5f, EngagementBand.Mid);
            Assert.AreEqual(0f, r.ExposedFraction, 1e-5f);
        }

        [Test]
        public void BandForRangeBoundaries()
        {
            Assert.IsTrue(CoverSpacingRules.BandForRange(10f) == EngagementBand.CloseQuarters);
            Assert.IsTrue(CoverSpacingRules.BandForRange(80f) == EngagementBand.Mid);
            Assert.IsTrue(CoverSpacingRules.BandForRange(300f) == EngagementBand.Open);
        }
    }
}
