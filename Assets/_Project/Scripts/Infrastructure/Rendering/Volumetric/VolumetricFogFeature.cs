using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// URP renderer özelliği: hacimsel sis + ışık huzmesi. Normalde VolumetricFog.Install çalışma zamanında ekler; elle de
    /// renderer varlığına eklenebilir. Ayarlar VolumetricFog.Settings / Tier üzerinden gelir (bu sınıfta serileştirilmiş veri yok).
    /// </summary>
    public sealed class VolumetricFogFeature : ScriptableRendererFeature
    {
        public const string ShaderName = "HAREKAT/Volumetric/Fog";
        private VolumetricFogPass _pass;

        public override void Create()
        {
            // Renderer yeniden oluşturulurken eski örnek Dispose edilebilir; kaynaklar tembel (lazy) ayrılır.
            if (_pass == null)
                _pass = new VolumetricFogPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var cfg = VolumetricFog.TierConfig;
            if (!cfg.Enabled || !VolumetricFog.Settings.Enabled || VolumetricFog.Settings.Density <= 0f)
                return;

            var camera = renderingData.cameraData.camera;
            if (camera == null)
                return;
            var type = camera.cameraType;
            if (type != CameraType.Game && type != CameraType.SceneView)
                return;

            VolumetricFog.Tick();
            if (_pass == null)
                _pass = new VolumetricFogPass();
            if (!_pass.Prepare(cfg, VolumetricFog.Settings.ToParams()))
                return;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_pass != null)
            {
                _pass.Dispose();
                _pass = null;
            }
        }
    }
}
