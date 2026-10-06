using System.Collections.Generic;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// S3-yansima: Su yüzeyleri için planar yansıma. Düzlemin altından bakan ikinci (gizli) kamera, eğik yakın kırpma düzlemi,
    /// ekranın yarısı/çeyreği çözünürlük, küçük eşya katmanları (Loot/Projectile/Viewmodel) çıkarılmış; en fazla BİR yüzey
    /// (kameraya en yakın görünen) için çizilir ve <c>_PlanarReflectionTex</c> küresel dokusuna yazılır.
    /// Örnekleme: su shader'ı ekran UV'siyle (main kamera) bu dokuyu örnekler. Düşük kademede kapalı; shader/doku yoksa
    /// küresel doku siyah, <c>_PlanarReflectionParams.x = 0</c> (shader probe yansımasına döner) — hiçbir şey kırılmaz.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class PlanarReflection : MonoBehaviour
    {
        public const string RootName = "PlanarReflection";
        public static readonly int TextureId = Shader.PropertyToID("_PlanarReflectionTex");
        public static readonly int ParamsId = Shader.PropertyToID("_PlanarReflectionParams"); // x: açık(1/0), y: su Y, z: güç, w: bozulma
        public static readonly int PlaneId = Shader.PropertyToID("_PlanarReflectionPlane");   // n.xyz, d

        private sealed class Surface
        {
            public Renderer Renderer;
            public float Level;
        }

        private static readonly List<Surface> Surfaces = new List<Surface>();
        private static PlanarReflection _instance;

        private int _tier = -1;
        private Camera _cam;
        private Camera _reflCam;
        private RenderTexture _rt;
        private bool _rendering;
        private bool _active;
        private int _frame;
        private readonly Plane[] _frustum = new Plane[6];
        private readonly List<float> _dist = new List<float>(8);
        private readonly List<bool> _vis = new List<bool>(8);

        /// <summary>Şu an yansıma çizilen yüzeyin su seviyesi (yoksa NaN) — test/hata ayıklama.</summary>
        public static float ActiveLevel { get; private set; } = float.NaN;

        /// <summary>Yansıma çalışıyor mu (kademe açık + görünür yüzey).</summary>
        public static bool IsActive => _instance != null && _instance._active;

        /// <summary>Yüzeyi kaydeder (WorldWater.CreateSurface). Aynı renderer ikinci kez eklenmez.</summary>
        public static void Register(Renderer renderer, float waterLevel)
        {
            if (renderer == null) return;
            for (var i = 0; i < Surfaces.Count; i++)
                if (Surfaces[i].Renderer == renderer) { Surfaces[i].Level = waterLevel; return; }
            Surfaces.Add(new Surface { Renderer = renderer, Level = waterLevel });
        }

        /// <summary>Kök altındaki tüm MeshRenderer'ları kaydeder (WorldWater.Build sonunda tek çağrı).</summary>
        public static void RegisterAll(Transform root, float waterLevel)
        {
            if (root == null) return;
            var rs = root.GetComponentsInChildren<MeshRenderer>(true);
            for (var i = 0; i < rs.Length; i++)
                if (rs[i].gameObject.layer == GameLayers.Water) Register(rs[i], waterLevel);
        }

        /// <summary>Sistemi kurar/günceller. tier &lt; 0 → QualitySettings'ten. Tekrar çağrı güvenlidir.</summary>
        public static PlanarReflection Install(Camera camera, int tier = -1)
        {
            if (_instance == null)
            {
                var go = new GameObject(RootName);
                _instance = go.AddComponent<PlanarReflection>();
            }

            _instance._cam = camera;
            _instance.SetTier(tier);
            return _instance;
        }

        public void SetTier(int tier)
        {
            if (tier < 0)
            {
                try { tier = PlanarReflectionMath.TierFromQuality(QualitySettings.GetQualityLevel(), QualitySettings.names.Length); }
                catch { tier = 2; }
            }

            _tier = Mathf.Clamp(tier, 0, PlanarReflectionMath.TierCount - 1);
            if (!PlanarReflectionMath.Get(_tier).Enabled)
                Deactivate();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBegin;
            RenderPipelineManager.endCameraRendering += OnEnd;
            Shader.SetGlobalTexture(TextureId, Texture2D.blackTexture);
            Shader.SetGlobalVector(ParamsId, Vector4.zero);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBegin;
            RenderPipelineManager.endCameraRendering -= OnEnd;
            Deactivate();
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            if (_reflCam != null) Destroy(_reflCam.gameObject);
            if (_instance == this) _instance = null;
        }

        // Yansıma kamerası negatif determinantlı view matrisiyle çizer: yüz yönü ters çevrilmeli.
        private void OnBegin(ScriptableRenderContext ctx, Camera cam)
        {
            if (_reflCam != null && cam == _reflCam) GL.invertCulling = true;
        }

        private void OnEnd(ScriptableRenderContext ctx, Camera cam)
        {
            if (_reflCam != null && cam == _reflCam) GL.invertCulling = false;
        }

        private void Deactivate()
        {
            if (_active || !float.IsNaN(ActiveLevel))
            {
                _active = false;
                ActiveLevel = float.NaN;
                Shader.SetGlobalTexture(TextureId, Texture2D.blackTexture);
                Shader.SetGlobalVector(ParamsId, Vector4.zero);
            }
        }

        private void LateUpdate()
        {
            if (_rendering || _tier < 0) return;
            var tier = PlanarReflectionMath.Get(_tier);
            if (!tier.Enabled) return;

            var cam = _cam != null ? _cam : Camera.main;
            if (cam == null || !cam.isActiveAndEnabled) { Deactivate(); return; }

            _frame++;
            var pick = PickSurface(cam, tier);
            if (pick == null) { Deactivate(); return; }

            var camPos = cam.transform.position;
            if (camPos.y < pick.Level + 0.05f) { Deactivate(); return; } // su altında yansıma yok

            if (_active && !PlanarReflectionMath.ShouldUpdate(_frame, tier.UpdateInterval))
            {
                PublishParams(pick, camPos, tier);
                return;
            }

            try
            {
                Render(cam, pick, tier);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HAREKÂT] Planar yansıma kapatıldı: " + e.Message);
                _tier = 0;
                Deactivate();
            }
        }

        private Surface PickSurface(Camera cam, PlanarReflectionTier tier)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _frustum);
            _dist.Clear();
            _vis.Clear();
            var list = new List<Surface>(Surfaces.Count);
            for (var i = Surfaces.Count - 1; i >= 0; i--)
            {
                if (Surfaces[i].Renderer == null) { Surfaces.RemoveAt(i); continue; }
            }

            for (var i = 0; i < Surfaces.Count; i++)
            {
                var r = Surfaces[i].Renderer;
                var b = r.bounds;
                list.Add(Surfaces[i]);
                _dist.Add(Mathf.Sqrt(b.SqrDistance(cam.transform.position)));
                _vis.Add(r.enabled && r.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(_frustum, b));
            }

            var idx = PlanarReflectionMath.PickNearest(_dist, _vis, tier.MaxSurfaceDistance);
            return idx < 0 ? null : list[idx];
        }

        private void EnsureResources(Camera cam, PlanarReflectionTier tier)
        {
            var size = PlanarReflectionMath.TextureSize(cam.pixelWidth, cam.pixelHeight, tier.ResolutionDivisor);
            if (_rt == null || _rt.width != size.x || _rt.height != size.y)
            {
                if (_rt != null) { _rt.Release(); Destroy(_rt); }
                var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR)
                    ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                _rt = new RenderTexture(size.x, size.y, 16, fmt) { name = "HK_PlanarReflectionRT", filterMode = FilterMode.Bilinear, antiAliasing = 1, useMipMap = false };
                _rt.Create();
            }

            if (_reflCam == null)
            {
                var go = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                _reflCam = go.AddComponent<Camera>();
                _reflCam.enabled = false;
                try
                {
                    var data = _reflCam.GetUniversalAdditionalCameraData();
                    if (data != null)
                    {
                        data.renderPostProcessing = false;
                        data.antialiasing = AntialiasingMode.None;
                    }
                }
                catch { /* URP yoksa sorun değil */ }
            }

            try
            {
                var data = _reflCam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderShadows = tier.Shadows;
            }
            catch { }
        }

        private void Render(Camera cam, Surface s, PlanarReflectionTier tier)
        {
            EnsureResources(cam, tier);

            var normal = Vector3.up;
            var point = new Vector3(0f, s.Level, 0f);
            var plane = PlanarReflectionMath.PlaneFrom(point, normal);

            _reflCam.CopyFrom(cam);
            _reflCam.ResetWorldToCameraMatrix();
            _reflCam.ResetProjectionMatrix();
            _reflCam.targetTexture = _rt;
            _reflCam.depth = cam.depth - 10f;
            _reflCam.farClipPlane = Mathf.Min(cam.farClipPlane, tier.FarClip);
            _reflCam.clearFlags = cam.clearFlags;
            _reflCam.allowHDR = true;
            _reflCam.allowMSAA = false;
            _reflCam.cullingMask = BuildMask();
            ApplyLayerDistances(tier);

            var camTr = cam.transform;
            _reflCam.transform.SetPositionAndRotation(
                PlanarReflectionMath.ReflectPosition(camTr.position, plane),
                Quaternion.LookRotation(PlanarReflectionMath.ReflectDirection(camTr.forward, normal),
                    PlanarReflectionMath.ReflectDirection(camTr.up, normal)));

            _reflCam.worldToCameraMatrix = PlanarReflectionMath.ReflectedView(cam.worldToCameraMatrix, plane);
            var clip = PlanarReflectionMath.CameraSpacePlane(_reflCam.worldToCameraMatrix, point, normal, 1f, 0.05f);
            _reflCam.projectionMatrix = _reflCam.CalculateObliqueMatrix(clip);

            _rendering = true;
            try
            {
                _reflCam.Render();
            }
            finally
            {
                _rendering = false;
                GL.invertCulling = false;
            }

            _active = true;
            ActiveLevel = s.Level;
            Shader.SetGlobalTexture(TextureId, _rt);
            Shader.SetGlobalVector(PlaneId, plane);
            PublishParams(s, camTr.position, tier);
        }

        private static void PublishParams(Surface s, Vector3 camPos, PlanarReflectionTier tier)
        {
            var d = Mathf.Sqrt(s.Renderer.bounds.SqrDistance(camPos));
            var intensity = PlanarReflectionMath.IntensityByDistance(d, tier.MaxSurfaceDistance);
            Shader.SetGlobalVector(ParamsId, new Vector4(1f, s.Level, intensity, 0.02f));
        }

        private static int BuildMask()
        {
            // Küçük eşya katmanları (Loot, Projectile), Viewmodel, Hitbox, UI ve Water (özyineleme) yok.
            return PlanarReflectionMath.BuildCullingMask(GameLayers.Default, GameLayers.Vehicle, GameLayers.Player, GameLayers.Bot);
        }

        private void ApplyLayerDistances(PlanarReflectionTier tier)
        {
            var d = new float[32];
            d[GameLayers.Player] = tier.CharacterDistance;
            d[GameLayers.Bot] = tier.CharacterDistance;
            _reflCam.layerCullDistances = d;
        }
    }
}
