#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Catalogs;
using Project.Application.Dialogue;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Dialogue;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class AudioIntegrationHooksTests
    {
        [Test]
        public void CaliberMapping_CoversAllClasses()
        {
            Assert.AreEqual(CaliberClass.Pistol, AcousticsBridge.CaliberFor(WeaponCaliber.Pistol9));
            Assert.AreEqual(CaliberClass.Pistol, AcousticsBridge.CaliberFor(WeaponCaliber.Smg9));
            Assert.AreEqual(CaliberClass.Rifle, AcousticsBridge.CaliberFor(WeaponCaliber.Rifle556));
            Assert.AreEqual(CaliberClass.Rifle, AcousticsBridge.CaliberFor(WeaponCaliber.Shotgun12));
            Assert.AreEqual(CaliberClass.MachineGun, AcousticsBridge.CaliberFor(WeaponCaliber.Lmg762));
            Assert.AreEqual(CaliberClass.Sniper, AcousticsBridge.CaliberFor(WeaponCaliber.Sniper762));
        }

        [Test]
        public void DialogueStress_MapsToVoiceFolder()
        {
            Assert.AreEqual(VoiceStress.Sakin, DialogueClipLibrary.ToVoiceStress(DialogueStress.Calm));
            Assert.AreEqual(VoiceStress.Catisma, DialogueClipLibrary.ToVoiceStress(DialogueStress.Combat));
            Assert.AreEqual(VoiceStress.Panik, DialogueClipLibrary.ToVoiceStress(DialogueStress.Panic));
        }

        [Test]
        public void ReloadFoleyPlan_MatchesViewmodelTimeline()
        {
            // Pistol ve tüfek mag-out/in kesirleri görünüm modeliyle (ViewmodelPoseTimeline) aynı olmalı.
            Assert.AreEqual(0.15f, Frac(Project.Core.Domain.WeaponCategory.Pistol, Project.Infrastructure.Audio.Foley.FoleyStep.MagOut), 0.001f);
            Assert.AreEqual(0.66f, Frac(Project.Core.Domain.WeaponCategory.AssaultRifle, Project.Infrastructure.Audio.Foley.FoleyStep.MagIn), 0.001f);
        }

        private static float Frac(Project.Core.Domain.WeaponCategory c, Project.Infrastructure.Audio.Foley.FoleyStep step)
        {
            var plan = Project.Infrastructure.Audio.Foley.ReloadFoleyPlanner.Plan(c, false, 2.5f);
            for (var i = 0; i < plan.Length; i++)
                if (plan[i].Step == step)
                    return plan[i].Fraction;
            return -1f;
        }
    }
}
#endif
