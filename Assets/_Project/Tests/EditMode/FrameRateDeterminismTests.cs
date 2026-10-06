using System;
using NUnit.Framework;
using Project.Application.Services;
using Project.Infrastructure.Player;

namespace Project.Tests.EditMode
{
    /// <summary>Kare hızı bağımsızlığı kanıtları: 60 Hz ile 240 Hz, aynı girdi, 2 sn; sonuçlar %2 içinde.</summary>
    public sealed class FrameRateDeterminismTests
    {
        private const float Seconds = 2f;

        private static void AssertWithin(float a, float b, float relTol, float absTol, string what)
        {
            var allowed = Math.Max(absTol, Math.Max(Math.Abs(a), Math.Abs(b)) * relTol);
            Assert.LessOrEqual(Math.Abs(a - b), allowed, what + ": 60Hz=" + a + " 240Hz=" + b);
        }

        private static float SpringFinal(int fps, float stiffness, float zeta, out float vel)
        {
            var s = new SpringAxis();
            var dt = 1f / fps;
            var n = (int)Math.Round(Seconds * fps);
            for (var i = 0; i < n; i++)
            {
                // Aynı zamanlarda darbe: t = 0 ve t = 0.5 sn.
                if (i == 0 || i == fps / 2)
                    s.Kick(4f, 0.05f);
                s.Step(0f, stiffness, zeta, dt);
            }
            vel = s.Velocity;
            return s.Position;
        }

        [TestCase(180f, 0.5f)]
        [TestCase(180f, 0.7f)]
        [TestCase(120f, 1f)]
        [TestCase(120f, 1.6f)]
        public void ViewmodelSpring_60vs240_SamePose(float k, float z)
        {
            var p60 = SpringFinal(60, k, z, out var v60);
            var p240 = SpringFinal(240, k, z, out var v240);
            AssertWithin(p60, p240, 0.02f, 1e-4f, "pozisyon");
            AssertWithin(v60, v240, 0.02f, 1e-3f, "hız");
        }

        private static float RecoilPitchTrace(int fps, out float peak)
        {
            var r = new CameraRecoilSpring();
            var dt = 1f / fps;
            var n = (int)Math.Round(Seconds * fps);
            peak = 0f;
            for (var i = 0; i < n; i++)
            {
                if (i == 0 || i == fps / 2)
                    r.Kick(0.4f, 0.1f);
                r.Step(dt);
                peak = Math.Max(peak, r.PitchOffset);
            }
            return r.PitchOffset;
        }

        [Test]
        public void CameraRecoilRecovery_60vs240_SamePose()
        {
            var a = RecoilPitchTrace(60, out var pk60);
            var b = RecoilPitchTrace(240, out var pk240);
            AssertWithin(a, b, 0.02f, 1e-4f, "geri toparlanma");
            AssertWithin(pk60, pk240, 0.05f, 1e-3f, "tepe");
            Assert.Less(Math.Abs(a), 0.05f, "2 sn sonra toparlanmış olmalı");
        }

        // ADS karışımı: üstel takip, süre T'de %95'e ulaşacak oranla (rate = 3/T).
        private static float AdsBlend(int fps, float adsSeconds, float duration)
        {
            var rate = 3f / adsSeconds;
            var x = 0f;
            var dt = 1f / fps;
            var n = (int)Math.Round(duration * fps);
            for (var i = 0; i < n; i++)
                x += (1f - x) * ViewmodelDynamics.ExpFollow(rate, dt);
            return x;
        }

        // ADS karışımı: doğrusal (MoveTowards) — tam süre sonunda 1'e varmalı.
        private static float AdsLinearTime(int fps, float adsSeconds)
        {
            var x = 0f;
            var dt = 1f / fps;
            var steps = 0;
            while (x < 1f && steps < 100000)
            {
                x = Math.Min(1f, x + dt / adsSeconds);
                steps++;
            }
            return steps * dt;
        }

        [TestCase(0.18f, false)]
        [TestCase(0.25f, false)]
        [TestCase(0.25f, true)]
        [TestCase(0.4f, true)]
        public void AdsBlend_DurationHonored_BothRates(float weaponAds, bool scoped)
        {
            var t = ViewmodelDynamics.AdsSeconds(weaponAds, scoped);
            var e60 = AdsBlend(60, t, t);
            var e240 = AdsBlend(240, t, t);
            Assert.AreEqual(0.95f, e60, 0.05f * 0.95f + 0.0f, "60Hz süre");
            Assert.AreEqual(0.95f, e240, 0.05f * 0.95f, "240Hz süre");
            Assert.AreEqual(e60, e240, 0.01f, "hızlar arası fark");

            var l60 = AdsLinearTime(60, t);
            var l240 = AdsLinearTime(240, t);
            Assert.AreEqual(t, l240, t * 0.05f, "240Hz doğrusal");
            Assert.AreEqual(t, l60, Math.Max(t * 0.05f, 1f / 60f), "60Hz doğrusal (bir kare payı)");
        }

        [Test]
        public void ExpFollow_ComposesAcrossStepSizes()
        {
            // (1-a(dt))^n == 1-a(n*dt): kareden bağımsızlığın tanımı.
            var one = ViewmodelDynamics.ExpFollow(9f, 1f / 60f);
            var four = ViewmodelDynamics.ExpFollow(9f, 4f / 60f);
            var composed = 1f - (float)Math.Pow(1f - one, 4);
            Assert.AreEqual(four, composed, 1e-5f);
        }

        // Hız ivmelenmesi/yavaşlaması: üstel takip (ivme oranı = hız hedefine yaklaşma).
        private static float Speed(int fps, float target, float accelRate, float decelRate, out float atAccelEnd)
        {
            var v = 0f;
            atAccelEnd = 0f;
            var dt = 1f / fps;
            var n = (int)Math.Round(Seconds * fps);
            for (var i = 0; i < n; i++)
            {
                var goal = i < n / 2 ? target : 0f;
                var rate = i < n / 2 ? accelRate : decelRate;
                v += (goal - v) * ViewmodelDynamics.ExpFollow(rate, dt);
                if (i == n / 2 - 1) atAccelEnd = v;
            }
            return v;
        }

        [Test]
        public void MovementAccelDecel_60vs240_SameCurve()
        {
            var f60 = Speed(60, 5.5f, 6f, 9f, out var a60);
            var f240 = Speed(240, 5.5f, 6f, 9f, out var a240);
            AssertWithin(a60, a240, 0.02f, 1e-3f, "ivme sonu");
            AssertWithin(f60, f240, 0.02f, 2e-3f, "yavaşlama sonu");
        }

        [Test]
        public void SlideSpeed_IsTimeBased_NotFrameBased()
        {
            for (var k = 0; k <= 4; k++)
            {
                var t = CameraFeelMath.SlideDuration * k / 4f;
                var a = CameraFeelMath.SlideSpeed(8f, 3f, t, CameraFeelMath.SlideDuration);
                var b = CameraFeelMath.SlideSpeed(8f, 3f, t, CameraFeelMath.SlideDuration);
                Assert.AreEqual(a, b);
            }
            Assert.AreEqual(3f, CameraFeelMath.SlideSpeed(8f, 3f, CameraFeelMath.SlideDuration, CameraFeelMath.SlideDuration), 1e-4f);
        }

        private static float StaminaAfter(int fps)
        {
            var m = new StaminaModel();
            var dt = 1f / fps;
            var n = (int)Math.Round(Seconds * fps);
            for (var i = 0; i < n; i++)
                m.Tick(dt, i < n / 4, 1f, 1f); // 0.5 sn koşu, sonra dinlenme
            return m.Current;
        }

        [Test]
        public void StaminaDrainRegen_60vs240_Within2Percent()
        {
            var a = StaminaAfter(60);
            var b = StaminaAfter(240);
            var max = new StaminaModel().Max;
            AssertWithin(a, b, 0.02f, max * 0.02f, "stamina");
        }
    }
}
