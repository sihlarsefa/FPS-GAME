using NUnit.Framework;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class HorizonVistaMathTests
    {
        [Test]
        public void RidgeHeights_AreFiniteAndBounded()
        {
            foreach (var l in HorizonVistaMath.Layers)
                for (var s = 0; s <= l.Segments; s++)
                {
                    var h = HorizonVistaMath.RidgeHeight(l, s);
                    Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h));
                    Assert.GreaterOrEqual(h, l.MinH - 0.01f);
                    Assert.LessOrEqual(h, l.MaxH + l.TreeH + 0.01f);
                }
        }

        [Test]
        public void TotalTriangles_Under20k()
        {
            var t = HorizonVistaMath.HazeTriangles(128) + HorizonVistaMath.StarCount * 2;
            foreach (var l in HorizonVistaMath.Layers) t += HorizonVistaMath.TriangleCount(l);
            Assert.Less(t, 20000);
        }

        [Test]
        public void FarLayerIsLighterThanNear_Day()
        {
            var fog = new Color(0.7f, 0.75f, 0.85f);
            var tint = new Color(0.4f, 0.5f, 0.8f);
            var far = HorizonVistaMath.LayerRidgeColor(HorizonVistaMath.Layers[0], 0, false, 0f, fog, tint);
            var near = HorizonVistaMath.LayerRidgeColor(HorizonVistaMath.Layers[2], 2, false, 0f, fog, tint);
            Assert.Greater(far.r + far.g + far.b, near.r + near.g + near.b);
        }

        [Test]
        public void Night_SilhouetteDarkerThanFog()
        {
            var fog = new Color(0.2f, 0.25f, 0.4f);
            var c = HorizonVistaMath.LayerRidgeColor(HorizonVistaMath.Layers[0], 0, true, 0f, fog, fog);
            Assert.Less(c.r + c.g + c.b, fog.r + fog.g + fog.b);
        }

        [Test]
        public void Sanitize_ReplacesNaN()
        {
            var c = HorizonVistaMath.Sanitize(new Color(float.NaN, 0, 0, 1), Color.gray);
            Assert.AreEqual(Color.gray, c);
        }

        [Test]
        public void Parallax_ClampedAndNearerMoreThanFar()
        {
            var a = HorizonVistaMath.ParallaxOffset(new Vector3(100, 0, 0), Vector3.zero, HorizonVistaMath.Layers[0].Lag);
            var b = HorizonVistaMath.ParallaxOffset(new Vector3(100, 0, 0), Vector3.zero, HorizonVistaMath.Layers[2].Lag);
            Assert.Greater(Mathf.Abs(b.x), Mathf.Abs(a.x));
            var big = HorizonVistaMath.ParallaxOffset(new Vector3(1e6f, 0, 0), Vector3.zero, 0.5f);
            Assert.LessOrEqual(Mathf.Abs(big.x), HorizonVistaMath.MaxParallaxOffset);
        }

        [Test]
        public void Day_AllLayersDarkerThanHorizonSky_EvenWithNearWhiteFog()
        {
            var fog = new Color(0.92f, 0.94f, 0.9f);
            var tint = new Color(0.35f, 0.5f, 0.78f);
            foreach (var mix in new[] { 0f, 0.5f, 1f })
            {
                var sky = HorizonVistaMath.SkyHorizonColor(fog, tint, false);
                var skyL = HorizonVistaMath.Luma(sky);
                Assert.Less(skyL, HorizonVistaMath.Luma(fog) + 1e-4f);
                for (var i = 0; i < 3; i++)
                {
                    var c = HorizonVistaMath.LayerRidgeColor(HorizonVistaMath.Layers[i], i, false, mix, fog, tint);
                    Assert.Less(HorizonVistaMath.Luma(c), skyL, "layer " + i + " mix " + mix);
                }

                var far = HorizonVistaMath.LayerRidgeColor(HorizonVistaMath.Layers[0], 0, false, mix, fog, tint);
                Assert.GreaterOrEqual(HorizonVistaMath.Luma(far), skyL * 0.8f, "far layer should be only mildly darker");
            }
        }

        [Test]
        public void ClampLuma_NeverExceedsLimit()
        {
            var c = HorizonVistaMath.ClampLuma(new Color(1f, 1f, 1f), 0.5f);
            Assert.LessOrEqual(HorizonVistaMath.Luma(c), 0.5001f);
        }

        [Test]
        public void Ridge_IsIrregular_NotFlat()
        {
            foreach (var l in HorizonVistaMath.Layers)
            {
                float mn = float.MaxValue, mx = float.MinValue;
                for (var s = 0; s < l.Segments; s++)
                {
                    var h = HorizonVistaMath.RidgeHeight(l, s);
                    mn = Mathf.Min(mn, h);
                    mx = Mathf.Max(mx, h);
                }

                Assert.Greater(mx - mn, (l.MaxH - l.MinH) * 0.25f);
            }
        }
    }
}
