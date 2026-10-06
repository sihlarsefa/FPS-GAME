#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;

namespace Project.Tests.EditMode
{
    public class AirAbsorptionTests
    {
        [Test]
        public void ReferenceCurve_MatchesAnchors()
        {
            Assert.AreEqual(22000f, AirAbsorption.CutoffHz(10f), 1f);
            Assert.AreEqual(16000f, AirAbsorption.CutoffHz(50f), 50f);
            Assert.AreEqual(3000f, AirAbsorption.CutoffHz(500f), 50f);
        }

        [Test]
        public void Curve_IsMonotonic()
        {
            var prev = 99999f;
            for (var d = 0f; d <= 1500f; d += 10f)
            {
                var h = AirAbsorption.CutoffHz(d);
                Assert.IsTrue(h <= prev + 0.01f);
                prev = h;
            }
        }

        [Test]
        public void Rifle_CutoffNearAnchors_AndPistolDullerThanSniper()
        {
            Assert.AreEqual(16000f, AcousticsMath.CutoffHz(CaliberClass.Rifle, 50f), 200f);
            Assert.Less(AcousticsMath.CutoffHz(CaliberClass.Pistol, 200f), AcousticsMath.CutoffHz(CaliberClass.Sniper, 200f));
        }

        [Test]
        public void ReverbTap_IsLaterAndQuieter()
        {
            Assert.Greater(AirAbsorption.ReverbTapDelay(0.05f), 0.05f);
            Assert.Less(AirAbsorption.ReverbTapDelay(5f), 0.31f);
            Assert.AreEqual(0.3f, AirAbsorption.ReverbTapGain(0.6f), 0.001f);
        }
    }
}
#endif
