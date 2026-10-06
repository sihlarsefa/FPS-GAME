using NUnit.Framework;
using Project.Infrastructure.Weapons;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class SightAlignmentTests
    {
        [Test]
        public void RearSightLandsOnEyeAxis()
        {
            var rear = new Vector3(0.001f, 0.09f, 0.02f);
            var front = new Vector3(0f, 0.093f, 0.5f);
            var pos = SightAlignment.EyeOffset(rear, front, 0.14f);
            var rot = SightAlignment.SightLineRotation(rear, front);
            var p = pos + rot * rear;
            Assert.AreEqual(0f, p.x, 1e-5f);
            Assert.AreEqual(0f, p.y, 1e-5f);
            Assert.AreEqual(0.14f, p.z, 1e-5f);
        }

        [Test]
        public void RearAndFrontOnViewAxisWithinHalfMrad()
        {
            var rear = new Vector3(0.001f, 0.095f, -0.04f);
            var front = new Vector3(-0.0005f, 0.0962f, 0.29f);
            var pos = SightAlignment.EyeOffset(rear, front, 0.14f);
            var rot = SightAlignment.SightLineRotation(rear, front);
            Assert.IsTrue(SightAlignment.IsIronLine(rear, front));
            var rp = pos + rot * rear;
            var fp = pos + rot * front;
            Assert.Less(Mathf.Sqrt(rp.x * rp.x + rp.y * rp.y) / rp.z * 1000f, 0.5f);
            Assert.Less(Mathf.Sqrt(fp.x * fp.x + fp.y * fp.y) / fp.z * 1000f, 0.5f);
            Assert.Less(SightAlignment.AxisErrorMilliradians(pos, rot, rear, front), 0.5f);
        }

        [Test]
        public void RedDotCaseIsNotIronLine()
        {
            Assert.IsFalse(SightAlignment.IsIronLine(new Vector3(0f, 0.113f, 0.025f), new Vector3(0f, 0.095f, 0.3f)));
        }

        [Test]
        public void RedDotIsAboutTwoAndHalfPixelsAt1080AndScalesAt4K()
        {
            var w = SightVisuals.AdsDotWorldSize(0.15f, 0.0742f);
            var d = 0.15f + 0.0742f;
            var px1080 = SightVisuals.DotScreenPixels(w, d, SightVisuals.AdsVerticalFov, 1080f);
            var px4k = SightVisuals.DotScreenPixels(w, d, SightVisuals.AdsVerticalFov, 2160f);
            Assert.AreEqual(2.5f, px1080, 0.3f);
            Assert.AreEqual(px1080 * 2f, px4k, 1e-3f);
        }

        [Test]
        public void DotWorldSizeScalesWithDistance()
        {
            var a = SightVisuals.DotWorldSize(1f, 60f, 1080f);
            var b = SightVisuals.DotWorldSize(2f, 60f, 1080f);
            Assert.AreEqual(a * 2f, b, 1e-6f);
        }
    }
}
