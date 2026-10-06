using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// PUBG tarzı ufuk manzarası: 3 katmanlı uzak dağ (uzak = açık/mavi, yakın = koyu + orman bandı), ufuk sis bandı,
    /// güneş tarafı parlaması, gece yıldızları (yalnız ufuk üstünde) ve katman başına paralaks. Toplam &lt; 2.5k üçgen.
    /// SkyEnvironment sahiplenir; Atmosphere kancasıyla Configure edilir.
    /// </summary>
    public sealed class HorizonVista
    {
        private const float HazeRadius = 1085f;
        private const float StarRadius = 1300f;
        private const int HazeSegments = 128;

        private readonly Transform _root;
        private readonly Transform[] _layerT = new Transform[3];
        private readonly MeshFilter[] _layerMf = new MeshFilter[3];
        private readonly Material[] _layerMat = new Material[3];
        private MeshFilter _hazeMf, _starMf;
        private Material _hazeMat, _starMat;
        private Vector3 _origin;
        private bool _hasOrigin;
        public bool Ready { get; private set; }

        public HorizonVista(Transform parent)
        {
            _root = new GameObject("[Ufuk]").transform;
            _root.SetParent(parent, false);
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return;
            for (var i = 0; i < 3; i++)
            {
                _layerMat[i] = new Material(shader) { name = "HK_Vista" + i, renderQueue = 3002 + i };
                var go = Part("Vista" + i, _layerMat[i]);
                _layerT[i] = go.transform;
                _layerMf[i] = go.GetComponent<MeshFilter>();
            }

            _hazeMat = new Material(shader) { name = "HK_VistaHaze", renderQueue = 3005 };
            _hazeMf = Part("UfukSis", _hazeMat).GetComponent<MeshFilter>();
            _starMat = new Material(shader) { name = "HK_VistaStars", renderQueue = 2999 };
            _starMf = Part("Yildizlar", _starMat).GetComponent<MeshFilter>();
            Ready = true;
        }

        private GameObject Part(string name, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        public void SetActive(bool on) { if (_root != null) _root.gameObject.SetActive(on); }

        public void Configure(bool night, bool clear, float fogMix, Color fog, Color tint, Color sunColor, Vector3 sunDir, bool sunGlow)
        {
            if (!Ready) return;
            fog = HorizonVistaMath.Sanitize(fog, new Color(0.7f, 0.75f, 0.8f, 1f));
            tint = HorizonVistaMath.Sanitize(tint, new Color(0.4f, 0.5f, 0.7f, 1f));
            var horizon = HorizonVistaMath.SkyHorizonColor(fog, tint, night);
            sunColor = HorizonVistaMath.Sanitize(sunColor, Color.white);
            for (var i = 0; i < 3; i++)
            {
                var l = HorizonVistaMath.Layers[i];
                var ridge = HorizonVistaMath.LayerRidgeColor(l, i, night, fogMix, fog, tint);
                Swap(_layerMf[i], BuildLayer(l, i, ridge, horizon, night, sunColor, sunDir, sunGlow));
            }

            Swap(_hazeMf, BuildHaze(horizon, night, fogMix, sunColor, sunDir, sunGlow));
            _starMf.gameObject.SetActive(night && clear);
            if (night && clear)
                Swap(_starMf, BuildStars());
        }

        private static void Swap(MeshFilter mf, Mesh m)
        {
            var old = mf.sharedMesh;
            mf.sharedMesh = m;
            if (old != null) Object.Destroy(old);
        }

        private static Mesh BuildLayer(HorizonVistaMath.Layer l, int index, Color ridge, Color fog, bool night, Color sunColor, Vector3 sunDir, bool sunGlow)
        {
            var seg = l.Segments;
            const int rows = 4;
            var verts = new Vector3[(seg + 1) * rows];
            var cols = new Color[verts.Length];
            var tris = new int[seg * (rows - 1) * 6];
            // fog = ufuk gökyüzü rengi. Alt: ufuk rengine yakın (üst sınırı ufuk parlaklığı), tepe: koyu silüet.
            var hl = HorizonVistaMath.Luma(fog);
            var baseCol = night ? new Color(ridge.r * 0.8f, ridge.g * 0.8f, ridge.b * 0.85f, 1f) : HorizonVistaMath.ClampLuma(Color.Lerp(ridge, fog, 0.5f), hl * 0.95f);
            baseCol.a = 1f;
            var fade = Mathf.Max(4f, (l.MaxH - l.MinH) * 0.12f);
            var mid = Color.Lerp(baseCol, ridge, 0.55f);
            for (var s = 0; s <= seg; s++)
            {
                var a = s / (float)seg * Mathf.PI * 2f;
                var x = Mathf.Cos(a) * l.Radius;
                var z = Mathf.Sin(a) * l.Radius;
                var h = HorizonVistaMath.RidgeHeight(l, s % seg);
                var glow = sunGlow && !night ? HorizonVistaMath.SunSide(a, sunDir, 3f) * 0.12f : 0f;
                var rc = glow > 0f ? HorizonVistaMath.ClampLuma(Color.Lerp(ridge, sunColor, glow), night ? 1f : hl * 0.95f) : ridge;
                if (l.Forest)
                {
                    var v = 0.88f + 0.24f * HorizonVistaMath.Hash01(s, l.Seed + 3);
                    rc = new Color(rc.r * v, rc.g * v, rc.b * v, 1f);
                }

                var o = s * rows;
                verts[o] = new Vector3(x, l.BaseY, z);
                verts[o + 1] = new Vector3(x, h * 0.45f, z);
                verts[o + 2] = new Vector3(x, h, z);
                verts[o + 3] = new Vector3(x, h + fade, z);
                cols[o] = HorizonVistaMath.Sanitize(baseCol, fog);
                cols[o + 1] = HorizonVistaMath.Sanitize(glow > 0f ? Color.Lerp(mid, sunColor, glow * 0.5f) : mid, fog);
                cols[o + 2] = HorizonVistaMath.Sanitize(rc, fog);
                var topC = HorizonVistaMath.Sanitize(rc, fog);
                topC.a = 0f; // tepede yumuşak alfa solması: ufuk çizgisi sert beyaz kesim olmaz
                cols[o + 3] = topC;
            }

            var t = 0;
            for (var s = 0; s < seg; s++)
            for (var r = 0; r < rows - 1; r++)
            {
                var a0 = s * rows + r;
                var b0 = (s + 1) * rows + r;
                tris[t++] = a0; tris[t++] = a0 + 1; tris[t++] = b0;
                tris[t++] = b0; tris[t++] = a0 + 1; tris[t++] = b0 + 1;
            }

            return Finish(new Mesh { name = "HK_Vista" + index }, verts, cols, tris, l.Radius);
        }

        private static Mesh BuildHaze(Color fog, bool night, float fogMix, Color sunColor, Vector3 sunDir, bool sunGlow)
        {
            const int rows = 3;
            var verts = new Vector3[(HazeSegments + 1) * rows];
            var cols = new Color[verts.Length];
            var tris = new int[HazeSegments * (rows - 1) * 6];
            var peak = Mathf.Clamp01((night ? 0.25f : 0.2f) + fogMix * 0.2f);
            var hz = night ? new Color(fog.r * 0.8f, fog.g * 0.8f, fog.b * 0.9f) : fog;
            for (var s = 0; s <= HazeSegments; s++)
            {
                var a = s / (float)HazeSegments * Mathf.PI * 2f;
                var x = Mathf.Cos(a) * HazeRadius;
                var z = Mathf.Sin(a) * HazeRadius;
                var glow = sunGlow && !night ? HorizonVistaMath.SunSide(a, sunDir, 4f) : 0f;
                var c = glow > 0f ? HorizonVistaMath.ClampLuma(Color.Lerp(hz, sunColor, glow * 0.3f), HorizonVistaMath.Luma(hz) * 1.06f) : hz;
                var pa = Mathf.Clamp01(peak + glow * 0.1f);
                var o = s * rows;
                verts[o] = new Vector3(x, -25f, z);
                verts[o + 1] = new Vector3(x, 8f, z);
                verts[o + 2] = new Vector3(x, 55f + glow * 40f, z);
                cols[o] = HorizonVistaMath.Sanitize(new Color(c.r, c.g, c.b, 0f), fog);
                cols[o + 1] = HorizonVistaMath.Sanitize(new Color(c.r, c.g, c.b, pa), fog);
                cols[o + 2] = HorizonVistaMath.Sanitize(new Color(c.r, c.g, c.b, 0f), fog);
            }

            var t = 0;
            for (var s = 0; s < HazeSegments; s++)
            for (var r = 0; r < rows - 1; r++)
            {
                var a0 = s * rows + r;
                var b0 = (s + 1) * rows + r;
                tris[t++] = a0; tris[t++] = a0 + 1; tris[t++] = b0;
                tris[t++] = b0; tris[t++] = a0 + 1; tris[t++] = b0 + 1;
            }

            return Finish(new Mesh { name = "HK_VistaHaze" }, verts, cols, tris, HazeRadius);
        }

        private static Mesh BuildStars()
        {
            var n = HorizonVistaMath.StarCount;
            var verts = new Vector3[n * 4];
            var cols = new Color[n * 4];
            var tris = new int[n * 6];
            for (var i = 0; i < n; i++)
            {
                var az = HorizonVistaMath.Hash01(i, 101) * Mathf.PI * 2f;
                var el = Mathf.Lerp(HorizonVistaMath.StarMinElevation, 1f, Mathf.Sqrt(HorizonVistaMath.Hash01(i, 202)));
                var ce = Mathf.Sqrt(Mathf.Max(0f, 1f - el * el));
                var dir = new Vector3(Mathf.Cos(az) * ce, el, Mathf.Sin(az) * ce);
                var c = dir * StarRadius;
                var right = Vector3.Cross(Vector3.up, dir).normalized;
                var up = Vector3.Cross(dir, right).normalized;
                var sz = Mathf.Lerp(1.6f, 4.2f, Mathf.Pow(HorizonVistaMath.Hash01(i, 303), 3f));
                var br = Mathf.Lerp(0.45f, 1f, HorizonVistaMath.Hash01(i, 404));
                var col = new Color(0.85f * br, 0.9f * br, br, 1f);
                var o = i * 4;
                verts[o] = c - right * sz - up * sz;
                verts[o + 1] = c + right * sz - up * sz;
                verts[o + 2] = c + right * sz + up * sz;
                verts[o + 3] = c - right * sz + up * sz;
                cols[o] = cols[o + 1] = cols[o + 2] = cols[o + 3] = col;
                var t = i * 6;
                tris[t] = o; tris[t + 1] = o + 2; tris[t + 2] = o + 1;
                tris[t + 3] = o; tris[t + 4] = o + 3; tris[t + 5] = o + 2;
            }

            return Finish(new Mesh { name = "HK_VistaStars" }, verts, cols, tris, StarRadius);
        }

        private static Mesh Finish(Mesh m, Vector3[] verts, Color[] cols, int[] tris, float radius)
        {
            for (var i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z))
                    verts[i] = new Vector3(0f, -60f, radius);
            }

            m.vertices = verts;
            m.colors = cols;
            m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2.4f, radius * 1.2f, radius * 2.4f));
            return m;
        }

        /// <summary>Paralaks güncellemesi için yatay hareket eşiği (m). Katman Lag en çok 0,04 => hata &lt; 2 cm (görünmez).</summary>
        public const float ParallaxMoveGate = 0.5f;

        /// <summary>Saf kapı: kamera son uygulanan konumdan yatay olarak eşikten fazla oynadı mı (ilk uygulama her zaman evet).</summary>
        public static bool ShouldApplyParallax(bool hasApplied, Vector3 camPos, Vector3 lastApplied, float gate)
        {
            if (!hasApplied) return true;
            var dx = camPos.x - lastApplied.x;
            var dz = camPos.z - lastApplied.z;
            return dx * dx + dz * dz > gate * gate;
        }

        private Vector3 _lastApplied;
        private bool _hasApplied;

        /// <summary>Her kare çağrılır; kamera ~0,5 m'den az oynadıysa erken çıkar (3 localPosition yazımı + 3 ofset hesabı atlanır).</summary>
        public void Tick(Vector3 camPos)
        {
            if (!Ready) return;
            if (!_hasOrigin || (camPos - _origin).sqrMagnitude > 4e6f)
            {
                _origin = camPos;
                _hasOrigin = true;
                _hasApplied = false;
            }

            if (!ShouldApplyParallax(_hasApplied, camPos, _lastApplied, ParallaxMoveGate))
                return;
            _lastApplied = camPos;
            _hasApplied = true;

            var layers = HorizonVistaMath.Layers;
            for (var i = 0; i < 3; i++)
                if (_layerT[i] != null)
                    _layerT[i].localPosition = HorizonVistaMath.ParallaxOffset(camPos, _origin, layers[i].Lag);
        }
    }
}
