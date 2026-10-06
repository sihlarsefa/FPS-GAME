using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// S6: Mipmap doku akışı + occlusion culling kademe kurulumu. Tek giriş noktası: Install(camera, tier).
    /// Doku başına "Streaming Mipmaps" içe aktarma ayarı gerekir (Editor: HAREKÂT/Optimizasyon/Doku Akışını Aç).
    /// </summary>
    public static class TextureStreamingSetup
    {
        public static int LastBudgetMb { get; private set; }

        public static void Install(Camera camera, int tier)
        {
            tier = HlodMath.ClampTier(tier);
            ApplyQuality(tier);
            if (camera != null)
                camera.useOcclusionCulling = tier >= 1;   // Düşük: kapalı (bake yoksa zaten maliyetsiz)
            Project.Infrastructure.World.HlodProxy.SetTier(tier);
        }

        public static void ApplyQuality(int tier)
        {
            tier = HlodMath.ClampTier(tier);
            try
            {
                var vram = SystemInfo.graphicsMemorySize;
                LastBudgetMb = HlodMath.EffectiveStreamingBudgetMb(tier, vram);
                QualitySettings.streamingMipmapsActive = true;
                QualitySettings.streamingMipmapsAddAllCameras = true;
                QualitySettings.streamingMipmapsMemoryBudget = LastBudgetMb;
                QualitySettings.streamingMipmapsMaxLevelReduction = HlodMath.StreamingMaxLevelReduction(tier);
                QualitySettings.streamingMipmapsRenderersPerFrame = HlodMath.StreamingRenderersPerFrame(tier);
                QualitySettings.streamingMipmapsMaxFileIORequests = 1024;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TextureStreaming] Ayarlanamadı: " + e.Message);
            }
        }
    }
}
