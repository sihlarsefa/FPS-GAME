using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class PingMarkerMathTests
    {
        [Test]
        public void IconSize_StaysInPixelRange()
        {
            for (var p = -2f; p <= 2f; p += 0.25f)
            {
                var s = PingMarkerMath.IconSize(p);
                Assert.GreaterOrEqual(s, 28f);
                Assert.LessOrEqual(s, 36f);
            }
        }

        [Test]
        public void BeamHeight_IsClamped()
        {
            Assert.AreEqual(PingMarkerMath.BeamMinPx, PingMarkerMath.BeamHeightPx(100f, 101f));
            Assert.AreEqual(PingMarkerMath.BeamMaxPx, PingMarkerMath.BeamHeightPx(0f, 5000f));
            Assert.AreEqual(100f, PingMarkerMath.BeamHeightPx(0f, 100f), 0.001f);
        }

        [Test]
        public void Alpha_FadesAndOccludes()
        {
            Assert.AreEqual(1f, PingMarkerMath.Alpha(1f, false), 0.001f);
            Assert.AreEqual(0.4f, PingMarkerMath.Alpha(1f, true), 0.001f);
            Assert.AreEqual(0f, PingMarkerMath.Alpha(0f, false), 0.001f);
            Assert.Less(PingMarkerMath.Alpha(0.1f, false), 1f);
        }

        [Test]
        public void FormatDistance_Rounds()
        {
            Assert.AreEqual("325 m", PingMarkerMath.FormatDistance(324.6f));
            Assert.AreEqual("0 m", PingMarkerMath.FormatDistance(-3f));
        }

        [Test]
        public void ClampAnchored_KeepsStackOnScreen()
        {
            var p = PingMarkerMath.ClampAnchored(new Vector2(5000f, 5000f), new Vector2(1920f, 1080f), 100f, 24f);
            Assert.AreEqual(936f, p.x, 0.01f);
            Assert.AreEqual(540f - 24f - 100f, p.y, 0.01f);
        }
    }
}
