#if UNITY_EDITOR // Unity EditMode koşucusunda çalışır (Infrastructure/Presentation referansı gerekir)
using NUnit.Framework;
using Project.Infrastructure;
using Project.Infrastructure.Rendering;

namespace Project.Tests.EditMode
{
    [TestFixture]
    public sealed class PerformanceProfileTests
    {
        [Test]
        public void Distances_GrowWithQuality()
        {
            for (var l = 0; l < 3; l++)
            {
                Assert.Less(PerformanceProfile.SmallPropCullDistance(l), PerformanceProfile.SmallPropCullDistance(l + 1));
                Assert.Less(PerformanceProfile.BotCullDistance(l), PerformanceProfile.BotCullDistance(l + 1));
                Assert.Less(PerformanceProfile.TreeDrawDistance(l), PerformanceProfile.TreeDrawDistance(l + 1));
            }
        }

        [Test]
        public void LevelIsClamped()
        {
            Assert.AreEqual(PerformanceProfile.TreeDrawDistance(0), PerformanceProfile.TreeDrawDistance(-5));
            Assert.AreEqual(PerformanceProfile.TreeDrawDistance(3), PerformanceProfile.TreeDrawDistance(99));
        }

        [Test]
        public void LayerCullDistances_OnlySmallLayersSet()
        {
            var d = new float[32];
            PerformanceProfile.FillLayerCullDistances(1, d);
            Assert.Greater(d[GameLayers.Loot], 0f);
            Assert.Greater(d[GameLayers.Bot], 0f);
            Assert.AreEqual(0f, d[GameLayers.Default]);
            Assert.AreEqual(0f, d[GameLayers.Viewmodel]);
        }

        [Test]
        public void FillLayerCullDistances_ShortArrayIsIgnored()
        {
            Assert.DoesNotThrow(() => PerformanceProfile.FillLayerCullDistances(1, new float[4]));
        }
    }
}
#endif
