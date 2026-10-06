using Project.Infrastructure.Rendering;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Arazi katmanları (TerrainData.terrainLayers / alphamap kanal sırası). İlk dördü temel geçişte çizilir.</summary>
    public enum TerrainLayerKind
    {
        Grass = 0,
        DryGrass = 1,
        Dirt = 2,
        Rock = 3,
        Gravel = 4,
        Mud = 5,
        Snow = 6,
        Asphalt = 7
    }

    /// <summary>
    /// Arazi katmanları için prosedürel, DÖŞENEBİLİR (kenarları tekrar eden) dokular ve TerrainLayer nesneleri üretir.
    /// Dokular okunabilir bırakılır (editör PNG olarak kaydedebilir). Aynı tohum aynı dokuyu verir.
    /// </summary>
    public static class TerrainTextureFactory
    {
        public const int LayerCount = 8;
        public const int DefaultTextureSize = 1024;

        /// <summary>Editör/yüksek kalite çözünürlüğü (AssetGeneration ya da ayarlar bunu seçebilir).</summary>
        public const int HighTextureSize = 1024;

        /// <summary>Ultra kalite çözünürlüğü (bellek: 8 katman x 3 doku; yalnız Ultra'da).</summary>
        public const int UltraTextureSize = 2048;

        /// <summary>Kalite kademesine (0..3) göre katman doku çözünürlüğü: Ultra 2048, diğerleri 1024.</summary>
        public static int TextureSizeForTier(int tier) => tier >= 3 ? UltraTextureSize : DefaultTextureSize;

        /// <summary>Normal harita gücü (bayağı kabartma) — katman türüne göre.</summary>
        public static float NormalStrength(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Rock: return 8f;
                case TerrainLayerKind.Gravel: return 7f;
                case TerrainLayerKind.Dirt: return 5f;
                case TerrainLayerKind.Mud: return 4f;
                case TerrainLayerKind.Grass: return 4f;
                case TerrainLayerKind.DryGrass: return 3.5f;
                case TerrainLayerKind.Snow: return 2f;
                default: return 3f;
            }
        }

        /// <summary>Katman maskesi için ortalama ao gücü.</summary>
        private static float AoStrength(TerrainLayerKind kind) =>
            kind == TerrainLayerKind.Rock || kind == TerrainLayerKind.Gravel ? 0.55f : 0.35f;

        /// <summary>Katman adları (varlık adları için, ASCII).</summary>
        public static string LayerName(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Grass: return "Grass";
                case TerrainLayerKind.DryGrass: return "DryGrass";
                case TerrainLayerKind.Dirt: return "Dirt";
                case TerrainLayerKind.Rock: return "Rock";
                case TerrainLayerKind.Gravel: return "Gravel";
                case TerrainLayerKind.Mud: return "Mud";
                case TerrainLayerKind.Snow: return "Snow";
                default: return "Asphalt";
            }
        }

        /// <summary>Doku döşeme boyu (m).</summary>
        public static float TileSize(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Grass: return 3f;
                case TerrainLayerKind.DryGrass: return 3.5f;
                case TerrainLayerKind.Dirt: return 3f;
                case TerrainLayerKind.Rock: return 4f;
                case TerrainLayerKind.Gravel: return 2f;
                case TerrainLayerKind.Mud: return 3f;
                case TerrainLayerKind.Snow: return 4f;
                default: return 4f; // Asfalt: zemin katmanları 2-4 m döşeme (yakından pikselleşme sınırı)
            }
        }

        /// <summary>Katman pürüzsüzlüğü (URP Terrain/Lit sabit pürüzsüzlük).</summary>
        public static float Smoothness(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Mud: return 0.42f;
                case TerrainLayerKind.Snow: return 0.25f;
                case TerrainLayerKind.Asphalt: return 0.18f;
                case TerrainLayerKind.Rock: return 0.12f;
                default: return 0.06f;
            }
        }

        /// <summary>Katmanın ortalama rengi (mini harita / uzak görünüm için yaklaşık).</summary>
        public static Color AverageColor(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Grass: return new Color(0.33f, 0.41f, 0.18f);
                case TerrainLayerKind.DryGrass: return new Color(0.6f, 0.54f, 0.32f);
                case TerrainLayerKind.Dirt: return new Color(0.43f, 0.34f, 0.24f);
                case TerrainLayerKind.Rock: return new Color(0.5f, 0.48f, 0.45f);
                case TerrainLayerKind.Gravel: return new Color(0.52f, 0.5f, 0.46f);
                case TerrainLayerKind.Mud: return new Color(0.28f, 0.22f, 0.15f);
                case TerrainLayerKind.Snow: return new Color(0.86f, 0.89f, 0.94f);
                default: return new Color(0.2f, 0.2f, 0.21f);
            }
        }

        /// <summary>Tüm katmanları (kanal sırasıyla) üretir.</summary>
        public static TerrainLayer[] CreateLayers(int seed, int textureSize = DefaultTextureSize)
        {
            var layers = new TerrainLayer[LayerCount];
            for (var i = 0; i < LayerCount; i++)
            {
                // C7 ContentOverrides: TerrainLayerOverride varsa o katman kullanılır (eksik normal/mask tamamlanır,
                // döşeme 2-4 m'ye kıstırılır), yoksa prosedürel katman aynen üretilir.
                if (TryGetOverrideLayer((TerrainLayerKind)i, out var overrideLayer))
                    layers[i] = EnsureOverrideLayer(overrideLayer, (TerrainLayerKind)i, seed, textureSize);
                else
                    layers[i] = CreateLayer((TerrainLayerKind)i, seed, textureSize);
            }
            return layers;
        }

        /// <summary>Katman türünün ContentOverrides'taki malzeme kimliği (8 katmanın hepsi override edilebilir).</summary>
        public static Project.Infrastructure.Rendering.MaterialId MaterialFor(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Grass: return Project.Infrastructure.Rendering.MaterialId.Grass;
                case TerrainLayerKind.DryGrass: return Project.Infrastructure.Rendering.MaterialId.DryGrass;
                case TerrainLayerKind.Dirt: return Project.Infrastructure.Rendering.MaterialId.Dirt;
                case TerrainLayerKind.Rock: return Project.Infrastructure.Rendering.MaterialId.Rock;
                case TerrainLayerKind.Gravel: return Project.Infrastructure.Rendering.MaterialId.Gravel;
                case TerrainLayerKind.Mud: return Project.Infrastructure.Rendering.MaterialId.Mud;
                case TerrainLayerKind.Snow: return Project.Infrastructure.Rendering.MaterialId.Snow;
                default: return Project.Infrastructure.Rendering.MaterialId.Asphalt;
            }
        }

        /// <summary>Override katmanı (null güvenli; ContentOverrides yüklenemezse false).</summary>
        public static bool TryGetOverrideLayer(TerrainLayerKind kind, out TerrainLayer layer)
        {
            layer = null;
            try
            {
                return Project.Infrastructure.Content.ContentOverrides.TryGetTerrainLayer(MaterialFor(kind), out layer) && layer != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TerrainTextureFactory] Arazi katmanı override okunamadı: " + e.Message);
                layer = null;
                return false;
            }
        }

        /// <summary>
        /// Override katmanını zemin sözleşmesine getirir: eksik normal/mask prosedürel üretimle tamamlanır (HER katmanda
        /// normal+mask şarttır), döşeme boyu 2-4 m aralığına kıstırılır (2K dokuda ≈512 px/m; büyük döşeme yakından pikselleşir).
        /// </summary>
        public static TerrainLayer EnsureOverrideLayer(TerrainLayer layer, TerrainLayerKind kind, int seed, int textureSize = DefaultTextureSize)
        {
            if (layer == null)
                return CreateLayer(kind, seed, textureSize);

            if (layer.normalMapTexture == null || layer.maskMapTexture == null)
            {
                textureSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(textureSize, 32, 2048));
                var height = ProceduralPbr.HeightFromLuminance(ComputePixels(kind, seed, textureSize), textureSize);
                if (layer.normalMapTexture == null)
                    layer.normalMapTexture = ProceduralPbr.MakeTexture("HK_Terrain_" + LayerName(kind) + "_N", textureSize,
                        ProceduralPbr.NormalFromHeight(height, textureSize, NormalStrength(kind)), true);
                if (layer.maskMapTexture == null)
                    layer.maskMapTexture = ProceduralPbr.MakeTexture("HK_Terrain_" + LayerName(kind) + "_M", textureSize,
                        ProceduralPbr.MaskFromHeight(height, textureSize, 0f, AoStrength(kind), Mathf.Clamp01(Smoothness(kind) * 2.2f), 0.5f), true);
            }

            var tile = layer.tileSize;
            var clamped = new Vector2(Mathf.Clamp(tile.x, 2f, 4f), Mathf.Clamp(tile.y, 2f, 4f));
            if (clamped != tile)
                layer.tileSize = clamped;
            return layer;
        }

        /// <summary>Tek bir TerrainLayer (dokusuyla birlikte) üretir.</summary>
        public static TerrainLayer CreateLayer(TerrainLayerKind kind, int seed, int textureSize = DefaultTextureSize)
        {
            var tile = TileSize(kind);
            textureSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(textureSize, 32, 2048));
            var pixels = ComputePixels(kind, seed, textureSize);
            var height = ProceduralPbr.HeightFromLuminance(pixels, textureSize);
            var layer = new TerrainLayer
            {
                name = "HK_TL_" + LayerName(kind),
                diffuseTexture = MakeTexture(kind, textureSize, pixels),
                normalMapTexture = ProceduralPbr.MakeTexture("HK_Terrain_" + LayerName(kind) + "_N", textureSize,
                    ProceduralPbr.NormalFromHeight(height, textureSize, NormalStrength(kind)), true),
                maskMapTexture = ProceduralPbr.MakeTexture("HK_Terrain_" + LayerName(kind) + "_M", textureSize,
                    ProceduralPbr.MaskFromHeight(height, textureSize, 0f, AoStrength(kind), Mathf.Clamp01(Smoothness(kind) * 2.2f), 0.5f), true),
                normalScale = 1f,
                tileSize = new Vector2(tile, tile),
                tileOffset = Vector2.zero,
                smoothness = Smoothness(kind),
                metallic = 0f,
                specular = Color.black
            };
            layer.smoothnessSource = TerrainLayerSmoothnessSource.ConstantOnly;
            return layer;
        }

        /// <summary>Katman dokusu (RGBA32, mip'li, döşenebilir, okunabilir).</summary>
        public static Texture2D CreateTexture(TerrainLayerKind kind, int seed, int size = DefaultTextureSize)
        {
            size = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size, 32, 2048));
            return MakeTexture(kind, size, ComputePixels(kind, seed, size));
        }

        private static Texture2D MakeTexture(TerrainLayerKind kind, int size, Color32[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "HK_Terrain_" + LayerName(kind),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8 // eğik bakışta (zemine paralel) netlik
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>Doku piksellerini üretir (Unity nesnesi gerektirmez; size 2'nin kuvveti olmalı).</summary>
        public static Color32[] ComputePixels(TerrainLayerKind kind, int seed, int size)
        {
            var noise = new TerrainNoise(seed * 7 + (int)kind * 101 + 3);
            var pixels = new Color32[size * size];
            var inv = 1f / size;
            for (var py = 0; py < size; py++)
            {
                var v = py * inv;
                for (var px = 0; px < size; px++)
                {
                    var u = px * inv;
                    pixels[py * size + px] = Shade(kind, noise, u, v, px, py, size);
                }
            }

            return pixels;
        }

        // ================================================================== Gölgelendirme

        private static Color32 Shade(TerrainLayerKind kind, TerrainNoise n, float u, float v, int px, int py, int size)
        {
            switch (kind)
            {
                case TerrainLayerKind.Grass: return Grass(n, u, v, px, py);
                case TerrainLayerKind.DryGrass: return DryGrass(n, u, v, px, py);
                case TerrainLayerKind.Dirt: return Dirt(n, u, v, px, py);
                case TerrainLayerKind.Rock: return Rock(n, u, v, px, py);
                case TerrainLayerKind.Gravel: return Gravel(n, u, v, px, py);
                case TerrainLayerKind.Mud: return Mud(n, u, v, px, py);
                case TerrainLayerKind.Snow: return Snow(n, u, v, px, py);
                default: return Asphalt(n, u, v, px, py);
            }
        }

        private static Color32 Grass(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f, v * 4f, 4, 4) * 0.5f + 0.5f;
            var mid = n.FbmPeriodic(u * 16f + 3f, v * 16f + 7f, 16, 3) * 0.5f + 0.5f;
            var blades = Hash01(px, py, 11);
            var c = Color.Lerp(new Color(0.24f, 0.31f, 0.13f), new Color(0.33f, 0.40f, 0.17f), Mathf.Clamp01(low * 1.2f - 0.1f));
            c = Color.Lerp(c, new Color(0.22f, 0.3f, 0.12f), Mathf.Clamp01(mid - 0.55f) * 1.3f);
            // İnce çim lifleri ve sarımsı benekler.
            c *= 0.86f + 0.28f * blades;
            if (Hash01(px >> 1, py >> 1, 23) > 0.985f)
                c = Color.Lerp(c, new Color(0.55f, 0.52f, 0.28f), 0.6f);
            return ToColor32(c);
        }

        private static Color32 DryGrass(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 1f, v * 4f + 2f, 4, 4) * 0.5f + 0.5f;
            var streak = n.PerlinPeriodic(u * 32f, v * 6f, 32, 6) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.55f, 0.49f, 0.28f), new Color(0.68f, 0.61f, 0.37f), low);
            c = Color.Lerp(c, new Color(0.46f, 0.42f, 0.25f), Mathf.Clamp01(streak - 0.6f) * 1.5f);
            c *= 0.88f + 0.24f * Hash01(px, py, 29);
            if (Hash01(px >> 2, py >> 2, 31) > 0.97f)
                c = Color.Lerp(c, new Color(0.4f, 0.44f, 0.22f), 0.45f);
            return ToColor32(c);
        }

        private static Color32 Dirt(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 5f, v * 4f + 1f, 4, 5) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.37f, 0.28f, 0.19f), new Color(0.5f, 0.39f, 0.28f), low);
            // Küçük çakıllar.
            var cell = Voronoi(u, v, 18, 41, out var cellHash);
            if (cell < 0.22f && cellHash > 0.45f)
            {
                var pebble = Color.Lerp(new Color(0.52f, 0.47f, 0.4f), new Color(0.62f, 0.58f, 0.52f), cellHash);
                c = Color.Lerp(c, pebble * (1.05f - cell * 1.5f), 0.85f);
            }

            c *= 0.9f + 0.2f * Hash01(px, py, 37);
            return ToColor32(c);
        }

        private static Color32 Rock(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 9f, v * 4f + 4f, 4, 5) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.42f, 0.41f, 0.38f), new Color(0.6f, 0.58f, 0.54f), low);

            // Kaya blokları: alan bükmeli (doğal, düzensiz) hücreler; hücre başına ton, sınırlarda çatlak.
            var wu = u + 0.07f * n.PerlinPeriodic(u * 4f + 1.3f, v * 4f + 7.1f, 4, 4);
            var wv = v + 0.07f * n.PerlinPeriodic(u * 4f + 5.7f, v * 4f + 2.9f, 4, 4);
            Voronoi2(wu, wv, 4, 83, out var f1, out var f2, out var plate);
            c *= 0.9f + 0.16f * plate;
            var seam = 1f - TerrainNoise.SmoothStep(0.01f, 0.05f, f2 - f1);
            c = Color.Lerp(c, new Color(0.27f, 0.26f, 0.24f), seam * 0.55f);

            // Hafif tabakalanma (tam sayı periyot → döşenebilir).
            var strata = Mathf.Sin((v * 9f + n.PerlinPeriodic(u * 4f, v * 4f, 4, 4) * 0.4f) * Mathf.PI * 2f) * 0.5f + 0.5f;
            c *= 0.94f + 0.08f * strata;

            // Seyrek ince çatlaklar (yalnızca bazı bölgelerde).
            var fineCrack = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(n.PerlinPeriodic(u * 16f + 2f, v * 16f + 6f, 16, 16))), 28f);
            c = Color.Lerp(c, new Color(0.26f, 0.25f, 0.23f), fineCrack * 0.5f * TerrainNoise.SmoothStep(0.45f, 0.7f, low));

            // Liken lekeleri.
            var lichen = n.PerlinPeriodic(u * 12f + 4f, v * 12f + 1f, 12, 12);
            if (lichen > 0.45f)
                c = Color.Lerp(c, new Color(0.47f, 0.5f, 0.36f), (lichen - 0.45f) * 1.2f);
            c *= 0.94f + 0.12f * Hash01(px, py, 43);
            return ToColor32(c);
        }

        private static Color32 Gravel(TerrainNoise n, float u, float v, int px, int py)
        {
            var d = Voronoi(u, v, 26, 53, out var cellHash);
            var stone = Color.Lerp(new Color(0.45f, 0.43f, 0.4f), new Color(0.64f, 0.61f, 0.56f), cellHash);
            var gap = new Color(0.3f, 0.27f, 0.23f);
            var t = TerrainNoise.SmoothStep(0.36f, 0.5f, d);
            var c = Color.Lerp(stone * (1.08f - d * 0.5f), gap, t);
            var low = n.FbmPeriodic(u * 4f, v * 4f, 4, 3) * 0.5f + 0.5f;
            c *= 0.9f + 0.18f * low;
            c *= 0.94f + 0.12f * Hash01(px, py, 59);
            return ToColor32(c);
        }

        private static Color32 Mud(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 2f, v * 4f + 8f, 4, 4) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.22f, 0.17f, 0.12f), new Color(0.33f, 0.26f, 0.18f), low);
            var wet = n.FbmPeriodic(u * 8f + 6f, v * 8f + 3f, 8, 3);
            if (wet > 0.15f)
                c = Color.Lerp(c, new Color(0.16f, 0.13f, 0.1f), Mathf.Clamp01((wet - 0.15f) * 2.5f));
            c *= 0.94f + 0.12f * Hash01(px, py, 61);
            return ToColor32(c);
        }

        private static Color32 Snow(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 7f, v * 4f + 5f, 4, 4) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.8f, 0.84f, 0.9f), new Color(0.89f, 0.91f, 0.95f), low);
            c *= 0.97f + 0.05f * Hash01(px, py, 67);
            return ToColor32(c);
        }

        private static Color32 Asphalt(TerrainNoise n, float u, float v, int px, int py)
        {
            var low = n.FbmPeriodic(u * 4f + 3f, v * 4f + 9f, 4, 4) * 0.5f + 0.5f;
            var c = Color.Lerp(new Color(0.15f, 0.15f, 0.16f), new Color(0.22f, 0.22f, 0.23f), low);
            var h = Hash01(px, py, 71);
            if (h > 0.93f)
                c = Color.Lerp(c, new Color(0.42f, 0.41f, 0.39f), (h - 0.93f) * 10f);
            else
                c *= 0.92f + 0.12f * h;
            // Yama izleri.
            var patch = n.PerlinPeriodic(u * 6f + 1f, v * 6f + 2f, 6, 6);
            if (patch > 0.5f)
                c *= 0.88f;
            return ToColor32(c);
        }

        // ================================================================== Yardımcılar

        /// <summary>Döşenebilir Voronoi F1 uzaklığı (hücre birimi, ~0..0.8) ve en yakın hücre özeti (0..1).</summary>
        private static float Voronoi(float u, float v, int cells, int salt, out float cellHash)
        {
            var x = u * cells;
            var y = v * cells;
            var cx = Mathf.FloorToInt(x);
            var cy = Mathf.FloorToInt(y);
            var best = float.MaxValue;
            cellHash = 0f;
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var gx = cx + ox;
                    var gy = cy + oy;
                    var wx = Mod(gx, cells);
                    var wy = Mod(gy, cells);
                    var fx = gx + 0.15f + 0.7f * Hash01(wx, wy, salt);
                    var fy = gy + 0.15f + 0.7f * Hash01(wx, wy, salt + 1);
                    var dx = fx - x;
                    var dy = fy - y;
                    var d = dx * dx + dy * dy;
                    if (d < best)
                    {
                        best = d;
                        cellHash = Hash01(wx, wy, salt + 2);
                    }
                }
            }

            return Mathf.Sqrt(best);
        }

        /// <summary>Döşenebilir Voronoi: en yakın (f1) ve ikinci en yakın (f2) öznitelik uzaklıkları, en yakın hücre özeti.</summary>
        private static void Voronoi2(float u, float v, int cells, int salt, out float f1, out float f2, out float cellHash)
        {
            var x = u * cells;
            var y = v * cells;
            var cx = Mathf.FloorToInt(x);
            var cy = Mathf.FloorToInt(y);
            var best = float.MaxValue;
            var second = float.MaxValue;
            cellHash = 0f;
            for (var oy = -1; oy <= 1; oy++)
            {
                for (var ox = -1; ox <= 1; ox++)
                {
                    var gx = cx + ox;
                    var gy = cy + oy;
                    var wx = Mod(gx, cells);
                    var wy = Mod(gy, cells);
                    var fx = gx + 0.1f + 0.8f * Hash01(wx, wy, salt);
                    var fy = gy + 0.1f + 0.8f * Hash01(wx, wy, salt + 1);
                    var dx = fx - x;
                    var dy = fy - y;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d < best)
                    {
                        second = best;
                        best = d;
                        cellHash = Hash01(wx, wy, salt + 2);
                    }
                    else if (d < second)
                    {
                        second = d;
                    }
                }
            }

            f1 = best;
            f2 = second;
        }

        private static int Mod(int a, int m)
        {
            var r = a % m;
            return r < 0 ? r + m : r;
        }

        /// <summary>Tam sayı karması → [0,1).</summary>
        internal static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static Color32 ToColor32(Color c)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255),
                255);
        }
    }

    /// <summary>
    /// Arazi makro renk çeşitlemesi (saf, deterministik). 40-180 m ölçeğinde kuru saman yamaları, nemli koyu çukurlar (±%10 parlaklık)
    /// ve ağaç gölgesi toprağı benekleri. Çıktılar çarpandır (nötr = 1).
    /// </summary>
    public static class TerrainMacroVariation
    {
        public const float MaxBrightness = 0.10f;
        public const float MinMult = 0.70f;
        public const float MaxMult = 1.15f;

        /// <summary>Kaya/uçurum/moloz varsa çeşitleme azalır (1 = serbest, 0 = dokunma).</summary>
        public static float Protection(float cliff, float scree)
        {
            return Mathf.Clamp01(1f - 1.6f * Mathf.Clamp01(cliff) - 1.2f * Mathf.Clamp01(scree));
        }

        public static void Evaluate(float straw, float moist, float canopy, float freckle, float protect,
            out float bright, out float r, out float g, out float b)
        {
            protect = Mathf.Clamp01(protect);
            var s = Smooth(0.12f, 0.55f, Mathf.Clamp(straw, -1f, 1f));
            var m = Smooth(0.15f, 0.6f, -Mathf.Clamp(moist, -1f, 1f));
            // Gölge toprağı: gölgelik kümesi içinde sık benek.
            var c = Smooth(0.45f, 0.8f, Mathf.Clamp01(canopy)) * Smooth(0.5f, 0.75f, Mathf.Clamp01(freckle));

            bright = 1f + MaxBrightness * 0.6f * s - MaxBrightness * m - 0.08f * c;
            r = 1f + 0.10f * s - 0.03f * m + 0.05f * c;
            g = 1f + 0.03f * s + 0.02f * m - 0.07f * c;
            b = 1f - 0.12f * s - 0.02f * m - 0.12f * c;

            bright = 1f + (bright - 1f) * protect;
            r = 1f + (r - 1f) * protect;
            g = 1f + (g - 1f) * protect;
            b = 1f + (b - 1f) * protect;
        }

        private static float Smooth(float a, float b, float x)
        {
            var t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
