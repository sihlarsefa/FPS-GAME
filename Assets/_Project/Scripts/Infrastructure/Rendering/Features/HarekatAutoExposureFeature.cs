using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering.Features
{
    /// <summary>Otomatik pozlama çalışma zamanı ayarları (ayar menüsü / sinematikler bağlar).</summary>
    public static class AutoExposureSettings
    {
        /// <summary>0..1: etki ölçeği (0 = çarpan 1'e yakın = etkisiz, 1 = tam). Erişilebilirlik/ayar için.</summary>
        public static float Amount = 1f;
        /// <summary>Haritaya özel ek EV kayması (örn. iç mekân bölgesi tetikleyicisi; negatif = daha karanlık).</summary>
        public static float EvBias = 0f;
        /// <summary>Uyumu sıfırla: sonraki karede hedefe anında atlar (ışınlanma, sahne geçişi, ölüm kamerası).</summary>
        public static void RequestSnap() => SnapRequested = true;
        internal static bool SnapRequested;
    }

    /// <summary>
    /// Göz uyumu (otomatik pozlama): sahne rengi pozlama öncesi GxG log2-parlaklık ızgarasına indirgenir (4x4 örnek/hücre, merkez ağırlıklı),
    /// 8x8'e ve 1x1 EV'ye düşürülür; EV, önceki kareyle üstel uyumlanır (karanlıktan aydınlığa HIZLI, aydınlıktan karanlığa YAVAŞ) ve
    /// ucuz bir çarpım geçişiyle (Blend DstColor Zero) sahne rengine uygulanır; son işleme (ColorAdjustments/ton eşleme) bundan sonra gelir.
    /// Telafi gücü 1'den küçük: iç mekân gerçek kameralardaki gibi koyu okunur, dış mekân parlak kalır. Saat/harita/hava <see cref="Atmosphere"/> statiklerinden okunur.
    /// Gölgelendirici: HAREKAT/PostFx/AutoExposure. Yoksa tek uyarı + kapanır (mevcut pozlama aynen kalır). Düşük kademede kapalı.
    /// </summary>
    public sealed class HarekatAutoExposureFeature : HarekatFeatureBase
    {
        public const string Key = "AutoExposure";
        public const string ShaderName = "HAREKAT/PostFx/AutoExposure";

        private AutoExposurePass _pass;
        private Material _material;
        private ExposureTier _tier;

        public override string FeatureKey => Key;
        protected override bool TierEnabled(int tier) => LightingMath.ExposureForTier(tier).Enabled;
        protected override void OnTier(int tier) => _tier = LightingMath.ExposureForTier(tier);

        public override void Create()
        {
            _pass = new AutoExposurePass();
            if (Tier >= 0)
                _tier = LightingMath.ExposureForTier(Tier);
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

            var p = LightingMath.ExposureFor(Atmosphere.CurrentTime, Atmosphere.CurrentMapId);
            _pass.Setup(_material, _tier, p, Time.unscaledDeltaTime);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Release();
            _pass = null;
            HarekatShaders.Destroy(ref _material);
        }

        private sealed class AutoExposurePass : ScriptableRenderPass
        {
            private static readonly int P0 = Shader.PropertyToID("_HkAeParams");
            private static readonly int P1 = Shader.PropertyToID("_HkAeParams2");
            private static readonly int GridId = Shader.PropertyToID("_HkAeGrid");
            private static readonly int PrevId = Shader.PropertyToID("_HkAePrev");

            private Material _mat;
            private ExposureTier _t;
            private ExposureParams _p;
            private float _dt;
            private RTHandle _histA, _histB;
            private bool _flip;

            public AutoExposurePass()
            {
                profilingSampler = new ProfilingSampler("HAREKAT Auto Exposure");
                // Sahne rengi (opak + gökyüzü + saydam) hazır, ton eşleme/ColorAdjustments öncesi.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void Setup(Material material, ExposureTier tier, ExposureParams p, float dt)
            {
                _mat = material;
                _t = tier;
                _p = p;
                _dt = dt;
            }

            public void Release()
            {
                _histA?.Release();
                _histB?.Release();
                _histA = null;
                _histB = null;
            }

            private class GridData
            {
                internal Material mat;
                internal Vector4 p0, p1, grid;
                internal TextureHandle src;
            }

            private class AdaptData
            {
                internal Material mat;
                internal Vector4 p0, p1, grid;
                internal TextureHandle src, prev;
            }

            private class ApplyData
            {
                internal Material mat;
                internal TextureHandle ev;
            }

            private static GraphicsFormat Pick(GraphicsFormat preferred, GraphicsFormat fallback)
            {
                return SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render) ? preferred : fallback;
            }

            private static RenderTextureDescriptor Desc(int w, int h, GraphicsFormat fmt)
            {
                var d = new RenderTextureDescriptor(w, h)
                {
                    msaaSamples = 1,
                    depthBufferBits = 0,
                    graphicsFormat = fmt,
                    sRGB = false,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                return d;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null)
                    return;
                var res = frameData.Get<UniversalResourceData>();
                if (!res.activeColorTexture.IsValid())
                    return;

                var amount = Mathf.Clamp01(AutoExposureSettings.Amount);
                var p0 = new Vector4(
                    Mathf.Log(Mathf.Max(1e-4f, _p.ReferenceLuma), 2f),
                    Mathf.Clamp01(_p.Strength) * amount,
                    _p.MinEv + AutoExposureSettings.EvBias,
                    _p.MaxEv + AutoExposureSettings.EvBias);

                var g = Mathf.Max(8, (_t.GridSize / 8) * 8);
                var grid = new Vector4(g, 1f / g, 0f, 0f);

                // Geçmiş EV dokuları (1x1, ping-pong). Yeniden oluşturulduysa uyum atlanır ve hedefe yakalanır.
                var evFmt = Pick(GraphicsFormat.R32_SFloat, GraphicsFormat.R16_SFloat);
                var evDesc = Desc(1, 1, evFmt);
                var snap = AutoExposureSettings.SnapRequested;
                AutoExposureSettings.SnapRequested = false;
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref _histA, evDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0f, "_HkAeHistA"))
                    snap = true;
                if (RenderingUtils.ReAllocateHandleIfNeeded(ref _histB, evDesc, FilterMode.Point, TextureWrapMode.Clamp, 1, 0f, "_HkAeHistB"))
                    snap = true;
                if (_histA == null || _histB == null)
                    return;

                var prevH = _flip ? _histB : _histA;
                var curH = _flip ? _histA : _histB;
                _flip = !_flip;
                var prev = renderGraph.ImportTexture(prevH);
                var cur = renderGraph.ImportTexture(curH);

                var p1 = new Vector4(_p.SpeedToBright, _p.SpeedToDark, _dt, snap ? 1f : 0f);

                var lumFmt = Pick(GraphicsFormat.R16_SFloat, GraphicsFormat.R16G16B16A16_SFloat);
                var redFmt = Pick(GraphicsFormat.R16G16_SFloat, GraphicsFormat.R16G16B16A16_SFloat);
                var gridTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, Desc(g, g, lumFmt), "_HkAeGridTex", false, FilterMode.Point);
                var smallTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, Desc(8, 8, redFmt), "_HkAeSmallTex", false, FilterMode.Point);

                using (var builder = renderGraph.AddRasterRenderPass<GridData>("HAREKAT AE Grid", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.grid = grid; data.src = res.activeColorTexture;
                    builder.UseTexture(res.activeColorTexture);
                    builder.SetRenderAttachment(gridTex, 0, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (GridData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(GridId, d.grid);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<GridData>("HAREKAT AE Reduce", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.grid = grid; data.src = gridTex;
                    builder.UseTexture(gridTex);
                    builder.SetRenderAttachment(smallTex, 0, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (GridData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(GridId, d.grid);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, 1);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<AdaptData>("HAREKAT AE Adapt", out var data, profilingSampler))
                {
                    data.mat = _mat; data.p0 = p0; data.p1 = p1; data.grid = grid; data.src = smallTex; data.prev = prev;
                    builder.UseTexture(smallTex);
                    builder.UseTexture(prev);
                    builder.SetRenderAttachment(cur, 0, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (AdaptData d, RasterGraphContext ctx) =>
                    {
                        d.mat.SetVector(P0, d.p0);
                        d.mat.SetVector(P1, d.p1);
                        d.mat.SetVector(GridId, d.grid);
                        ctx.cmd.SetGlobalTexture(PrevId, d.prev);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1f, 1f, 0f, 0f), d.mat, 2);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<ApplyData>("HAREKAT AE Apply", out var data, profilingSampler))
                {
                    data.mat = _mat; data.ev = cur;
                    builder.UseTexture(cur);
                    builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (ApplyData d, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, d.ev, new Vector4(1f, 1f, 0f, 0f), d.mat, 3);
                    });
                }
            }
        }
    }
}
