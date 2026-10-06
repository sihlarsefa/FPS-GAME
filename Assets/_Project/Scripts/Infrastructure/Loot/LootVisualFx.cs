using System.Collections.Generic;
using Project.Core.Domain;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// Yerdeki eşyanın okunabilir görünümü (PUBG tarzı): yakındaki eşyalar nadirlik renginde zemin halkası alır,
    /// Uncommon+ eşyalar 1.2 m'lik yumuşak ışın sütunu alır (30–40 m arasında solar, kalite kademesiyle sınırlı sayıda),
    /// model yavaşça döner ve süzülür, mühimmat/yığın eşyalarda adet rozeti görünür; alınan eşya ölçek patlaması +
    /// kıvılcım bırakır. Tek sürücü, havuzlu nesneler, kare başına bellek ayırmaz. Dedicated sunucuda oluşturulmaz.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LootVisualFx : MonoBehaviour
    {
        private const float ScanInterval = 0.2f;
        private const float ScanRadius = 40f;
        private const float RingRange = 26f;
        private const float BadgeRange = 9f;
        private const float BeamHeight = 1.2f;
        private const int GhostCount = 6;
        private const float GhostDuration = 0.22f;
        private const int CountCache = 1000;

        private struct Entry
        {
            public LootPickupComponent Pickup;
            public float SqrDistance;
        }

        private sealed class EntryComparer : IComparer<Entry>
        {
            public int Compare(Entry a, Entry b) => a.SqrDistance.CompareTo(b.SqrDistance);
        }

        private sealed class Marker
        {
            public Transform Root;
            public MeshRenderer Ring;
            public MeshRenderer Beam;
            public TextMesh Badge;
            public MeshRenderer BadgeRenderer;
            public LootPickupComponent Target;
            public bool BeamAllowed;
            public int BadgeCount = -1;
            public bool Used;
        }

        private sealed class Ghost
        {
            public Transform T;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public float Age = 1f;
            public Vector3 Start;
            public Vector3 Scale;
        }

        private static readonly EntryComparer Comparer = new EntryComparer();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly string[] CountText = new string[CountCache];

        private static LootVisualFx _instance;
        private static Mesh _beamMesh;
        private static Mesh _ringMesh;
        private static Texture2D _beamTexture;
        private static Texture2D _ringTexture;
        private static Material _beamMaterial;
        private static Material _ringMaterial;
        private static Font _font;
        private static bool _fontTried;
        private static MaterialPropertyBlock _block;

        private readonly List<LootPickupComponent> _scan = new List<LootPickupComponent>(128);
        private readonly List<Entry> _near = new List<Entry>(128);
        private readonly List<Marker> _markers = new List<Marker>(48);
        private readonly Ghost[] _ghosts = new Ghost[GhostCount];
        private ParticleSystem _sparks;
        private Camera _camera;
        private float _nextScan;
        private int _ghostCursor;
        private int _tier = 2;

        /// <summary>Sürücünün sahnede olmasını sağlar (grafik aygıtı varken). Tekrarlı çağrı ucuzdur.</summary>
        public static void Ensure()
        {
            if (_instance != null || !UnityEngine.Application.isPlaying || WorldItemVisuals.IsHeadless)
                return;

            var go = new GameObject("[Eşya Görsel Sürücüsü]") { hideFlags = HideFlags.HideInHierarchy };
            _instance = go.AddComponent<LootVisualFx>();
        }

        /// <summary>Alınan eşya için ölçek patlaması + kıvılcım. Bütün alımda model hayaleti de oynar.</summary>
        internal static void PlayPickup(LootPickupComponent pickup, bool wholly)
        {
            if (_instance == null || pickup == null)
                return;

            _instance.Pop(pickup, wholly);
        }

        // ------------------------------------------------------------------ Unity

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                Scan();
            }

            AnimateMarkers(now);
            AnimateGhosts(Time.deltaTime);
        }

        // ------------------------------------------------------------------ Scan

        private void Scan()
        {
            var tier = QualityTierApplier.LastTier;
            _tier = tier < 0 ? 2 : Mathf.Clamp(tier, 0, 3);

            if (_camera == null || !_camera.isActiveAndEnabled)
                _camera = Camera.main;

            _near.Clear();
            if (_camera != null && LootRegistry.Count > 0)
            {
                var camPos = _camera.transform.position;
                _scan.Clear();
                LootRegistry.FindInRadius(camPos, ScanRadius, _scan);
                for (var i = 0; i < _scan.Count; i++)
                {
                    var p = _scan[i];
                    if (p == null || !p.IsAvailable || p.Display == null)
                        continue;

                    _near.Add(new Entry { Pickup = p, SqrDistance = (p.transform.position - camPos).sqrMagnitude });
                }

                _scan.Clear();
                _near.Sort(Comparer);
            }

            var maxMarkers = LootRarityRules.MaxMarkers(_tier);
            var maxBeams = LootRarityRules.MaxBeams(_tier);
            var count = Mathf.Min(_near.Count, maxMarkers);
            var beams = 0;
            for (var i = 0; i < count; i++)
            {
                var marker = GetMarker(i);
                var p = _near[i].Pickup;
                marker.Target = p;
                marker.Used = true;
                var wantBeam = p.Rarity >= LootRarity.Uncommon && beams < maxBeams;
                if (wantBeam)
                    beams++;
                marker.BeamAllowed = wantBeam;
            }

            for (var i = count; i < _markers.Count; i++)
                Hide(_markers[i]);
        }

        private Marker GetMarker(int index)
        {
            while (_markers.Count <= index)
                _markers.Add(CreateMarker(_markers.Count));
            return _markers[index];
        }

        private Marker CreateMarker(int index)
        {
            var rootGo = new GameObject("LootMarker_" + index) { layer = GameLayers.IgnoreRaycast };
            rootGo.transform.SetParent(transform, false);
            var marker = new Marker { Root = rootGo.transform };

            marker.Ring = MakeRenderer("Ring", RingMesh(), RingMaterial(), marker.Root);
            marker.Beam = MakeRenderer("Beam", BeamMesh(), BeamMaterial(), marker.Root);
            rootGo.SetActive(false);
            return marker;
        }

        private static MeshRenderer MakeRenderer(string name, Mesh mesh, Material material, Transform parent)
        {
            var go = new GameObject(name) { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static void Hide(Marker marker)
        {
            marker.Target = null;
            if (marker.Used)
            {
                marker.Used = false;
                marker.Root.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ Animate

        private void AnimateMarkers(float now)
        {
            if (_camera == null)
                return;

            var camPos = _camera.transform.position;
            if (_block == null)
                _block = new MaterialPropertyBlock();

            for (var i = 0; i < _markers.Count; i++)
            {
                var m = _markers[i];
                if (!m.Used)
                    continue;

                var p = m.Target;
                if (p == null || !p.IsAvailable || p.Display == null)
                {
                    Hide(m);
                    continue;
                }

                var t = p.transform;
                var pos = t.position;
                var distance = Vector3.Distance(camPos, pos);
                var fade = LootRarityRules.BeamFade(distance);
                if (fade <= 0f && distance > RingRange)
                {
                    if (m.Root.gameObject.activeSelf)
                        m.Root.gameObject.SetActive(false);
                    continue;
                }

                if (!m.Root.gameObject.activeSelf)
                    m.Root.gameObject.SetActive(true);

                // Model: yavaş dönüş + süzülme (çarpıştırıcı/kayıt köktedir, etkilenmez).
                var phase = p.AnimPhase;
                var rot = Quaternion.AngleAxis(LootRarityRules.Spin(now, phase), Vector3.up);
                var pivot = p.DisplayPivot;
                p.Display.SetLocalPositionAndRotation(pivot - rot * pivot + Vector3.up * LootRarityRules.Bob(now, phase), rot);

                var color = LootRarityRules.ColorOf(p.Rarity);
                m.Root.SetPositionAndRotation(pos, t.rotation);

                // Zemin halkası (nadirlik renginde, hafif nabız).
                var ringOn = distance <= RingRange;
                if (m.Ring.enabled != ringOn)
                    m.Ring.enabled = ringOn;
                if (ringOn)
                {
                    var radius = Mathf.Max(0.3f, p.RingRadius + 0.06f);
                    var ringTf = m.Ring.transform;
                    ringTf.localPosition = new Vector3(0f, 0.03f, 0f);
                    ringTf.localScale = new Vector3(radius, 1f, radius);
                    var pulse = 0.55f + 0.2f * Mathf.Sin(now * 2.2f + phase);
                    var ringFade = Mathf.Clamp01((RingRange - distance) / 6f);
                    var a = pulse * ringFade * (p.Rarity == LootRarity.Common ? 0.55f : 1f);
                    Paint(m.Ring, color, a);
                }

                // Işın sütunu (Uncommon+, kademe sınırlı, uzakta solar).
                var beamOn = m.BeamAllowed && fade > 0.01f;
                if (m.Beam.enabled != beamOn)
                    m.Beam.enabled = beamOn;
                if (beamOn)
                {
                    var beamTf = m.Beam.transform;
                    beamTf.SetPositionAndRotation(pos + Vector3.up * 0.02f, Quaternion.identity);
                    var width = 1f + 0.25f * (int)p.Rarity;
                    beamTf.localScale = new Vector3(width, 1f, width);
                    Paint(m.Beam, color, 0.5f * fade);
                }

                UpdateBadge(m, p, distance, camPos);
            }
        }

        private static void Paint(Renderer renderer, Color color, float alpha)
        {
            var c = new Color(color.r * alpha, color.g * alpha, color.b * alpha, alpha);
            _block.SetColor(BaseColorId, c);
            _block.SetColor(ColorId, c);
            renderer.SetPropertyBlock(_block);
        }

        private void UpdateBadge(Marker m, LootPickupComponent p, float distance, Vector3 camPos)
        {
            var item = p.Item;
            var wanted = (item.Category == ItemCategory.Ammunition || item.Quantity > 1) && distance <= BadgeRange;
            if (!wanted)
            {
                if (m.BadgeRenderer != null && m.BadgeRenderer.enabled)
                    m.BadgeRenderer.enabled = false;
                return;
            }

            if (m.Badge == null && !TryCreateBadge(m))
                return;

            if (!m.BadgeRenderer.enabled)
                m.BadgeRenderer.enabled = true;

            if (m.BadgeCount != item.Quantity)
            {
                m.BadgeCount = item.Quantity;
                m.Badge.text = CountString(item.Quantity);
            }

            var bt = m.Badge.transform;
            var at = p.FocusPoint + Vector3.up * 0.28f;
            bt.position = at;
            var toCam = at - camPos;
            if (toCam.sqrMagnitude > 0.0001f)
                bt.rotation = Quaternion.LookRotation(toCam);
            var s = Mathf.Lerp(1f, 1.6f, Mathf.Clamp01(distance / BadgeRange));
            bt.localScale = new Vector3(s, s, s);
        }

        private bool TryCreateBadge(Marker m)
        {
            var font = BadgeFont();
            if (font == null)
                return false;

            var go = new GameObject("Badge") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(m.Root, false);
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.95f, 0.75f, 1f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            m.Badge = text;
            m.BadgeRenderer = renderer;
            m.BadgeCount = -1;
            return true;
        }

        private static Font BadgeFont()
        {
            if (_fontTried)
                return _font;

            _fontTried = true;
            try
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception)
            {
                _font = null;
            }

            return _font;
        }

        private static string CountString(int quantity)
        {
            if (quantity < 0) quantity = 0;
            if (quantity >= CountCache)
                return "x" + quantity;

            var s = CountText[quantity];
            if (s == null)
                CountText[quantity] = s = "x" + quantity;
            return s;
        }

        // ------------------------------------------------------------------ Pickup pop

        private void Pop(LootPickupComponent pickup, bool wholly)
        {
            var color = LootRarityRules.ColorOf(pickup.Rarity);
            var origin = pickup.FocusPoint;
            Sparkle(origin, color, 8 + 4 * (int)pickup.Rarity);

            var display = pickup.Display;
            var mesh = pickup.DisplayMesh;
            if (!wholly || display == null || mesh == null)
                return;

            var ghost = _ghosts[_ghostCursor];
            _ghostCursor = (_ghostCursor + 1) % GhostCount;
            if (ghost == null)
            {
                var go = new GameObject("LootPopGhost") { layer = GameLayers.IgnoreRaycast };
                go.transform.SetParent(transform, false);
                ghost = new Ghost
                {
                    T = go.transform,
                    Filter = go.AddComponent<MeshFilter>(),
                    Renderer = go.AddComponent<MeshRenderer>()
                };
                ghost.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                ghost.Renderer.receiveShadows = false;
                _ghosts[(_ghostCursor + GhostCount - 1) % GhostCount] = ghost;
            }

            ghost.Filter.sharedMesh = mesh;
            ghost.Renderer.sharedMaterials = pickup.DisplayMaterials;
            ghost.T.SetPositionAndRotation(display.position, display.rotation);
            ghost.Scale = display.lossyScale;
            ghost.T.localScale = ghost.Scale;
            ghost.Start = display.position;
            ghost.Age = 0f;
            ghost.T.gameObject.SetActive(true);
        }

        private void AnimateGhosts(float dt)
        {
            for (var i = 0; i < GhostCount; i++)
            {
                var g = _ghosts[i];
                if (g == null || g.Age >= 1f)
                    continue;

                g.Age += dt / GhostDuration;
                if (g.Age >= 1f)
                {
                    g.T.gameObject.SetActive(false);
                    continue;
                }

                g.T.localScale = g.Scale * LootRarityRules.PopScale(g.Age);
                g.T.position = g.Start + Vector3.up * (g.Age * 0.25f);
            }
        }

        private void Sparkle(Vector3 position, Color color, int count)
        {
            if (_sparks == null)
                _sparks = CreateSparks();
            if (_sparks == null)
                return;

            var emit = new ParticleSystem.EmitParams();
            emit.position = position;
            emit.startColor = color;
            emit.startSize = 0.07f;
            emit.startLifetime = 0.55f;
            for (var i = 0; i < count; i++)
            {
                var v = Random.onUnitSphere;
                v.y = Mathf.Abs(v.y) * 0.8f + 0.35f;
                emit.velocity = v * Random.Range(0.6f, 1.6f);
                _sparks.Emit(emit, 1);
            }
        }

        private ParticleSystem CreateSparks()
        {
            var go = new GameObject("LootSparks") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.gravityModifier = 0.25f;
            main.startLifetime = 0.55f;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialLibrary.ParticleAdditive;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        // ------------------------------------------------------------------ Shared assets

        private static Material BeamMaterial()
        {
            if (_beamMaterial == null)
                _beamMaterial = MaterialLibrary.Particle(BeamTexture(), true);
            return _beamMaterial;
        }

        private static Material RingMaterial()
        {
            if (_ringMaterial == null)
                _ringMaterial = MaterialLibrary.Particle(RingTexture(), true);
            return _ringMaterial;
        }

        /// <summary>Dikey alfa rampası: tabanda yumuşak yoğun, tepede sıfır.</summary>
        private static Texture2D BeamTexture()
        {
            if (_beamTexture != null)
                return _beamTexture;

            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false, true) { name = "LootBeamRamp", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[h];
            for (var y = 0; y < h; y++)
            {
                var v = y / (h - 1f);
                var a = Mathf.Pow(1f - v, 1.6f) * Mathf.Clamp01(v * 14f + 0.35f);
                var b = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
                px[y] = new Color32(255, 255, 255, b);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            _beamTexture = tex;
            return tex;
        }

        /// <summary>Halka genişliği boyunca yumuşak profil (içte sönük, dış kenara doğru parlak, kenarda sıfır).</summary>
        private static Texture2D RingTexture()
        {
            if (_ringTexture != null)
                return _ringTexture;

            const int w = 64;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false, true) { name = "LootRingProfile", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w];
            for (var x = 0; x < w; x++)
            {
                var u = x / (w - 1f);
                var a = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                a = Mathf.Pow(a, 0.8f) * (0.45f + 0.55f * u);
                px[x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            _ringTexture = tex;
            return tex;
        }

        /// <summary>Çift yüzlü açık koni sütun: UV.y 0→1 (taban→tepe), yarıçap 0.13 → 0.07, yükseklik 1.2 m.</summary>
        private static Mesh BeamMesh()
        {
            if (_beamMesh != null)
                return _beamMesh;

            const int seg = 10;
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 12];
            for (var i = 0; i <= seg; i++)
            {
                var ang = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts[i * 2] = dir * 0.13f;
                verts[i * 2 + 1] = dir * 0.07f + Vector3.up * BeamHeight;
                uvs[i * 2] = new Vector2(0.5f, 0f);
                uvs[i * 2 + 1] = new Vector2(0.5f, 1f);
            }

            var t = 0;
            for (var i = 0; i < seg; i++)
            {
                var b0 = i * 2;
                var t0 = b0 + 1;
                var b1 = b0 + 2;
                var t1 = b0 + 3;
                tris[t++] = b0; tris[t++] = t0; tris[t++] = b1;
                tris[t++] = b1; tris[t++] = t0; tris[t++] = t1;
                tris[t++] = b0; tris[t++] = b1; tris[t++] = t0;
                tris[t++] = b1; tris[t++] = t1; tris[t++] = t0;
            }

            _beamMesh = new Mesh { name = "LootBeam", vertices = verts, uv = uvs, triangles = tris };
            _beamMesh.RecalculateBounds();
            return _beamMesh;
        }

        /// <summary>Birim yarıçaplı düz halka (iç 0.78, dış 1.0); UV.x genişlik boyunca 0→1.</summary>
        private static Mesh RingMesh()
        {
            if (_ringMesh != null)
                return _ringMesh;

            const int seg = 40;
            var verts = new Vector3[(seg + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 12];
            for (var i = 0; i <= seg; i++)
            {
                var ang = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                verts[i * 2] = dir * 0.78f;
                verts[i * 2 + 1] = dir;
                uvs[i * 2] = new Vector2(0f, 0.5f);
                uvs[i * 2 + 1] = new Vector2(1f, 0.5f);
            }

            var t = 0;
            for (var i = 0; i < seg; i++)
            {
                var i0 = i * 2;
                var o0 = i0 + 1;
                var i1 = i0 + 2;
                var o1 = i0 + 3;
                tris[t++] = i0; tris[t++] = o0; tris[t++] = i1;
                tris[t++] = i1; tris[t++] = o0; tris[t++] = o1;
                tris[t++] = i0; tris[t++] = i1; tris[t++] = o0;
                tris[t++] = i1; tris[t++] = o1; tris[t++] = o0;
            }

            _ringMesh = new Mesh { name = "LootRing", vertices = verts, uv = uvs, triangles = tris };
            _ringMesh.RecalculateBounds();
            return _ringMesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _beamMesh = null;
            _ringMesh = null;
            _beamTexture = null;
            _ringTexture = null;
            _beamMaterial = null;
            _ringMaterial = null;
            _block = null;
            _fontTried = false;
            _font = null;
        }
    }
}
