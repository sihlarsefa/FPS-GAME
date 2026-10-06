using NUnit.Framework;
using Project.Presentation.UI;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class MapMathExtraTests
    {
        private static readonly MapFrame Frame = new MapFrame(Vector2.zero, 512f);

        [Test]
        public void MilitaryGrid_SouthwestCorner_IsZero()
        {
            Assert.AreEqual("36S KD 000 000", MapMath.MilitaryGrid(Frame, new Vector3(-512f, 0f, -512f)));
        }

        [Test]
        public void MilitaryGrid_UsesTenMeterSteps()
        {
            // Güneybatıdan 510 m doğu, 870 m kuzey → 051, 087.
            Assert.AreEqual("36S KD 051 087", MapMath.MilitaryGrid(Frame, new Vector3(-2f, 0f, 358f)));
        }

        [Test]
        public void MilitaryGrid_OutsideMap_IsClamped()
        {
            Assert.AreEqual("36S KD 999 000", MapMath.MilitaryGrid(Frame, new Vector3(99999f, 0f, -99999f)));
            Assert.AreEqual("36S KD 000 000", MapMath.FormatMilitaryGrid(-5, -9));
        }

        [Test]
        public void FormatMeasure_ShowsDistanceAndBearing()
        {
            var text = MapMath.FormatMeasure(Vector3.zero, new Vector3(0f, 0f, 320f));
            Assert.AreEqual("320 m · 000° K", text);
            Assert.AreEqual("100 m · 090° D", MapMath.FormatMeasure(Vector3.zero, new Vector3(100f, 0f, 0f)));
            Assert.AreEqual("1,5 km · 225° GB", MapMath.FormatMeasure(Vector3.zero, new Vector3(-1060.66f, 0f, -1060.66f)));
        }

        [Test]
        public void RotateOffset_HeadingEast_PutsEastUp()
        {
            var r = MapMath.RotateOffset(new Vector2(1f, 0f), 90f);
            Assert.Less(Mathf.Abs(r.x), 1e-4f);
            Assert.Less(Mathf.Abs(r.y - 1f), 1e-4f);
        }

        [Test]
        public void RotateOffset_NorthHeading_IsIdentity()
        {
            var r = MapMath.RotateOffset(new Vector2(3f, -4f), 0f);
            Assert.Less(Mathf.Abs(r.x - 3f), 1e-4f);
            Assert.Less(Mathf.Abs(r.y + 4f), 1e-4f);
        }

        [Test]
        public void RotateOffset_PreservesLength()
        {
            var v = new Vector2(5f, 12f);
            Assert.Less(Mathf.Abs(MapMath.RotateOffset(v, 137f).magnitude - 13f), 1e-3f);
        }

        [Test]
        public void RotatedUiAngle_ObjectFacingSameAsMap_PointsUp()
        {
            Assert.Less(Mathf.Abs(MapMath.RotatedUiAngle(90f, 90f)), 1e-3f);
        }

        [Test]
        public void NearestPointOnCircle_LiesOnCircleTowardsPlayer()
        {
            var p = MapMath.NearestPointOnCircle(new Vector3(300f, 5f, 0f), 0f, 0f, 100f);
            Assert.Less(Mathf.Abs(p.x - 100f), 1e-3f);
            Assert.Less(Mathf.Abs(p.z), 1e-3f);
            Assert.AreEqual(5f, p.y);
        }

        [Test]
        public void NearestPointOnCircle_AtCenter_DoesNotProduceNaN()
        {
            var p = MapMath.NearestPointOnCircle(Vector3.zero, 0f, 0f, 50f);
            Assert.IsTrue(MapMath.IsFinite(p));
        }

        [Test]
        public void ZoomFocus_KeepsPivotAtSameScreenOffset()
        {
            var focus = new Vector2(0.5f, 0.5f);
            var pivot = new Vector2(0.7f, 0.4f);
            var newFocus = MapMath.ZoomFocus(focus, pivot, 880f, 1760f);
            var before = (pivot - focus) * 880f;
            var after = (pivot - newFocus) * 1760f;
            Assert.Less((before - after).magnitude, 1e-2f);
        }

        [Test]
        public void ZoomFocus_InvalidPixels_ReturnsFocus()
        {
            var focus = new Vector2(0.3f, 0.6f);
            Assert.AreEqual(focus, MapMath.ZoomFocus(focus, Vector2.one, 0f, 100f));
        }

        [Test]
        public void ClampFocus_KeepsViewInsideMap()
        {
            var f = MapMath.ClampFocus(new Vector2(-1f, 2f), 2f);
            Assert.AreEqual(0.25f, f.x);
            Assert.AreEqual(0.75f, f.y);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), MapMath.ClampFocus(new Vector2(float.NaN, 0.5f), 1f));
        }

        [Test]
        public void NiceScaleMeters_PicksSmallestStepWideEnough()
        {
            Assert.AreEqual(100, MapMath.NiceScaleMeters(1f, 90f));
            Assert.AreEqual(25, MapMath.NiceScaleMeters(4f, 90f));
            Assert.AreEqual(1000, MapMath.NiceScaleMeters(0.01f, 90f));
        }

        [Test]
        public void MapMarkers_Fallen_RecordsAndCapsAtMax()
        {
            MapMarkers.Reset();
            for (var i = 0; i < MapMarkers.MaxFallen + 3; i++)
                MapMarkers.RecordFallen(new Vector3(i, 0f, 0f), "Er " + i, Project.Core.Domain.MilitaryRank.Er, i);

            var list = MapMarkers.Fallen;
            Assert.AreEqual(MapMarkers.MaxFallen, list.Count);
            Assert.AreEqual(3f, list[0].Position.x);
            MapMarkers.Reset();
            Assert.AreEqual(0, MapMarkers.Fallen.Count);
        }

        [Test]
        public void MapMarkers_Fallen_IgnoresNonFinitePositions()
        {
            MapMarkers.Reset();
            MapMarkers.RecordFallen(new Vector3(float.NaN, 0f, 0f), "x", Project.Core.Domain.MilitaryRank.Er, 0f);
            Assert.AreEqual(0, MapMarkers.Fallen.Count);
        }
    }
}
