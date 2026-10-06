using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Su altı ucuz "god-ray": kameranın çevresinde 2-3 yavaş dönen, güneş yönünde eğik alfa dörtgeni (yalnız su altında,
    /// güneş görünürken, kademe >= 2). Tek malzeme, tek mesh; gölge yok.
    /// </summary>
    public sealed class WaterSurfaceShafts
    {
        private const float Length = 14f;
        private const float Width = 2.6f;

        private readonly Transform[] _quads = new Transform[WaterSurfaceMath.MaxShafts];
        private readonly GameObject _root;
        private readonly Material _mat;
        private float _lastAlpha = -1f;

        public WaterSurfaceShafts(Transform parent)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;
            _root = new GameObject("SuAltıHuzme");
            _root.layer = GameLayers.Water;
            _root.transform.SetParent(parent, false);
            _mat = new Material(shader) { name = "HK_WaterShafts", mainTexture = BuildTexture(), color = new Color(0.75f, 0.95f, 0.9f, 0f), renderQueue = 3020 };
            var mesh = BuildMesh();
            for (var i = 0; i < _quads.Length; i++)
            {
                var go = new GameObject("Huzme" + i);
                go.layer = GameLayers.Water;
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = LightProbeUsage.Off;
                mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                _quads[i] = go.transform;
            }

            _root.SetActive(false);
        }

        /// <summary>Her karede çağrılır; koşul yoksa kökü kapatır.</summary>
        public void Tick(Camera cam, float level, float blend, int tier, float time)
        {
            if (_root == null || cam == null)
                return;
            var sun = RenderSettings.sun;
            var visible = sun != null && sun.enabled && Atmosphere.CurrentTime != TimeOfDay.Gece;
            var sunDir = sun != null ? sun.transform.forward : Vector3.down;
            var alpha = WaterSurfaceMath.ShaftAlpha(blend, visible, sunDir.y, tier);
            if (alpha <= 0.001f)
            {
                if (_root.activeSelf) _root.SetActive(false);
                return;
            }

            if (!_root.activeSelf) _root.SetActive(true);
            if (Mathf.Abs(alpha - _lastAlpha) > 0.004f)
            {
                _lastAlpha = alpha;
                var c = _mat.color; c.a = alpha; _mat.color = c;
            }

            var count = WaterSurfaceMath.ShaftCount(tier);
            var tilt = Quaternion.FromToRotation(Vector3.down, sunDir);
            var cp = cam.transform.position;
            for (var i = 0; i < _quads.Length; i++)
            {
                var q = _quads[i];
                if (q == null) continue;
                var on = i < count;
                if (q.gameObject.activeSelf != on) q.gameObject.SetActive(on);
                if (!on) continue;
                var a = WaterSurfaceMath.ShaftAngle(time, i) * Mathf.Deg2Rad;
                var r = WaterSurfaceMath.ShaftRadius(i);
                q.position = new Vector3(cp.x + Mathf.Cos(a) * r, level, cp.z + Mathf.Sin(a) * r);
                // Dörtgen yüzü kameraya dönük kalsın (yaw), uzun ekseni güneş yönünde.
                var toCam = cp - q.position; toCam.y = 0f;
                var yaw = toCam.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCam.normalized, Vector3.up) : Quaternion.identity;
                q.rotation = tilt * Quaternion.Euler(0f, yaw.eulerAngles.y, 0f);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
            if (_mat != null) Object.Destroy(_mat);
        }

        private static Mesh BuildMesh()
        {
            var m = new Mesh { name = "HK_ShaftQuad" };
            m.vertices = new[]
            {
                new Vector3(-Width * 0.5f, 0f, 0f), new Vector3(Width * 0.5f, 0f, 0f),
                new Vector3(-Width * 0.5f, -Length, 0f), new Vector3(Width * 0.5f, -Length, 0f)
            };
            m.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
            m.colors = new[] { Color.white, Color.white, new Color(1, 1, 1, 0), new Color(1, 1, 1, 0) };
            m.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            m.RecalculateBounds();
            return m;
        }

        private static Texture2D BuildTexture()
        {
            const int w = 32;
            var t = new Texture2D(w, 1, TextureFormat.RGBA32, false) { name = "HK_ShaftGrad", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w];
            for (var i = 0; i < w; i++)
            {
                var x = (i + 0.5f) / w * 2f - 1f;
                var a = Mathf.Clamp01(1f - x * x);
                a *= a;
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
