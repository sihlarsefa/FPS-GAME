#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;

namespace Project.Tests
{
    public class PipelineTiersTests
    {
        [Test]
        public void RenderScale_MatchesPlan()
        {
            Assert.AreEqual(0.67f, PipelineTiers.Get(0).RenderScale, 0.001f);
            Assert.AreEqual(0.77f, PipelineTiers.Get(1).RenderScale, 0.001f);
            Assert.AreEqual(0.87f, PipelineTiers.Get(2).RenderScale, 0.001f);
            Assert.AreEqual(1f, PipelineTiers.Get(3).RenderScale, 0.001f);
        }

        [Test]
        public void Stp_DisablesMsaa()
        {
            for (var i = 0; i < PipelineTiers.Count; i++)
                if (PipelineTiers.Get(i).UseStp)
                    Assert.AreEqual(1, PipelineTiers.EffectiveMsaa(PipelineTiers.Get(i)));
        }

        [Test]
        public void Occlusion_MediumAndUp_Only()
        {
            Assert.IsFalse(PipelineTiers.Get(0).GpuOcclusion);
            Assert.IsTrue(PipelineTiers.Get(1).GpuOcclusion);
            Assert.IsTrue(PipelineTiers.Get(3).GpuOcclusion);
        }

        [Test]
        public void Level_IsClamped_AndNullApplySafe()
        {
            Assert.AreEqual("Low", PipelineTiers.Get(-5).Name);
            Assert.AreEqual("Ultra", PipelineTiers.Get(99).Name);
            PipelineTiers.ApplyRuntime(null, 2);
            Assert.AreEqual("URP etkin değil", PipelineTiers.Report(null));
        }

        [Test]
        public void TextureSharpness_HighUltra_ForceAnisoAndFullResMips()
        {
            // 4K netlik: Yüksek/Ultra aniso zorlanır ve mip sınırı 0 (tam çözünürlük doku).
            Assert.IsTrue(PipelineTiers.Get(2).ForceAniso);
            Assert.IsTrue(PipelineTiers.Get(3).ForceAniso);
            Assert.AreEqual(0, PipelineTiers.Get(2).MipmapLimit);
            Assert.AreEqual(0, PipelineTiers.Get(3).MipmapLimit);
            Assert.IsFalse(PipelineTiers.Get(0).ForceAniso);
            // Akış bütçesi 4K dokulara yeter (Ultra >= 2048 MB) ve kademeyle artar.
            Assert.GreaterOrEqual(PipelineTiers.Get(3).StreamingMipBudgetMb, 2048f);
            for (var i = 1; i < PipelineTiers.Count; i++)
                Assert.GreaterOrEqual(PipelineTiers.Get(i).StreamingMipBudgetMb, PipelineTiers.Get(i - 1).StreamingMipBudgetMb);
        }

        [Test]
        public void Cascades_AscendingSplits()
        {
            for (var i = 0; i < PipelineTiers.Count; i++)
            {
                var s = PipelineTiers.Get(i).CascadeSplits;
                Assert.IsTrue(s.x < s.y && s.y < s.z);
            }
        }
    }
}
#endif
