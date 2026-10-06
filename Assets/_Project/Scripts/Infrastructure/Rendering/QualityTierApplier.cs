using System;
using Project.Infrastructure.Rendering.Features;
using Project.Infrastructure.Vfx;
using Project.Infrastructure.World;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Kalite kademesini (0 Düşük … 3 Ultra) uygulayan TEK nokta. PostProcessing.ApplyQuality sonunda çağrılır
    /// (başlangıç + ayar değişimi). Her adım ayrı try/catch içindedir; eksik gölgelendirici/varlık davranışı değiştirmez.
    /// </summary>
    public static class QualityTierApplier
    {
        private static bool _userFog = true;

        public static int LastTier { get; private set; } = -1;

        /// <summary>Kullanıcı hacimsel sis tercihi (ayar menüsü); kademe yeniden uygulanır.</summary>
        public static void SetVolumetricUser(bool enabled)
        {
            _userFog = enabled;
            Atmosphere.SetVolumetricUser(enabled);
            if (LastTier >= 0)
                Step("VolumetricFog", () => VolumetricFog.Install(LastTier, _userFog));
            Atmosphere.RefreshFog();
        }

        public static void Apply(int tier, Camera cam)
        {
            tier = PerformanceProfile.Clamp(tier);
            LastTier = tier;
            if (cam == null)
                cam = Camera.main;

            Step("Pipeline", () => PerformanceProfile.ApplyPipeline(tier));
            Step("SSAO", () => SsaoTuner.Apply(tier));
            Step("RendererFeatures", () => HarekatRendererFeatures.Install(tier));
            Step("VolumetricFog", () => VolumetricFog.Install(cam, tier, _userFog));
            Step("Atmosphere", () => { Atmosphere.QualityLevel = tier; Atmosphere.RefreshFog(); });
            Step("TextureStreaming", () => TextureStreamingSetup.Install(cam, tier));
            // 4K dokular: VRAM kısıtlı HlodMath bütçesi, PipelineTiers tablosundaki kademe bütçesinin altına inmesin.
            Step("MipBudget4K", () => QualitySettings.streamingMipmapsMemoryBudget = Mathf.Max(QualitySettings.streamingMipmapsMemoryBudget, PipelineTiers.Get(tier).StreamingMipBudgetMb));
            Step("Hlod", () => HlodProxy.SetTier(tier));
            Step("Grass", () => GrassSystem.SetTier(tier));
            Step("Wind", () => WindSystem.SetTier(tier));
            Step("DecalScatter", () => DecalScatter.SetTier(tier));
            Step("BuildingWeathering", () => Project.Infrastructure.World.BuildingWeathering.SetTier(tier));
            Step("GpuVfx", () => GpuVfx.Tier = tier);
            Step("Terrain", () =>
            {
                var terrains = Terrain.activeTerrains;
                if (terrains == null)
                    return;
                for (var i = 0; i < terrains.Length; i++)
                {
                    var t = terrains[i];
                    if (t == null)
                        continue;
                    TerrainShaderBinder.Install(t, tier);
                    // ApplyTier detay yoğunluğunu yazar; GrassSystem etkinken 0 kalmalı.
                    if (!GrassSystem.Active)
                        TerrainPaintRules.ApplyTier(t, tier);
                    // ≥800 m: uzak zemin düşük çözünürlüklü basemap'e erken düşüp bulanıklaşmasın (kademe tablosu 160-520 veriyor).
                    t.basemapDistance = Mathf.Max(800f, t.basemapDistance);
                }
            });
            if (cam != null)
                Step("PlanarReflection", () => PlanarReflection.Install(cam, tier));
        }

        private static void Step(string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogWarning("[QualityTierApplier] " + name + " uygulanamadı: " + e.Message); }
        }
    }
}
