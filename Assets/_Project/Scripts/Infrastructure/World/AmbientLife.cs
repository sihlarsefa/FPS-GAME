using System;
using System.Collections.Generic;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Yaşayan dünya: uzak kuş sürüleri (yakın ateşte dağılır), köy bacalarından duman (şafak/alacakaranlık/soğuk harita),
    /// rüzgâr esinti dalgaları (WindSystem.GustBias), öğle vakti kameranın yakınında kelebekler, gece cırcır katmanı kancası.
    /// Tamamı kademe-sınırlı, havuzlu, tohuma göre deterministik (kurulumda), billboard quad'larla çalışır; NavMesh'e dokunmaz.
    /// Tek giriş: <see cref="Install"/> (MatchBootstrap, null-güvenli).
    /// </summary>
    public sealed class AmbientLife : MonoBehaviour
    {
        private const float BirdCullDist = 650f, SmokeCullDist = 380f, ButterflyRadius = 30f;
        private const float ScatterRadius = 140f;
        private const float BirdCullSq = BirdCullDist * BirdCullDist, SmokeCullSq = SmokeCullDist * SmokeCullDist, ButterflyRadiusSq = ButterflyRadius * ButterflyRadius;
        private const int ColorFrameSkip = 3;   // duman rengi/MPB her 3 karede bir (konum/dönüş her kare)
        private const int GroundFrameSkip = 2;  // sürü zemin yüksekliği (SampleHeight) her 2 karede bir
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RendererColorId = Shader.PropertyToID("_RendererColor");

        public static AmbientLife Instance { get; private set; }

        /// <summary>Cırcır böceği seviyesi 0..1 (ENTEGRASYON Audio: GameAudio bu değeri gece ambiyans katmanına bağlar).</summary>
        public static float CricketIntensity { get; private set; }

        /// <summary>CricketIntensity değiştiğinde (yaklaşık saniyede bir) tetiklenir.</summary>
        public static event Action<float> CricketChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; CricketIntensity = 0f; CricketChanged = null; }

        /// <summary>Atış/patlama konumu bildirir (Acoustics çağırır). Yakın sürüler dağılır.</summary>
        public static void NotifyGunfire(Vector3 position)
        {
            if (Instance != null) Instance.Scatter(position);
        }

        /// <summary>Haritaya özel kurulum. Tekrar çağrılırsa eskisi yenilenir. Sunucuda (headless) çağrılmaz.</summary>
        public static AmbientLife Install(Camera camera, WorldMetadata world, int tier)
        {
            if (!UnityEngine.Application.isPlaying || tier <= 0)
                return null;
            if (Instance != null)
                Destroy(Instance.gameObject);
            var go = new GameObject("HK_AmbientLife");
            var life = go.AddComponent<AmbientLife>();
            life.Setup(camera, world, Mathf.Clamp(tier, 0, 3));
            Instance = life;
            return life;
        }

        private sealed class Billboard
        {
            public Transform T;
            public MeshRenderer R;
            public MaterialPropertyBlock Mpb;
            public Color Tint;
            public bool Active;
        }

        private sealed class Flock
        {
            public Vector2 Center; public float Rx, Rz, Phase, Speed, Alt, Scatter;
            public Vector3 ScatterDir;
            public Billboard[] Birds;
            public float[] PhaseOff;
            public Vector3 LastPos;
            public Vector3[] BaseOff;   // kuş başına sabit ofset (Hash01 bir kez, kurulumda)
            public float GroundY;       // önbellek
            public bool HasGround, Shown;
        }

        private sealed class Column
        {
            public Vector3 Base; public Billboard[] Puffs; public float Offset; public bool Shown;
        }

        private sealed class Butterfly
        {
            public Billboard B; public Vector3 Pos; public float Seed, Speed; public int Respawns;
        }

        private Camera _cam;
        private WorldMetadata _world;
        private Terrain _terrain;
        private int _tier, _seed;
        private bool _cold;
        private float _waterY;
        private float _sunElev = 45f, _levelTimer;
        private float _smoke, _butter, _bird, _cricketSent = -1f;
        private float _rain;
        private float _t;
        private int _frame;
        private float _butterApplied = -1f;
        private Transform _camT;
        private Mesh _quad;
        private Material _birdMat, _puffMat, _wingMat;
        private readonly List<Flock> _flocks = new List<Flock>();
        private readonly List<Column> _columns = new List<Column>();
        private readonly List<Butterfly> _butterflies = new List<Butterfly>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        private void Setup(Camera cam, WorldMetadata world, int tier)
        {
            _cam = cam; _world = world; _tier = tier;
            _terrain = world != null && world.Terrain != null ? world.Terrain : Terrain.activeTerrain;
            _waterY = world != null ? world.WaterLevel : float.NegativeInfinity;
            _seed = world != null && world.Layout != null ? world.Layout.Seed : 1337;
            _cold = world != null && world.Layout != null && world.Layout.SnowLine < 100f;
            _quad = BuildQuad();
            _birdMat = MakeMaterial("HK_AL_Bird", MakeTexture(32, 16, (u, v) => new Color(0.06f, 0.06f, 0.07f, AmbientLifeMath.BirdAlpha(u, v))));
            _puffMat = MakeMaterial("HK_AL_Smoke", MakeTexture(32, 32, (u, v) => new Color(0.78f, 0.78f, 0.8f, AmbientLifeMath.PuffAlpha(u, v))));
            _wingMat = MakeMaterial("HK_AL_Wing", MakeTexture(32, 32, (u, v) =>
            {
                var a = AmbientLifeMath.WingAlpha(u, v);
                return new Color(1f, 0.82f - 0.3f * Mathf.Abs(u - 0.5f), 0.25f, a);
            }));
            if (_terrain != null)
            {
                BuildFlocks();
                BuildSmoke();
                BuildButterflies();
            }
        }

        // ---------- kurulum ----------
        private float Ground(float x, float z)
        {
            if (_terrain == null) return 0f;
            var p = new Vector3(x, 0f, z);
            return _terrain.SampleHeight(p) + _terrain.transform.position.y;
        }

        private float HalfSize => _world != null ? Mathf.Max(100f, _world.MapHalfSize) : 500f;

        private void BuildFlocks()
        {
            var n = AmbientLifeMath.FlockCount(_tier);
            var half = HalfSize * 0.8f;
            for (var f = 0; f < n; f++)
            {
                // Aday noktalardan en alçak (vadi) olanı seç.
                var best = Vector2.zero; var bestH = float.MaxValue;
                for (var k = 0; k < 8; k++)
                {
                    var x = (AmbientLifeMath.Hash01(_seed, f, k * 2) * 2f - 1f) * half;
                    var z = (AmbientLifeMath.Hash01(_seed, f, k * 2 + 1) * 2f - 1f) * half;
                    var h = Ground(x, z);
                    if (h > _waterY + 1f && h < bestH) { bestH = h; best = new Vector2(x, z); }
                }
                if (bestH == float.MaxValue) continue;
                var count = AmbientLifeMath.BirdsPerFlock(f, _seed);
                var fl = new Flock
                {
                    Center = best,
                    Rx = 90f + AmbientLifeMath.Hash01(_seed, f, 91) * 90f,
                    Rz = 70f + AmbientLifeMath.Hash01(_seed, f, 92) * 90f,
                    Phase = AmbientLifeMath.Hash01(_seed, f, 93) * AmbientLifeMath.TwoPi,
                    Speed = (0.045f + AmbientLifeMath.Hash01(_seed, f, 94) * 0.03f) * (AmbientLifeMath.Hash01(_seed, f, 95) > 0.5f ? 1f : -1f),
                    Alt = 45f + AmbientLifeMath.Hash01(_seed, f, 96) * 40f,
                    Birds = new Billboard[count],
                    PhaseOff = new float[count],
                    BaseOff = new Vector3[count]
                };
                for (var b = 0; b < count; b++)
                {
                    fl.Birds[b] = NewBillboard("HK_Bird", _birdMat, false);
                    fl.PhaseOff[b] = AmbientLifeMath.Hash01(_seed, f * 31 + b, 97);
                    fl.BaseOff[b] = new Vector3((fl.PhaseOff[b] - 0.5f) * 22f, (AmbientLifeMath.Hash01(_seed, f * 31 + b, 9) - 0.5f) * 8f, (AmbientLifeMath.Hash01(_seed, f * 31 + b, 8) - 0.5f) * 18f);
                }
                _flocks.Add(fl);
            }
        }

        private void BuildSmoke()
        {
            var cap = AmbientLifeMath.SmokeColumnCount(_tier);
            var spots = new List<Vector3>();
            // Köy/oba yerleşimleri; yapı ayak izi varsa bacayı bina çatısına koy.
            var locs = _world != null && _world.Layout != null ? _world.Layout.Locations : null;
            if (locs != null)
            {
                for (var i = 0; i < locs.Count && spots.Count < cap * 3; i++)
                {
                    var l = locs[i];
                    if (l == null || l.Kind != LocationKind.Village) continue;
                    var found = 0;
                    var sb = _world.StructureBounds;
                    for (var b = 0; sb != null && b < sb.Count && found < 4; b++)
                    {
                        var bd = sb[b];
                        if (bd.size.y < 3f) continue;
                        var dx = bd.center.x - l.Center.x; var dz = bd.center.z - l.Center.y;
                        if (dx * dx + dz * dz > l.Radius * l.Radius) continue;
                        if (AmbientLifeMath.Hash01(_seed, i * 97 + b, 5) > 0.55f) continue;
                        spots.Add(new Vector3(bd.center.x + bd.extents.x * 0.3f, bd.max.y + 0.4f, bd.center.z));
                        found++;
                    }
                    for (var k = found; k < 3; k++)
                    {
                        var ang = AmbientLifeMath.Hash01(_seed, i, 100 + k) * AmbientLifeMath.TwoPi;
                        var r = l.Radius * (0.2f + 0.5f * AmbientLifeMath.Hash01(_seed, i, 120 + k));
                        var x = l.Center.x + Mathf.Cos(ang) * r; var z = l.Center.y + Mathf.Sin(ang) * r;
                        spots.Add(new Vector3(x, Ground(x, z) + 6.5f, z));
                    }
                }
            }
            // Deterministik seçim: başa karıştır, kapasiteye kes.
            for (var i = spots.Count - 1; i > 0; i--)
            {
                var j = (int)(AmbientLifeMath.Hash01(_seed, i, 777) * (i + 1)) % (i + 1);
                var tmp = spots[i]; spots[i] = spots[j]; spots[j] = tmp;
            }
            for (var i = 0; i < spots.Count && _columns.Count < cap; i++)
            {
                var col = new Column { Base = spots[i], Offset = AmbientLifeMath.Hash01(_seed, i, 321), Puffs = new Billboard[AmbientLifeMath.PuffsPerColumn] };
                for (var p = 0; p < col.Puffs.Length; p++)
                    col.Puffs[p] = NewBillboard("HK_Smoke", _puffMat, true);
                _columns.Add(col);
            }
        }

        private void BuildButterflies()
        {
            var n = AmbientLifeMath.ButterflyCount(_tier);
            for (var i = 0; i < n; i++)
            {
                var b = new Butterfly { B = NewBillboard("HK_Butterfly", _wingMat, true), Seed = AmbientLifeMath.Hash01(_seed, i, 555) * 50f, Speed = 0.8f + AmbientLifeMath.Hash01(_seed, i, 556) };
                b.B.Tint = Color.HSVToRGB(AmbientLifeMath.Hash01(_seed, i, 557) * 0.18f + 0.02f, 0.75f, 1f);
                b.B.T.gameObject.SetActive(false);
                _butterflies.Add(b);
            }
        }

        // ---------- yardımcılar ----------
        private Billboard NewBillboard(string name, Material mat, bool useTint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            return new Billboard { T = go.transform, R = r, Mpb = useTint ? new MaterialPropertyBlock() : null, Tint = Color.white };
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "HK_AL_Quad" };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.bounds = new Bounds(Vector3.zero, new Vector3(1, 1, 0.1f));
            return m;
        }

        private Texture2D MakeTexture(int w, int h, Func<float, float, Color> f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "HK_AL_Tex", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
            tex.SetPixels(px);
            tex.Apply(false, true);
            _textures.Add(tex);
            return tex;
        }

        private Material MakeMaterial(string name, Texture2D tex)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
            if (sh == null) return null;
            var m = new Material(sh) { name = name, hideFlags = HideFlags.HideAndDontSave, mainTexture = tex, renderQueue = 3000 };
            return m;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (CricketIntensity != 0f) { CricketIntensity = 0f; CricketChanged?.Invoke(0f); }
            WindSystem.GustBias = 0f;
            if (_quad != null) Destroy(_quad);
            if (_birdMat != null) Destroy(_birdMat);
            if (_puffMat != null) Destroy(_puffMat);
            if (_wingMat != null) Destroy(_wingMat);
            for (var i = 0; i < _textures.Count; i++) if (_textures[i] != null) Destroy(_textures[i]);
        }

        private void Scatter(Vector3 pos)
        {
            for (var i = 0; i < _flocks.Count; i++)
            {
                var f = _flocks[i];
                var d = Vector3.Distance(f.LastPos, pos);
                if (!AmbientLifeMath.ShotScatters(d, ScatterRadius)) continue;
                f.Scatter = 1f;
                var away = f.LastPos - pos; away.y = 0f;
                f.ScatterDir = away.sqrMagnitude < 0.01f ? Vector3.right : away.normalized;
            }
        }

        private void OnEnable() { Acoustics.NearMiss += OnNearMiss; }
        private void OnDisable() { Acoustics.NearMiss -= OnNearMiss; }
        private void OnNearMiss(Vector3 p, float mm, float d) => Scatter(p);

        // ---------- döngü ----------
        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            _t += dt;
            _frame++;
            if (_cam == null || !_cam.isActiveAndEnabled)
            {
                _cam = Camera.main;
                _camT = _cam != null ? _cam.transform : null;
                if (_cam == null) return;
            }
            else if (_camT == null) _camT = _cam.transform;

            _levelTimer -= dt;
            if (_levelTimer <= 0f) { _levelTimer = 1f; RefreshLevels(); }

            UpdateGust();
            var camPos = _camT.position;
            var rot = _camT.rotation; // LookRotation(forward, up) == kamera dönüşü (ortonormal); hesap yok
            UpdateFlocks(camPos, rot, dt);
            UpdateSmoke(camPos, rot);
            UpdateButterflies(camPos, rot, dt);
        }

        private void RefreshLevels()
        {
            var sun = RenderSettings.sun;
            _sunElev = sun != null ? Mathf.Asin(Mathf.Clamp(-sun.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg : 45f;
            var ws = WeatherSystem.Instance;
            _rain = ws != null ? ws.Current.Rain : 0f;
            _smoke = AmbientLifeMath.SmokeLevel(_sunElev, _cold);
            _butter = AmbientLifeMath.ButterflyLevel(_sunElev, _cold, _rain);
            _bird = AmbientLifeMath.BirdLevel(_sunElev, _rain);
            var c = AmbientLifeMath.CricketLevel(_sunElev, _cold, _rain);
            if (Mathf.Abs(c - _cricketSent) > 0.02f)
            {
                _cricketSent = c;
                CricketIntensity = c;
                CricketChanged?.Invoke(c);
            }
        }

        private void UpdateGust()
        {
            var w = WindSystem.Current;
            if (!w.Enabled) { WindSystem.GustBias = 0f; return; }
            var cp = _camT.position;
            var along = cp.x * w.DirX + cp.z * w.DirZ;
            WindSystem.GustBias = AmbientLifeMath.GustBand(along, _t, 150f, 11f + 8f * w.Strength) * Mathf.Clamp01(0.4f + w.Strength);
        }

        private void UpdateFlocks(Vector3 camPos, Quaternion rot, float dt)
        {
            var birdsOn = _bird > 0.02f;
            for (var i = 0; i < _flocks.Count; i++)
            {
                var f = _flocks[i];
                AmbientLifeMath.FlockPoint(f.Center.x, f.Center.y, f.Rx, f.Rz, f.Phase, f.Speed, _t, out var x, out var z);
                if (!f.HasGround || (_frame + i) % GroundFrameSkip == 0) { f.GroundY = Ground(x, z); f.HasGround = true; }
                var y = f.GroundY + f.Alt + 6f * Mathf.Sin(_t * 0.2f + i);
                var lead = new Vector3(x, y, z);
                f.LastPos = lead;
                f.Scatter = AmbientLifeMath.ScatterDecay(f.Scatter, dt);
                var visible = birdsOn && (lead - camPos).sqrMagnitude < BirdCullSq;
                if (!visible && !f.Shown) continue; // gizli sürü: hiçbir kuşa dokunma
                var spread = 1f + 2.5f * f.Scatter;
                var flapRate = 7f + 4f * f.Scatter;
                var scatterVec = f.ScatterDir * 55f + Vector3.up * 28f;
                for (var b = 0; b < f.Birds.Length; b++)
                {
                    var bb = f.Birds[b];
                    if (bb.Active != visible) { bb.Active = visible; bb.T.gameObject.SetActive(visible); }
                    if (!visible) continue;
                    var po = f.PhaseOff[b];
                    var bo = f.BaseOff[b];
                    var off = new Vector3(bo.x * spread, bo.y, bo.z * spread) + scatterVec * (f.Scatter * (0.5f + po));
                    bb.T.SetPositionAndRotation(lead + off, rot);
                    var flap = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(_t * flapRate + po * 20f));
                    var sc = 1.9f + po * 0.5f;
                    bb.T.localScale = new Vector3(sc * (0.55f + flap), sc * 0.55f, 1f);
                }
                f.Shown = visible;
            }
        }

        private void UpdateSmoke(Vector3 camPos, Quaternion rot)
        {
            if (_columns.Count == 0) return;
            var wind = WindSystem.Current;
            var wx = wind.Enabled ? wind.DirX : 0.5f; var wz = wind.Enabled ? wind.DirZ : 0.3f;
            var drift = 1.5f + 5f * (wind.Enabled ? wind.Strength : 0.2f);
            var smokeOn = _smoke > 0.02f;
            for (var c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];
                var near = smokeOn && (col.Base - camPos).sqrMagnitude < SmokeCullSq;
                if (!near && !col.Shown) continue; // uzak/gizli sütun: sıfır iş
                var colorTick = (_frame + c) % ColorFrameSkip == 0 || near != col.Shown;
                var n = col.Puffs.Length;
                var invN = 1f / n;
                for (var p = 0; p < n; p++)
                {
                    var pb = col.Puffs[p];
                    if (pb.Active != near) { pb.Active = near; pb.T.gameObject.SetActive(near); }
                    if (!near) continue;
                    var age = Mathf.Repeat(_t / 11f + col.Offset + p * invN, 1f);
                    AmbientLifeMath.PuffState(age, out var size, out var alpha, out var rise);
                    var sway = Mathf.Sin(_t * 0.7f + p * 1.3f + c) * 0.4f * age;
                    var pos = col.Base + new Vector3(wx * drift * age * 6f + sway, rise, wz * drift * age * 6f);
                    pb.T.SetPositionAndRotation(pos, rot);
                    pb.T.localScale = new Vector3(size, size, 1f);
                    if (colorTick)
                    {
                        var tint = new Color(0.78f, 0.78f, 0.8f, alpha * _smoke);
                        pb.Mpb.SetColor(ColorId, tint);
                        pb.Mpb.SetColor(RendererColorId, tint);
                        pb.R.SetPropertyBlock(pb.Mpb);
                    }
                }
                col.Shown = near;
            }
        }

        private void UpdateButterflies(Vector3 camPos, Quaternion rot, float dt)
        {
            if (_butterflies.Count == 0) return;
            var on = _butter > 0.05f;
            var tintDirty = Mathf.Abs(_butter - _butterApplied) > 0.01f;
            if (on && tintDirty) _butterApplied = _butter;
            for (var i = 0; i < _butterflies.Count; i++)
            {
                var b = _butterflies[i];
                var go = b.B.T.gameObject;
                if (!on) { if (b.B.Active) { b.B.Active = false; go.SetActive(false); } continue; }
                var flat = new Vector3(b.Pos.x - camPos.x, 0f, b.Pos.z - camPos.z);
                var spawned = false;
                if (!b.B.Active || flat.sqrMagnitude > ButterflyRadiusSq)
                {
                    b.Respawns++;
                    var ang = AmbientLifeMath.Hash01(_seed, i, b.Respawns * 3) * AmbientLifeMath.TwoPi;
                    var r = 6f + AmbientLifeMath.Hash01(_seed, i, b.Respawns * 3 + 1) * 20f;
                    var x = camPos.x + Mathf.Cos(ang) * r; var z = camPos.z + Mathf.Sin(ang) * r;
                    var g = Ground(x, z);
                    if (g < _waterY + 0.6f) { b.B.Active = false; go.SetActive(false); b.Pos = new Vector3(camPos.x + 999f, 0f, camPos.z); continue; }
                    b.Pos = new Vector3(x, g + 0.8f, z);
                    b.B.Active = true; go.SetActive(true);
                    spawned = true;
                }

                var t = _t * b.Speed + b.Seed;
                var step = new Vector3(Mathf.Sin(t * 1.3f) + 0.6f * Mathf.Sin(t * 2.7f), 0f, Mathf.Cos(t * 1.1f) + 0.6f * Mathf.Cos(t * 2.3f)) * (0.9f * dt * b.Speed);
                b.Pos += step;
                var gy = Ground(b.Pos.x, b.Pos.z) + 0.7f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f)) + 0.15f * Mathf.Sin(t * 6f);
                b.Pos.y = Mathf.Lerp(b.Pos.y, gy, 1f - Mathf.Exp(-3f * dt));
                b.B.T.SetPositionAndRotation(b.Pos, rot);
                var flap = 0.2f + 0.8f * Mathf.Abs(Mathf.Sin(_t * 14f * b.Speed + b.Seed));
                b.B.T.localScale = new Vector3(0.22f * flap + 0.05f, 0.17f, 1f);
                if (tintDirty || spawned)
                {
                    var tint = b.B.Tint; tint.a = _butter;
                    b.B.Mpb.SetColor(ColorId, tint);
                    b.B.Mpb.SetColor(RendererColorId, tint);
                    b.B.R.SetPropertyBlock(b.B.Mpb);
                }
            }
        }
    }
}
