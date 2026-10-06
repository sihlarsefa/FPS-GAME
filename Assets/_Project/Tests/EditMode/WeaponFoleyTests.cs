#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Infrastructure.Audio.Foley;

namespace Project.Tests.EditMode
{
    public sealed class WeaponFoleyTests
    {
        [Test]
        public void EveryCatalogWeapon_HasExplicitProfile()
        {
            foreach (var w in WeaponCatalog.All)
            {
                Assert.IsTrue(WeaponSoundProfiles.TryGet(w.WeaponId, out var p), w.WeaponId);
                Assert.IsTrue(p.ThumpGain > 0f && p.TailMaxSeconds > 0.2f, w.WeaponId);
                Assert.IsFalse(string.IsNullOrEmpty(p.CoreSet) || string.IsNullOrEmpty(p.TailSet) || string.IsNullOrEmpty(p.MechSet));
            }
        }

        [Test]
        public void Profile_CaliberMatchesCategory()
        {
            Assert.AreEqual(WeaponCaliber.Pistol9, WeaponSoundProfiles.Get(WeaponIds.Sar9).Caliber);
            Assert.AreEqual(WeaponCaliber.Sniper762, WeaponSoundProfiles.Get(WeaponIds.Jng90).Caliber);
            Assert.AreEqual(WeaponCaliber.Lmg762, WeaponSoundProfiles.Get(WeaponIds.Mg3).Caliber);
            Assert.AreEqual(WeaponCaliber.Shotgun12, WeaponSoundProfiles.Get(WeaponIds.Escort).Caliber);
            Assert.IsFalse(WeaponSoundProfiles.Get(WeaponIds.Jng90).EjectsOnFire);
            Assert.IsNotNull(WeaponSoundProfiles.Get("bilinmeyen", WeaponCategory.Smg));
        }

        [Test]
        public void TailCut_FollowsFireRate()
        {
            var mg3 = WeaponSoundProfiles.Get(WeaponIds.Mg3);
            var fast = mg3.TailCutSeconds(0.05f);
            var slow = mg3.TailCutSeconds(2f);
            Assert.IsTrue(fast < slow);
            Assert.IsTrue(fast >= 0.12f);
            Assert.AreEqual(mg3.TailMaxSeconds, slow, 0.001f);
        }

        [Test]
        public void ReloadPlan_TacticalVsEmpty()
        {
            var tactical = ReloadFoleyPlanner.Plan(WeaponCategory.AssaultRifle, false, 2.5f);
            var empty = ReloadFoleyPlanner.Plan(WeaponCategory.AssaultRifle, true, 2.5f);
            Assert.AreEqual(empty.Length, tactical.Length + 1);
            Assert.AreEqual(FoleyStep.ChargingHandle, empty[empty.Length - 1].Step);
            for (var i = 1; i < empty.Length; i++)
                Assert.IsTrue(empty[i].Fraction > empty[i - 1].Fraction);
            Assert.AreEqual(FoleyStep.MagOut, tactical[1].Step);
            var sn = ReloadFoleyPlanner.Plan(WeaponCategory.Sniper, true, 3f);
            Assert.AreEqual(FoleyStep.Bolt, sn[sn.Length - 1].Step);
        }

        [Test]
        public void ReloadPlan_ShotgunInsertsShells()
        {
            var p = ReloadFoleyPlanner.Plan(WeaponCategory.Shotgun, true, 4f);
            Assert.IsTrue(p.Length >= 4);
            Assert.AreEqual(FoleyStep.ShellInsert, p[0].Step);
            Assert.AreEqual(FoleyStep.Bolt, p[p.Length - 1].Step);
            for (var i = 0; i < p.Length; i++)
                Assert.IsTrue(p[i].Fraction > 0f && p[i].Fraction <= 1f);
        }

        [Test]
        public void Tracker_FiresEachStepOnce_AndCancels()
        {
            var t = new ReloadFoleyTracker();
            t.Begin(ReloadFoleyPlanner.Plan(WeaponCategory.Pistol, true, 2f));
            var due = new List<FoleyStep>();
            t.Advance(0.2f, due);
            Assert.AreEqual(2, due.Count);
            due.Clear();
            t.Advance(0.2f, due);
            Assert.AreEqual(0, due.Count);
            t.Advance(1f, due);
            Assert.AreEqual(3, due.Count);
            Assert.IsFalse(t.Active);
            t.Begin(ReloadFoleyPlanner.Plan(WeaponCategory.Pistol, false, 2f));
            t.Cancel();
            due.Clear();
            t.Advance(1f, due);
            Assert.AreEqual(0, due.Count);
        }

        [Test]
        public void Naming_Convention()
        {
            Assert.AreEqual("Audio/Weapons/ar_mpt55", FoleyNaming.WeaponFolder(WeaponIds.Mpt55));
            Assert.AreEqual("Audio/Weapons/_class/rifle556", FoleyNaming.ClassFolderPath("rifle556"));
            Assert.AreEqual("tail_outdoor", FoleyNaming.LayerOfClipName("tail_outdoor_2"));
            Assert.AreEqual("bang", FoleyNaming.LayerOfClipName("Bang_01"));
            Assert.AreEqual("magout", FoleyNaming.LayerOfClipName("magout"));
            Assert.AreEqual("magin", FoleyNaming.StepLayer(FoleyStep.MagIn));
            Assert.AreEqual("supp", FoleyNaming.FireLayerName(FireLayer.Suppressed));
        }

        [Test]
        public void EveryStep_HasLayerName_AndInfo()
        {
            foreach (FoleyStep s in System.Enum.GetValues(typeof(FoleyStep)))
            {
                if (s == FoleyStep.None) continue;
                Assert.IsNotNull(FoleyNaming.StepLayer(s), s.ToString());
                Assert.IsTrue(FoleyStepInfo.For(s).Volume > 0f);
            }
        }

        [Test]
        public void Synth_RendersAllSteps_Deterministic()
        {
            foreach (FoleyStep s in System.Enum.GetValues(typeof(FoleyStep)))
            {
                if (s == FoleyStep.None) continue;
                var a = FoleySynth.Render(s, WeaponCaliber.Rifle556, 1);
                var b = FoleySynth.Render(s, WeaponCaliber.Rifle556, 1);
                Assert.IsNotNull(a, s.ToString());
                Assert.IsTrue(a.Length > 1000);
                Assert.AreEqual(a.Length, b.Length);
                Assert.AreEqual(a[a.Length / 3], b[b.Length / 3], 0f);
                var peak = 0f;
                for (var i = 0; i < a.Length; i++)
                {
                    Assert.IsFalse(float.IsNaN(a[i]));
                    if (System.Math.Abs(a[i]) > peak) peak = System.Math.Abs(a[i]);
                }

                Assert.IsTrue(peak > 0.1f && peak <= 1f, s.ToString());
            }
        }

        [Test]
        public void GearRattle_SprintTriggers_WalkQuiet()
        {
            var m = new GearRattleModel();
            var hits = 0;
            var plate = false;
            for (var i = 0; i < 120; i++)
                if (m.Tick(5.5f, 0.016f, true, out var inten, out var p))
                {
                    hits++;
                    plate |= p;
                    Assert.IsTrue(inten >= 0.25f && inten <= 1f);
                }

            Assert.IsTrue(hits >= 2);
            m.Reset();
            for (var i = 0; i < 120; i++)
                Assert.IsFalse(m.Tick(0.5f, 0.016f, true, out _, out _));
            Assert.IsFalse(m.Tick(6f, 0.016f, false, out _, out _));
        }

        [Test]
        public void Landing_Intensity()
        {
            Assert.IsFalse(BodyFoleyRules.LandingAudible(1f));
            Assert.IsTrue(BodyFoleyRules.LandingIntensity(14f) > BodyFoleyRules.LandingIntensity(4f));
            Assert.AreEqual(1f, BodyFoleyRules.LandingIntensity(40f), 0.001f);
        }
    }
}
#endif
