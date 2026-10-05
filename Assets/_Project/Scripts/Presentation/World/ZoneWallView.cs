using System;
using Project.Core.Domain;
using Project.Core.Interfaces;
using Project.Infrastructure;
using Project.Infrastructure.Rendering;
using Project.Infrastructure.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Presentation.World
{
    /// <summary>
    /// Harekât alanı sınırının dünyadaki görünümü: <see cref="IZoneService.CurrentZone"/>'u izleyen, yarı saydam, çift yüzlü
    /// yüksek silindir duvar (ZoneWall malzemesi) ve bir sonraki güvenli bölgeyi arazi üzerinde gösteren beyaz zemin halkası.
    /// Duvar her karede yalnızca dönüşümü günceller (GC yok); halka yalnızca sonraki bölge değişince yeniden örneklenir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ZoneWallView : MonoBehaviour
    {
        public const int WallSegments = 128;
        public const int RingPoints = 192;
        public const float WallBottom = -80f;
        public const float WallHeight = 520f;
        public const float RingWidth = 1.6f;
        public const float RingLift = 0.6f;

        private static readonly Color FallbackWallColor = new(0.18f, 0.45f, 1f, 0.28f);
        private static readonly Color RingColor = new(1f, 1f, 1f, 0.9f);

        private static Mesh _sharedWallMesh;

        private IZoneService _zone;
        private WorldMetadata _world;
        private Transform _wall;
        private MeshRenderer _wallRenderer;
        private LineRenderer _ring;
        private readonly Vector3[] _ringPositions = new Vector3[RingPoints];

        private float _lastCenterX = float.NaN;
        private float _lastCenterZ = float.NaN;
        private float _lastRadius = float.NaN;
        private float _ringCenterX = float.NaN;
        private float _ringCenterZ = float.NaN;
        private float _ringRadius = float.NaN;

        /// <summary>Bölge servisi (null ise GameContext'ten çözülür).</summary>
        public IZoneService Zone => _zone;

        /// <summary>Duvar görünür mü (bölge yoksa gizlenir).</summary>
        public bool WallVisible => _wallRenderer != null && _wallRenderer.enabled;

        /// <summary>Sonraki bölge halkası görünür mü.</summary>
        public bool RingVisible => _ring != null && _ring.enabled;

        public static ZoneWallView Create(IZoneService zone, WorldMetadata world = null, Transform parent = null)
        {
            var go = new GameObject("[Harekât Alanı Sınırı]");
            if (parent != null)
                go.transform.SetParent(parent, false);

            var view = go.AddComponent<ZoneWallView>();
            view.Initialize(zone, world);
            return view;
        }

        public void Initialize(IZoneService zone, WorldMetadata world)
        {
            _zone = zone;
            _world = world;
            EnsureVisuals();
            _lastRadius = float.NaN;
            _ringRadius = float.NaN;
            Refresh();
        }

        private void Awake()
        {
            EnsureVisuals();
        }

        private void LateUpdate()
        {
            if (_zone == null && !GameContext.TryGet(out _zone))
            {
                SetWallVisible(false);
                SetRingVisible(false);
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            if (_zone == null || _wall == null)
                return;

            var current = _zone.CurrentZone;
            if (!IsFinite(current.Radius) || current.Radius <= 0.01f)
            {
                SetWallVisible(false);
            }
            else
            {
                SetWallVisible(true);
                if (current.CenterX != _lastCenterX || current.CenterZ != _lastCenterZ || current.Radius != _lastRadius)
                {
                    _lastCenterX = current.CenterX;
                    _lastCenterZ = current.CenterZ;
                    _lastRadius = current.Radius;
                    _wall.localPosition = new Vector3(current.CenterX, WallBottom, current.CenterZ);
                    _wall.localScale = new Vector3(current.Radius, WallHeight, current.Radius);
                }
            }

            UpdateRing(current);
        }

        private void UpdateRing(ZoneState current)
        {
            if (_ring == null)
                return;

            var stage = _zone.Stage;
            var showRing = stage == ZoneStage.Waiting || stage == ZoneStage.Shrinking;
            if (!showRing)
            {
                SetRingVisible(false);
                return;
            }

            var next = _zone.NextZone;
            if (!IsFinite(next.Radius) || next.Radius < 0.5f || next.Radius >= current.Radius - 0.5f)
            {
                SetRingVisible(false);
                return;
            }

            if (next.CenterX != _ringCenterX || next.CenterZ != _ringCenterZ || next.Radius != _ringRadius)
            {
                _ringCenterX = next.CenterX;
                _ringCenterZ = next.CenterZ;
                _ringRadius = next.Radius;
                BuildRing(next);
            }

            SetRingVisible(true);
        }

        private void BuildRing(ZoneState next)
        {
            var step = Mathf.PI * 2f / RingPoints;
            for (var i = 0; i < RingPoints; i++)
            {
                var angle = i * step;
                var x = next.CenterX + Mathf.Cos(angle) * next.Radius;
                var z = next.CenterZ + Mathf.Sin(angle) * next.Radius;
                _ringPositions[i] = new Vector3(x, SampleGround(x, z) + RingLift, z);
            }

            _ring.positionCount = RingPoints;
            _ring.SetPositions(_ringPositions);
            // Halka büyükken çizgiyi biraz kalınlaştır (uzaktan görünür kalsın).
            var width = Mathf.Lerp(RingWidth, RingWidth * 2.5f, Mathf.InverseLerp(50f, 450f, next.Radius));
            _ring.widthMultiplier = width;
        }

        private float SampleGround(float x, float z)
        {
            var position = new Vector3(x, 0f, z);
            if (_world != null || (_world = WorldMetadata.Instance) != null)
            {
                try
                {
                    return _world.SampleGroundHeight(position);
                }
                catch (Exception)
                {
                    // Aşağıdaki yedeklere düş.
                }
            }

            var terrain = Terrain.activeTerrain;
            if (terrain != null && terrain.terrainData != null)
                return terrain.SampleHeight(position) + terrain.transform.position.y;

            if (Physics.Raycast(new Vector3(x, 1500f, z), Vector3.down, out var hit, 3000f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return 0f;
        }

        private void EnsureVisuals()
        {
            if (_wall != null)
                return;

            var wallGo = new GameObject("Duvar");
            wallGo.layer = GameLayers.IgnoreRaycast;
            wallGo.transform.SetParent(transform, false);
            _wall = wallGo.transform;

            var filter = wallGo.AddComponent<MeshFilter>();
            filter.sharedMesh = GetWallMesh();

            _wallRenderer = wallGo.AddComponent<MeshRenderer>();
            _wallRenderer.sharedMaterial = ResolveWallMaterial();
            _wallRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _wallRenderer.receiveShadows = false;
            _wallRenderer.lightProbeUsage = LightProbeUsage.Off;
            _wallRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _wallRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _wallRenderer.enabled = false;

            var ringGo = new GameObject("Sonraki Bölge Halkası");
            ringGo.layer = GameLayers.IgnoreRaycast;
            ringGo.transform.SetParent(transform, false);
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.useWorldSpace = true;
            _ring.loop = true;
            _ring.positionCount = 0;
            _ring.widthMultiplier = RingWidth;
            _ring.numCornerVertices = 0;
            _ring.numCapVertices = 0;
            _ring.alignment = LineAlignment.View;
            _ring.textureMode = LineTextureMode.Stretch;
            _ring.shadowCastingMode = ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.startColor = RingColor;
            _ring.endColor = RingColor;
            _ring.sharedMaterial = ResolveRingMaterial();
            _ring.enabled = false;
        }

        private void SetWallVisible(bool visible)
        {
            if (_wallRenderer != null && _wallRenderer.enabled != visible)
                _wallRenderer.enabled = visible;
        }

        private void SetRingVisible(bool visible)
        {
            if (_ring != null && _ring.enabled != visible)
                _ring.enabled = visible;
        }

        private static Material ResolveWallMaterial()
        {
            try
            {
                var material = MaterialLibrary.Get(MaterialId.ZoneWall);
                if (material != null)
                    return material;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ZoneWallView] ZoneWall malzemesi alınamadı: " + e.Message);
            }

            try
            {
                var material = MaterialLibrary.Transparent(FallbackWallColor, true);
                if (material != null)
                    return material;
            }
            catch (Exception)
            {
                // Son yedek aşağıda.
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) { color = FallbackWallColor } : null;
        }

        private static Material ResolveRingMaterial()
        {
            try
            {
                var material = MaterialLibrary.Unlit(RingColor);
                if (material != null)
                    return material;
            }
            catch (Exception)
            {
                // Yedek aşağıda.
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            return shader != null ? new Material(shader) { color = RingColor } : null;
        }

        /// <summary>
        /// Birim yarıçaplı, 0..1 yükseklikte, kapaksız ve çift yüzlü silindir (dış yüz dışarı, iç yüz içeri bakar).
        /// UV: u çevre boyunca 64 tekrar, v yükseklik boyunca 10 tekrar.
        /// </summary>
        public static Mesh GetWallMesh()
        {
            if (_sharedWallMesh != null)
                return _sharedWallMesh;

            const int segments = WallSegments;
            var columns = segments + 1;
            var vertices = new Vector3[columns * 4];
            var normals = new Vector3[columns * 4];
            var uvs = new Vector2[columns * 4];
            var triangles = new int[segments * 12];

            for (var i = 0; i < columns; i++)
            {
                var t = (float)i / segments;
                var angle = t * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var u = t * 64f;

                // Dış yüz (0: alt, 1: üst)
                vertices[i * 4 + 0] = dir;
                vertices[i * 4 + 1] = dir + Vector3.up;
                normals[i * 4 + 0] = dir;
                normals[i * 4 + 1] = dir;
                uvs[i * 4 + 0] = new Vector2(u, 0f);
                uvs[i * 4 + 1] = new Vector2(u, 10f);

                // İç yüz (2: alt, 3: üst)
                vertices[i * 4 + 2] = dir;
                vertices[i * 4 + 3] = dir + Vector3.up;
                normals[i * 4 + 2] = -dir;
                normals[i * 4 + 3] = -dir;
                uvs[i * 4 + 2] = new Vector2(u, 0f);
                uvs[i * 4 + 3] = new Vector2(u, 10f);
            }

            var tri = 0;
            for (var i = 0; i < segments; i++)
            {
                var a = i * 4;
                var b = (i + 1) * 4;

                // Dış yüz: dışarıdan bakınca saat yönünde.
                triangles[tri++] = a + 0;
                triangles[tri++] = a + 1;
                triangles[tri++] = b + 0;
                triangles[tri++] = b + 0;
                triangles[tri++] = a + 1;
                triangles[tri++] = b + 1;

                // İç yüz: ters sargı.
                triangles[tri++] = a + 2;
                triangles[tri++] = b + 2;
                triangles[tri++] = a + 3;
                triangles[tri++] = b + 2;
                triangles[tri++] = b + 3;
                triangles[tri++] = a + 3;
            }

            var mesh = new Mesh { name = "ZoneWallCylinder" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            // Büyük ölçekte kamera duvarın içinde kalsa da kırpılmasın.
            mesh.bounds = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1.2f, 2.2f));
            _sharedWallMesh = mesh;
            return mesh;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _sharedWallMesh = null;
        }
    }
}
