#if UNITY_EDITOR
using NUnit.Framework;
using Project.Application.Loadouts;
using Project.Core.Domain;
using Project.Core.Domain.Attachments;

namespace Project.Tests.EditMode
{
    public class AttachmentTests
    {
        [Test]
        public void Suppressor_NotOnShotgun()
        {
            Assert.IsTrue(AttachmentRules.CanAttach(WeaponCategory.AssaultRifle, AttachmentKind.Suppressor));
            Assert.IsTrue(!AttachmentRules.CanAttach(WeaponCategory.Shotgun, AttachmentKind.Suppressor));
        }

        [Test]
        public void Scope8x_OnlySniperDmr()
        {
            Assert.IsTrue(AttachmentRules.CanAttach(WeaponCategory.Sniper, AttachmentKind.Scope8x));
            Assert.IsTrue(!AttachmentRules.CanAttach(WeaponCategory.Smg, AttachmentKind.Scope8x));
        }

        [Test]
        public void Melee_AcceptsNothing()
        {
            Assert.IsTrue(!AttachmentRules.CanAttach(WeaponCategory.Melee, AttachmentKind.RedDot));
        }

        [Test]
        public void SameSlotReplaces()
        {
            var w = new WeaponAttachments(WeaponCategory.Dmr);
            Assert.IsTrue(w.TryAttach(AttachmentKind.Scope4x));
            Assert.IsTrue(w.TryAttach(AttachmentKind.Scope8x));
            Assert.AreEqual(AttachmentKind.Scope8x, w.Get(AttachmentSlot.Optic));
        }

        [Test]
        public void ForeGrip_Reduces15PercentRecoil()
        {
            var w = new WeaponAttachments(WeaponCategory.AssaultRifle);
            w.TryAttach(AttachmentKind.ForeGrip);
            Assert.AreEqual(0.85f, AttachmentEffects.Compute(w).RecoilMultiplier, 0.0001f);
        }

        [Test]
        public void Suppressor_ReducesSoundAndFlash()
        {
            var w = new WeaponAttachments(WeaponCategory.Smg);
            w.TryAttach(AttachmentKind.Suppressor);
            var e = AttachmentEffects.Compute(w);
            Assert.Less(e.SoundMultiplier, 0.5f);
            Assert.Less(e.FlashMultiplier, 0.5f);
        }

        [Test]
        public void ExtendedMag_IncreasesCapacity()
        {
            var w = new WeaponAttachments(WeaponCategory.AssaultRifle);
            w.TryAttach(AttachmentKind.ExtendedMag);
            Assert.AreEqual(45, AttachmentEffects.Compute(w).ApplyMagazine(30));
        }

        [Test]
        public void Loadout_RejectsInvalidAndTracksEffects()
        {
            var l = new WeaponAttachmentLoadout();
            Assert.IsTrue(!l.TryAttach("pompali", WeaponCategory.Shotgun, AttachmentKind.Suppressor));
            Assert.IsTrue(l.TryAttach("mpt76", WeaponCategory.AssaultRifle, AttachmentKind.Scope4x));
            Assert.AreEqual(4f, l.Effects("mpt76").Zoom, 0.0001f);
            Assert.AreEqual(1f, l.Effects("yok").Zoom, 0.0001f);
        }

        [Test]
        public void Combined_Multiplies()
        {
            var w = new WeaponAttachments(WeaponCategory.AssaultRifle);
            w.TryAttach(AttachmentKind.Suppressor);
            w.TryAttach(AttachmentKind.ForeGrip);
            w.TryAttach(AttachmentKind.RedDot);
            var e = AttachmentEffects.Compute(w);
            Assert.AreEqual(0.85f, e.RecoilMultiplier, 0.0001f);
            Assert.AreEqual(0.3f, e.SoundMultiplier, 0.0001f);
            Assert.AreEqual(1.25f, e.Zoom, 0.0001f);
        }
    }
}
#endif
