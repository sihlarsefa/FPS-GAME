using UnityEngine;

namespace Project.Infrastructure.Combat
{
    /// <summary>
    /// Bölge duvarı için prosedürel altıgen enerji deseni (alfa kanalı): U yönünde döşenebilir, V yönünde bant boyunca
    /// dikey sönüm içerir. Piksel hesabı saf fonksiyondur (test edilebilir).
    /// </summary>
    public static class ZoneWallPattern
    {
        public const int Size = 256;
        private const int ColumnPairs = 4;

        /// <summary>(u,v) 0..1 için desen alfa'sı 0..1.</summary>
        public static float Sample(float u, float v)
        {
            // Düz tepeli altıgenler; yatay periyot 3s (2 sütun) = 1 birim.
            var s = 1f / (3f * ColumnPairs);
            var px = u / s;
            var py = v / s;
            const float sqrt3 = 1.7320508f;

            var cx0 = Mathf.Floor(px / 1.5f);
            var best = float.MaxValue;
            var bestId = 0;
            for (var dc = -1; dc <= 1; dc++)
            {
                var col = cx0 + dc;
                var centerX = col * 1.5f;
                var offset = (((int)col & 1) != 0) ? sqrt3 * 0.5f : 0f;
                var row0 = Mathf.Floor((py - offset) / sqrt3);
                for (var dr = -1; dr <= 1; dr++)
                {
                    var row = row0 + dr;
                    var centerY = row * sqrt3 + offset;
                    var qx = Mathf.Abs(px - centerX);
                    var qy = Mathf.Abs(py - centerY);
                    var h = Mathf.Max(qy, qx * 0.8660254f + qy * 0.5f);
                    if (h < best)
                    {
                        best = h;
                        // Hücre kimliği U'da periyodik olmalı (col mod 2*ColumnPairs).
                        var colWrapped = (((int)col % (2 * ColumnPairs)) + 2 * ColumnPairs) % (2 * ColumnPairs);
                        bestId = colWrapped * 7919 + (int)row * 104729;
                    }
                }
            }

            var inner = sqrt3 * 0.5f;
            var edge = inner - best; // kenara uzaklık (0 = kenar)
            var line = 1f - Mathf.Clamp01(edge / 0.07f);
            line *= line;
            var cell = Hash01(bestId);
            var glow = cell > 0.82f ? 0.22f : 0.05f + cell * 0.05f;
            var alpha = Mathf.Clamp01(line * 0.85f + glow);
            return alpha * ZoneWallGeometry.VerticalFade(v);
        }

        public static float Hash01(int n)
        {
            unchecked
            {
                var h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFu) / 65535f;
            }
        }

        /// <summary>Beyaz RGB + desen alfa'sı; doğrusal renk, V kenetli, U tekrarlı.</summary>
        public static Texture2D BuildTexture()
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, true)
            {
                name = "ZoneWallPattern",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color32[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                var v = (y + 0.5f) / Size;
                for (var x = 0; x < Size; x++)
                {
                    var u = (x + 0.5f) / Size;
                    var a = (byte)Mathf.RoundToInt(Sample(u, v) * 255f);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }
    }
}
