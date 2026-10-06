using NUnit.Framework;
using Project.Infrastructure.Audio.Weapons;

namespace Project.Tests.EditMode
{
    public sealed class WeaponShotLayerTests
    {
        [Test]
        public void Bucket_SplitsByDistance()
        {
            Assert.AreEqual(TailDistance.Near, WeaponShotRules.Bucket(10f));
            Assert.AreEqual(TailDistance.Mid, WeaponShotRules.Bucket(100f));
            Assert.AreEqual(TailDistance.Far, WeaponShotRules.Bucket(400f));
        }

        [Test]
        public void Tail_FarIsDarkerAndLonger_IndoorShorter()
        {
            Assert.IsTrue(WeaponShotRules.TailCutoffHz(TailDistance.Far, false) < WeaponShotRules.TailCutoffHz(TailDistance.Near, false));
            Assert.IsTrue(WeaponShotRules.TailSeconds(TailDistance.Far, false, ShotClass.Rifle) > WeaponShotRules.TailSeconds(TailDistance.Near, false, ShotClass.Rifle));
            Assert.IsTrue(WeaponShotRules.TailSeconds(TailDistance.Mid, true, ShotClass.Rifle) < WeaponShotRules.TailSeconds(TailDistance.Mid, false, ShotClass.Rifle));
        }

        [Test]
        public void ReloadChain_EmptyAddsBoltAndIsOrdered()
        {
            var tac = WeaponShotRules.ReloadChain(true);
            var empty = WeaponShotRules.ReloadChain(false);
            Assert.Greater(empty.Length, tac.Length);
            for (var i = 1; i < empty.Length; i++)
                Assert.IsTrue(empty[i].Time >= empty[i - 1].Time);
        }

        [Test]
        public void LastRound_Rule()
        {
            Assert.IsTrue(WeaponShotRules.IsLastRound(0));
            Assert.IsTrue(!WeaponShotRules.IsLastRound(5));
        }

        [Test]
        public void Synth_ProducesFiniteDistinctBuffers()
        {
            var normal = WeaponShotSynth.RenderMech(0, false);
            var last = WeaponShotSynth.RenderMech(0, true);
            Assert.Greater(last.Length, normal.Length);
            var near = WeaponShotSynth.RenderTail(ShotClass.Rifle, TailDistance.Near, false, 0);
            var far = WeaponShotSynth.RenderTail(ShotClass.Rifle, TailDistance.Far, false, 0);
            Assert.Greater(far.Length, near.Length);
            var shot = WeaponShotSynth.RenderShot(ShotClass.Rifle, 80f, true, false, 1);
            var peak = 0f;
            for (var i = 0; i < shot.Length; i++)
            {
                Assert.IsTrue(!float.IsNaN(shot[i]) && !float.IsInfinity(shot[i]));
                if (System.Math.Abs(shot[i]) > peak) peak = System.Math.Abs(shot[i]);
            }
            Assert.Greater(peak, 0.1f);
            Assert.IsTrue(WeaponShotSynth.RenderReloadChain(false).Length > 0);
        }
    }
}
