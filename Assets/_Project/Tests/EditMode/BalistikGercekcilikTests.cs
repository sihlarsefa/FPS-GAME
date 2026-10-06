using NUnit.Framework;
using Project.Application.Combat.Ballistics;
using Project.Application.Services;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    /// <summary>Balistik gerçekçilik: drag tablosu, namlu hızı varyansı, zeroing, ses gecikmesi, sekme açıları.</summary>
    [TestFixture]
    public sealed class BalistikGercekcilikTests
    {
        [Test]
        public void DragFactor_TransonikTepeVeSesAltiDusuk()
        {
            Assert.IsTrue(BallisticsTable.DragFactor(200f) < 1f);
            Assert.AreEqual(1f, BallisticsTable.DragFactor(900f), 0.0001f);
            Assert.Greater(BallisticsTable.DragFactor(357f), 1.5f);
        }

        [Test]
        public void HizMenzildeAzalir_HafifMermiDahaCabukYavaslar()
        {
            var r762 = BallisticsTable.SpeedAt(800f, AmmoType.Mm762, 400f);
            var r9 = BallisticsTable.SpeedAt(800f, AmmoType.Mm9, 400f);
            Assert.IsTrue(r762 < 800f);
            Assert.Greater(r762, r9);
        }

        [Test]
        public void UcusSuresiMenzilleArtar()
        {
            var t1 = BallisticsTable.TimeOfFlight(800f, AmmoType.Mm556, 100f);
            var t2 = BallisticsTable.TimeOfFlight(800f, AmmoType.Mm556, 300f);
            Assert.Greater(t2, t1);
            Assert.Greater(t1, 0.1f);
        }

        [Test]
        public void Varyans_AyniTohumAyniSonuc_SinirIcinde()
        {
            var a = MuzzleVelocityVariance.SampleSeeded(800f, AmmoType.Mm762, 12345u);
            var b = MuzzleVelocityVariance.SampleSeeded(800f, AmmoType.Mm762, 12345u);
            Assert.AreEqual(a, b, 0f);
            for (uint s = 1; s < 200; s++)
            {
                var v = MuzzleVelocityVariance.SampleSeeded(800f, AmmoType.Gauge12, s, 1f);
                var lim = 800f * 0.02f * 3f * 1.5f * 1.01f;
                Assert.IsTrue(v >= 800f - lim && v <= 800f + lim);
            }
        }

        [Test]
        public void Varyans_IsinmaArtirir()
        {
            Assert.Greater(MuzzleVelocityVariance.HeatMultiplier(1f), MuzzleVelocityVariance.HeatMultiplier(0f));
            var cold = MuzzleVelocityVariance.Sample(800f, AmmoType.Mm9, 0.9f, 0.1f, 0f);
            var hot = MuzzleVelocityVariance.Sample(800f, AmmoType.Mm9, 0.9f, 0.1f, 1f);
            Assert.IsTrue(System.Math.Abs(hot - 800f) > System.Math.Abs(cold - 800f));
        }

        [Test]
        public void Zeroing_ZeroMenzildeSapmaSifir()
        {
            foreach (var z in ZeroingRules.Steps)
                Assert.AreEqual(0f, ZeroingRules.ImpactOffset(800f, AmmoType.Mm762, z, z), 0.001f);
        }

        [Test]
        public void Zeroing_YakindaYukariUzaktaAsagi()
        {
            Assert.Greater(ZeroingRules.ImpactOffset(800f, AmmoType.Mm762, 300f, 150f), -0.001f - 0.05f);
            Assert.IsTrue(ZeroingRules.ImpactOffset(800f, AmmoType.Mm762, 100f, 400f) < -0.1f);
        }

        [Test]
        public void Zeroing_AdimlarDonguVeKirpma()
        {
            Assert.AreEqual(200f, ZeroingRules.NextStep(100f), 0.001f);
            Assert.AreEqual(100f, ZeroingRules.NextStep(300f), 0.001f);
            Assert.AreEqual(300f, ZeroingRules.ClampToStep(450f), 0.001f);
        }

        [Test]
        public void Zeroing_ElevasyonYonuYukselter()
        {
            ZeroingRules.ApplyElevation(0f, 0f, 1f, 0.01f, out var x, out var y, out var z);
            Assert.Greater(y, 0f);
            Assert.AreEqual(1f, x * x + y * y + z * z, 0.001f);
        }

        [Test]
        public void SesGecikmesi_SupersonikCrackOnceThumpSonra()
        {
            Assert.IsTrue(SupersonicSoundRules.IsSupersonic(700f));
            Assert.IsTrue(!SupersonicSoundRules.IsSupersonic(300f));
            Assert.Greater(SupersonicSoundRules.CrackThumpGap(800f, AmmoType.Mm762, 500f), 0.2f);
            Assert.AreEqual(0f, SupersonicSoundRules.CrackThumpGap(800f, AmmoType.Mm762, 0f), 0.0001f);
        }

        [Test]
        public void SesGecikmesi_UzakVeSesAltiCrackYok()
        {
            Assert.IsTrue(SupersonicSoundRules.HasCrack(800f, AmmoType.Mm762, 200f, 3f));
            Assert.IsTrue(!SupersonicSoundRules.HasCrack(800f, AmmoType.Mm762, 200f, 30f));
            Assert.IsTrue(!SupersonicSoundRules.HasCrack(300f, AmmoType.Mm9, 50f, 2f));
        }

        [Test]
        public void Sekme_KritikAciUstundeYok()
        {
            Assert.IsTrue(RicochetAngleRules.CanRicochet(PenetrationMaterial.Concrete, AmmoType.Mm556, 10f));
            Assert.IsTrue(!RicochetAngleRules.CanRicochet(PenetrationMaterial.Concrete, AmmoType.Mm556, 40f));
            Assert.AreEqual(0f, RicochetAngleRules.Chance(PenetrationMaterial.Wood, AmmoType.Mm556, 5f), 0f);
        }

        [Test]
        public void Sekme_SigAciDahaCokSekerVeHizKorur()
        {
            var lo = RicochetAngleRules.Chance(PenetrationMaterial.ThinMetal, AmmoType.Mm762, 3f);
            var hi = RicochetAngleRules.Chance(PenetrationMaterial.ThinMetal, AmmoType.Mm762, 20f);
            Assert.Greater(lo, hi);
            Assert.Greater(RicochetAngleRules.SpeedKeep(PenetrationMaterial.ThinMetal, AmmoType.Mm762, 3f),
                RicochetAngleRules.SpeedKeep(PenetrationMaterial.ThinMetal, AmmoType.Mm762, 20f));
            Assert.IsTrue(RicochetAngleRules.ExitAngle(20f) < 20f);
        }
    }
}
