using System.Collections.Generic;
using NUnit.Framework;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class RoadsideExtrasTests
    {
        private static List<RoadSample> Straight(float length)
        {
            return RoadsidePlan.Resample(new List<Vector3> { new Vector3(0f, 10f, 0f), new Vector3(0f, 10f, length) }, 4f);
        }

        [Test]
        public void Catenary_ZeroAtEnds_SagAtMiddle()
        {
            Assert.AreEqual(0f, RoadsidePlan.CatenaryDrop(0f, 38f, 1f), 0.01f);
            Assert.AreEqual(0f, RoadsidePlan.CatenaryDrop(1f, 38f, 1f), 0.01f);
            Assert.AreEqual(1f, RoadsidePlan.CatenaryDrop(0.5f, 38f, 1f), 0.01f);
        }

        [Test]
        public void Catenary_SymmetricAndMonotonicToMiddle()
        {
            Assert.AreEqual(RoadsidePlan.CatenaryDrop(0.2f, 40f, 1.2f), RoadsidePlan.CatenaryDrop(0.8f, 40f, 1.2f), 0.01f);
            Assert.Less(RoadsidePlan.CatenaryDrop(0.2f, 40f, 1.2f), RoadsidePlan.CatenaryDrop(0.4f, 40f, 1.2f));
        }

        [Test]
        public void Catenary_NoSagIsFlat()
        {
            Assert.AreEqual(0f, RoadsidePlan.CatenaryDrop(0.5f, 40f, 0f), 0.0001f);
        }

        [Test]
        public void Signs_SpacedAndAlternateSides()
        {
            var s = Straight(1500f);
            var signs = RoadsidePlan.PlanSigns(s, 7f, 4, 0, 24, null);
            Assert.Greater(signs.Count, 3);
            Assert.AreEqual(0, signs[0].Kind);
            Assert.IsTrue(signs[0].Pos.x * signs[1].Pos.x < 0f);
            for (var i = 1; i < signs.Count; i++)
                Assert.IsTrue(Mathf.Abs(signs[i].Pos.z - signs[i - 1].Pos.z) > RoadsidePlan.SignSpacing * 0.9f);
        }

        [Test]
        public void Signs_RespectMaxAndBlocked()
        {
            var s = Straight(3000f);
            Assert.AreEqual(3, RoadsidePlan.PlanSigns(s, 7f, 4, 0, 3, null).Count);
            Assert.IsTrue(RoadsidePlan.PlanSigns(s, 7f, 4, 0, 24, (x, z) => true).Count == 0);
        }

        [Test]
        public void Wrecks_NoneOnShortRoad_SpacedOnLong()
        {
            Assert.AreEqual(0, RoadsidePlan.PlanWrecks(Straight(200f), 7f, 6, null).Count);
            var w = RoadsidePlan.PlanWrecks(Straight(2000f), 7f, 6, null);
            Assert.Greater(w.Count, 1);
            Assert.IsTrue(Mathf.Abs(w[1].Pos.z - w[0].Pos.z) > RoadsidePlan.WreckSpacing * 0.9f);
            Assert.IsTrue(Mathf.Abs(w[0].Pos.x) > 3.5f);
        }

        [Test]
        public void Guardrail_PathFollowsSlope()
        {
            var run = new GuardRun { Start = new Vector3(0, 0, 0), End = new Vector3(0, 8, 40) };
            var path = RoadsidePlan.RunPath(run, 4f);
            Assert.Greater(path.Count, 8);
            Assert.AreEqual(8f, path[path.Count - 1].y, 0.01f);
            Assert.Greater(RoadsidePlan.PitchDegrees(path[0], path[1]), 5f);
            Assert.AreEqual(0f, RoadsidePlan.PitchDegrees(new Vector3(0, 5, 0), new Vector3(0, 5, 4)), 0.01f);
        }

        [Test]
        public void Guardrail_PlanKeepsPoints()
        {
            var s = Straight(80f);
            var runs = RoadsidePlan.PlanGuardrails(s, 7f, 10, (x, z) => 0f, null);
            Assert.Greater(runs.Count, 0);
            Assert.Greater(runs[0].Points.Count, 5);
        }

        [Test]
        public void Glyphs_SupportTurkishCharacters()
        {
            foreach (var c in "İÖÜŞĞÇıiöüşğç")
                Assert.IsTrue(SignGlyphs.Supports(c));
            Assert.AreEqual('İ', SignGlyphs.Upper('i'));
            Assert.AreEqual('I', SignGlyphs.Upper('ı'));
        }

        [Test]
        public void Glyphs_AccentPixelsDifferFromBase()
        {
            Assert.IsFalse(SignGlyphs.IsSet('I', 2, 0));
            Assert.IsTrue(SignGlyphs.IsSet('İ', 2, 0));
            Assert.IsTrue(SignGlyphs.IsSet('Ş', 2, 9));
            Assert.IsFalse(SignGlyphs.IsSet('S', 2, 9));
        }

        [Test]
        public void SignText_FitsCellAndHasInk()
        {
            for (var k = 0; k < RoadsideSignSet.Count; k++)
            {
                var d = RoadsideSignSet.Defs[k];
                Assert.IsTrue(SignGlyphs.FitScale(d.Text, RoadsideSignSet.CellW, RoadsideSignSet.CellH, 7) >= 1);
                foreach (var c in d.Text)
                    Assert.IsTrue(SignGlyphs.Supports(c));
                var px = SignGlyphs.RenderCell(d.Text, RoadsideSignSet.CellW, RoadsideSignSet.CellH, d.Bg, d.Fg);
                var fgCount = 0;
                var inner = new Color32[0];
                foreach (var p in px)
                    if (p.r == d.Fg.r && p.g == d.Fg.g && p.b == d.Fg.b) fgCount++;
                Assert.Greater(fgCount, 600);
            }
        }

        [Test]
        public void Atlas_SizeAndVRanges()
        {
            var px = RoadsideSignSet.BuildAtlasPixels();
            Assert.AreEqual(RoadsideSignSet.CellW * RoadsideSignSet.CellH * RoadsideSignSet.Count, px.Length);
            var v = RoadsideSignSet.VRange(1);
            Assert.AreEqual(0.25f, v.x, 0.001f);
            Assert.AreEqual(0.5f, v.y, 0.001f);
        }
    }
}
