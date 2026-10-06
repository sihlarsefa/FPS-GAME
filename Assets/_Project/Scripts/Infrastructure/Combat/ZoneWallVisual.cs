using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// PUBG tarzı bölge duvarı: kamera yüksekliğini izleyen 60 m'lik, 128 segmentli tek mesh bant; alt/üst alfa sönümlü,
    /// yavaş kayan altıgen desenli, HDR mavi unlit saydam malzeme (iki yüzden görünür). Bölge küçülürken yarıçap/merkez
    /// yumuşakça izlenir; kamera bölge dışındayken duvar kırmızıya döner; duvara çok yakınken parlaklık düşer.
    /// Karede yalnızca dönüşüm ve malzeme değerleri yazılır (GC yok). Kalite kademesi 0'da <see cref="IsRendering"/> false
    /// kalır ve çağıran (ZoneWallView) basit eski duvarı kullanır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneWallVisual : MonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private static readonly Color SafeTint = new Color(0.16f, 0.46f, 1.45f, 1f);
        private static readonly Color DangerTint = new Color(1.55f, 0.18f, 0.12f, 1f);

        private const float FollowRate = 5f;
        private const float ScrollSpeed = 0.012f;

        private static Mesh _mesh;
        private static Texture2D _pattern;

        private IZoneService _zone;
        private Transform _wall;
        private MeshRenderer _renderer;
        private Material _material;
        private Camera _camera;
        private ZoneState _shown;
        private bool _hasShown;
        private int _lastTiles = -1;
        private float _tint;
        private float _baseY;
        private bool _hasBaseY;

        /// <summary>Yeni duvar şu an çiziliyor mu (false ise eski basit duvar kullanılmalı).</summary>
        public bool IsRendering => _renderer != null && _renderer.enabled;

        /// <summary>Yeni duvar bu kalite kademesinde kullanılabilir mi (kademe 0 ve malzeme yoksa false).</summary>
        public bool Supported => _material != null && !IsLowTier();

        public ZoneState ShownZone => _shown;

        public static ZoneWallVisual Create(IZoneService zone, Transform parent)
        {
            var go = new GameObject("Bölge Duvarı (Enerji)");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var visual = go.AddComponent<ZoneWallVisual>();
            visual._zone = zone;
            visual.EnsureVisuals();
            return visual;
        }

        public void SetZone(IZoneService zone) => _zone = zone;

        private static bool IsLowTier() => QualityTierApplier.LastTier == 0;

        private void Awake() => EnsureVisuals();

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private void LateUpdate()
        {
            if (_wall == null || _zone == null || _material == null || IsLowTier())
            {
                SetRendering(false);
                return;
            }

            var target = _zone.CurrentZone;
            if (float.IsNaN(target.Radius) || float.IsInfinity(target.Radius) || target.Radius <= 0.01f)
            {
                SetRendering(false);
                return;
            }

            var dt = Time.unscaledDeltaTime;
            _shown = ZoneWallGeometry.Follow(_shown, target, dt, FollowRate, !_hasShown);
            _hasShown = true;

            if (_camera == null)
                _camera = Camera.main;
            var camPos = _camera != null ? _camera.transform.position : new Vector3(_shown.CenterX, 0f, _shown.CenterZ);

            var wantBase = ZoneWallGeometry.BaseY(camPos.y);
            _baseY = _hasBaseY ? ZoneWallGeometry.Smooth(_baseY, wantBase, dt, 3f) : wantBase;
            _hasBaseY = true;

            _wall.position = new Vector3(_shown.CenterX, _baseY, _shown.CenterZ);
            _wall.localScale = new Vector3(_shown.Radius, ZoneWallGeometry.BandHeight, _shown.Radius);

            var tiles = ZoneWallGeometry.TilesAround(_shown.Radius);
            if (tiles != _lastTiles)
            {
                _lastTiles = tiles;
                _material.SetTextureScale(BaseMapId, new Vector2(tiles, 1f));
            }

            var time = Time.unscaledTime;
            _material.SetTextureOffset(BaseMapId, new Vector2(time * ScrollSpeed, 0f));

            var signed = ZoneWallGeometry.SignedDistance(_shown, camPos.x, camPos.z);
            var tintTarget = signed > 0f ? 1f : 0f;
            _tint = ZoneWallGeometry.Smooth(_tint, tintTarget, dt, 6f);

            var pulse = 1f + 0.1f * Mathf.Sin(time * 2.2f);
            var k = pulse * ZoneWallGeometry.ProximityFade(signed);
            var c = Color.Lerp(SafeTint, DangerTint, _tint);
            c.r *= k;
            c.g *= k;
            c.b *= k;
            _material.SetColor(BaseColorId, c);

            SetRendering(true);
        }

        private void SetRendering(bool on)
        {
            if (_renderer != null && _renderer.enabled != on)
                _renderer.enabled = on;
        }

        private void EnsureVisuals()
        {
            if (_wall != null)
                return;

            _material = BuildMaterial();
            var go = new GameObject("Bant");
            go.layer = GameLayers.IgnoreRaycast;
            go.transform.SetParent(transform, false);
            _wall = go.transform;

            go.AddComponent<MeshFilter>().sharedMesh = GetMesh();
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.enabled = false;
        }

        private static Material BuildMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;

            if (_pattern == null)
                _pattern = ZoneWallPattern.BuildTexture();

            var m = new Material(shader) { name = "ZoneWallEnergy", hideFlags = HideFlags.DontSave };
            m.SetTexture(BaseMapId, _pattern);
            m.SetColor(BaseColorId, SafeTint);
            m.SetFloat(SurfaceId, 1f);
            m.SetFloat(BlendId, 2f);
            m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlendId, (float)BlendMode.One);
            m.SetFloat(ZWriteId, 0f);
            m.SetFloat(CullId, 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent + 20;
            m.enableInstancing = false;
            return m;
        }

        private static Mesh GetMesh()
        {
            if (_mesh != null)
                return _mesh;

            var segments = ZoneWallGeometry.Segments;
            var vertices = new Vector3[ZoneWallGeometry.VertexCount(segments)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[ZoneWallGeometry.IndexCount(segments)];
            ZoneWallGeometry.Fill(segments, vertices, uvs, triangles);

            var mesh = new Mesh { name = "ZoneWallBand128", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1.2f, 2.2f));
            _mesh = mesh;
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _mesh = null;
            _pattern = null;
        }
    }
}
