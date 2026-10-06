using System.Collections.Generic;
using System.Runtime.InteropServices;
using Project.Core.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Örnek verisi. RenderMeshInstanced yalnızca tanıdığı alan adlarını kabul eder (objectToWorld/prevObjectToWorld/renderingLayerMask);
    /// başka alan eklenirse çalışma anında "Field passed in is not a marshaled member" fırlatır. Renkler MaterialPropertyBlock
    /// SetVectorArray (_InstColor) ile ayrı verilir.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassInstance
    {
        public Matrix4x4 objectToWorld;
    }

    /// <summary>
    /// GPU instancing'li mesh çim. Kamera çevresinde 16 m'lik hücreler tembel üretilir (kare başına bütçeli), alphamap'ten
    /// çıkan yoğunlukla (çim katmanlarında var, yol/kaya/kar/asfaltta yok) yerleştirilir; 3 demet varyantı Graphics.RenderMeshInstanced
    /// ile çizilir. Frustum + mesafe elemesi, uzakta seyreltme ve boy solması vardır. Ezilme için GrassInteractors,
    /// rüzgâr için WindSystem kullanılır. Gölgelendirici yoksa sistem kendini kapatır (arazi detay çimi çalışmaya devam eder).
    ///
    /// Kullanım: GrassSystem.Install(camera, terrain, qualityTier, waterLevel).
    /// </summary>
    public sealed class GrassSystem : MonoBehaviour
    {
        private sealed class Cell
        {
            public int Cx, Cz;
            public GrassInstance[][] Data;
            public Vector4[][] Colors;
            public Vector4[][] ChunkColors; // yedek (URP/Lit) yol: 1023'lük parça başına ortalama ton
            public int[] Count;
            public Bounds Bounds;
            public bool Empty;
        }

        private const int ChunkSize = 1023;
        private const float MaxBladeScale = 1.7f;
        // Görünürlük ayarı: demetler 0.25-0.45 m boyda okunur, bıçaklar genişletilir (uzaktan kapsama), renk araziden açık.
        private static readonly float[] HeightMul = { 0.78f, 0.55f, 0.38f }; // ort. boy ~0.37-0.39 m (aralık ~0.3-0.45 m)
        private const float WidthMul = 2.1f;
        private const float TintBoost = 1.28f;

        private static readonly int FadeId = Shader.PropertyToID("_HarekatGrassFade");

        public static GrassSystem Instance { get; private set; }

        /// <summary>
        /// true: HAREKAT/Grass rüzgâr gölgelendiricisi denenir (bulunamazsa yine yedeğe düşer). Varsayılan false: URP/Lit yedek yol,
        /// rüzgâr gölgelendiricisi bir yakalamada kanıtlanana dek özel gölgelendirici gerekmez.
        /// </summary>
        public static bool PreferWindShader = false;

        /// <summary>Mesh çim çalışıyor ve arazi detay çimini devralmış mı.</summary>
        public static bool Active => Instance != null && Instance._active;

        private readonly Dictionary<long, Cell> _cells = new Dictionary<long, Cell>();
        private readonly List<GrassInstance>[] _scratch =
            { new List<GrassInstance>(1024), new List<GrassInstance>(1024), new List<GrassInstance>(1024) };
        private readonly List<Vector4>[] _scratchColor =
            { new List<Vector4>(1024), new List<Vector4>(1024), new List<Vector4>(1024) };
        private MaterialPropertyBlock _mpb;
        private readonly Vector4[] _tmpColors = new Vector4[ChunkSize];
        private static readonly int InstColorId = Shader.PropertyToID("_InstColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<long> _remove = new List<long>();
        private readonly List<Vector3Int> _missing = new List<Vector3Int>();
        private readonly Plane[] _planes = new Plane[6];

        private Camera _camera;
        private Terrain _terrain;
        private GrassDensityMap _map;
        private float _waterLevel = float.NegativeInfinity;
        private int _tier;
        private GrassTierConfig _cfg;
        private Mesh[] _meshes;
        private float _meshDetail = -1f;
        private Material _material;
        private bool _active;
        private bool _usingFallback;
        private float _savedDetailDensity = -1f;
        private int _seed = 1337;
        private int _lastCamCx = int.MinValue, _lastCamCz = int.MinValue;
        private int _frame;
        private string _why = "kurulmadi";
        private bool _firstDrawLogged, _zeroDrawLogged;
        private int _activeFrames;
        private float _coverage = -1f;      // yoğunluk > 0.01 olan alphamap hücre oranı
        private float _minDensity;          // kapsama ~0 ise (katman eşlemesi uyumsuz) tüm kara için taban yoğunluk

        /// <summary>Çizilen örnek sayısı (son kare) ve hücre sayısı: perf HUD / hata ayıklama.</summary>
        public int LastDrawnInstances { get; private set; }
        public int CellCount => _cells.Count;
        public int LastDrawCalls { get; private set; }

        /// <summary>Tek satır durum: perf CSV / hata ayıklama (kurulu değilse "grass=none").</summary>
        public static string Report()
        {
            var g = Instance;
            if (g == null) return "grass=none";
            return "grass=" + (g._active ? "on" : "off") + " mode=" + g.ModeName + " tier=" + g._tier + " cells=" + g._cells.Count
                   + " inst=" + g.LastDrawnInstances + " calls=" + g.LastDrawCalls
                   + " dist=" + g._cfg.DrawDistance.ToString("0") + " cover=" + g._coverage.ToString("0.00")
                   + (g._active ? "" : " why=" + g._why);
        }

        /// <summary>"wind" (HAREKAT/Grass), "urp-lit" (yedek) veya "none".</summary>
        public string ModeName => _material == null ? "none" : (_usingFallback ? "urp-lit" : "wind");

        /// <summary>Sahneye çim sistemini kurar (tekrar çağrılırsa mevcut olan güncellenir). Rüzgâr kademesi de ayarlanır.</summary>
        public static GrassSystem Install(Camera camera, Terrain terrain, int tier, float waterLevel = float.NegativeInfinity)
        {
            if (Instance == null)
            {
                var go = new GameObject("HK_Grass");
                Instance = go.AddComponent<GrassSystem>();
            }

            Instance.Configure(camera, terrain, tier, waterLevel);
            Debug.Log("[CIM] Install: " + Report() + " kamera=" + (camera != null ? camera.name : "yok") + " arazi=" + (terrain != null ? terrain.name : "yok"));
            return Instance;
        }

        /// <summary>Kalite kademesini değiştirir (0 Düşük: kapalı, arazi detay çimi geri gelir).</summary>
        public static void SetTier(int tier)
        {
            WindSystem.SetTier(tier);
            if (Instance != null)
                Instance.ApplyTier(tier);
        }

        /// <summary>Oyuncu/araç ezici ekle: GrassSystem.AddInteractor(playerTransform, 0.9f).</summary>
        public static void AddInteractor(Transform t, float radius) => GrassInteractors.Register(t, radius);

        public static void RemoveInteractor(Transform t) => GrassInteractors.Unregister(t);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Configure(Camera camera, Terrain terrain, int tier, float waterLevel)
        {
            _camera = camera;
            _waterLevel = waterLevel;
            if (_terrain != terrain)
            {
                RestoreTerrainDetails();
                _terrain = terrain;
                _map = BakeDensity(terrain, out _coverage);
                _minDensity = _map != null && _coverage < 0.02f ? 0.55f : 0f;
                if (_map != null && _minDensity > 0f)
                    Debug.LogWarning("[CIM] Alphamap kapsama " + _coverage.ToString("0.000") + " (<%2): katman eşlemesi çim saymıyor, taban yoğunluk 0.55 uygulanıyor.");
                ClearCells();
            }

            if (camera != null && camera.transform.root != null)
                GrassInteractors.Register(camera.transform.root, 0.9f);

            ApplyTier(tier);
        }

        private void ApplyTier(int tier)
        {
            _tier = Mathf.Clamp(tier, 0, 3);
            _cfg = GrassRules.ForTier(_tier);
            WindSystem.SetTier(_tier);

            bool want = _cfg.Enabled && _terrain != null && _map != null;
            if (!_cfg.Enabled) _why = "kademe0";
            else if (_terrain == null) _why = "arazi-yok";
            else if (_map == null) _why = "alphamap-yok";
            if (want)
            {
                EnsureAssets();
                want = _material != null && _meshes != null;
                if (!want) _why = "malzeme-yok";
            }

            if (want) _why = "ok";

            if (want != _active)
                ClearCells();
            _active = want;

            Shader.SetGlobalVector(FadeId, _active ? new Vector4(_cfg.DrawDistance, GrassRules.FadeWidth, 0f, 0f) : Vector4.zero);
            if (_active) SuppressTerrainDetails(); else RestoreTerrainDetails();
        }

        private void EnsureAssets()
        {
            float detail = _cfg.DetailScale;
            if (_meshes != null && Mathf.Approximately(_meshDetail, detail) && _material != null)
                return;
            DestroyAssets();
            _material = PreferWindShader ? GrassAssets.CreateGrassMaterial() : null;
            _usingFallback = false;
            if (_material == null)
            {
                _material = GrassAssets.CreateFallbackMaterial();
                _usingFallback = _material != null;
            }

            if (_material == null)
                return;
            _meshes = new Mesh[GrassRules.VariantCount];
            for (var v = 0; v < _meshes.Length; v++)
                _meshes[v] = GrassAssets.CreateMesh(v, _seed, detail);
            _meshDetail = detail;
        }

        private void DestroyAssets()
        {
            if (_meshes != null)
            {
                for (var i = 0; i < _meshes.Length; i++)
                    if (_meshes[i] != null) Destroy(_meshes[i]);
            }

            _meshes = null;
            if (_material != null) Destroy(_material);
            _material = null;
        }

        private static GrassDensityMap BakeDensity(Terrain terrain, out float coverage)
        {
            coverage = -1f;
            if (terrain == null || terrain.terrainData == null)
                return null;
            var td = terrain.terrainData;
            int w = td.alphamapWidth, h = td.alphamapHeight, layers = td.alphamapLayers;
            if (w < 2 || h < 2 || layers < 1)
                return null;
            var tp = terrain.transform.position;
            float texX = td.size.x / w, texZ = td.size.z / h;
            var map = new GrassDensityMap(w, h, tp.x + texX * 0.5f, tp.z + texZ * 0.5f, td.size.x - texX, td.size.z - texZ);
            var a = td.GetAlphamaps(0, 0, w, h);
            var weights = new float[layers];
            int covered = 0;
            for (var z = 0; z < h; z++)
            {
                for (var x = 0; x < w; x++)
                {
                    for (var l = 0; l < layers; l++)
                        weights[l] = a[z, x, l];
                    float dn = GrassRules.Density(weights, layers);
                    if (dn > 0.01f) covered++;
                    map.Set(x, z, dn, GrassRules.Dryness(weights, layers));
                }
            }

            coverage = covered / (float)(w * h);
            return map;
        }

        private void SuppressTerrainDetails()
        {
            if (_terrain == null) return;
            if (_savedDetailDensity < 0f)
                _savedDetailDensity = _terrain.detailObjectDensity;
            _terrain.detailObjectDensity = 0f;
        }

        private void RestoreTerrainDetails()
        {
            if (_terrain != null && _savedDetailDensity >= 0f)
                _terrain.detailObjectDensity = _savedDetailDensity;
            _savedDetailDensity = -1f;
        }

        private void ClearCells()
        {
            _cells.Clear();
            _lastCamCx = _lastCamCz = int.MinValue;
        }

        private void OnDestroy()
        {
            RestoreTerrainDetails();
            DestroyAssets();
            Shader.SetGlobalVector(FadeId, Vector4.zero);
            if (Instance == this) Instance = null;
        }

        private static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        private void LateUpdate()
        {
            if (!_active)
                return;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null || _terrain == null)
                return;

            _frame++;
            if ((_frame & 127) == 0 && _terrain.detailObjectDensity > 0f)
                _terrain.detailObjectDensity = 0f; // başka sistem (kademe uygulaması) geri açtıysa tekrar kapat

            var camPos = cam.transform.position;
            GrassInteractors.Upload(camPos, _cfg.Interactors, _cfg.DrawDistance + 8f);

            int ccx = GrassRules.CellIndex(camPos.x), ccz = GrassRules.CellIndex(camPos.z);
            if (ccx != _lastCamCx || ccz != _lastCamCz)
            {
                _lastCamCx = ccx; _lastCamCz = ccz;
                EvictFar(camPos);
            }

            BuildMissing(camPos, ccx, ccz);
            Draw(cam, camPos);
        }

        private void EvictFar(Vector3 camPos)
        {
            _remove.Clear();
            foreach (var kv in _cells)
            {
                var c = kv.Value;
                if (!GrassRules.CellShouldKeep(c.Cx, c.Cz, camPos.x, camPos.z, _cfg.DrawDistance))
                    _remove.Add(kv.Key);
            }

            for (var i = 0; i < _remove.Count; i++)
                _cells.Remove(_remove[i]);
        }

        private void BuildMissing(Vector3 camPos, int ccx, int ccz)
        {
            int range = Mathf.CeilToInt((_cfg.DrawDistance + GrassRules.CellSize) / GrassRules.CellSize);
            _missing.Clear();
            for (var dz = -range; dz <= range; dz++)
            {
                for (var dx = -range; dx <= range; dx++)
                {
                    int cx = ccx + dx, cz = ccz + dz;
                    if (!GrassRules.CellInRange(cx, cz, camPos.x, camPos.z, _cfg.DrawDistance)) continue;
                    if (_cells.ContainsKey(Key(cx, cz))) continue;
                    _missing.Add(new Vector3Int(cx, cz, dx * dx + dz * dz));
                }
            }

            int budget = Mathf.Min(_cfg.CellBuildsPerFrame, _missing.Count);
            for (var b = 0; b < budget; b++)
            {
                int best = 0;
                for (var i = 1; i < _missing.Count; i++)
                    if (_missing[i].z < _missing[best].z) best = i;
                var m = _missing[best];
                _missing[best] = _missing[_missing.Count - 1];
                _missing.RemoveAt(_missing.Count - 1);
                _cells[Key(m.x, m.y)] = BuildCell(m.x, m.y);
            }
        }

        private Cell BuildCell(int cx, int cz)
        {
            var cell = new Cell { Cx = cx, Cz = cz, Count = new int[GrassRules.VariantCount], Data = new GrassInstance[GrassRules.VariantCount][], Colors = new Vector4[GrassRules.VariantCount][], ChunkColors = new Vector4[GrassRules.VariantCount][] };
            for (var v = 0; v < _scratch.Length; v++) { _scratch[v].Clear(); _scratchColor[v].Clear(); }

            float ox = cx * GrassRules.CellSize, oz = cz * GrassRules.CellSize;
            int candidates = GrassRules.CandidatesPerCell(_cfg);
            float minY = float.MaxValue, maxY = float.MinValue;

            for (var i = 0; i < candidates; i++)
            {
                int k = i * 8;
                float x = ox + GrassRules.Hash01(cx, cz, k, _seed) * GrassRules.CellSize;
                float z = oz + GrassRules.Hash01(cx, cz, k + 1, _seed) * GrassRules.CellSize;
                float density = Mathf.Max(_map.SampleDensity(x, z), _minDensity);
                if (density <= 0.01f) continue;
                float patch = GrassRules.PatchNoise(x, z, _seed);
                density *= GrassRules.Lerp(0.6f, 1f, patch);
                if (GrassRules.Hash01(cx, cz, k + 2, _seed) > density) continue;

                float y = _terrain.SampleHeight(new Vector3(x, 0f, z)) + _terrain.transform.position.y;
                if (y < _waterLevel + 0.05f) continue;

                float rv = GrassRules.Hash01(cx, cz, k + 3, _seed);
                int variant = rv < 0.4f ? 0 : (rv < 0.78f ? 1 : 2);
                if (patch > 0.7f && variant == 0) variant = 1;

                float yaw = GrassRules.Hash01(cx, cz, k + 4, _seed) * 360f;
                float sx = GrassRules.Lerp(0.8f, 1.25f, GrassRules.Hash01(cx, cz, k + 5, _seed));
                float sy = GrassRules.Lerp(0.8f, 1.22f, GrassRules.Hash01(cx, cz, k + 6, _seed)) * GrassRules.Lerp(0.85f, 1.12f, patch);
                float tilt = (GrassRules.Hash01(cx, cz, k + 7, _seed) - 0.5f) * 14f;

                float dry = Mathf.Clamp01(_map.SampleDryness(x, z) * 0.8f + (patch - 0.5f) * 0.3f);
                float bright = GrassRules.Lerp(0.82f, 1.12f, GrassRules.Hash01(cx, cz, k + 6, _seed + 11));
                var green = new Color(0.92f, 1.0f, 0.88f);
                var yellow = new Color(1.25f, 1.05f, 0.62f);
                var c = Color.Lerp(green, yellow, dry) * (bright * TintBoost);

                var inst = new GrassInstance
                {
                    objectToWorld = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(tilt, yaw, tilt * 0.6f), new Vector3(sx * WidthMul, sy * HeightMul[variant], sx * WidthMul))
                };
                _scratch[variant].Add(inst);
                _scratchColor[variant].Add(new Vector4(c.r, c.g, c.b, 1f));
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            int total = 0;
            for (var v = 0; v < _scratch.Length; v++)
            {
                cell.Data[v] = _scratch[v].ToArray();
                cell.Colors[v] = _scratchColor[v].ToArray();
                cell.Count[v] = cell.Data[v].Length;
                int chunks = (cell.Count[v] + ChunkSize - 1) / ChunkSize;
                var cc = new Vector4[chunks];
                for (var ch = 0; ch < chunks; ch++)
                {
                    int from = ch * ChunkSize, to = Mathf.Min(cell.Count[v], from + ChunkSize);
                    var sum = Vector4.zero;
                    for (var i = from; i < to; i++) sum += cell.Colors[v][i];
                    sum /= Mathf.Max(1, to - from);
                    // parça başına küçük sabit ton farkı (hücre/parça karması): düz tek renk görünümünü kırar
                    float jit = GrassRules.Lerp(0.9f, 1.1f, GrassRules.Hash01(cx, cz, 900 + v * 31 + ch, _seed));
                    var bc = GrassAssets.FallbackBaseColor;
                    cc[ch] = new Vector4(Mathf.Min(1.4f, bc.r * sum.x / TintBoost * jit), Mathf.Min(1.4f, bc.g * sum.y / TintBoost * jit), Mathf.Min(1.4f, bc.b * sum.z / TintBoost * jit), 1f);
                }

                cell.ChunkColors[v] = cc;
                total += cell.Count[v];
            }

            cell.Empty = total == 0;
            if (!cell.Empty)
            {
                float top = maxY + MaxBladeScale;
                var center = new Vector3(ox + GrassRules.CellSize * 0.5f, (minY + top) * 0.5f, oz + GrassRules.CellSize * 0.5f);
                cell.Bounds = new Bounds(center, new Vector3(GrassRules.CellSize + 1f, top - minY, GrassRules.CellSize + 1f));
            }

            return cell;
        }

        private void Draw(Camera cam, Vector3 camPos)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            var rp = new RenderParams(_material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer
            };

            int drawn = 0, calls = 0;
            foreach (var kv in _cells)
            {
                var cell = kv.Value;
                if (cell.Empty) continue;
                float dx = cell.Bounds.center.x - camPos.x, dz = cell.Bounds.center.z - camPos.z;
                float dist = Mathf.Sqrt(dx * dx + dz * dz);
                // Hücreye en yakın nokta için yarı çaprazı düş; çizim mesafesi dışındakini ele.
                float near = Mathf.Max(0f, dist - GrassRules.CellSize * 0.7072f);
                if (near > _cfg.DrawDistance) continue;
                if (!GeometryUtility.TestPlanesAABB(_planes, cell.Bounds)) continue;

                float keep = GrassRules.KeepFraction(near, _cfg.DrawDistance);
                rp.worldBounds = cell.Bounds;
                for (var v = 0; v < cell.Count.Length; v++)
                {
                    int n = Mathf.CeilToInt(cell.Count[v] * keep);
                    if (n <= 0) continue;
                    var data = cell.Data[v];
                    var cols = cell.Colors[v];
                    for (var start = 0; start < n; start += ChunkSize)
                    {
                        int c = Mathf.Min(ChunkSize, n - start);
                        // Renkler instanceData ile verilemez (yalnız objectToWorld alanı tanınır); MPB dizisiyle instanced _InstColor doldurulur.
                        if (_usingFallback)
                        {
                            _mpb ??= new MaterialPropertyBlock();
                            var fc = cell.ChunkColors[v][start / ChunkSize];
                            _mpb.SetColor(BaseColorId, fc);
                            _mpb.SetColor(ColorId, fc);
                            rp.matProps = _mpb;
                        }
                        else if (cols != null)
                        {
                            for (var i = 0; i < c; i++) _tmpColors[i] = cols[start + i];
                            for (var i = c; i < ChunkSize; i++) _tmpColors[i] = Vector4.one;
                            _mpb ??= new MaterialPropertyBlock();
                            _mpb.SetVectorArray(InstColorId, _tmpColors);
                            rp.matProps = _mpb;
                        }
                        Graphics.RenderMeshInstanced(rp, _meshes[v], 0, data, c, start);
                        calls++;
                    }

                    drawn += n;
                }
            }

            LastDrawnInstances = drawn;
            LastDrawCalls = calls;
            _activeFrames++;
            if (!_firstDrawLogged && drawn > 0)
            {
                _firstDrawLogged = true;
                Debug.Log("[CIM] İlk çizim: " + Report());
            }
            else if (!_zeroDrawLogged && !_firstDrawLogged && _activeFrames == 180)
            {
                _zeroDrawLogged = true;
                Debug.LogWarning("[CIM] 180 karede hiç çizim yok: " + Report() + " kamera=" + cam.name + " konum=" + camPos);
            }
        }
    }
}
