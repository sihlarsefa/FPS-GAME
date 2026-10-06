using System;
using System.Reflection;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>
    /// SSR-lite: derinlik + normal üstünde hi-Z'siz doğrusal ışın yürüyüşü (yarı çözünürlük) ile ıslak zemin/cam/metal yansımaları.
    /// Pürüzsüzlük = normal dokusu alfası (URP 17.6 _WRITE_SMOOTHNESS; yoksa) ya da ıslaklık*yukarı-bakan sezgiseli; roughness cutoff altı yansımasız.
    /// Isabet yoksa/ekran kenarında/uzakta katkı 0'a söner: yansıma probu (zaten sahne renginde) aynen kalır = "probe'a geri düşer".
    /// Yalnız Yüksek/Ultra. Gölgelendirici: HAREKAT/ScreenSpace/SSRLite. Yoksa tek uyarı + kapanır.
    /// </summary>
    public sealed class HarekatSsrLiteFeature : HarekatFeatureBase
    {
        public const string Key = "SsrLite";
        public const string ShaderName = "HAREKAT/ScreenSpace/SSRLite";

        private SsrPass _pass;
        private Material _material;
        private SsrTier _tier;

        // UniversalRenderingData.writesSmoothnessToDepthNormalsAlpha (internal set) için önbellek.
        private static bool _smoothnessResolved;
        private static PropertyInfo _smoothnessProp;
        private static MethodInfo _getRenderingData;
        private static FieldInfo _frameDataField;
        private static bool _smoothnessWarned;

        public override string FeatureKey => Key;
        protected override bool TierEnabled(int tier) => ScreenSpaceMath.SsrForTier(tier).Enabled;
        protected override void OnTier(int tier) => _tier = ScreenSpaceMath.SsrForTier(tier);

        public override void Create()
        {
            _pass = new SsrPass();
            if (Tier >= 0)
                _tier = ScreenSpaceMath.SsrForTier(Tier);
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
                SetActive(false);
                return;
            }

            // Derinlik-normal önpasının malzeme pürüzsüzlüğünü alfaya yazmasını iste (başarısızsa gölgelendirici ıslaklık sezgiseline düşer).
            var alpha = RequestSmoothnessInNormals(ref renderingData);
            _pass.Setup(_material, _tier, alpha);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass = null;
            HarekatShaders.Destroy(ref _material);
        }

        private static bool RequestSmoothnessInNormals(ref RenderingData renderingData)
        {
            try
            {
                if (!_smoothnessResolved)
                {
                    _smoothnessResolved = true;
                    var rdType = typeof(RenderingData);
                    _frameDataField = rdType.GetField("frameData", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var urdType = rdType.Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderingData");
                    _smoothnessProp = urdType?.GetProperty("writesSmoothnessToDepthNormalsAlpha", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    var getGeneric = typeof(ContextContainer).GetMethod("Get", Type.EmptyTypes);
                    if (getGeneric != null && urdType != null)
                        _getRenderingData = getGeneric.MakeGenericMethod(urdType);
                }

                if (_frameDataField == null || _smoothnessProp == null || _getRenderingData == null)
                    return Fail("alan/özellik bulunamadı");

                object boxed = renderingData;
                var container = _frameDataField.GetValue(boxed) as ContextContainer;
                if (container == null)
                    return Fail("frameData yok");
                var urd = _getRenderingData.Invoke(container, null);
                if (urd == null)
                    return Fail("UniversalRenderingData yok");
                var setter = _smoothnessProp.GetSetMethod(true);
                if (setter == null)
                    return Fail("setter yok");
                setter.Invoke(urd, new object[] { true });
                return true;
            }
            catch (Exception e)
            {
                return Fail(e.Message);
            }
        }

        private static bool Fail(string why)
        {
            if (!_smoothnessWarned)
            {
                _smoothnessWarned = true;
                Debug.LogWarning("[HAREKAT] SSR-lite: pürüzsüzlük alfası istenemedi (" + why + "); ıslaklık sezgiseline düşülüyor.");
            }
            return false;
        }

        private sealed class SsrPass : ScriptableRenderPass
        {
            private static readonly int TexId = Shader.PropertyToID("_HarekatSsrTex");
            private static readonly int P0 = Shader.PropertyToID("_HkSsrParams");
            private static readonly int P1 = Shader.PropertyToID("_HkSsrParams2");
            private static readonly int P2 = Shader.PropertyToID("_HkSsrParams3");
            private static readonly int P3 = Shader.PropertyToID("_HkSsrParams4");
            private static readonly int LowResId = Shader.PropertyToID("_HkLowResTexel");

            private Material _mat;
            private SsrTier _t;
            private bool _alpha;

            public SsrPass()
            {
                profilingSampler = new ProfilingSampler("HAREKAT SSR Lite");
                // Skybox sonrası: sahne rengi (opak + gökyüzü) hazır, saydamlar öncesi.
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            public void Setup(Material material, SsrTier tier, bool smoothnessInAlpha)
            {
                _mat = material;
                _t = tier;
                _alpha = smoothnessInAlpha;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            private class MarchData
            {
                internal Material mat;
                internal Vector4 p0, p1, p2, p3, lowRes;
                internal TextureHandle color;
            }

            private class ComposeData
            {
                internal Material mat;
                internal Vector4 p0, p1, p2, p3, lowRes;
                internal TextureHandle src;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null)
                    return;
                var cameraData = frameData.Get<UniversalCameraData>();
                var res = frameData.Get<UniversalResourceData>();
                if (!res.cameraDepthTexture.IsValid() || !res.cameraNormalsTexture.IsValid() || !res.activeColorTexture.IsValid())
                    return;

                var div = Mathf.Max(1, _t.ResolutionDivisor);
                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.width = Mathf.Max(1, desc.width / div);
                desc.height = Mathf.Max(1, desc.height / div);
                desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.B8G8R8A8_UNorm;
                var low = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_HarekatSsrTex", false, FilterMode.Bilinear);

                var strength = Mathf.Clamp(_t.Strength * Mathf.Clamp(ScreenSpaceSettings.SsrStrengthScale, 0f, 1.5f), 0f, 1.5f);
                var p0 = new Vector4(_t.MaxDistance, _t.MaxSteps, _t.ThicknessAbs, _t.ThicknessRel);
                var p1 = new Vector4(_t.EdgeFade, strength, Mathf.Clamp01(ScreenSpaceSettings.Wetness), _alpha ? 1f : 0f);
                var p2 = new Vector4(_t.SmoothnessCutoff, _t.SmoothnessFadeStart, 0.04f, _t.RefineSteps);
                var p3 = new Vector4(_t.StridePx, 0f, 0f, 0f);
                var lowRes = new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height);

                using (var builder = renderGraph.AddRasterRenderPass<MarchData>("HAREKAT SSR March", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.p2 = p2; data.p3 = p3; data.lowRes = lowRes;
                    data.color = res.activeColorTexture;
                    builder.UseTexture(res.activeColorTexture);
                    builder.UseTexture(res.cameraDepthTexture);
                    builder.UseTexture(res.cameraNormalsTexture);
                    builder.SetRenderAttachment(low, 0, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.SetGlobalTextureAfterPass(low, TexId);
                    builder.SetRenderFunc(static (MarchData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(P0, d.p0);
                        d.mat.SetVector(P1, d.p1);
                        d.mat.SetVector(P2, d.p2);
                        d.mat.SetVector(P3, d.p3);
                        d.mat.SetVector(LowResId, d.lowRes);
                        Blitter.BlitTexture(ctx.cmd, d.color, new Vector4(1f, 1f, 0f, 0f), d.mat, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<ComposeData>("HAREKAT SSR Apply", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.p2 = p2; data.p3 = p3; data.lowRes = lowRes; data.src = low;
                    builder.UseTexture(low);
                    builder.UseTexture(res.cameraDepthTexture);
                    builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (ComposeData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(P0, d.p0);
                        d.mat.SetVector(P1, d.p1);
                        d.mat.SetVector(P2, d.p2);
                        d.mat.SetVector(P3, d.p3);
                        d.mat.SetVector(LowResId, d.lowRes);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, 1);
                    });
                }
            }
        }
    }
}
