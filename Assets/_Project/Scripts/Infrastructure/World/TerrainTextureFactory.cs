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
        public const int DefaultTextureSize = 256;

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
                case TerrainLayerKind.Grass: return 9f;
                case TerrainLayerKind.DryGrass: return 10f;
                case TerrainLayerKind.Dirt: return 8f;
                case TerrainLayerKind.Rock: return 13f;
                case TerrainLayerKind.Gravel: return 5f;
                case TerrainLayerKind.Mud: return 7f;
                case TerrainLayerKind.Snow: return 15f;
                default: return 6f;
            }
        }

        /// <summary>Katman pürüzsüzlüğü (URP Terrain/Lit sabit pürüzsüzlük).</summary>
        public static float Smoothness(TerrainLayerKind kind)
        {
            switch (kind)
            {
                case TerrainLayerKind.Mud: return 0.42f;
                case TerrainLayerKind.Snow: return 0.35f;
                case TerrainLayerKind.Asphalt: return 0.18f;
                case TerrainLayerKind.Rock: return 0.16f;
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
                case TerrainLayerKind.Snow: return new Color(0.9f, 0.92f, 0.96f);
                default: return new Color(0.2f, 0.2f, 0.21f);
            }
        }

        /// <summary>Tüm katmanları (kanal sırasıyla) üretir.</summary>
        public static TerrainLayer[] CreateLayers(int seed, int textureSize = DefaultTextureSize)
        {
            var layers = new TerrainLayer[LayerCount];
            for (var i = 0; i < LayerCount; i++)
                layers[i] = CreateLayer((TerrainLayerKind)i, seed, textureSize);
            return layers;
        }

        /// <summary>Tek bir TerrainLayer (dokusuyla birlikte) üretir.</summary>
        public static TerrainLayer CreateLayer(TerrainLayerKind kind, int seed, int textureSize = DefaultTextureSize)
        {
            var tile = TileSize(kind);
            var layer = new TerrainLayer
            {
                name = "HK_TL_" + LayerName(kind),
                diffuseTexture = CreateTexture(kind, seed, textureSize),
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
            var pixels = ComputePixels(kind, seed, size);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "HK_Terrain_" + LayerName(kind),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
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
            var c = Color.Lerp(new Color(0.27f, 0.36f, 0.15f), new Color(0.38f, 0.46f, 0.2f), Mathf.Clamp01(low * 1.2f - 0.1f));
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
            var c = Color.Lerp(new Color(0.8f, 0.84f, 0.9f), new Color(0.95f, 0.96f, 0.98f), low);
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
}
