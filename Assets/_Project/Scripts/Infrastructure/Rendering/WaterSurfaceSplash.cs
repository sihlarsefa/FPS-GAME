using System.Collections.Generic;
using Project.Infrastructure.Vfx;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Su sıçrama/halka saf matematiği (test edilebilir).</summary>
    public static class WaterSplashMath
    {
        /// <summary>Önceki ve şimdiki yükseklik su seviyesini kesiyor mu (giriş veya çıkış).</summary>
        public static bool Crossed(float prevY, float curY, float level) =>
            (prevY - level) * (curY - level) < 0f;

        /// <summary>Dikey/yatay hıza ve gövde boyutuna göre halka ölçeği (m), 0.4..6.</summary>
        public static float RingScale(float speed, float size) => Mathf.Clamp(0.5f + Mathf.Abs(speed) * 0.18f + size * 0.6f, 0.4f, 6f);

        /// <summary>Halka yarıçapı: yaşa göre ease-out büyür.</summary>
        public static float RingRadius(float age, float life, float maxRadius)
        {
            var t = Mathf.Clamp01(life <= 0f ? 1f : age / life);
            return maxRadius * (1f - (1f - t) * (1f - t));
        }

        /// <summary>Halka saydamlığı: hızlı doğar, doğrusal söner.</summary>
        public static float RingAlpha(float age, float life)
        {
            if (life <= 0f || age >= life) return 0f;
            var t = age / life;
            return t < 0.1f ? t / 0.1f : 1f - (t - 0.1f) / 0.9f;
        }

        /// <summary>Aynı noktaya çok sık halka basmamak için bekleme süresi geçti mi.</summary>
        public static bool CooldownPassed(float now, float last, float interval) => now - last >= interval;
    }

    /// <summary>
    /// Su girişlerinde sıçrama (GameVfx.Impact su) + yayılan halkalar. Oyuncular (CharacterController) ve araçlar (hareketli Rigidbody)
    /// izlenir; suda koşan/yüzen gövde için ara sıra küçük halka bırakır. Kurşun için <see cref="Ripple"/> kullanın.
    /// </summary>
    public sealed class WaterSurfaceSplash : MonoBehaviour
    {
        private sealed class Tracked
        {
            public Transform T;
            public Collider C;
            public float PrevY;
            public Vector3 PrevPos;
            public float NextWake;
        }

        private sealed class Ring
        {
            public Transform T;
            public MeshRenderer R;
            public float Age;
            public float Life;
            public float MaxRadius;
            public bool Active;
        }

        private const int RingPool = 16;
        private static WaterSurfaceSplash _instance;
        private static Texture2D _ringTex;
        private static readonly Dictionary<Transform, Tracked> Tmp = new Dictionary<Transform, Tracked>();

        private readonly List<Tracked> _tracked = new List<Tracked>(32);
        private readonly List<Ring> _rings = new List<Ring>(RingPool);
        private float _level;
        private float _nextScan;
        private Material _ringMat;
        private Mesh _quad;
        private int _next;
        private MaterialPropertyBlock _block; // alan başlatıcısında oluşturulamaz (Unity CreateImpl kısıtı)

        public static void Install(GameObject root, float level)
        {
            if (root == null) return;
            var s = root.GetComponent<WaterSurfaceSplash>() ?? root.AddComponent<WaterSurfaceSplash>();
            s._level = level;
            _instance = s;
        }

        /// <summary>Verilen noktada (su yüzeyine oturtulur) halka + sıçrama. scale ≈ 0.4..6. Kurşun isabeti: ENTEGRASYON BallisticsSystem.</summary>
        public static void Ripple(Vector3 point, float scale, bool splash = true)
        {
            if (_instance == null) return;
            point.y = _instance._level;
            _instance.SpawnRing(point, scale);
            if (splash)
            {
                try { GameVfx.Impact(point, Vector3.up, SurfaceKind.Water); }
                catch (System.Exception) { /* VFX hazır değilse sessiz */ }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var now = Time.time;
            if (now >= _nextScan)
            {
                _nextScan = now + 2f;
                Rescan();
            }

            for (var i = _tracked.Count - 1; i >= 0; i--)
            {
                var tr = _tracked[i];
                if (tr.T == null || tr.C == null) { _tracked.RemoveAt(i); continue; }
                var b = tr.C.bounds;
                var y = b.min.y;
                var pos = tr.T.position;
                var speed = (pos - tr.PrevPos).magnitude / Mathf.Max(Time.deltaTime, 1e-3f);
                var vy = (y - tr.PrevY) / Mathf.Max(Time.deltaTime, 1e-3f);
                var size = Mathf.Max(b.extents.x, b.extents.z);
                if (WaterSplashMath.Crossed(tr.PrevY, y, _level) && Mathf.Abs(vy) > 0.3f && speed < 80f)
                {
                    var sc = WaterSplashMath.RingScale(Mathf.Max(Mathf.Abs(vy), speed * 0.3f), size);
                    Ripple(new Vector3(b.center.x, _level, b.center.z), sc);
                    tr.NextWake = now + 0.4f;
                }
                else if (y < _level && b.max.y > _level && speed > 1.2f && now >= tr.NextWake)
                {
                    // Suda hareket: iz halkası.
                    tr.NextWake = now + Mathf.Clamp(0.9f - speed * 0.05f, 0.25f, 0.9f);
                    SpawnRing(new Vector3(b.center.x, _level, b.center.z), WaterSplashMath.RingScale(speed * 0.2f, size) * 0.6f);
                }

                tr.PrevY = y;
                tr.PrevPos = pos;
            }

            for (var i = 0; i < _rings.Count; i++)
            {
                var r = _rings[i];
                if (!r.Active) continue;
                r.Age += Time.deltaTime;
                if (r.Age >= r.Life) { r.Active = false; r.R.enabled = false; continue; }
                var rad = Mathf.Max(0.05f, WaterSplashMath.RingRadius(r.Age, r.Life, r.MaxRadius));
                r.T.localScale = new Vector3(rad * 2f, rad * 2f, 1f);
                if (_block == null) _block = new MaterialPropertyBlock();
                _block.Clear();
                _block.SetColor("_Color", new Color(1f, 1f, 1f, 0.55f * WaterSplashMath.RingAlpha(r.Age, r.Life)));
                r.R.SetPropertyBlock(_block);
            }
        }

        private void Rescan()
        {
            Tmp.Clear();
            for (var i = 0; i < _tracked.Count; i++)
                if (_tracked[i].T != null) Tmp[_tracked[i].T] = _tracked[i];

            _tracked.Clear();
            var ccs = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
            for (var i = 0; i < ccs.Length; i++) Track(ccs[i], ccs[i].transform);
            var rbs = Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            for (var i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (rb.isKinematic || rb.mass < 150f) continue; // araç sınıfı; küçük cisimler hariç
                var col = rb.GetComponentInChildren<Collider>();
                if (col != null) Track(col, rb.transform);
            }

            Tmp.Clear();
        }

        private void Track(Collider c, Transform t)
        {
            if (c == null || !c.enabled || c.gameObject.layer == GameLayers.Water) return;
            if (!Tmp.TryGetValue(t, out var tr))
                tr = new Tracked { T = t, PrevY = c.bounds.min.y, PrevPos = t.position };
            tr.C = c;
            _tracked.Add(tr);
        }

        private void SpawnRing(Vector3 pos, float scale)
        {
            if (!EnsureAssets()) return;
            Ring ring = null;
            for (var k = 0; k < _rings.Count && ring == null; k++)
                if (!_rings[k].Active) ring = _rings[k];
            if (ring == null)
            {
                if (_rings.Count < RingPool) ring = NewRing();
                else { ring = _rings[_next]; _next = (_next + 1) % _rings.Count; }
            }

            ring.Active = true;
            ring.Age = 0f;
            ring.Life = Mathf.Lerp(1.1f, 2.2f, Mathf.InverseLerp(0.4f, 6f, scale));
            ring.MaxRadius = scale * 1.4f;
            ring.T.position = pos + Vector3.up * 0.06f;
            ring.T.rotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            ring.R.enabled = true;
        }

        private Ring NewRing()
        {
            var go = new GameObject("SuHalka");
            go.layer = GameLayers.Water;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _ringMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.enabled = false;
            var ring = new Ring { T = go.transform, R = mr };
            _rings.Add(ring);
            return ring;
        }

        private bool EnsureAssets()
        {
            if (_ringMat != null && _quad != null) return true;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return false;
            if (_ringTex == null) _ringTex = BuildRingTexture(64);
            _ringMat = new Material(shader) { name = "HK_WaterRing", mainTexture = _ringTex, renderQueue = 3015 };
            _quad = new Mesh { name = "HK_RingQuad" };
            _quad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0) };
            _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quad.RecalculateBounds();
            return true;
        }

        private static Texture2D BuildRingTexture(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "HK_RingTex", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var d = new Vector2((x + .5f) / n - .5f, (y + .5f) / n - .5f).magnitude * 2f;
                var a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) / 0.12f) * Mathf.Clamp01((1f - d) / 0.05f + 0.001f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
