#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Characters;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public sealed class BattleWearTests
    {
        [Test]
        public void Compute_StartsAtFifteenPercentAndCapsAtOne()
        {
            Assert.AreEqual(0.15f, WearRules.Compute(0f, 0f, 0), 1e-5f);
            Assert.AreEqual(0.35f, WearRules.Compute(0f, 0f, 1), 1e-5f);
            Assert.AreEqual(1f, WearRules.Compute(1f, 5000f, 9), 1e-5f);
        }

        [Test]
        public void Compute_RisesWithDamageAndTime()
        {
            Assert.Greater(WearRules.Compute(0.5f, 0f, 0), WearRules.Compute(0f, 0f, 0));
            Assert.Greater(WearRules.Compute(0f, 600f, 0), WearRules.Compute(0f, 0f, 0));
        }

        [Test]
        public void Quantize_SnapsToFourLevels()
        {
            Assert.AreEqual(0f, WearRules.Quantize(0.1f));
            Assert.AreEqual(0.33f, WearRules.Quantize(0.35f), 1e-5f);
            Assert.AreEqual(0.66f, WearRules.Quantize(0.6f), 1e-5f);
            Assert.AreEqual(1f, WearRules.Quantize(2f));
            Assert.IsFalse(WearRules.NeedsRebind(0.30f, 0.40f));
            Assert.IsTrue(WearRules.NeedsRebind(0.1f, 0.5f));
        }

        [Test]
        public void FaceVariant_IsDeterministicAndInRange()
        {
            for (var s = -50; s < 200; s++)
            {
                var v = WearRules.FaceVariant(s);
                Assert.That(v, Is.InRange(0, WearRules.Variants - 1));
                Assert.AreEqual(v, WearRules.FaceVariant(s));
            }
        }

        [Test]
        public void FaceWear_ZeroLevelIsCleanWhite()
        {
            var px = CharacterTextureGen.FaceWearPixels(32, 0f, 0, new Color(0.8f, 0.6f, 0.5f));
            foreach (var c in px)
                Assert.AreEqual(255, c.r);
        }

        [Test]
        public void FaceWear_HighLevelDarkensSomePixelsAndIsDeterministic()
        {
            var skin = new Color(0.8f, 0.6f, 0.5f);
            var a = CharacterTextureGen.FaceWearPixels(64, 1f, 2, skin);
            var b = CharacterTextureGen.FaceWearPixels(64, 1f, 2, skin);
            var dark = 0;
            for (var i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].r, b[i].r);
                if (a[i].r < 200) dark++;
            }

            Assert.Greater(dark, 20);
        }

        [Test]
        public void FaceWear_VariantsDiffer()
        {
            var skin = new Color(0.8f, 0.6f, 0.5f);
            var a = CharacterTextureGen.FaceWearPixels(64, 1f, 0, skin);
            var b = CharacterTextureGen.FaceWearPixels(64, 1f, 3, skin);
            var diff = 0;
            for (var i = 0; i < a.Length; i++) if (a[i].r != b[i].r) diff++;
            Assert.Greater(diff, 10);
        }

        [Test]
        public void SweatSheen_IsForeheadBandOnly()
        {
            Assert.AreEqual(0f, CharacterTextureGen.SweatSheen(0.05f), 1e-5f);
            Assert.AreEqual(1f, CharacterTextureGen.SweatSheen(0.155f), 1e-5f);
            Assert.AreEqual(0f, CharacterTextureGen.SweatSheen(0.2f), 1e-5f);
        }

        [Test]
        public void UniformWear_DarkensAndDesaturates()
        {
            var src = new Color32[64 * 64];
            for (var i = 0; i < src.Length; i++) src[i] = new Color32(60, 120, 40, 255);
            var same = CharacterTextureGen.UniformWearPixels(src, 0f, 5);
            Assert.AreEqual(60, same[10].r);
            var worn = CharacterTextureGen.UniformWearPixels(src, 1f, 5);
            long before = 0, after = 0;
            for (var i = 0; i < src.Length; i++) { before += src[i].g; after += worn[i].g; }
            Assert.Less(after, before);
        }

        [Test]
        public void GearWear_ScratchesLightenAboveBase()
        {
            var px = CharacterTextureGen.GearWearPixels(64, 1f, 7, true, false);
            var baseV = Mathf.RoundToInt(CharacterTextureGen.GearScratchBase * 255f);
            var lighter = 0;
            foreach (var c in px) if (c.r > baseV + 5) lighter++;
            Assert.Greater(lighter, 10);
        }
    }
}
#endif
