#if UNITY_EDITOR
using NUnit.Framework;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    public class MaterialMathTests
    {
        [Test]
        public void TexelDensity_ScalesTilingWithDensityAndTextureSize()
        {
            Assert.AreEqual(1f, MaterialMath.TilingForTexelDensity(512f, 1f, 512), 0.0001f);
            Assert.AreEqual(2f, MaterialMath.TilingForTexelDensity(1024f, 1f, 512), 0.0001f);
            Assert.AreEqual(1f, MaterialMath.TilingForTexelDensity(0f, 1f, 512), 0.0001f);
        }

        [Test]
        public void Macro_TilingAndScaleBounded()
        {
            Assert.Less(MaterialMath.MacroTiling(20f, 1f), MaterialMath.MacroTiling(5f, 1f));
            Assert.AreEqual(1f, MaterialMath.MacroAlbedoScale(0f), 0.0001f);
            Assert.AreEqual(2f, MaterialMath.MacroAlbedoScale(5f), 0.0001f);
        }

        [Test]
        public void Spec_NewFieldsDefaultOff()
        {
            var s = new MaterialSpec();
            Assert.IsFalse(s.Triplanar);
            Assert.IsFalse(s.HasDetail);
            Assert.AreEqual(0f, s.MacroVariation);
            Assert.AreEqual(0f, s.TexelDensity);
        }

        [Test]
        public void ProceduralMask_IsConsistentOrm()
        {
            var px = ProceduralPbr.Generate(PbrSurface.Concrete, 32, 1);
            Assert.IsTrue(MaterialMath.IsMaskConsistent(px.Mask));
        }
    }
}
#endif
