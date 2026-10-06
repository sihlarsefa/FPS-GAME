using NUnit.Framework;
using Project.Core.Domain;

namespace Project.Tests.EditMode
{
    public class TerrainShadingMathTests
    {
        [Test]
        public void Tiers_LowIsOff_HighIsRicher()
        {
            var low = TerrainShadingMath.GetTier(0);
            Assert.IsFalse(low.UseHarekatTerrain);
            Assert.AreEqual(0f, low.PomQuality);
            Assert.AreEqual(0f, low.CliffStrength);
            Assert.AreEqual(0f, low.WetnessScale);
            var mid = TerrainShadingMath.GetTier(1);
            var hi = TerrainShadingMath.GetTier(2);
            var ultra = TerrainShadingMath.GetTier(3);
            Assert.IsTrue(mid.UseHarekatTerrain);
            Assert.AreEqual(0f, mid.CliffStrength);
            Assert.Greater(hi.CliffStrength, 0f);
            Assert.Greater(ultra.PomQuality, hi.PomQuality);
            Assert.Greater(ultra.DetailDistance, hi.DetailDistance);
            Assert.AreEqual(TerrainShadingMath.GetTier(3).PomQuality, TerrainShadingMath.GetTier(99).PomQuality);
            Assert.IsFalse(TerrainShadingMath.GetTier(-5).UseHarekatTerrain);
        }

        [Test]
        public void SlopeWeight_FlatZero_SteepOne()
        {
            var thr = TerrainShadingMath.NormalYThreshold(35f);
            Assert.AreEqual(0.819f, thr, 0.002f);
            Assert.AreEqual(0f, TerrainShadingMath.SlopeWeight(1f, thr, 0.12f), 1e-5f);
            Assert.AreEqual(1f, TerrainShadingMath.SlopeWeight(0.3f, thr, 0.12f), 1e-5f);
            var mid = TerrainShadingMath.SlopeWeight(thr - 0.06f, thr, 0.12f);
            Assert.Greater(mid, 0.4f);
            Assert.Less(mid, 0.6f);
        }

        [Test]
        public void Wetness_RisesWithRain_DriesWithout_Clamped()
        {
            var w = 0f;
            for (var i = 0; i < 100; i++)
                w = TerrainShadingMath.StepWetness(w, 1f, 1f);
            Assert.AreEqual(1f, w, 1e-5f);
            var dried = TerrainShadingMath.StepWetness(1f, 0f, 60f);
            Assert.Less(dried, 1f);
            Assert.Greater(dried, 0.7f);
            Assert.AreEqual(0f, TerrainShadingMath.StepWetness(0f, 0f, 10f), 1e-6f);
            Assert.AreEqual(0.5f, TerrainShadingMath.StepWetness(0.5f, 0.5f, 0f), 1e-6f);
        }

        [Test]
        public void Puddles_OnlyOnFlat_AndGrowWithCoverageAndMud()
        {
            Assert.AreEqual(0f, TerrainShadingMath.PuddleMask(0.9f, 0.5f, 0.35f, 0f), 1e-5f);
            var flat = TerrainShadingMath.PuddleMask(0.9f, 1f, 0.35f, 0f);
            Assert.Greater(flat, 0.9f);
            Assert.AreEqual(0f, TerrainShadingMath.PuddleMask(0.2f, 1f, 0.35f, 0f), 1e-5f);
            Assert.Greater(TerrainShadingMath.PuddleMask(0.6f, 1f, 0.35f, 1f), TerrainShadingMath.PuddleMask(0.6f, 1f, 0.35f, 0f));
            Assert.AreEqual(0f, TerrainShadingMath.PuddleLevel(0.2f));
            Assert.AreEqual(1f, TerrainShadingMath.PuddleLevel(1f), 1e-5f);
        }

        [Test]
        public void WetLook_DarkensAndSmooths()
        {
            Assert.AreEqual(1f, TerrainShadingMath.WetAlbedoMultiplier(0f, 0.6f, 0f), 1e-5f);
            Assert.Less(TerrainShadingMath.WetAlbedoMultiplier(1f, 0.6f, 1f), 0.6f);
            Assert.Greater(TerrainShadingMath.WetSmoothness(0.3f, 0.92f, 1f, 1f), 0.9f);
            Assert.AreEqual(0.3f, TerrainShadingMath.WetSmoothness(0.3f, 0.92f, 0f, 0f), 1e-5f);
            Assert.Less(TerrainShadingMath.NormalFlatten(1f), 0.1f);
            Assert.AreEqual(1f, TerrainShadingMath.NormalFlatten(0f), 1e-5f);
        }

        [Test]
        public void Pom_StepsAndFade()
        {
            Assert.AreEqual(0, TerrainShadingMath.PomSteps(0.5f, 8f, 24f, 0f));
            var top = TerrainShadingMath.PomSteps(1f, 8f, 24f, 1f);
            var grazing = TerrainShadingMath.PomSteps(0.1f, 8f, 24f, 1f);
            Assert.AreEqual(8, top);
            Assert.Greater(grazing, top);
            Assert.AreEqual(64, TerrainShadingMath.PomSteps(0f, 8f, 200f, 1.5f));
            Assert.AreEqual(1f, TerrainShadingMath.PomFade(5f, 12f, 28f), 1e-5f);
            Assert.AreEqual(0f, TerrainShadingMath.PomFade(40f, 12f, 28f), 1e-5f);
            Assert.Greater(TerrainShadingMath.MacroFade(200f, 40f, 160f), TerrainShadingMath.MacroFade(1f, 40f, 160f));
        }

        [Test]
        public void PbrStandard_TexelDensityAndAlbedoRanges()
        {
            Assert.AreEqual(512, TerrainShadingMath.TexelDensityPxPerMeter);
            Assert.AreEqual(512, TerrainShadingMath.TextureSizeForTile(1f));
            Assert.AreEqual(2048, TerrainShadingMath.TextureSizeForTile(4f));
            Assert.AreEqual(4096, TerrainShadingMath.TextureSizeForTile(50f));
            Assert.AreEqual(2f, TerrainShadingMath.TileMetersForTexture(1024), 1e-5f);
            Assert.AreEqual(1f, TerrainShadingMath.TexelDensityRatio(1024, 2f), 1e-5f);
            Assert.AreEqual(0f, TerrainShadingMath.AlbedoLuminance(0f, 0f, 0f));
            Assert.AreEqual(255f, TerrainShadingMath.AlbedoLuminance(1f, 1f, 1f), 0.01f);
            Assert.IsTrue(TerrainShadingMath.IsAlbedoInRange(PbrMaterialClass.Mud, 0.2f, 0.17f, 0.12f));
            Assert.IsFalse(TerrainShadingMath.IsAlbedoInRange(PbrMaterialClass.Mud, 0.9f, 0.9f, 0.9f));
            Assert.IsTrue(TerrainShadingMath.IsAlbedoInRange(PbrMaterialClass.Snow, 0.9f, 0.9f, 0.92f));
            Assert.AreEqual(0.3f, TerrainShadingMath.RoughnessFromSmoothness(0.7f), 1e-5f);
        }

        [Test]
        public void Presets_MudIsWettestAndDeepest()
        {
            var mud = TerrainShadingMath.MudPreset();
            var rock = TerrainShadingMath.RockPreset();
            Assert.Greater(mud.ParallaxDepth, rock.ParallaxDepth);
            Assert.Greater(mud.WetResponse, rock.WetResponse);
            Assert.Greater(mud.BaseWet, 0f);
            Assert.Less(mud.WetDarken, rock.WetDarken);
        }
    }
}
