using NUnit.Framework;
using Project.Application.Replay;
using Project.Presentation.Replay;

namespace Project.Tests
{
    public sealed class KillCamTimelineTests
    {
        private static ReplayData Make(float duration, float killTime, int killer = 1, int victim = 2, bool withHit = true)
        {
            var d = new ReplayData();
            for (var t = 0f; t <= duration + 1e-3f; t += 0.1f)
            {
                var f = new ReplayFrame { Time = t };
                f.Actors = new[]
                {
                    new ReplayActorSample { Id = killer, X = t, Y = 0f, Z = 0f, Yaw = 90f, Alive = true },
                    new ReplayActorSample { Id = victim, X = t + 10f, Y = 0f, Z = 0f, Alive = true }
                };
                d.Frames.Add(f);
            }
            if (withHit)
                d.Events.Add(new ReplayEvent { Time = killTime - 0.1f, Type = ReplayEventType.Hit, Actor = killer, Target = victim, Value = 40f });
            d.Events.Add(new ReplayEvent { Time = killTime, Type = ReplayEventType.Death, Actor = killer, Target = victim, Flag = true });
            return d;
        }

        [Test]
        public void Build_UsesLastSixSecondsBeforeKill()
        {
            var plan = KillCamTimeline.Build(Make(30f, 20f), 2);
            Assert.IsTrue(plan.Valid);
            Assert.AreEqual(1, plan.KillerId);
            Assert.AreEqual(14f, plan.Start, 0.01f);
            Assert.AreEqual(20f, plan.KillTime, 0.01f);
            Assert.AreEqual(19.9f, plan.HitTime, 0.01f);
            Assert.AreEqual(20.8f, plan.End, 0.01f);
            Assert.IsTrue(plan.Headshot);
        }

        [Test]
        public void Build_ShortMatchClampsToFirstFrame()
        {
            var plan = KillCamTimeline.Build(Make(10f, 3f), 2);
            Assert.IsTrue(plan.Valid);
            Assert.AreEqual(0f, plan.Start, 0.001f);
        }

        [Test]
        public void Build_NoHit_FallsBackToKillTime()
        {
            var plan = KillCamTimeline.Build(Make(30f, 20f, withHit: false), 2);
            Assert.AreEqual(plan.KillTime, plan.HitTime, 0.001f);
        }

        [Test]
        public void Build_InvalidWhenNoDataDeathOrKiller()
        {
            Assert.IsFalse(KillCamTimeline.Build(null, 2).Valid);
            Assert.IsFalse(KillCamTimeline.Build(new ReplayData(), 2).Valid);
            Assert.IsFalse(KillCamTimeline.Build(Make(30f, 20f), 99).Valid);
            Assert.IsFalse(KillCamTimeline.Build(Make(30f, 20f, killer: -1), 2).Valid);
            var suicide = Make(30f, 20f);
            suicide.Events[suicide.Events.Count - 1] = new ReplayEvent { Time = 20f, Type = ReplayEventType.Death, Actor = 2, Target = 2 };
            Assert.IsFalse(KillCamTimeline.Build(suicide, 2).Valid);
        }

        [Test]
        public void Build_PicksLastDeathOfVictim()
        {
            var d = Make(30f, 10f);
            d.Events.Add(new ReplayEvent { Time = 25f, Type = ReplayEventType.Death, Actor = 1, Target = 2 });
            Assert.AreEqual(25f, KillCamTimeline.Build(d, 2).KillTime, 0.001f);
        }

        [Test]
        public void Speed_IsSlowOnlyAroundHit()
        {
            var plan = KillCamTimeline.Build(Make(30f, 20f), 2);
            Assert.AreEqual(1f, KillCamTimeline.PlaybackSpeed(plan, 15f), 1e-5f);
            Assert.AreEqual(KillCamTimeline.SlowSpeed, KillCamTimeline.PlaybackSpeed(plan, 19.95f), 1e-5f);
            Assert.AreEqual(1f, KillCamTimeline.PlaybackSpeed(plan, 20.5f), 1e-5f);
        }

        [Test]
        public void Advance_RealDurationMatchesSlowMotionMath()
        {
            var plan = KillCamTimeline.Build(Make(30f, 20f), 2);
            var t = plan.Start;
            var real = 0f;
            const float dt = 0.005f;
            while (!KillCamTimeline.IsFinished(plan, t) && real < 60f)
            {
                t = KillCamTimeline.Advance(plan, t, dt);
                real += dt;
            }
            Assert.AreEqual(KillCamTimeline.RealDuration(plan), real, 0.1f);
            Assert.Greater(real, plan.End - plan.Start);
        }

        [Test]
        public void Vignette_PeaksAtKillThenFades()
        {
            var plan = KillCamTimeline.Build(Make(30f, 20f), 2);
            Assert.AreEqual(0f, KillCamTimeline.VignetteIntensity(plan, 15f), 1e-5f);
            Assert.AreEqual(1f, KillCamTimeline.VignetteIntensity(plan, 20f), 1e-4f);
            Assert.Less(KillCamTimeline.VignetteIntensity(plan, 20.4f), 1f);
            Assert.AreEqual(0f, KillCamTimeline.VignetteIntensity(plan, 21f), 1e-5f);
        }

        [Test]
        public void Pose_IsBehindAndAboveKillerLookingForward()
        {
            var d = Make(30f, 20f);
            var plan = KillCamTimeline.Build(d, 2);
            var pose = KillCamTimeline.Pose(d, plan, 18f);
            Assert.IsTrue(pose.Valid);
            // Katil yaw=90 (+X ileri) x=18: kamera geride (x < 18), omuz yüksekliğinde.
            Assert.Less(pose.PosX, 18f);
            Assert.Greater(pose.PosY, 1.5f);
            Assert.Greater(pose.LookX, 18f);
        }

        [Test]
        public void Pose_InvalidWithoutPlan()
        {
            Assert.IsFalse(KillCamTimeline.Pose(new ReplayData(), default, 1f).Valid);
        }
    }
}
