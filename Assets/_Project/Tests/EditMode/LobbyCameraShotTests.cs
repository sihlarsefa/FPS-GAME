using NUnit.Framework;
using Project.Presentation.Lobby.CameraWork;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class LobbyCameraShotTests
    {
        private static LobbyShot A() => LobbyShot.Focused(new Vector3(0, 1.5f, 0), new Vector3(0, 1.5f, 3), 40f, 0.2f, 0f, 1f, 0f);
        private static LobbyShot B() => LobbyShot.Focused(new Vector3(2, 1.2f, 0), new Vector3(1, 1f, 6), 30f, 0.9f, -1f, 0.5f, 1f);

        [Test]
        public void EasingEndpointsAndMonotonic()
        {
            Assert.AreEqual(0f, LobbyEasing.InOutCubic(0f), 1e-5f);
            Assert.AreEqual(1f, LobbyEasing.InOutCubic(1f), 1e-5f);
            Assert.AreEqual(0.5f, LobbyEasing.InOutCubic(0.5f), 1e-5f);
            var prev = -1f;
            for (var i = 0; i <= 20; i++)
            {
                var v = LobbyEasing.Smootherstep(i / 20f);
                Assert.IsTrue(v >= prev);
                prev = v;
            }
            Assert.AreEqual(0f, LobbyEasing.Clamp01(float.NaN), 1e-6f);
            Assert.AreEqual(0f, LobbyEasing.Delayed(0.2f, 0.25f), 1e-6f);
            Assert.AreEqual(1f, LobbyEasing.Early(0.9f, 0.8f), 1e-6f);
        }

        [Test]
        public void BlendEndpointsExact()
        {
            var a = A(); var b = B();
            Assert.AreEqual(a.Position.x, LobbyShotBlend.Evaluate(a, b, 0f).Position.x, 1e-6f);
            var end = LobbyShotBlend.Evaluate(a, b, 1f);
            Assert.AreEqual(b.Position.x, end.Position.x, 1e-6f);
            Assert.AreEqual(b.FieldOfView, end.FieldOfView, 1e-6f);
        }

        [Test]
        public void BlendChannelsHaveDifferentTiming()
        {
            var a = A(); var b = B();
            var mid = LobbyShotBlend.Evaluate(a, b, 0.5f);
            // FOV erken biter: ortada konumdan daha ileridedir (30'a doğru).
            var fovFrac = (a.FieldOfView - mid.FieldOfView) / (a.FieldOfView - b.FieldOfView);
            Assert.Greater(fovFrac, 0.5f);
            // Odak gecikir: ortada bulanıklık yarıdan azdır.
            var blurFrac = (mid.Blur - a.Blur) / (b.Blur - a.Blur);
            Assert.Less(blurFrac, 0.5f);
        }

        [Test]
        public void ArcIsZeroAtEndsAndBulgesInMiddle()
        {
            var from = new Vector3(0, 1, 0); var to = new Vector3(2, 1, 0);
            Assert.AreEqual(0f, LobbyShotBlend.ArcOffset(from, to, 0f).magnitude, 1e-6f);
            Assert.AreEqual(0f, LobbyShotBlend.ArcOffset(from, to, 1f).magnitude, 1e-4f);
            Assert.Greater(LobbyShotBlend.ArcOffset(from, to, 0.5f).magnitude, 0.05f);
            Assert.AreEqual(0f, LobbyShotBlend.ArcOffset(from, from, 0.5f).magnitude, 1e-6f);
        }

        [Test]
        public void FocusLerpIsLogarithmic()
        {
            Assert.AreEqual(Mathf.Sqrt(1.2f * 8f), LobbyShotBlend.FocusLerp(1.2f, 8f, 0.5f), 1e-3f);
            Assert.AreEqual(2f, LobbyShotBlend.FocusLerp(2f, 5f, 0f), 1e-4f);
        }

        [Test]
        public void DurationIsClamped()
        {
            var a = A(); var b = B();
            var d = LobbyShotBlend.DurationFor(a, b);
            Assert.IsTrue(d >= 0.9f && d <= 2.2f);
            Assert.AreEqual(0.9f, LobbyShotBlend.DurationFor(a, a), 1e-5f);
        }

        [Test]
        public void ThirdsPlacesSubjectOnLeftThird()
        {
            var cam = new Vector3(0.45f, 1.5f, -0.5f);
            var subject = new Vector3(0.2f, 1.62f, 2.6f);
            var target = LobbyThirdsFraming.SolveTarget(cam, subject, 38f, 16f / 9f, LobbyThirdsFraming.LeftThird, 0.26f);
            Assert.IsTrue(LobbyThirdsFraming.ProjectToNdc(cam, target, 38f, 16f / 9f, subject, out var ndc));
            Assert.AreEqual(-1f / 3f, ndc.x, 0.02f);
            Assert.AreEqual(0.26f, ndc.y, 0.03f);
        }

        [Test]
        public void ThirdsRightSideAndHorizontalFov()
        {
            var cam = Vector3.zero;
            var subject = new Vector3(0, 0, 5);
            var target = LobbyThirdsFraming.SolveTarget(cam, subject, 40f, 1.5f, LobbyThirdsFraming.RightThird, 0f);
            Assert.IsTrue(LobbyThirdsFraming.ProjectToNdc(cam, target, 40f, 1.5f, subject, out var ndc));
            Assert.AreEqual(1f / 3f, ndc.x, 0.02f);
            Assert.Greater(LobbyThirdsFraming.HorizontalFov(40f, 16f / 9f), 40f);
            Assert.AreEqual(0f, LobbyThirdsFraming.DistanceToThirdLine(-1f / 3f), 1e-6f);
        }

        [Test]
        public void HandheldIsBoundedDeterministicAndRamps()
        {
            for (var i = 0; i < 200; i++)
            {
                var n = LobbyHandheld.Fbm(i * 0.37f, 11);
                Assert.IsTrue(n >= -1.001f && n <= 1.001f);
            }
            Assert.AreEqual(LobbyHandheld.Fbm(3.3f, 5), LobbyHandheld.Fbm(3.3f, 5), 1e-9f);
            Assert.AreEqual(0f, LobbyHandheld.Amplitude(40f, 0f, 1f, 0f), 1e-6f);
            Assert.AreEqual(1f, LobbyHandheld.Amplitude(40f, 5f, 1f, 0f), 1e-4f);
            Assert.Greater(LobbyHandheld.Amplitude(40f, 5f, 1f, 1f), 1.3f);
            Assert.Less(LobbyHandheld.Amplitude(20f, 5f, 1f, 0f), LobbyHandheld.Amplitude(60f, 5f, 1f, 0f));
            var s = LobbyHandheld.Sample(12.3f, 40f, 10f, 1f, 1f);
            Assert.Less(Mathf.Abs(s.YawDegrees), 0.2f);
            Assert.Less(s.PositionOffset.magnitude, 0.01f);
            Assert.AreEqual(0f, LobbyHandheld.Noise(float.NaN, 1), 1e-6f);
        }

        [Test]
        public void LibraryHasAllTabsWithSaneValues()
        {
            var shots = LobbyShotLibrary.Build(16f / 9f);
            Assert.AreEqual(LobbyShotLibrary.Count, shots.Length);
            Assert.Less(shots[LobbyShotLibrary.Settings].ExposureEv, -1f);
            Assert.Less(shots[LobbyShotLibrary.Loadout].FieldOfView, shots[LobbyShotLibrary.Main].FieldOfView);
            Assert.Greater(shots[LobbyShotLibrary.Loadout].Blur, shots[LobbyShotLibrary.Main].Blur);
            Assert.Greater(shots[LobbyShotLibrary.Play].Position.y, shots[LobbyShotLibrary.Main].Position.y);
            for (var i = 0; i < shots.Length; i++)
            {
                Assert.Greater(shots[i].FocusDistance, 0.1f);
                Assert.IsFalse(float.IsNaN(shots[i].Target.x));
            }
            Assert.AreEqual(0, LobbyShotLibrary.Clamp(99));
        }

        [Test]
        public void DirectorTransitionsAndInterrupts()
        {
            var shots = LobbyShotLibrary.Build(16f / 9f);
            var d = new LobbyShotDirector(shots, LobbyShotLibrary.Main);
            Assert.IsFalse(d.IsMoving);
            d.SetShot(LobbyShotLibrary.Loadout);
            Assert.IsTrue(d.IsMoving);
            var mid = d.Tick(d.Duration * 0.5f);
            var before = mid.Position;
            d.SetShot(LobbyShotLibrary.Settings);
            var after = d.Tick(0f).Position;
            Assert.AreEqual(before.x, after.x, 1e-4f);
            Assert.AreEqual(before.z, after.z, 1e-4f);
            d.Tick(10f);
            Assert.IsFalse(d.IsMoving);
            Assert.AreEqual(shots[LobbyShotLibrary.Settings].FieldOfView, d.Current.FieldOfView, 1e-4f);
            Assert.AreEqual(0f, d.Transit01, 1e-6f);
        }

        [Test]
        public void GaussianMapping()
        {
            Assert.AreEqual(3.6f, LobbyCameraGrade.GaussianStartFor(3f), 1e-4f);
            Assert.AreEqual(5.6f, LobbyCameraGrade.GaussianEndFor(3f, 1f) , 1e-3f);
            Assert.AreEqual(17.6f, LobbyCameraGrade.GaussianEndFor(3f, 0f), 1e-3f);
            Assert.AreEqual(1.5f, LobbyCameraGrade.GaussianRadiusFor(1f), 1e-4f);
            Assert.IsFalse(LobbyCameraGrade.DofAllowedForTier(1));
            Assert.IsTrue(LobbyCameraGrade.DofAllowedForTier(2));
        }
    }
}
