using NUnit.Framework;
using Project.Infrastructure.Vehicles;

namespace Project.Tests.EditMode
{
    public sealed class VehicleAmbienceMathTests
    {
        [Test]
        public void Spring_ConvergesToTarget()
        {
            float x = 0f, v = 0f;
            for (var i = 0; i < 400; i++) VehicleAmbienceMath.SpringStep(ref x, ref v, 0.1f, 120f, 14f, 0.016f);
            Assert.That(x, Is.EqualTo(0.1f).Within(0.005f));
        }

        [Test]
        public void Spring_LargeDtDoesNotExplode()
        {
            float x = 0f, v = 0f;
            VehicleAmbienceMath.SpringStep(ref x, ref v, 1f, 120f, 14f, 5f);
            Assert.Less(System.Math.Abs(x), 1f);
        }

        [Test]
        public void HeadlightMode_Cycles()
        {
            var m = HeadlightMode.Kapali;
            m = VehicleAmbienceMath.NextMode(m); Assert.AreEqual(HeadlightMode.Acik, m);
            m = VehicleAmbienceMath.NextMode(m); Assert.AreEqual(HeadlightMode.Karartma, m);
            m = VehicleAmbienceMath.NextMode(m); Assert.AreEqual(HeadlightMode.Kapali, m);
        }

        [Test]
        public void Blackout_DimmerAndShorterThanNormal()
        {
            Assert.Greater(VehicleAmbienceMath.HeadlightIntensity(HeadlightMode.Acik), VehicleAmbienceMath.HeadlightIntensity(HeadlightMode.Karartma));
            Assert.Greater(VehicleAmbienceMath.HeadlightRange(HeadlightMode.Acik), VehicleAmbienceMath.HeadlightRange(HeadlightMode.Karartma));
            Assert.AreEqual(0f, VehicleAmbienceMath.HeadlightIntensity(HeadlightMode.Kapali));
        }

        [Test]
        public void Muffle_InsideLowerThanOutside_HatchOpensIt()
        {
            Assert.Greater(VehicleAmbienceMath.MuffleCutoff(false, 0f), VehicleAmbienceMath.MuffleCutoff(true, 1f));
            Assert.Greater(VehicleAmbienceMath.MuffleCutoff(true, 1f), VehicleAmbienceMath.MuffleCutoff(true, 0f));
            Assert.Greater(1f, VehicleAmbienceMath.InsideVolumeScale(true));
        }

        [Test]
        public void Dust_LowTierSparser_SnowBigger()
        {
            Assert.Greater(VehicleAmbienceMath.DustIntervalScale(0), VehicleAmbienceMath.DustIntervalScale(3));
            Assert.Greater(VehicleAmbienceMath.TrailScale(50f, 85f, true), VehicleAmbienceMath.TrailScale(50f, 85f, false));
        }
    }
}
