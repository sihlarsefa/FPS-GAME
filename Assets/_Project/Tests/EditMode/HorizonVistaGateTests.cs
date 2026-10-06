using NUnit.Framework;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class HorizonVistaGateTests
    {
        [Test]
        public void FirstApply_AlwaysPasses()
        {
            Assert.IsTrue(HorizonVista.ShouldApplyParallax(false, Vector3.zero, Vector3.zero, 0.5f));
        }

        [Test]
        public void SmallMove_IsGated_LargeMovePasses()
        {
            Assert.IsFalse(HorizonVista.ShouldApplyParallax(true, new Vector3(0.3f, 50f, 0.3f), Vector3.zero, 0.5f));
            Assert.IsTrue(HorizonVista.ShouldApplyParallax(true, new Vector3(0.6f, 0f, 0f), Vector3.zero, 0.5f));
        }

        [Test]
        public void GatedError_StaysInvisible()
        {
            var worst = 0f;
            foreach (var l in HorizonVistaMath.Layers) worst = Mathf.Max(worst, l.Lag * HorizonVista.ParallaxMoveGate);
            Assert.Less(worst, 0.05f);
        }
    }
}
