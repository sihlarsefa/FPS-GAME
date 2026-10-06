using System;
using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Hacimsel sis pass'i (RenderGraph). Üç raster geçişi:
    ///   1) Işın yürütme: çeyrek çözünürlük, derinlikten dünya konumu, yükseklik sisi + ana ışık gölge haritası örnekleme, mavi gürültü jitter.
    ///   2) Zamansal: önceki karenin yeniden izdüşümü + komşuluk sıkıştırma (clamp), ping-pong geçmiş dokusu (kamera başına).
    ///   3) Birleştirme: çift yönlü (derinlik duyarlı) üst örnekleme, kamera rengine "One SrcAlpha" harmanı (inscatter + renk * geçirgenlik).
    /// Olay: BeforeRenderingTransparents (parçacıklar sisin üstüne çizilir). Kaynaklar tembel ayrılır; Dispose geri alınabilir.
    /// </summary>
    internal sealed class VolumetricFogPass : ScriptableRenderPass, IDisposable
    {
        private sealed class History
        {
            public RTHandle A, B;
            public bool CurIsA;
            public int Width, Height, LastFrame = -100;
            public Matrix4x4 PrevVP = Matrix4x4.identity;
            public Vector3 PrevPos;
            public Quaternion PrevRot = Quaternion.identity;
            public RTHandle Cur => CurIsA ? A : B;
            public RTHandle Prev => CurIsA ? B : A;
        }

        private sealed class RayData { public Material Material; }
        private sealed class TemporalData { public Material Material; public MaterialPropertyBlock Mpb; public TextureHandle Raw, Prev; }
        private sealed class CompositeData { public Material Material; public MaterialPropertyBlock Mpb; public TextureHandle Src; }

        private static readonly int P0 = Shader.PropertyToID("_VolParams0");
        private static readonly int P1 = Shader.PropertyToID("_VolParams1");
        private static readonly int TintId = Shader.PropertyToID("_VolTint");
        private static readonly int AmbientId = Shader.PropertyToID("_VolAmbient");
        private static readonly int TargetId = Shader.PropertyToID("_VolTarget");
        private static readonly int JitterId = Shader.PropertyToID("_VolJitter");
        private static readonly int PrevVpId = Shader.PropertyToID("_VolPrevVP");
        private static readonly int HistoryId = Shader.PropertyToID("_VolHistory");
        private static readonly int NoiseId = Shader.PropertyToID("_VolNoiseTex");
        private static readonly int RawTexId = Shader.PropertyToID("_VolRawTex");
        private static readonly int HistTexId = Shader.PropertyToID("_VolHistTex");
        private static readonly int SrcTexId = Shader.PropertyToID("_VolSrcTex");

        private Material _material;
        private Texture2D _noise;
        private bool _shaderMissing;
        private VolumetricTierConfig _cfg;
        private VolumetricFogParams _params;
        private readonly Dictionary<int, History> _history = new Dictionary<int, History>();
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private static readonly Vector3[] AmbientDir = { Vector3.up };
        private static readonly Color[] AmbientOut = new Color[1];

        public VolumetricFogPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler("HAREKAT Volumetric Fog");
        }

        public bool Prepare(VolumetricTierConfig cfg, VolumetricFogParams p)
        {
            if (!EnsureMaterial())
                return false;
            _cfg = cfg;
            _params = p;
            ConfigureInput(ScriptableRenderPassInput.Depth);
            return true;
        }

        private bool EnsureMaterial()
        {
            if (_material != null)
                return true;
            if (_shaderMissing)
                return false;
            var shader = Shader.Find(VolumetricFogFeature.ShaderName);
            if (shader == null || !shader.isSupported)
            {
                _shaderMissing = true;
                VolumetricFog.Warn("shader bulunamadı/desteklenmiyor (" + VolumetricFogFeature.ShaderName + "); Always Included Shaders'a ekleyin");
                return false;
            }
            _material = new Material(shader) { name = "HAREKAT Volumetric Fog (runtime)", hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private Texture2D Noise()
        {
            if (_noise != null)
                return _noise;
            var size = VolumetricFogMath.NoiseSize;
            var f = VolumetricFogMath.GenerateBlueNoise(size, 20260610);
            var bytes = new byte[f.Length];
            for (var i = 0; i < f.Length; i++)
                bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(f[i] * 255f), 0, 255);
            _noise = new Texture2D(size, size, TextureFormat.R8, false, true)
            {
                name = "HAREKAT BlueNoise64", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave
            };
            _noise.SetPixelData(bytes, 0);
            _noise.Apply(false, true);
            return _noise;
        }

        private static Vector4 AmbientColor()
        {
            Color c;
            switch (RenderSettings.ambientMode)
            {
                case UnityEngine.Rendering.AmbientMode.Flat:
                    c = RenderSettings.ambientLight;
                    c = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
                    break;
                case UnityEngine.Rendering.AmbientMode.Trilight:
                    c = RenderSettings.ambientSkyColor;
                    c = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
                    break;
                default:
                    RenderSettings.ambientProbe.Evaluate(AmbientDir, AmbientOut);
                    c = AmbientOut[0];
                    break;
            }
            return new Vector4(Mathf.Max(0f, c.r), Mathf.Max(0f, c.g), Mathf.Max(0f, c.b), 1f);
        }

        private History GetHistory(Camera camera, int w, int h, out bool usable)
        {
            var id = camera.GetHashCode();
            if (!_history.TryGetValue(id, out var hist))
            {
                hist = new History();
                _history[id] = hist;
            }
            var desc = new RenderTextureDescriptor(w, h, GraphicsFormat.R16G16B16A16_SFloat, 0) { msaaSamples = 1, useMipMap = false };
            var realloc = false;
            realloc |= RenderingUtils.ReAllocateHandleIfNeeded(ref hist.A, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolFogHistoryA");
            realloc |= RenderingUtils.ReAllocateHandleIfNeeded(ref hist.B, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_VolFogHistoryB");
            var jump = (camera.transform.position - hist.PrevPos).magnitude;
            usable = !realloc && VolumetricFogMath.HistoryUsable(hist.Width, hist.Height, w, h, Time.frameCount - hist.LastFrame, jump);
            return hist;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
                return;
            var res = frameData.Get<UniversalResourceData>();
            var cam = frameData.Get<UniversalCameraData>();
            if (cam.renderType == CameraRenderType.Overlay || cam.camera == null)
                return;
            if (!res.cameraDepthTexture.IsValid() || !res.activeColorTexture.IsValid())
                return;

            var camera = cam.camera;
            var desc = cam.cameraTargetDescriptor;
            var div = Mathf.Max(1, _cfg.ResolutionDivisor);
            var lw = VolumetricFogMath.LowRes(desc.width, div);
            var lh = VolumetricFogMath.LowRes(desc.height, div);
            var maxDist = Mathf.Max(10f, Mathf.Min(_params.MaxDistance, Mathf.Min(_cfg.MaxDistance, camera.farClipPlane)));
            var debug = VolumetricFog.DebugMode;
            var temporal = _cfg.Temporal && debug != 1;
            var useShadow = _cfg.ShadowRays && res.mainShadowsTexture.IsValid();
            var frame = Time.frameCount;

            var vp = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
            History hist = null;
            var usable = false;
            var historyWeight = 0f;
            if (temporal)
            {
                hist = GetHistory(camera, lw, lh, out usable);
                var motion = (camera.transform.position - hist.PrevPos).magnitude * 4f
                             + Quaternion.Angle(camera.transform.rotation, hist.PrevRot) * (desc.height / Mathf.Max(1f, camera.fieldOfView));
                historyWeight = VolumetricFogMath.HistoryWeight(usable, motion / div);
            }

            var m = _material;
            m.SetVector(P0, new Vector4(_params.Density, _params.HeightFalloff, _params.BaseHeight, _params.Anisotropy));
            m.SetVector(P1, new Vector4(_params.SunIntensity, _params.AmbientIntensity, maxDist, _cfg.Steps));
            m.SetVector(TintId, new Vector4(_params.TintR, _params.TintG, _params.TintB, 1f));
            m.SetVector(AmbientId, AmbientColor());
            m.SetVector(TargetId, new Vector4(lw, lh, 1f / lw, 1f / lh));
            m.SetVector(JitterId, new Vector4(VolumetricFogMath.TemporalNoiseOffset(frame), VolumetricFogMath.Halton(frame, 3), useShadow ? 1f : 0f, debug));
            m.SetMatrix(PrevVpId, hist != null && usable ? hist.PrevVP : vp);
            m.SetVector(HistoryId, new Vector4(historyWeight, 0f, 0f, 0f));
            m.SetTexture(NoiseId, Noise());

            var rawDesc = new TextureDesc(lw, lh)
            {
                name = "_VolFogRaw", format = GraphicsFormat.R16G16B16A16_SFloat, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, clearBuffer = false
            };
            var raw = renderGraph.CreateTexture(rawDesc);

            // 1) Işın yürütme
            using (var builder = renderGraph.AddRasterRenderPass<RayData>("HAREKAT Volumetric Raymarch", out var rayData, profilingSampler))
            {
                rayData.Material = m;
                builder.UseTexture(res.cameraDepthTexture);
                if (useShadow)
                    builder.UseTexture(res.mainShadowsTexture);
                builder.SetRenderAttachment(raw, 0, AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (RayData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3);
                });
            }

            var source = raw;

            // 2) Zamansal karışım
            if (temporal && hist != null)
            {
                var prevH = renderGraph.ImportTexture(hist.Prev);
                var curH = renderGraph.ImportTexture(hist.Cur);
                using (var builder = renderGraph.AddRasterRenderPass<TemporalData>("HAREKAT Volumetric Temporal", out var td, profilingSampler))
                {
                    td.Material = m; td.Mpb = _mpb; td.Raw = raw; td.Prev = prevH;
                    builder.UseTexture(raw);
                    builder.UseTexture(prevH);
                    builder.UseTexture(res.cameraDepthTexture);
                    builder.SetRenderAttachment(curH, 0, AccessFlags.WriteAll);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (TemporalData d, RasterGraphContext ctx) =>
                    {
                        d.Mpb.Clear();
                        d.Mpb.SetTexture(RawTexId, (Texture)d.Raw);
                        d.Mpb.SetTexture(HistTexId, (Texture)d.Prev);
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, d.Material, 1, MeshTopology.Triangles, 3, 1, d.Mpb);
                    });
                }
                source = curH;
            }

            // 3) Birleştirme (çift yönlü üst örnekleme + harman)
            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("HAREKAT Volumetric Composite", out var cd, profilingSampler))
            {
                cd.Material = m; cd.Mpb = _mpb; cd.Src = source;
                builder.UseTexture(source);
                builder.UseTexture(res.cameraDepthTexture);
                builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                {
                    d.Mpb.Clear();
                    d.Mpb.SetTexture(SrcTexId, (Texture)d.Src);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, d.Material, 2, MeshTopology.Triangles, 3, 1, d.Mpb);
                });
            }

            if (hist != null)
            {
                hist.PrevVP = vp;
                hist.PrevPos = camera.transform.position;
                hist.PrevRot = camera.transform.rotation;
                hist.Width = lw; hist.Height = lh;
                hist.LastFrame = frame;
                hist.CurIsA = !hist.CurIsA;
            }
            if ((frame & 127) == 0)
                CullStaleHistory(frame);
        }

        private void CullStaleHistory(int frame)
        {
            List<int> dead = null;
            foreach (var kv in _history)
                if (frame - kv.Value.LastFrame > 600)
                    (dead ?? (dead = new List<int>())).Add(kv.Key);
            if (dead == null) return;
            foreach (var id in dead)
            {
                Release(_history[id]);
                _history.Remove(id);
            }
        }

        private static void Release(History h)
        {
            h.A?.Release(); h.B?.Release();
            h.A = null; h.B = null;
        }

        public void Dispose()
        {
            foreach (var kv in _history)
                Release(kv.Value);
            _history.Clear();
            if (_material != null) { DestroyObj(_material); _material = null; }
            if (_noise != null) { DestroyObj(_noise); _noise = null; }
            _shaderMissing = false;
        }

        private static void DestroyObj(UnityEngine.Object o)
        {
            if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
