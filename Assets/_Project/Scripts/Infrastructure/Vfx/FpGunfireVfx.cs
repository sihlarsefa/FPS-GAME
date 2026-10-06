using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Vfx
{
    /// <summary>
    /// Birinci şahıs ateş cilası çalışma zamanı parçası: ısı sisi kartı, seri sonrası 2 sn duman lifi, parlayan
    /// kovan yayı. Üç küçük kart havuzu (kademeye göre sınırlı), kare başına bellek ayırmaz.
    /// </summary>
    internal sealed class FpGunfireVfx
    {
        private sealed class Quad
        {
            public Transform T;
            public MeshRenderer R;
            public MaterialPropertyBlock Mpb = new MaterialPropertyBlock();
            public bool Active;
            public float Age, Life, Size, Spin;
            public Vector3 P0, V;
        }

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh _mesh;

        private readonly Transform _root;
        private readonly Quad _shimmer;
        private readonly Quad[] _wisps = new Quad[4];
        private readonly Quad[] _glints = new Quad[6];

        private float _heat;
        private int _pendingShots;
        private int _burstShots;
        private float _sinceShot = 99f;
        private Vector3 _muzzle;
        private Vector3 _aim = Vector3.forward;
        private bool _wispPending;
        private int _nextWisp;
        private int _nextGlint;

        public FpGunfireVfx(Transform root)
        {
            _root = root;
            _shimmer = Make("HeatShimmer", VfxMaterials.AlphaSoft);
            for (var i = 0; i < _wisps.Length; i++) _wisps[i] = Make("MuzzleWisp", VfxMaterials.AlphaPuff);
            for (var i = 0; i < _glints.Length; i++) _glints[i] = Make("BrassGlint", VfxMaterials.AdditiveGlow);
        }

        public void OnShot(Vector3 muzzle, Vector3 aim)
        {
            _pendingShots++;
            _burstShots++;
            _sinceShot = 0f;
            _muzzle = muzzle;
            _aim = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector3.forward;
            _wispPending = true;
        }

        public void SpawnGlint(Vector3 p0, Vector3 v, VfxTier tier)
        {
            var cap = Mathf.Min(_glints.Length, FpGunfireRules.GlintCap(tier));
            if (cap <= 0) return;
            var q = _glints[_nextGlint++ % cap];
            Begin(q, p0, v, 0.55f, 0.03f);
            q.Spin = 40f + (p0.x * 977f % 1f) * 20f;
        }

        public void Tick(float dt, bool hasCamera, Vector3 camPos, VfxTier tier)
        {
            _heat = FpGunfireRules.Heat(_heat, _pendingShots, dt);
            _pendingShots = 0;
            _sinceShot += dt;

            // Isı sisi: namlu önünde kamera yönüne dönük, hafif dalgalanan kart.
            var a = FpGunfireRules.ShimmerAlpha(_heat, tier);
            if (a > 0.002f && _sinceShot < 6f)
            {
                var wobble = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 23f);
                Place(_shimmer, _muzzle + _aim * 0.25f + Vector3.up * 0.03f * _heat, 0.35f * wobble, hasCamera, camPos);
                SetColor(_shimmer, new Color(1f, 0.93f, 0.85f, a));
            }
            else if (_shimmer.R.enabled)
            {
                _shimmer.R.enabled = false;
            }

            // Seri bitti: 2 sn duman lifi.
            if (_wispPending && FpGunfireRules.BurstEnded(_burstShots, _sinceShot))
            {
                _wispPending = false;
                var cap = Mathf.Min(_wisps.Length, FpGunfireRules.WispCap(tier));
                if (cap > 0)
                {
                    var q = _wisps[_nextWisp++ % cap];
                    Begin(q, _muzzle + _aim * 0.2f, _aim * 0.25f + Vector3.up * 0.35f, FpGunfireRules.WispLifetime, 0.12f);
                }
            }
            if (_sinceShot >= FpGunfireRules.BurstGap && !_wispPending)
                _burstShots = 0;

            for (var i = 0; i < _wisps.Length; i++)
            {
                var q = _wisps[i];
                if (!q.Active) continue;
                q.Age += dt;
                if (q.Age >= q.Life) { Off(q); continue; }
                var t = q.Age / q.Life;
                Place(q, q.P0 + q.V * (q.Age * (1f - t * 0.4f)),
                    Mathf.Lerp(q.Size, 0.55f, t), hasCamera, camPos);
                SetColor(q, new Color(0.7f, 0.7f, 0.72f, FpGunfireRules.WispAlpha(t)));
            }

            for (var i = 0; i < _glints.Length; i++)
            {
                var q = _glints[i];
                if (!q.Active) continue;
                q.Age += dt;
                if (q.Age >= q.Life) { Off(q); continue; }
                var b = FpGunfireRules.GlintBrightness(q.Age, q.Life, q.Spin);
                Place(q, FpGunfireRules.GlintPosition(q.P0, q.V, q.Age), q.Size, hasCamera, camPos);
                SetColor(q, new Color(1f, 0.85f, 0.45f, b));
            }
        }

        public void Clear()
        {
            _heat = 0f;
            _wispPending = false;
            _burstShots = 0;
            Off(_shimmer);
            foreach (var q in _wisps) Off(q);
            foreach (var q in _glints) Off(q);
        }

        // ---------------------------------------------------------------- yardımcılar

        private static void Begin(Quad q, Vector3 p0, Vector3 v, float life, float size)
        {
            q.Active = true; q.Age = 0f; q.Life = life; q.Size = size; q.P0 = p0; q.V = v;
            q.R.enabled = true;
        }

        private static void Off(Quad q)
        {
            q.Active = false;
            if (q.R != null) q.R.enabled = false;
        }

        private static void Place(Quad q, Vector3 pos, float size, bool hasCamera, Vector3 camPos)
        {
            if (q.T == null) return;
            q.R.enabled = true;
            q.T.position = pos;
            if (hasCamera)
            {
                var look = pos - camPos;
                if (look.sqrMagnitude > 1e-6f) q.T.rotation = Quaternion.LookRotation(look);
            }
            q.T.localScale = new Vector3(size, size, size);
        }

        private static void SetColor(Quad q, Color c)
        {
            q.R.GetPropertyBlock(q.Mpb);
            q.Mpb.SetColor(ColorId, c);
            q.Mpb.SetColor(BaseColorId, c);
            q.R.SetPropertyBlock(q.Mpb);
        }

        private Quad Make(string name, Material material)
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Default;
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            return new Quad { T = go.transform, R = r };
        }

        private static Mesh QuadMesh()
        {
            if (_mesh != null) return _mesh;
            _mesh = new Mesh { name = "FpQuad" };
            _mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            _mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _mesh.RecalculateBounds();
            return _mesh;
        }

        public static void ResetCache() { _mesh = null; }
    }
}
