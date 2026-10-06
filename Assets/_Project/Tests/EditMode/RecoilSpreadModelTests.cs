using NUnit.Framework;
using Project.Application.Combat.Feel;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class RecoilSpreadModelTests
    {
        [Test]
        public void Bloom_AtisArtirir_GecikmedenSonraAzalir()
        {
            var m = new SpreadBloomModel(BloomTuning.ForCategory(WeaponCategory.AssaultRifle));
            m.OnShot(); m.OnShot(); m.OnShot();
            var b = m.BloomDeg;
            Assert.Greater(b, 0.3f);
            m.Step(0.05f);
            Assert.AreEqual(b, m.BloomDeg, 0.0001f);
            m.Step(1f);
            Assert.Less(m.BloomDeg, b);
            m.Step(5f);
            Assert.AreEqual(0f, m.BloomDeg, 0.0001f);
        }

        [Test]
        public void Bloom_TavandanFazlaOlmaz()
        {
            var t = BloomTuning.ForCategory(WeaponCategory.Smg);
            var m = new SpreadBloomModel(t);
            for (var i = 0; i < 100; i++) m.OnShot();
            Assert.IsTrue(m.BloomDeg <= t.MaxBloomDeg + 0.0001f);
        }

        [Test]
        public void IlkAtis_DinlenmisTutarken_DahaIsabetli()
        {
            var m = new SpreadBloomModel(BloomTuning.ForCategory(WeaponCategory.AssaultRifle));
            var first = m.CurrentSpreadDeg(1f, 1f);
            m.OnShot();
            m.OnShot();
            var later = m.CurrentSpreadDeg(1f, 1f);
            Assert.Less(first, later);
            Assert.Less(first, m.Tuning.BaseSpreadDeg);
        }

        [Test]
        public void Durus_KosuAyaktanKotu_YatisEnIyi()
        {
            Assert.Greater(StanceSpreadRules.SpreadMultiplier(ShooterStance.Sprinting), StanceSpreadRules.SpreadMultiplier(ShooterStance.Standing));
            Assert.Less(StanceSpreadRules.SpreadMultiplier(ShooterStance.Crouching), 1f);
            Assert.Less(StanceSpreadRules.SpreadMultiplier(ShooterStance.Prone), StanceSpreadRules.SpreadMultiplier(ShooterStance.Crouching));
        }

        [Test]
        public void Ads_HareketCezasiniSonumler()
        {
            var hip = StanceSpreadRules.EffectiveSpread(ShooterStance.Walking, 3f, 0f);
            var ads = StanceSpreadRules.EffectiveSpread(ShooterStance.Walking, 3f, 1f);
            Assert.Less(ads, hip);
            Assert.Greater(ads, 1f);
        }

        [Test]
        public void Resolve_OncelikSirasi()
        {
            Assert.AreEqual(ShooterStance.Airborne, StanceSpreadRules.Resolve(false, true, false, false, false, 5f));
            Assert.AreEqual(ShooterStance.Sprinting, StanceSpreadRules.Resolve(true, true, false, false, false, 5f));
            Assert.AreEqual(ShooterStance.Standing, StanceSpreadRules.Resolve(true, false, false, false, false, 0f));
            Assert.AreEqual(ShooterStance.Walking, StanceSpreadRules.Resolve(true, false, false, false, false, 2f));
        }

        [Test]
        public void AdsOrani_NisanciEnDar()
        {
            Assert.Less(StanceSpreadRules.AdsSpreadRatio(WeaponCategory.Sniper), StanceSpreadRules.AdsSpreadRatio(WeaponCategory.AssaultRifle));
            Assert.AreEqual(1f, StanceSpreadRules.AdsRatioAt(WeaponCategory.Smg, 0f), 0.0001f);
        }

        [Test]
        public void Tepme_AyniTohumAyniDesen()
        {
            var p = WeaponRecoilProfile.For("ak47", WeaponCategory.AssaultRifle);
            var a1 = new float[10]; var y1 = new float[10];
            var a2 = new float[10]; var y2 = new float[10];
            RecoilSession.PredictedPath(p, 1f, 1f, 10, a1, y1);
            RecoilSession.PredictedPath(p, 1f, 1f, 10, a2, y2);
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(a1[i], a2[i], 0.00001f);
                Assert.AreEqual(y1[i], y2[i], 0.00001f);
            }
        }

        [Test]
        public void Tepme_FarkliSilahFarkliDesen()
        {
            var a = new float[12]; var ya = new float[12];
            var b = new float[12]; var yb = new float[12];
            RecoilSession.PredictedPath(WeaponRecoilProfile.For("ak47", WeaponCategory.AssaultRifle), 1f, 1f, 12, a, ya);
            RecoilSession.PredictedPath(WeaponRecoilProfile.For("mpt76", WeaponCategory.AssaultRifle), 1f, 1f, 12, b, yb);
            var diff = 0f;
            for (var i = 0; i < 12; i++) diff += System.Math.Abs(ya[i] - yb[i]);
            Assert.Greater(diff, 0.01f);
        }

        [Test]
        public void Tepme_ComelmeVeAdsKucultur()
        {
            var p = WeaponRecoilProfile.For("x", WeaponCategory.AssaultRifle);
            var s1 = new RecoilSession(p);
            s1.OnShot(1f, 1f, 0.5f, ShooterStance.Standing, 0f, out var stand, out _);
            var s2 = new RecoilSession(p);
            s2.OnShot(1f, 1f, 0.5f, ShooterStance.Crouching, 1f, out var crouchAds, out _);
            Assert.Less(crouchAds, stand);
        }

        [Test]
        public void Toparlanma_GecikmeSonrasiBaslar()
        {
            var s = new RecoilSession(WeaponRecoilProfile.For("x", WeaponCategory.AssaultRifle));
            s.OnShot(1f, 1f, 0.5f, ShooterStance.Standing, 0f, out _, out _);
            var acc = s.AccumulatedPitch;
            s.Step(0.02f, 1f, ShooterStance.Standing, out var back0, out _);
            Assert.AreEqual(0f, back0, 0.0001f);
            s.Step(0.5f, 1f, ShooterStance.Standing, out var back, out _);
            Assert.Greater(back, 0f);
            Assert.Less(s.AccumulatedPitch, acc);
        }

        [Test]
        public void Seri_SessizlikteSifirlanir()
        {
            var s = new RecoilSession(WeaponRecoilProfile.For("x", WeaponCategory.AssaultRifle));
            s.OnShot(1f, 1f, 0.5f, ShooterStance.Standing, 0f, out _, out _);
            s.OnShot(1f, 1f, 0.5f, ShooterStance.Standing, 0f, out _, out _);
            Assert.AreEqual(2, s.ShotIndex);
            s.Step(2f, 1f, ShooterStance.Standing, out _, out _);
            s.OnShot(1f, 1f, 0.5f, ShooterStance.Standing, 0f, out _, out _);
            Assert.AreEqual(1, s.ShotIndex);
        }

        [Test]
        public void Telafi_Skoru()
        {
            Assert.AreEqual(1f, RecoilSession.CompensationScore(10f, 10f), 0.0001f);
            Assert.AreEqual(0.5f, RecoilSession.CompensationScore(10f, 5f), 0.0001f);
            Assert.AreEqual(0f, RecoilSession.CompensationScore(10f, 0f), 0.0001f);
        }
    }
}
