using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>
    /// Kenar yumuşatma/keskinlik çalışma zamanı ayarları. Kullanıcı kaydırıcısı "Keskinlik" (0..1, varsayılan 0,5) PlayerPrefs'te saklanır.
    /// Ayar menüsü: SharpenSettings.UserSharpness = v (kaydeder + anında uygular).
    /// </summary>
    public static class SharpenSettings
    {
        public const string PrefKey = "Keskinlik";
        public const float DefaultUser = 0.5f;

        private static float _user = -1f;

        /// <summary>Kullanıcı keskinliği (0..1). İlk okumada PlayerPrefs'ten gelir; yazma kaydeder.</summary>
        public static float UserSharpness
        {
            get
            {
                if (_user < 0f)
                {
                    _user = DefaultUser;
                    try { _user = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey, DefaultUser)); }
                    catch (System.Exception) { }
                }
                return _user;
            }
            set
            {
                _user = Mathf.Clamp01(value);
                try
                {
                    PlayerPrefs.SetFloat(PrefKey, _user);
                    PlayerPrefs.Save();
                }
                catch (System.Exception) { }
            }
        }

        /// <summary>Kademe tabanı x kullanıcı kaydırıcısı = geçişin etkin gücü (0..1).</summary>
        public static float EffectiveFor(int tier) => AntiAliasingMath.EffectiveSharpness(AntiAliasingMath.SharpenBaseFor(tier), UserSharpness);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _user = -1f;
    }

    /// <summary>
    /// CAS tarzı kontrast uyarlamalı keskinleştirme: son işlem (TAA/STP dahil) sonrası, yığının SON kamerasında (post-processing'in uygulandığı) çalışır.
    /// Gölgelendirici: HAREKAT/PostFx/CasSharpen. Yoksa tek uyarı + kapanır. Tüm kademelerde açık; güç kademe x kullanıcı kaydırıcısı.
    /// </summary>
    public sealed class HarekatSharpenFeature : HarekatFeatureBase
    {
        public const string Key = "CasSharpen";
        public const string ShaderName = "HAREKAT/PostFx/CasSharpen";

        private SharpenPass _pass;
        private Material _material;

        public override string FeatureKey => Key;
        protected override bool TierEnabled(int tier) => true;

        public override void Create()
        {
            _pass = new SharpenPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null)
                return;
            var cam = renderingData.cameraData;
            if (!IsMainGameCamera(cam.camera))
                return;
            // Yığında yalnız post-processing uygulanan son kamerada (taban tek başınaysa taban; overlay varsa overlay).
            var camData = cam.camera.GetUniversalAdditionalCameraData();
            if (camData == null || !camData.renderPostProcessing)
                return;

            var strength = SharpenSettings.EffectiveFor(Tier < 0 ? 0 : Tier);
            if (!AntiAliasingMath.SharpenWorthRunning(strength))
                return;

            if (_material == null)
                _material = HarekatShaders.CreateMaterial(ShaderName);
            if (_material == null)
            {
                SetActive(false);
                return;
            }

            _pass.Setup(_material, strength);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            HarekatShaders.Destroy(ref _material);
        }

        private sealed class SharpenPass : ScriptableRenderPass
        {
            private static readonly int ParamsId = Shader.PropertyToID("_HkCasParams");

            private Material _mat;
            private float _strength;

            public SharpenPass()
            {
                profilingSampler = new ProfilingSampler("HAREKAT CAS Sharpen");
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public void Setup(Material material, float strength)
            {
                _mat = material;
                _strength = strength;
            }

            private class CasData
            {
                internal Material mat;
                internal Vector4 p;
                internal TextureHandle src;
                internal int pass;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null)
                    return;
                var cameraData = frameData.Get<UniversalCameraData>();
                var res = frameData.Get<UniversalResourceData>();
                if (!res.activeColorTexture.IsValid())
                    return;

                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                var tmp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_HarekatCasTemp", false, FilterMode.Point);

                var p = new Vector4(_strength, 0f, 0f, 0f);

                using (var builder = renderGraph.AddRasterRenderPass<CasData>("HAREKAT CAS", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p = p; data.src = res.activeColorTexture; data.pass = 0;
                    builder.UseTexture(res.activeColorTexture);
                    builder.SetRenderAttachment(tmp, 0, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (CasData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(ParamsId, d.p);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, d.pass);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<CasData>("HAREKAT CAS Resolve", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p = p; data.src = tmp; data.pass = 1;
                    builder.UseTexture(tmp);
                    builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (CasData d, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, d.pass);
                    });
                }
            }
        }
    }
}
