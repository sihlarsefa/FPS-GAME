#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class AcousticsMathTests
    {
        [Test]
        public void Delay_Uses343()
        {
            Assert.AreEqual(1f, AcousticsMath.Delay(343f), 0.001f);
            Assert.AreEqual(0f, AcousticsMath.Delay(-5f));
        }

        [Test]
        public void BulletArrivesBeforeReport()
        {
            var v = AcousticsMath.Profile(CaliberClass.Rifle).MuzzleVelocity;
            Assert.Less(AcousticsMath.BulletTravelDelay(300f, v), AcousticsMath.Delay(300f));
        }

        [Test]
        public void Gain_DecreasesAndZeroOutOfRange()
        {
            var near = AcousticsMath.DistanceGain(CaliberClass.Rifle, 30f);
            var far = AcousticsMath.DistanceGain(CaliberClass.Rifle, 400f);
            Assert.Greater(near, far);
            Assert.AreEqual(0f, AcousticsMath.DistanceGain(CaliberClass.Pistol, 400f));
            Assert.Greater(AcousticsMath.DistanceGain(CaliberClass.Sniper, 600f), AcousticsMath.DistanceGain(CaliberClass.Pistol, 300f));
        }

        [Test]
        public void Suppressed_IsQuieter()
        {
            Assert.Less(AcousticsMath.DistanceGain(CaliberClass.Rifle, 50f, true), AcousticsMath.DistanceGain(CaliberClass.Rifle, 50f, false));
        }

        [Test]
        public void Cutoff_FallsWithDistanceAndClamps()
        {
            var a = AcousticsMath.CutoffHz(CaliberClass.Rifle, 10f);
            var b = AcousticsMath.CutoffHz(CaliberClass.Rifle, 500f);
            Assert.Greater(a, b);
            Assert.GreaterOrEqual(AcousticsMath.CutoffHz(CaliberClass.Rifle, 1000000f), AcousticsMath.Profile(CaliberClass.Rifle).MinCutoffHz);
            Assert.LessOrEqual(AcousticsMath.CutoffHz(CaliberClass.Rifle, 0f), AcousticsMath.OpenCutoffHz);
        }

        [Test]
        public void ClosestApproach_FindsPerpendicularPoint()
        {
            var ok = AcousticsMath.ClosestApproach(Vector3.zero, Vector3.forward * 5f, 100f, new Vector3(3f, 0f, 40f),
                out var along, out var miss, out var cp);
            Assert.IsTrue(ok);
            Assert.AreEqual(40f, along, 0.001f);
            Assert.AreEqual(3f, miss, 0.001f);
            Assert.AreEqual(40f, cp.z, 0.001f);
        }

        [Test]
        public void ClosestApproach_ClampsToMaxDist()
        {
            AcousticsMath.ClosestApproach(Vector3.zero, Vector3.forward, 10f, new Vector3(0f, 0f, 50f), out var along, out var miss, out _);
            Assert.AreEqual(10f, along, 0.001f);
            Assert.AreEqual(40f, miss, 0.001f);
            Assert.IsFalse(AcousticsMath.ClosestApproach(Vector3.zero, Vector3.zero, 10f, Vector3.one, out _, out _, out _));
        }

        [Test]
        public void NearMiss_CrackOnlySupersonic()
        {
            AcousticsMath.NearMissLayers(2f, AcousticsMath.Profile(CaliberClass.Rifle).MuzzleVelocity, out var crack, out var whiz);
            Assert.IsTrue(crack); Assert.IsTrue(whiz);
            AcousticsMath.NearMissLayers(2f, AcousticsMath.Profile(CaliberClass.Pistol).MuzzleVelocity, out crack, out whiz);
            Assert.IsFalse(crack); Assert.IsTrue(whiz);
            AcousticsMath.NearMissLayers(7f, 900f, out crack, out whiz);
            Assert.IsFalse(crack); Assert.IsFalse(whiz);
            Assert.Greater(AcousticsMath.CrackGain(0.5f), AcousticsMath.CrackGain(5f));
            Assert.AreEqual(0f, AcousticsMath.WhizGain(6f));
        }

        [Test]
        public void PlanEchoes_FiltersRangeAndSortsByDelay()
        {
            var l = new float[] { 300f, 20f, 100f, 0f, 450f };
            var s = new float[] { 200f, 50f, 300f, 0f, 100f }; // [2]: yol 400 m -> 0.44 sn; [0]: yol 500 m -> 0.73 sn; [1] çok yakın, [3] boş, [4] menzil dışı
            var res = new EchoPlan[5];
            var n = AcousticsMath.PlanEchoes(l, s, 5, 250f, CaliberClass.Rifle, false, res, 3);
            Assert.AreEqual(2, n);
            Assert.Less(res[0].Delay, res[1].Delay);
            Assert.Greater(res[0].Gain, res[1].Gain);
            Assert.Greater(res[0].Delay, AcousticsMath.MinEchoDelay);
        }

        [Test]
        public void PlanEchoes_CapsAtMax()
        {
            var l = new float[] { 100f, 120f, 140f, 160f, 180f };
            var s = new float[] { 300f, 300f, 300f, 300f, 300f };
            var res = new EchoPlan[5];
            Assert.AreEqual(3, AcousticsMath.PlanEchoes(l, s, 5, 300f, CaliberClass.Rifle, false, res, 3));
        }

        [Test]
        public void Indoor_AndSlapback()
        {
            Assert.AreEqual(0f, AcousticsMath.IndoorScore(0f, 4));
            Assert.Greater(AcousticsMath.IndoorScore(3f, 3), AcousticsMath.IndoorScore(3f, 0));
            Assert.AreEqual(0.02f, AcousticsMath.SlapbackDelay(0.5f), 0.0001f);
            Assert.AreEqual(0.12f, AcousticsMath.SlapbackDelay(50f), 0.0001f);
            Assert.AreEqual(0f, AcousticsMath.SlapbackGain(CaliberClass.Rifle, 0.2f, 20f, false));
            Assert.Greater(AcousticsMath.SlapbackGain(CaliberClass.Rifle, 1f, 20f, false), 0f);
        }

        [Test]
        public void Queue_PopsDueInOrderAndDropsQuietestWhenFull()
        {
            var q = new AcousticQueue(2);
            Assert.IsTrue(q.Enqueue(new AcousticEvent { DueTime = 2f, Volume = 0.5f }));
            Assert.IsTrue(q.Enqueue(new AcousticEvent { DueTime = 1f, Volume = 0.2f }));
            Assert.IsFalse(q.Enqueue(new AcousticEvent { DueTime = 3f, Volume = 0.1f }));
            Assert.IsTrue(q.Enqueue(new AcousticEvent { DueTime = 0.5f, Volume = 0.9f }));
            var list = new System.Collections.Generic.List<AcousticEvent>();
            q.PopDue(2.5f, list); // 2 sn'den eski olaylar atıldığı için hepsi bu pencerede vadesi gelmiş olmalı
            Assert.AreEqual(2, list.Count);
            Assert.Less(list[0].DueTime, list[1].DueTime);
            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void RayBudget_Refills()
        {
            var b = new RayBudget(10f, 5f);
            Assert.IsTrue(b.TryConsume(0f, 5));
            Assert.IsFalse(b.TryConsume(0f, 1));
            Assert.IsTrue(b.TryConsume(0.5f, 5));
        }
    }
}

#endif
