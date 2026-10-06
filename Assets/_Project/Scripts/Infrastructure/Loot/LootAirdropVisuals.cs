using System.Collections.Generic;
using Project.Core.Events;
using Project.Core.Interfaces;
using Project.Infrastructure.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Loot
{
    /// <summary>
    /// İkmal sandığının görseli (AirdropEvent'e abone): Announced'da paraşütle süzülen sandık, Landed'da yerde kırmızı
    /// duman + yanıp sönen strobe, Opened'da kapak açık; açıldıktan ~90 sn sonra kaldırılır. Konumun Y'si 0 geldiği için
    /// zemin ışınla bulunur. Yağma düşürme mantığı <see cref="MatchFlowLoot"/>'tadır; burada yalnızca görsel vardır.
    /// Dedicated sunucuda çalışmaz. Olay yolu (IEventBus) hazır olunca kendini abone eder.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class LootAirdropVisuals : MonoBehaviour
    {
        private const float StartHeight = 150f;
        private const float ChuteHeight = 3.6f;
        private const float LingerAfterOpen = 90f;
        private const float SmokeAfterOpen = 60f;
        private const float GroundProbe = 400f;

        private sealed class Drop
        {
            public int Id;
            public AirdropStage Stage;
            public Vector3 Ground;
            public Transform Root;
            public Transform Chute;
            public Transform Lid;
            public ParticleSystem Smoke;
            public Light Strobe;
            public Renderer Bulb;
            public float DescentTotal;
            public float DescentEnd;
            public float OpenedAt;
            public float Phase;
        }

        private static LootAirdropVisuals _instance;
        private static Material _crateMat, _bandMat, _chuteMat, _cordMat, _bulbMat;

        private readonly Dictionary<int, Drop> _drops = new Dictionary<int, Drop>(4);
        private readonly List<int> _expired = new List<int>(2);
        private System.Action<AirdropEvent> _handler;
        private IEventBus _bus;
        private float _nextBind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null || WorldItemVisuals.IsHeadless || !UnityEngine.Application.isPlaying)
                return;

            var go = new GameObject("[İkmal Görselleri]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<LootAirdropVisuals>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            _handler = OnAirdrop;
        }

        private void OnDestroy()
        {
            Unbind();
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextBind)
            {
                _nextBind = now + 1f;
                TryBind();
            }

            if (_drops.Count == 0)
                return;

            _expired.Clear();
            foreach (var pair in _drops)
            {
                if (!Animate(pair.Value, now))
                    _expired.Add(pair.Key);
            }

            for (var i = 0; i < _expired.Count; i++)
            {
                Remove(_expired[i]);
            }
        }

        // ------------------------------------------------------------------ Events

        private void TryBind()
        {
            IEventBus bus = null;
            if (GameContext.IsReady)
                GameContext.TryGet(out bus);

            if (ReferenceEquals(bus, _bus))
                return;

            Unbind();
            _bus = bus;
            if (_bus != null)
                _bus.Subscribe(_handler);
        }

        private void Unbind()
        {
            if (_bus == null)
                return;

            try
            {
                _bus.Unsubscribe(_handler);
            }
            catch (System.Exception)
            {
                // veri yolu kapanmış olabilir
            }

            _bus = null;
        }

        private void OnAirdrop(AirdropEvent e)
        {
            try
            {
                if (!_drops.TryGetValue(e.Id, out var drop))
                {
                    drop = Create(e);
                    if (drop == null)
                        return;
                    _drops[e.Id] = drop;
                }

                var now = Time.time;
                drop.Stage = e.Stage;
                switch (e.Stage)
                {
                    case AirdropStage.Announced:
                        drop.DescentTotal = Mathf.Max(1f, e.SecondsToNextStage);
                        drop.DescentEnd = now + drop.DescentTotal;
                        break;
                    case AirdropStage.Landed:
                        drop.DescentEnd = now;
                        Land(drop);
                        break;
                    case AirdropStage.Opened:
                        drop.DescentEnd = now;
                        Land(drop);
                        drop.OpenedAt = now;
                        if (drop.Lid != null)
                            drop.Lid.localRotation = Quaternion.Euler(-68f, 0f, 0f);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        private static void Land(Drop drop)
        {
            if (drop.Chute != null)
                drop.Chute.gameObject.SetActive(false);
            if (drop.Root != null)
                drop.Root.position = drop.Ground;
            if (drop.Smoke != null && !drop.Smoke.isEmitting)
                drop.Smoke.Play();
        }

        // ------------------------------------------------------------------ Build

        private Drop Create(AirdropEvent e)
        {
            var ground = new Vector3(e.Position.X, 0f, e.Position.Z);
            var origin = new Vector3(ground.x, ground.y + GroundProbe, ground.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, GroundProbe * 2f, GameLayers.GroundMask,
                    QueryTriggerInteraction.Ignore))
                ground = hit.point;
            else
                ground.y = e.Position.Y;

            var root = new GameObject("Airdrop_" + e.Id) { layer = GameLayers.IgnoreRaycast };
            root.transform.SetParent(transform, false);
            root.transform.position = ground + Vector3.up * StartHeight;
            var drop = new Drop { Id = e.Id, Ground = ground, Root = root.transform, Phase = e.Id * 1.7f, Stage = e.Stage };

            BuildCrate(drop);
            BuildChute(drop);
            BuildStrobe(drop);
            BuildSmoke(drop);
            return drop;
        }

        private static void EnsureMaterials()
        {
            if (_crateMat != null)
                return;

            _crateMat = MaterialLibrary.Lit(new Color(0.26f, 0.30f, 0.18f), 0.25f, 0.1f);
            _bandMat = MaterialLibrary.Lit(new Color(0.70f, 0.08f, 0.07f), 0.3f);
            _chuteMat = MaterialLibrary.Lit(new Color(0.88f, 0.34f, 0.10f), 0.15f);
            _cordMat = MaterialLibrary.Lit(new Color(0.12f, 0.12f, 0.10f), 0.1f);
            _bulbMat = MaterialLibrary.Unlit(new Color(1f, 0.15f, 0.1f));
        }

        private static void BuildCrate(Drop drop)
        {
            EnsureMaterials();
            var b = new LootMeshBuilder();
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.3f, 0.9f, 1.0f), _crateMat);
            b.Box(new Vector3(0f, 0.45f, 0f), new Vector3(1.34f, 0.12f, 1.04f), _bandMat);
            b.Box(new Vector3(-0.45f, 0.45f, 0f), new Vector3(0.1f, 0.94f, 1.04f), _bandMat);
            b.Box(new Vector3(0.45f, 0.45f, 0f), new Vector3(0.1f, 0.94f, 1.04f), _bandMat);
            var mesh = b.Build("AirdropCrate", out var mats);
            var go = new GameObject("Crate") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(drop.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;

            // Kapak: arka kenardan menteşeli.
            var lid = new GameObject("Lid") { layer = GameLayers.IgnoreRaycast };
            lid.transform.SetParent(drop.Root, false);
            lid.transform.localPosition = new Vector3(0f, 0.92f, -0.5f);
            b.Clear();
            b.Box(new Vector3(0f, 0.03f, 0.5f), new Vector3(1.34f, 0.06f, 1.04f), _crateMat);
            var lidMesh = b.Build("AirdropLid", out var lidMats);
            lid.AddComponent<MeshFilter>().sharedMesh = lidMesh;
            lid.AddComponent<MeshRenderer>().sharedMaterials = lidMats;
            drop.Lid = lid.transform;
        }

        private static void BuildChute(Drop drop)
        {
            var go = new GameObject("Parachute") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(drop.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = ChuteMesh(out var cordTop);
            var canopy = go.AddComponent<MeshRenderer>();
            canopy.sharedMaterial = _chuteMat;
            canopy.shadowCastingMode = ShadowCastingMode.Off;

            // 4 ip: sandık köşelerinden kubbe kenarına.
            var b = new LootMeshBuilder();
            for (var i = 0; i < 4; i++)
            {
                var sx = (i & 1) == 0 ? -1f : 1f;
                var sz = (i & 2) == 0 ? -1f : 1f;
                var from = new Vector3(sx * 0.6f, 0.92f, sz * 0.45f);
                var to = cordTop + new Vector3(sx * 1.7f, 0f, sz * 1.7f);
                var d = to - from;
                b.Box((from + to) * 0.5f, new Vector3(0.02f, 0.02f, d.magnitude), Quaternion.LookRotation(d), _cordMat);
            }

            var cords = b.Build("AirdropCords", out var mats);
            var cg = new GameObject("Cords") { layer = GameLayers.IgnoreRaycast };
            cg.transform.SetParent(go.transform, false);
            cg.AddComponent<MeshFilter>().sharedMesh = cords;
            var cr = cg.AddComponent<MeshRenderer>();
            cr.sharedMaterials = mats;
            cr.shadowCastingMode = ShadowCastingMode.Off;
            drop.Chute = go.transform;
        }

        /// <summary>Çift yüzlü kubbe (yarıçap 2.6 m, yükseklik 1.5 m); tabanı sandığın ~3.6 m üstünde.</summary>
        private static Mesh ChuteMesh(out Vector3 rimCenter)
        {
            const int seg = 14, rings = 4;
            const float radius = 2.6f, height = 1.5f;
            rimCenter = new Vector3(0f, ChuteHeight, 0f);
            var verts = new Vector3[(rings + 1) * (seg + 1)];
            var tris = new int[rings * seg * 12];
            for (var r = 0; r <= rings; r++)
            {
                var lat = r / (float)rings * (Mathf.PI * 0.5f);
                var ring = Mathf.Cos(lat) * radius;
                var y = Mathf.Sin(lat) * height + ChuteHeight;
                for (var s = 0; s <= seg; s++)
                {
                    var a = s * Mathf.PI * 2f / seg;
                    verts[r * (seg + 1) + s] = new Vector3(Mathf.Cos(a) * ring, y, Mathf.Sin(a) * ring);
                }
            }

            var t = 0;
            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < seg; s++)
                {
                    var a = r * (seg + 1) + s;
                    var b = a + 1;
                    var c = a + seg + 1;
                    var d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                    tris[t++] = a; tris[t++] = b; tris[t++] = c;
                    tris[t++] = b; tris[t++] = d; tris[t++] = c;
                }
            }

            var mesh = new Mesh { name = "AirdropChute", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildStrobe(Drop drop)
        {
            var go = new GameObject("Strobe") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(drop.Root, false);
            go.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.12f, 0.08f);
            light.range = 14f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            drop.Strobe = light;

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.layer = GameLayers.IgnoreRaycast;
            var col = bulb.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            bulb.transform.SetParent(go.transform, false);
            bulb.transform.localScale = Vector3.one * 0.16f;
            var br = bulb.GetComponent<Renderer>();
            br.sharedMaterial = _bulbMat;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.enabled = false;
            drop.Bulb = br;
        }

        private static void BuildSmoke(Drop drop)
        {
            var go = new GameObject("RedSmoke") { layer = GameLayers.IgnoreRaycast };
            go.transform.SetParent(drop.Root, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 9f;
            main.startSpeed = 2.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.startColor = new Color(0.86f, 0.10f, 0.07f, 0.6f);
            main.gravityModifier = -0.02f;
            main.maxParticles = 140;
            var emission = ps.emission;
            emission.rateOverTime = 14f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.15f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 3.2f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.9f, 0.5f, 0.45f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialLibrary.ParticleAlpha;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            drop.Smoke = ps;
        }

        // ------------------------------------------------------------------ Animate

        private static bool Animate(Drop d, float now)
        {
            if (d.Root == null)
                return false;

            if (d.Stage == AirdropStage.Announced)
            {
                var left = Mathf.Max(0f, d.DescentEnd - now);
                var h = LootRarityRules.DescentHeight(StartHeight, left, d.DescentTotal);
                var sway = Mathf.Sin(now * 0.9f + d.Phase);
                d.Root.position = d.Ground + new Vector3(sway * 1.2f, h, Mathf.Cos(now * 0.7f + d.Phase) * 0.8f);
                d.Root.rotation = Quaternion.Euler(sway * 4f, now * 8f, 0f);
                if (left <= 0f)
                    d.Root.position = d.Ground;
            }
            else
            {
                d.Root.SetPositionAndRotation(d.Ground, d.Root.rotation);
            }

            var on = LootRarityRules.StrobeOn(now + d.Phase, 1f, 0.14f);
            if (d.Strobe != null && d.Strobe.enabled != on)
                d.Strobe.enabled = on;
            if (d.Bulb != null && d.Bulb.enabled != on)
                d.Bulb.enabled = on;

            if (d.Stage == AirdropStage.Opened)
            {
                var since = now - d.OpenedAt;
                if (since > SmokeAfterOpen && d.Smoke != null && d.Smoke.isEmitting)
                    d.Smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (since > LingerAfterOpen)
                    return false;
            }

            return true;
        }

        private void Remove(int id)
        {
            if (_drops.TryGetValue(id, out var drop) && drop.Root != null)
                Destroy(drop.Root.gameObject);
            _drops.Remove(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _crateMat = _bandMat = _chuteMat = _cordMat = _bulbMat = null;
        }
    }
}
