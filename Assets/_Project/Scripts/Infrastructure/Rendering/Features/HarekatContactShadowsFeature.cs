using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>Ekran-uzayı efektlerinin çalışma zamanı ayarları (ayar menüsü / hava durumu bağlar). Kademe tablosunun üstüne çarpan olarak biner.</summary>
    public static class ScreenSpaceSettings
    {
        /// <summary>Kontakt gölge gücü çarpanı (0..1.5).</summary>
        public static float ContactShadowStrengthScale = 1f;
        /// <summary>Gün ışığı (1 gündüz .. 0 gece): kontakt gölge yoğunluğu/uzunluğu 0,35/0,5 tablosu (ScreenEffectsMath). Atmosphere.Apply yazar.</summary>
        public static float Daylight01 = 1f;
        /// <summary>SSR gücü çarpanı (0..1.5).</summary>
        public static float SsrStrengthScale = 1f;
        /// <summary>Islaklık (0..1): yağmur/çamurda artar; malzeme pürüzsüzlüğü yoksa yukarı bakan yüzeylerde yansıma üretir. Taban: nemli toprak.</summary>
        public static float Wetness = 0.2f;
    }

    /// <summary>
    /// Ekran-uzayı kontakt gölgeleri: derinlik tamponunda güneş yönünde kısa ışın yürüyüşü (yarı çözünürlük, R8) + derinlik duyarlı yukarı örnekleme ile
    /// renge çarpım (gölge haritasında zaten gölgede olan yerde etkisiz: çifte koyulaşma yok). Düşük kademede kapalı.
    /// Gölgelendirici: HAREKAT/ScreenSpace/ContactShadows. Yoksa tek uyarı + özellik kapanır (mevcut gölge yolu aynen kalır).
    /// Küresel çıktı: _HarekatContactShadowTex (R=görünürlük, yarı çözünürlük) özel gölgelendiriciler örnekleyebilir.
    /// </summary>
    public sealed class HarekatContactShadowsFeature : HarekatFeatureBase
    {
        public const string Key = "ContactShadows";
        public const string ShaderName = "HAREKAT/ScreenSpace/ContactShadows";

        private ContactShadowsPass _pass;
        private Material _material;
        private ContactShadowTier _tier;

        public override string FeatureKey => Key;
        protected override bool TierEnabled(int tier) => ScreenSpaceMath.ContactShadowsForTier(tier).Enabled;
        protected override void OnTier(int tier) => _tier = ScreenSpaceMath.ContactShadowsForTier(tier);

        public override void Create()
        {
            _pass = new ContactShadowsPass();
            if (Tier >= 0)
                _tier = ScreenSpaceMath.ContactShadowsForTier(Tier);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!_tier.Enabled || _pass == null)
                return;
            if (!IsMainGameCamera(renderingData.cameraData.camera))
                return;
            if (_material == null)
                _material = HarekatShaders.CreateMaterial(ShaderName);
            if (_material == null)
            {
                SetActive(false); // gölgelendirici yok: bir daha deneme
                return;
            }

            _pass.Setup(_material, _tier);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            HarekatShaders.Destroy(ref _material);
        }

        private sealed class ContactShadowsPass : ScriptableRenderPass
        {
            private static readonly int TexId = Shader.PropertyToID("_HarekatContactShadowTex");
            private static readonly int ParamsId = Shader.PropertyToID("_HkContactParams");
            private static readonly int Params2Id = Shader.PropertyToID("_HkContactParams2");
            private static readonly int LowResId = Shader.PropertyToID("_HkLowResTexel");

            private Material _mat;
            private ContactShadowTier _t;

            public ContactShadowsPass()
            {
                profilingSampler = new ProfilingSampler("HAREKAT Contact Shadows");
                // Skybox sonrası, saydamlar öncesi: derinlik kopyası ve gölge haritası hazır, gökyüzü derinliği uzak olduğundan etkilenmez.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            public void Setup(Material material, ContactShadowTier tier)
            {
                _mat = material;
                _t = tier;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            private class MarchData
            {
                internal Material mat;
                internal Vector4 p0, p1, lowRes;
            }

            private class ComposeData
            {
                internal Material mat;
                internal Vector4 p0, p1, lowRes;
                internal TextureHandle src;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null)
                    return;
                var cameraData = frameData.Get<UniversalCameraData>();
                var res = frameData.Get<UniversalResourceData>();
                if (!res.cameraDepthTexture.IsValid() || !res.activeColorTexture.IsValid())
                    return;

                var div = Mathf.Max(1, _t.ResolutionDivisor);
                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.width = Mathf.Max(1, desc.width / div);
                desc.height = Mathf.Max(1, desc.height / div);
                desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm
                    : GraphicsFormat.B8G8R8A8_UNorm;
                var low = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_HarekatContactShadowTex", false, FilterMode.Bilinear);

                var strength = Mathf.Clamp01(ScreenEffectsMath.ContactStrength(_t.Strength, ScreenSpaceSettings.Daylight01)
                                             * Mathf.Clamp(ScreenSpaceSettings.ContactShadowStrengthScale, 0f, 1.5f));
                var rayLength = _t.RayLength * ScreenEffectsMath.ContactLengthScale(ScreenSpaceSettings.Daylight01);
                var p0 = new Vector4(rayLength, _t.Thickness, strength, _t.MaxDistance);
                var p1 = new Vector4(_t.Steps, 0f, 0f, 0f);
                var lowRes = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);

                using (var builder = renderGraph.AddRasterRenderPass<MarchData>("HAREKAT Contact Shadows March", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.lowRes = lowRes;
                    builder.SetRenderAttachment(low, 0, AccessFlags.WriteAll);
                    builder.UseTexture(res.cameraDepthTexture);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(low, TexId);
                    builder.SetRenderFunc(static (MarchData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(ParamsId, d.p0);
                        d.mat.SetVector(Params2Id, d.p1);
                        d.mat.SetVector(LowResId, d.lowRes);
                        Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), d.mat, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<ComposeData>("HAREKAT Contact Shadows Apply", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.lowRes = lowRes; data.src = low;
                    builder.UseTexture(low);
                    builder.UseTexture(res.cameraDepthTexture);
                    if (res.mainShadowsTexture.IsValid())
                        builder.UseTexture(res.mainShadowsTexture); // gölge haritası ömrünü bu geçişe kadar uzatır
                    builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (ComposeData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(ParamsId, d.p0);
                        d.mat.SetVector(Params2Id, d.p1);
                        d.mat.SetVector(LowResId, d.lowRes);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, 1);
                    });
                }
            }
        }
    }
}
