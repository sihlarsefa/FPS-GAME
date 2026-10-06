#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vfx;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class ImpactRicochetTests
    {
        [Test]
        public void AngleGate_Below25Only()
        {
            Assert.IsTrue(RicochetRules.PassesAngleGate(0f));
            Assert.IsTrue(RicochetRules.PassesAngleGate(24.9f));
            Assert.IsFalse(RicochetRules.PassesAngleGate(25f));
            Assert.IsFalse(RicochetRules.PassesAngleGate(-1f));
        }

        [Test]
        public void HardSurfaces_OnlyMetalAndConcrete()
        {
            Assert.IsTrue(RicochetRules.IsHardSurface(SurfaceKind.Metal));
            Assert.IsTrue(RicochetRules.IsHardSurface(SurfaceKind.Concrete));
            Assert.IsFalse(RicochetRules.IsHardSurface(SurfaceKind.Wood));
            Assert.IsFalse(RicochetRules.IsHardSurface(SurfaceKind.Dirt));
            Assert.IsFalse(RicochetRules.IsHardSurface(SurfaceKind.Flesh));
        }

        [Test]
        public void Chance_IsDeterministicAndAboutTwentyPercent()
        {
            var hits = 0;
            const int n = 20000;
            for (var i = 0; i < n; i++)
            {
                var seed = RicochetRules.ShotSeed(new Vector3(i * 0.37f, 1.5f, -i * 0.11f), new Vector3(i, 0f, 2f), 0);
                var a = RicochetRules.ShouldRicochet(SurfaceKind.Metal, 10f, seed);
                Assert.AreEqual(a, RicochetRules.ShouldRicochet(SurfaceKind.Metal, 10f, seed));
                if (a) hits++;
            }

            var rate = hits / (float)n;
            Assert.That(rate, Is.InRange(0.17f, 0.23f));
        }

        [Test]
        public void ShouldRicochet_RejectsSoftSurfaceAndSteepAngle()
        {
            for (uint s = 1; s < 500; s++)
            {
                Assert.IsFalse(RicochetRules.ShouldRicochet(SurfaceKind.Dirt, 5f, s));
                Assert.IsFalse(RicochetRules.ShouldRicochet(SurfaceKind.Metal, 40f, s));
            }
        }

        [Test]
        public void DamageKeep_FallsWithAngleAndStaysBelowOne()
        {
            Assert.Greater(RicochetRules.DamageKeep(0f), RicochetRules.DamageKeep(24f));
            Assert.Less(RicochetRules.DamageKeep(0f), 1f);
            Assert.Greater(RicochetRules.DamageKeep(25f), 0f);
        }

        [Test]
        public void Deflect_StaysUnitAndLeavesSurface()
        {
            var n = Vector3.up;
            for (uint s = 1; s < 200; s++)
            {
                var d = RicochetRules.Deflect(new Vector3(1f, -0.1f, 0f).normalized, n, s);
                Assert.AreEqual(1f, d.magnitude, 1e-3f);
                Assert.Greater(Vector3.Dot(d, n), 0f);
                Assert.AreEqual(d, RicochetRules.Deflect(new Vector3(1f, -0.1f, 0f).normalized, n, s));
            }
        }

        [Test]
        public void Variations_AtLeastThreePerSurface()
        {
            foreach (SurfaceKind k in System.Enum.GetValues(typeof(SurfaceKind)))
            {
                Assert.GreaterOrEqual(ImpactSoundRules.VariationCount(k), 3);
                for (uint s = 0; s < 50; s++)
                    Assert.That(ImpactSoundRules.VariantFor(s, k), Is.InRange(0, ImpactSoundRules.VariationCount(k) - 1));
            }
        }

        [Test]
        public void DistanceBuckets_AndCutoffMonotonic()
        {
            Assert.AreEqual(0, ImpactSoundRules.DistanceBucket(10f));
            Assert.AreEqual(1, ImpactSoundRules.DistanceBucket(50f));
            Assert.AreEqual(2, ImpactSoundRules.DistanceBucket(200f));
            Assert.AreEqual(0f, ImpactSoundRules.CutoffHz(0));
            Assert.Greater(ImpactSoundRules.CutoffHz(1), ImpactSoundRules.CutoffHz(2));
            Assert.Greater(ImpactSoundRules.CutoffHz(2), 0f);
        }

        [Test]
        public void Synth_RendersFiniteBoundedAudioForEverySurface()
        {
            foreach (SurfaceKind k in System.Enum.GetValues(typeof(SurfaceKind)))
            {
                for (var v = 0; v < ImpactSoundRules.Variations; v++)
                {
                    var a = ImpactSynth.Render(k, v, 0);
                    var b = ImpactSynth.Render(k, v, 2);
                    Assert.Greater(a.Length, 1000);
                    Assert.AreEqual(a.Length, b.Length);
                    var peak = 0f;
                    foreach (var x in a)
                    {
                        Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x));
                        if (Mathf.Abs(x) > peak) peak = Mathf.Abs(x);
                    }

                    Assert.Greater(peak, 0.05f);
                    Assert.LessOrEqual(peak, 1f);
                }
            }
        }

        [Test]
        public void Synth_VariantsDiffer_AndRicochetRenders()
        {
            var a = ImpactSynth.Render(SurfaceKind.Metal, 0, 0);
            var b = ImpactSynth.Render(SurfaceKind.Metal, 1, 0);
            var diff = 0f;
            for (var i = 0; i < Mathf.Min(a.Length, b.Length); i++) diff += Mathf.Abs(a[i] - b[i]);
            Assert.Greater(diff, 1f);
            for (var v = 0; v < ImpactSynth.RicochetVariations; v++)
            {
                var r = ImpactSynth.RenderRicochet(v, 1);
                Assert.Greater(r.Length, 10000);
                foreach (var x in r) Assert.IsFalse(float.IsNaN(x));
            }
        }
    }
}
#endif
