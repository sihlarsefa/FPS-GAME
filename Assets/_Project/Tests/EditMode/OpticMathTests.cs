using NUnit.Framework;
using Project.Infrastructure.Weapons;
using Project.Infrastructure.Weapons.Optics;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class OpticMathTests
    {
        [Test]
        public void MagnificationFromFovInvertsFovFromMagnification()
        {
            var fov = ScopeMath.FovFromMagnification(70f, 4f);
            Assert.AreEqual(4f, OpticMath.MagnificationFromFov(70f, fov), 0.02f);
        }

        [Test]
        public void SensitivityScaleIsInverseMagnificationAtFullMatch()
        {
            var fov = ScopeMath.FovFromMagnification(70f, 4f);
            Assert.AreEqual(0.25f, OpticMath.SensitivityScale(70f, fov, 1f), 0.01f);
            Assert.AreEqual(1f, OpticMath.SensitivityScale(70f, fov, 0f), 1e-4f);
        }

        [Test]
        public void BlendCurvesHaveFixedEndpointsAndPositionLeadsFov()
        {
            Assert.AreEqual(0f, OpticMath.PositionBlend(0f), 1e-6f);
            Assert.AreEqual(1f, OpticMath.PositionBlend(1f), 1e-6f);
            Assert.AreEqual(0f, OpticMath.FovBlend(0f), 1e-6f);
            Assert.AreEqual(1f, OpticMath.FovBlend(1f), 1e-6f);
            Assert.Greater(OpticMath.PositionBlend(0.3f), OpticMath.FovBlend(0.3f));
        }

        [Test]
        public void AdsFovEndsAtZoomedFovAndIsMonotonic()
        {
            Assert.AreEqual(70f, OpticMath.AdsFov(70f, 4f, 0f), 1e-3f);
            Assert.AreEqual(ScopeMath.FovFromMagnification(70f, 4f), OpticMath.AdsFov(70f, 4f, 1f), 1e-3f);
            Assert.Less(OpticMath.AdsFov(70f, 4f, 0.7f), OpticMath.AdsFov(70f, 4f, 0.3f));
        }

        [Test]
        public void OverlayAlphaIsLaterForHighMagnification()
        {
            Assert.AreEqual(0f, OpticMath.OverlayAlpha(0f, 4f), 1e-6f);
            Assert.AreEqual(1f, OpticMath.OverlayAlpha(1f, 4f), 1e-6f);
            Assert.Greater(OpticMath.OverlayAlpha(0.4f, 1f), OpticMath.OverlayAlpha(0.4f, 4f));
        }

        [Test]
        public void AlignedBoreGivesZeroReticleOffset()
        {
            var r = OpticMath.Collimate(Quaternion.identity, Vector2.zero, 0.012f, 0.1f, 54f, 1080f);
            Assert.AreEqual(0f, r.OffsetPixels.x, 1e-3f);
            Assert.AreEqual(0f, r.OffsetPixels.y, 1e-3f);
            Assert.AreEqual(1f, r.Visibility, 1e-4f);
        }

        [Test]
        public void CollimatorOffsetIsIndependentOfEyePosition()
        {
            var bore = Quaternion.Euler(-0.5f, 0.3f, 0f);
            var a = OpticMath.Collimate(bore, Vector2.zero, 0.02f, 0.1f, 54f, 1080f);
            var b = OpticMath.Collimate(bore, new Vector2(0.004f, -0.003f), 0.02f, 0.1f, 54f, 1080f);
            Assert.AreEqual(a.OffsetPixels.x, b.OffsetPixels.x, 1e-4f);
            Assert.AreEqual(a.OffsetPixels.y, b.OffsetPixels.y, 1e-4f);
        }

        [Test]
        public void BoreUpwardMovesDotUpOnScreen()
        {
            var r = OpticMath.Collimate(Quaternion.Euler(-1f, 0f, 0f), Vector2.zero, 0.05f, 0.1f, 54f, 1080f);
            Assert.Less(r.OffsetPixels.y, 0f);
            Assert.AreEqual(0f, r.OffsetPixels.x, 1e-3f);
        }

        [Test]
        public void DotFadesWhenEyeLeavesWindow()
        {
            var inside = OpticMath.Collimate(Quaternion.identity, new Vector2(0.002f, 0f), 0.012f, 0.1f, 54f, 1080f);
            var outside = OpticMath.Collimate(Quaternion.identity, new Vector2(0.03f, 0f), 0.012f, 0.1f, 54f, 1080f);
            Assert.AreEqual(1f, inside.Visibility, 1e-4f);
            Assert.AreEqual(0f, outside.Visibility, 1e-4f);
        }

        [Test]
        public void ProjectionMatchesFovEdge()
        {
            // Dikey yarı FOV açısındaki yön ekranın tam kenarına (yarı yükseklik) düşer.
            var dir = Quaternion.Euler(-27f, 0f, 0f) * Vector3.forward;
            var p = OpticMath.DirectionToScreenPixels(dir, 54f, 1080f);
            Assert.AreEqual(-540f, p.y, 0.5f);
        }

        [Test]
        public void ExitPupilShrinksWithMagnificationAndShadowGrows()
        {
            Assert.AreEqual(8f, OpticMath.ExitPupilMm(32f, 4f), 1e-4f);
            Assert.Less(OpticMath.ExitPupilMm(32f, 8f), OpticMath.ExitPupilMm(32f, 4f));
            Assert.AreEqual(0f, OpticMath.EyeBoxShadow(0f, 8f), 1e-4f);
            Assert.Greater(OpticMath.EyeBoxShadow(0.02f, 4f), 0.99f);
            Assert.Greater(OpticMath.EyeBoxShadow(0.006f, 4f), OpticMath.EyeBoxShadow(0.006f, 8f) - 1f);
        }

        [Test]
        public void ReticleScaleSecondPlaneConstantFirstPlaneGrows()
        {
            Assert.AreEqual(1f, OpticMath.ReticleScale(FocalPlane.Second, 6f, 2f), 1e-6f);
            Assert.AreEqual(2f, OpticMath.ReticleScale(FocalPlane.First, 4f, 2f), 1e-6f);
        }

        [Test]
        public void HoldoverPixelsMatchesAngle()
        {
            // 1 m düşüş 100 m'de = 10 mrad; 10 derece FOV'da 1080 px: ~10e-3 / tan(5°) * 540.
            var px = OpticMath.HoldoverPixels(1f, 100f, 10f, 1080f);
            Assert.AreEqual(0.01f / Mathf.Tan(5f * Mathf.Deg2Rad) * 540f, px, 0.1f);
            Assert.AreEqual(0f, OpticMath.HoldoverPixels(1f, 0f, 10f, 1080f), 1e-6f);
        }

        [Test]
        public void OldSwayDampingExceededHalfMradNewOneStaysSmall()
        {
            var old = OpticMath.SwayResidualMrad(0.005f, 0.18f, 0.14f);
            Assert.Greater(old, 0.5f);
            Assert.Less(OpticMath.SwayResidualMrad(0.005f, OpticMath.AdsSwayFactor(SightKind.Iron), 0.14f), 1.5f);
            Assert.Less(OpticMath.AdsSwayFactor(SightKind.Scope), OpticMath.AdsSwayFactor(SightKind.Iron));
        }

        [Test]
        public void SolveOpticPutsWindowOnAxisAndLevelsBore()
        {
            var window = new Vector3(0.001f, 0.12f, 0.03f);
            var bore = Quaternion.Euler(1f, -0.5f, 0f) * Vector3.forward;
            var s = SightAlignment.SolveOptic(window, bore, 0.10f);
            var p = s.Position + s.Rotation * window;
            Assert.AreEqual(0f, p.x, 1e-5f);
            Assert.AreEqual(0f, p.y, 1e-5f);
            Assert.AreEqual(0.10f, p.z, 1e-5f);
            var b = s.Rotation * bore;
            Assert.Less(Mathf.Sqrt(b.x * b.x + b.y * b.y) / b.z * 1000f, 0.5f);
            Assert.IsFalse(s.Clamped);
        }

        [Test]
        public void SolveOpticClampsLargeTiltAndReportsResidual()
        {
            var bore = Quaternion.Euler(8f, 0f, 0f) * Vector3.forward;
            var s = SightAlignment.SolveOptic(new Vector3(0f, 0.1f, 0f), bore, 0.1f, 3f);
            Assert.IsTrue(s.Clamped);
            Assert.AreEqual(5f * Mathf.Deg2Rad * 1000f, s.ResidualMilliradians, 1f);
        }

        [Test]
        public void SnapToPixelRoundsBoth()
        {
            var p = OpticMath.SnapToPixel(new Vector2(10.4f, -3.6f));
            Assert.AreEqual(10f, p.x, 1e-6f);
            Assert.AreEqual(-4f, p.y, 1e-6f);
        }
    }
}
