using System.Collections.Generic;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Vehicles;
using UnityEngine;

namespace Project.Infrastructure.Vfx
{
    /// <summary>Ayak izi / lastik izi kuralları (saf mantık, Unity gerektirmez; testlenebilir).</summary>
    public static class FootprintRules
    {
        public const float FootLifetime = 60f;
        public const float FadeTime = 20f;
        public const float StepSpacing = 0.85f;
        public const float FootSideOffset = 0.11f;
        public const float TireSpacing = 0.9f;
        public const float MinTireSpeed = 1.5f;
        public const float MaxCullDistance = 60f;
        public const int MaxCapacity = 256;

        public static bool IsTrackSurface(SurfaceKind kind) => kind == SurfaceKind.Snow || kind == SurfaceKind.Dirt;

        public static int CapacityFor(VfxTier tier)
        {
            switch (tier)
            {
                case VfxTier.Low: return 64;
                case VfxTier.Medium: return 160;
                default: return MaxCapacity;
            }
        }

        public static bool VehicleTracksEnabled(VfxTier tier) => tier != VfxTier.Low;

        /// <summary>Katedilen mesafe birikir; adım aralığı dolunca true döner ve birikim düşer.</summary>
        public static bool Advance(ref float accumulated, float distance, float spacing)
        {
            if (distance <= 0f || float.IsNaN(distance) || spacing <= 0.01f)
                return false;

            accumulated += distance;
            if (accumulated < spacing)
                return false;

            accumulated -= spacing;
            if (accumulated > spacing)
                accumulated = 0f; // büyük sıçrama: birden fazla iz basma
            return true;
        }

        /// <summary>Yaşa göre alfa çarpanı: son <see cref="FadeTime"/> içinde doğrusal solar; ömürden sonra 0.</summary>
        public static float AlphaAt(float age)
        {
            if (age < 0f) return 1f;
            if (age >= FootLifetime) return 0f;
            var left = FootLifetime - age;
            return left >= FadeTime ? 1f : left / FadeTime;
        }

        /// <summary>Sol/sağ ayak için yan kayma (+ sağ, - sol).</summary>
        public static float SideOffset(bool leftFoot) => leftFoot ? -FootSideOffset : FootSideOffset;
    }

    /// <summary>Halka tamponlu iz zaman çizelgesi; DecalPool'un yuva sırasını yansıtır (doluyken en eski ezilir).</summary>
    public sealed class FootprintLedger
    {
        private readonly float[] _born;
        private int _cursor;
        private int _active;

        public FootprintLedger(int capacity)
        {
            Capacity = Mathf.Max(1, capacity);
            _born = new float[Capacity];
            for (var i = 0; i < Capacity; i++)
                _born[i] = -1f;
        }

        public int Capacity { get; }
        public int Active => _active;
        public int NextSlot => _cursor;

        public int Add(float now)
        {
            var slot = _cursor;
            _cursor = (_cursor + 1) % Capacity;
            if (_born[slot] < 0f)
                _active++;
            _born[slot] = now;
            return slot;
        }

        public bool IsActive(int slot) => slot >= 0 && slot < Capacity && _born[slot] >= 0f;

        public float Age(int slot, float now) => IsActive(slot) ? now - _born[slot] : -1f;

        public void Release(int slot)
        {
            if (!IsActive(slot))
                return;
            _born[slot] = -1f;
            _active--;
        }

        public void Clear()
        {
            for (var i = 0; i < Capacity; i++)
                _born[i] = -1f;
            _cursor = 0;
            _active = 0;
        }
    }

    /// <summary>
    /// Kar/çamur (Snow, Dirt) üzerinde oyuncu + botların bıraktığı dönüşümlü bot izleri (60 sn'de solar) ve araçların
    /// lastik izleri. <see cref="FootstepEmitter"/> taşıyan herkes izlenir (yalnızca <c>IsGrounded/Muted</c> okunur) ve
    /// araçlar <see cref="DrivableVehicle"/> yapılandırmasından tekerlek noktaları hesaplanarak bulunur; başka dosyaya
    /// kanca gerekmez. Tek havuz (en fazla 256, kademeye göre 64/160/256); düşük kademede lastik izi yok.
    /// Kendini otomatik kurar (sahne yüklenince); başsız sunucuda (<see cref="GameAudio.Enabled"/> false) çalışmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FootprintTrail : MonoBehaviour
    {
        private const float ScanInterval = 2f;
        private const float FadeTickInterval = 1f;
        private const float BootSize = 0.42f;

        private sealed class Walker
        {
            public Vector3 Last;
            public bool HasLast;
            public float Accumulated;
            public bool Left;
        }

        private sealed class Wheeler
        {
            public Vector3[] Last = new Vector3[4];
            public bool HasLast;
            public float Accumulated;
        }

        private static FootprintTrail _instance;

        private readonly Dictionary<FootstepEmitter, Walker> _walkers = new Dictionary<FootstepEmitter, Walker>();
        private readonly Dictionary<DrivableVehicle, Wheeler> _vehicles = new Dictionary<DrivableVehicle, Wheeler>();
        private readonly List<FootstepEmitter> _deadWalkers = new List<FootstepEmitter>();
        private readonly List<DrivableVehicle> _deadVehicles = new List<DrivableVehicle>();
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly RaycastHit[] _hits = new RaycastHit[6];
        private DecalPool _pool;
        private FootprintLedger _ledger;
        private Material _bootMaterial;
        private Material _tireMaterial;
        private Texture2D _bootTexture;
        private Texture2D _tireTexture;
        private readonly Vector3[] _wheelScratch = new Vector3[4];
        private readonly Vector3[] _prevScratch = new Vector3[4];
        private float _nextScan;
        private float _nextFade;
        private bool _tires;
        private Camera _camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static FootprintTrail Ensure()
        {
            if (_instance != null)
                return _instance;
            try
            {
                if (!GameAudio.Enabled)
                    return null;
            }
            catch
            {
                return null;
            }

            var go = new GameObject("FootprintTrail");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FootprintTrail>();
            return _instance;
        }

        public int ActiveCount => _ledger != null ? _ledger.Active : 0;

        private bool EnsurePool()
        {
            if (_pool != null)
                return true;

            try
            {
                var tier = VfxQuality.Tier;
                _tires = FootprintRules.VehicleTracksEnabled(tier);
                var cap = FootprintRules.CapacityFor(tier);
                _pool = new DecalPool("Footprint", transform, cap);
                _ledger = new FootprintLedger(cap);
                _bootTexture = BuildBootTexture();
                _tireTexture = BuildTireTexture();
                _bootMaterial = VfxMaterials.CreateDecal("VFX_Boot", _bootTexture);
                _tireMaterial = VfxMaterials.CreateDecal("VFX_Tire", _tireTexture);
                return _bootMaterial != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[FootprintTrail] Kurulamadı: " + e.Message);
                _pool = null;
                return false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_bootTexture != null) Destroy(_bootTexture);
            if (_tireTexture != null) Destroy(_tireTexture);
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                Scan();
            }

            if (_walkers.Count == 0 && _vehicles.Count == 0)
                return;
            if (!EnsurePool())
                return;

            if (_camera == null)
                _camera = Camera.main;
            var camPos = _camera != null ? _camera.transform.position : Vector3.zero;
            var hasCam = _camera != null;

            foreach (var pair in _walkers)
                TickWalker(pair.Key, pair.Value, now, hasCam, camPos);

            if (_tires)
            {
                foreach (var pair in _vehicles)
                    TickVehicle(pair.Key, pair.Value, now, hasCam, camPos);
            }

            if (now >= _nextFade)
            {
                _nextFade = now + FadeTickInterval;
                Fade(now);
            }
        }

        private void Scan()
        {
            _deadWalkers.Clear();
            foreach (var key in _walkers.Keys)
                if (key == null) _deadWalkers.Add(key);
            foreach (var key in _deadWalkers) _walkers.Remove(key);

            _deadVehicles.Clear();
            foreach (var key in _vehicles.Keys)
                if (key == null) _deadVehicles.Add(key);
            foreach (var key in _deadVehicles) _vehicles.Remove(key);

            foreach (var e in FindObjectsByType<FootstepEmitter>(FindObjectsInactive.Exclude))
                if (!_walkers.ContainsKey(e))
                    _walkers[e] = new Walker();

            foreach (var v in FindObjectsByType<DrivableVehicle>(FindObjectsInactive.Exclude))
                if (!_vehicles.ContainsKey(v))
                    _vehicles[v] = new Wheeler();
        }

        private void TickWalker(FootstepEmitter emitter, Walker w, float now, bool hasCam, Vector3 camPos)
        {
            if (emitter == null)
                return;

            var pos = emitter.transform.position;
            if (!w.HasLast)
            {
                w.Last = pos;
                w.HasLast = true;
                return;
            }

            var dx = pos.x - w.Last.x;
            var dz = pos.z - w.Last.z;
            w.Last = pos;
            var dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist > 3f || emitter.Muted || !emitter.IsGrounded)
            {
                w.Accumulated = 0f;
                return;
            }

            if (hasCam && (pos - camPos).sqrMagnitude > FootprintRules.MaxCullDistance * FootprintRules.MaxCullDistance)
                return;

            if (!FootprintRules.Advance(ref w.Accumulated, dist, FootprintRules.StepSpacing))
                return;

            var forward = new Vector3(dx, 0f, dz);
            if (forward.sqrMagnitude < 1e-6f)
                return;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            w.Left = !w.Left;
            var origin = pos + right * FootprintRules.SideOffset(w.Left);
            PlaceAt(origin, forward, BootSize, _bootMaterial, now);
        }

        private void TickVehicle(DrivableVehicle vehicle, Wheeler w, float now, bool hasCam, Vector3 camPos)
        {
            if (vehicle == null || vehicle.Config == null)
                return;

            var t = vehicle.transform;
            var c = vehicle.Config;
            var wheels = _wheelScratch;
            wheels[0] = t.TransformPoint(-c.TrackX, 0f, c.WheelBaseZ);
            wheels[1] = t.TransformPoint(c.TrackX, 0f, c.WheelBaseZ);
            wheels[2] = t.TransformPoint(-c.TrackX, 0f, -c.WheelBaseZ);
            wheels[3] = t.TransformPoint(c.TrackX, 0f, -c.WheelBaseZ);

            if (!w.HasLast)
            {
                System.Array.Copy(wheels, w.Last, 4);
                w.HasLast = true;
                return;
            }

            var move = wheels[0] - w.Last[0];
            var dist = new Vector2(move.x, move.z).magnitude;
            var dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var speed = dist / dt;
            System.Array.Copy(w.Last, _prevScratch, 4);
            var prev = _prevScratch;
            System.Array.Copy(wheels, w.Last, 4);
            if (dist > 5f || speed < FootprintRules.MinTireSpeed)
            {
                w.Accumulated = 0f;
                return;
            }

            if (hasCam && (t.position - camPos).sqrMagnitude > FootprintRules.MaxCullDistance * FootprintRules.MaxCullDistance)
                return;

            if (!FootprintRules.Advance(ref w.Accumulated, dist, FootprintRules.TireSpacing))
                return;

            for (var i = 0; i < 4; i++)
            {
                var dir = wheels[i] - prev[i];
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f)
                    dir = t.forward;
                PlaceAt(wheels[i], dir.normalized, FootprintRules.TireSpacing, _tireMaterial, now);
            }
        }

        private void PlaceAt(Vector3 origin, Vector3 forward, float size, Material material, float now)
        {
            var count = Physics.RaycastNonAlloc(origin + Vector3.up * 0.5f, Vector3.down, _hits, 1.6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var c = _hits[i].collider;
                if (c == null || _hits[i].distance <= 0f || c.attachedRigidbody != null)
                    continue;
                if (_hits[i].distance < bestDist) { bestDist = _hits[i].distance; best = i; }
            }

            if (best < 0)
                return;

            var hit = _hits[best];
            var kind = GameVfx.Classify(hit.collider, hit.point);
            if (kind == SurfaceKind.Default && hit.collider is TerrainCollider)
                kind = AudioQuality.SampleSurface(hit.point);
            if (!FootprintRules.IsTrackSurface(kind))
                return;

            var normal = hit.normal;
            // Quad +Z'ye bakar; yerden hafif yukarıda, uzun eksen yürüyüş yönünde.
            var up = Vector3.ProjectOnPlane(forward, normal);
            if (up.sqrMagnitude < 1e-6f)
                return;
            var baseRot = Quaternion.LookRotation(normal);
            var roll = Vector3.SignedAngle(baseRot * Vector3.up, up.normalized, normal);
            var slot = _pool.NextSlot;
            _ledger.Add(now);
            _pool.Place(hit.point + normal * 0.012f, normal, size, roll, material, null);
            SetTint(slot, kind, 1f);
        }

        private readonly Dictionary<int, SurfaceKind> _slotKind = new Dictionary<int, SurfaceKind>();

        private void SetTint(int slot, SurfaceKind kind, float alpha)
        {
            _slotKind[slot] = kind;
            _block.Clear();
            var c = kind == SurfaceKind.Snow ? new Color(0.5f, 0.57f, 0.68f, 0.85f * alpha) : new Color(0.12f, 0.09f, 0.06f, 0.9f * alpha);
            _block.SetColor("_BaseColor", c);
            _block.SetColor("_Color", c);
            _pool.TintSlot(slot, _block);
        }

        private void Fade(float now)
        {
            for (var slot = 0; slot < _ledger.Capacity; slot++)
            {
                if (!_ledger.IsActive(slot))
                    continue;
                var age = _ledger.Age(slot, now);
                var a = FootprintRules.AlphaAt(age);
                if (a <= 0f)
                {
                    _pool.HideSlot(slot);
                    _ledger.Release(slot);
                    continue;
                }

                if (a < 1f && _slotKind.TryGetValue(slot, out var kind))
                    SetTint(slot, kind, a);
            }
        }

        private static Texture2D BuildBootTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "VFX_BootPrint", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var u = (x + 0.5f) / n * 2f - 1f; // -1..1
                var v = (y + 0.5f) / n * 2f - 1f;
                // Ön tabanlık (geniş) + topuk (dar), aralarında boşluk.
                var sole = Ellipse(u, v - 0.35f, 0.32f, 0.5f);
                var heel = Ellipse(u, v + 0.58f, 0.24f, 0.28f);
                var a = (sole || heel) ? (byte)255 : (byte)0;
                px[y * n + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D BuildTireTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "VFX_TireTrack", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var inStrip = x >= 11 && x <= 20;
                var tread = (y / 3) % 2 == 0;
                var a = inStrip && (tread || x == 11 || x == 20) ? (byte)255 : (byte)0;
                px[y * n + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        private static bool Ellipse(float u, float v, float rx, float ry) => (u * u) / (rx * rx) + (v * v) / (ry * ry) <= 1f;
    }
}
