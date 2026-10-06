using Project.Infrastructure.Audio;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Havuzlanmış fiziksel kovanlar: elle entegre edilen balistik yörünge (Rigidbody yok), yüzeyde zıplama, dönme,
    /// dinlenme ve sönme. Üst sınır kalite kademesine göre (8/16/24); doluysa en eski kovan yeniden kullanılır.
    /// Sıcak yolda yönetilen bellek ayırmaz (tek paylaşılan mesh/malzeme, Linecast).
    /// </summary>
    internal sealed class ShellCasingPool
    {
        private const float Gravity = 9.81f;
        private const float RestSpeed = 0.5f;
        private const int MaxBounces = 4;

        private static Mesh _mesh;
        private static Material _brass;

        private sealed class Casing
        {
            public Transform T;
            public MeshRenderer R;
            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float SpinSpeed;
            public float Age;
            public float Life;
            public float Scale;
            public int Bounces;
            public bool Active;
            public bool Resting;
            public bool Tinkle;
        }

        private readonly Transform _container;
        private readonly Casing[] _items;
        private int _cursor;

        public ShellCasingPool(Transform container, int capacity)
        {
            _container = container;
            Capacity = Mathf.Max(1, capacity);
            _items = new Casing[Capacity];
        }

        public int Capacity { get; }

        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _items.Length; i++)
                    if (_items[i] != null && _items[i].Active)
                        n++;
                return n;
            }
        }

        /// <summary>Kovanı konumdan, verilen fırlatma hızıyla (m/s, dünya) bırakır.</summary>
        public void Eject(Vector3 position, Vector3 velocity, float size, bool tinkle)
        {
            if (_container == null)
                return;

            var index = Acquire();
            var c = _items[index];
            if (c == null && (c = Create(index)) == null)
                return;

            c.T.SetPositionAndRotation(position, Random.rotation);
            c.Scale = Mathf.Clamp(size, 0.4f, 3f);
            c.T.localScale = new Vector3(0.012f, 0.012f, 0.04f) * c.Scale;
            c.Velocity = velocity;
            c.SpinAxis = Random.onUnitSphere;
            c.SpinSpeed = Random.Range(500f, 1400f);
            c.Age = 0f;
            c.Life = VfxQuality.Tier == VfxTier.Low ? 3f : 6f;
            c.Bounces = 0;
            c.Resting = false;
            c.Tinkle = tinkle;
            c.Active = true;
            c.R.enabled = true;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            for (var i = 0; i < _items.Length; i++)
            {
                var c = _items[i];
                if (c == null || !c.Active)
                    continue;

                if (c.T == null)
                {
                    _items[i] = null;
                    continue;
                }

                c.Age += dt;
                if (c.Age >= c.Life)
                {
                    Deactivate(c);
                    continue;
                }

                // Son yarım saniyede küçülerek kaybol.
                var remain = c.Life - c.Age;
                if (remain < 0.5f)
                    c.T.localScale = new Vector3(0.012f, 0.012f, 0.04f) * (c.Scale * remain * 2f);

                if (c.Resting)
                    continue;

                var pos = c.T.position;
                c.Velocity.y -= Gravity * dt;
                var next = pos + c.Velocity * dt;
                var delta = next - pos;
                var dist = delta.magnitude;
                if (dist > 1e-4f && Physics.Raycast(pos, delta / dist, out var hit, dist + 0.01f,
                        GameLayers.WorldMask | GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                {
                    c.Bounces++;
                    var speed = c.Velocity.magnitude;
                    c.Velocity = Vector3.Reflect(c.Velocity, hit.normal) * 0.38f;
                    c.Velocity += Random.insideUnitSphere * 0.25f;
                    c.SpinSpeed *= 0.5f;
                    c.T.position = hit.point + hit.normal * 0.006f;
                    if (c.Bounces == 1 && c.Tinkle && speed > 0.8f)
                        PlayTinkle(hit.collider, hit.point);

                    if (c.Bounces >= MaxBounces || c.Velocity.magnitude < RestSpeed)
                    {
                        c.Resting = true;
                        // Yan yatmış dinlenme: normale dik uzun eksen.
                        var along = Vector3.Cross(hit.normal, Random.onUnitSphere);
                        if (along.sqrMagnitude < 1e-4f)
                            along = Vector3.right;
                        c.T.rotation = Quaternion.LookRotation(along.normalized, hit.normal);
                    }
                }
                else
                {
                    c.T.position = next;
                    c.T.Rotate(c.SpinAxis, c.SpinSpeed * dt, Space.World);
                }
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _items.Length; i++)
                if (_items[i] != null)
                    Deactivate(_items[i]);
            _cursor = 0;
        }

        private static void PlayTinkle(Collider collider, Vector3 point)
        {
            try
            {
                GameAudio.PlayShellCasing(point, GameVfx.Classify(collider, point));
            }
            catch
            {
                // Ses isteğe bağlı.
            }
        }

        private static void Deactivate(Casing c)
        {
            c.Active = false;
            if (c.R != null)
                c.R.enabled = false;
        }

        private int Acquire()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                var c = _items[i];
                if (c == null || !c.Active)
                    return i;
            }

            var index = _cursor;
            _cursor = (_cursor + 1) % _items.Length;
            return index;
        }

        private Casing Create(int index)
        {
            var go = new GameObject("Casing");
            go.layer = GameLayers.Default;
            go.transform.SetParent(_container, false);
            go.AddComponent<MeshFilter>().sharedMesh = CasingMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = BrassMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            r.enabled = false;
            var c = new Casing { T = go.transform, R = r };
            _items[index] = c;
            return c;
        }

        private static Material BrassMaterial
        {
            get
            {
                if (_brass != null)
                    return _brass;
                try
                {
                    _brass = Project.Infrastructure.Rendering.MaterialLibrary.Lit(new Color(0.78f, 0.58f, 0.2f), 0.7f, 0.85f);
                }
                catch
                {
                    _brass = null;
                }

                return _brass;
            }
        }

        /// <summary>Birim uzunlukta (Z ekseni), 6 kenarlı silindir (taban kapaksız yan yüzler + uç kapakları).</summary>
        private static Mesh CasingMesh
        {
            get
            {
                if (_mesh != null)
                    return _mesh;

                const int sides = 6;
                var verts = new Vector3[sides * 2 + 2];
                var tris = new int[sides * 12];
                for (var i = 0; i < sides; i++)
                {
                    var a = i * Mathf.PI * 2f / sides;
                    var x = Mathf.Cos(a) * 0.5f;
                    var y = Mathf.Sin(a) * 0.5f;
                    verts[i] = new Vector3(x, y, -0.5f);
                    verts[sides + i] = new Vector3(x, y, 0.5f);
                }

                verts[sides * 2] = new Vector3(0f, 0f, -0.5f);
                verts[sides * 2 + 1] = new Vector3(0f, 0f, 0.5f);
                var t = 0;
                for (var i = 0; i < sides; i++)
                {
                    var n = (i + 1) % sides;
                    tris[t++] = i; tris[t++] = sides + i; tris[t++] = sides + n;
                    tris[t++] = i; tris[t++] = sides + n; tris[t++] = n;
                    tris[t++] = sides * 2; tris[t++] = n; tris[t++] = i;
                    tris[t++] = sides * 2 + 1; tris[t++] = sides + i; tris[t++] = sides + n;
                }

                System.Array.Reverse(tris); // dışa bakan sarım
                _mesh = new Mesh { name = "VFX_Casing", hideFlags = HideFlags.DontSave, vertices = verts, triangles = tris };
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
                return _mesh;
            }
        }

        public static void ResetCache()
        {
            _mesh = null;
            _brass = null;
        }
    }
}
