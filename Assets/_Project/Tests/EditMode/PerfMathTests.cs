using NUnit.Framework;
using Project.Infrastructure.Rendering.Perf;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class PerfMathTests
    {
        [Test]
        public void ClassifyLod_Cases()
        {
            Assert.AreEqual(LodVerdict.Hatali, PerfMath.ClassifyLod(null));
            Assert.AreEqual(LodVerdict.TekSeviye, PerfMath.ClassifyLod(new[] { 0.01f }));
            Assert.AreEqual(LodVerdict.Ok, PerfMath.ClassifyLod(new[] { 0.5f, 0.2f, 0.05f }));
            Assert.AreEqual(LodVerdict.AsiriDetayli, PerfMath.ClassifyLod(new[] { 0.8f, 0.3f }));
            Assert.AreEqual(LodVerdict.Hatali, PerfMath.ClassifyLod(new[] { 0.2f, 0.5f }));
        }

        [Test]
        public void StaticBatch_Candidate()
        {
            Assert.IsTrue(PerfMath.IsStaticBatchCandidate(true, false, true, 500));
            Assert.IsFalse(PerfMath.IsStaticBatchCandidate(false, false, true, 500));
            Assert.IsFalse(PerfMath.IsStaticBatchCandidate(true, true, true, 500));
            Assert.IsFalse(PerfMath.IsStaticBatchCandidate(true, false, true, 90000));
        }

        [Test]
        public void Particle_PausesOnlyBehindAndFar()
        {
            var cam = Vector3.zero; var fwd = Vector3.forward;
            Assert.IsTrue(PerfMath.ShouldPauseParticle(cam, fwd, new Vector3(0, 0, -30), 6f));
            Assert.IsFalse(PerfMath.ShouldPauseParticle(cam, fwd, new Vector3(0, 0, 30), 6f));
            Assert.IsFalse(PerfMath.ShouldPauseParticle(cam, fwd, new Vector3(0, 0, -3), 6f));
        }

        [Test]
        public void Audio_HysteresisAndTier()
        {
            Assert.Greater(PerfMath.AudioCullDistance(3, 50f), PerfMath.AudioCullDistance(0, 50f));
            Assert.IsTrue(PerfMath.ShouldDisableAudio(60f, 50f, false));
            Assert.IsFalse(PerfMath.ShouldDisableAudio(48f, 50f, false));
            Assert.IsTrue(PerfMath.ShouldDisableAudio(48f, 50f, true));
            Assert.IsFalse(PerfMath.ShouldDisableAudio(40f, 50f, true));
        }
    }
}
