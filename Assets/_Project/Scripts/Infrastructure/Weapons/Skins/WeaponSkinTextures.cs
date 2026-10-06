using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.Weapons.Skins
{
    /// <summary>Prosedürel kaplama dokusu: desen + kenar aşınma maskesi. Boyut kalite kademesine göre 256..1024.</summary>
    public static class WeaponSkinTextures
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static int SizeForTier(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3)) { case 0: return 256; case 1: return 512; case 2: return 768; default: return 1024; }
        }

        /// <summary>Saf gürültü: deterministik 0..1 hash.</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Kenar aşınma maskesi 0..1 (1 = çıplak metal): kenara yakınlık + gürültü, wear ile ölçeklenir.</summary>
        public static float EdgeWear(int x, int y, int size, float wear, int seed)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            float edge = 1f - Mathf.Clamp01(Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) / 0.12f);
            float n = Hash(x / 2, y / 2, seed);
            return Mathf.Clamp01((edge * 1.2f + (n - 0.5f) * 0.5f) * Mathf.Clamp01(wear) * 1.6f - 0.2f);
        }

        public static Color Sample(in WeaponSkin s, int x, int y, int size, int seed)
        {
            float t;
            switch (s.Pattern)
            {
                case SkinPattern.Digital:
                    int cell = Mathf.Max(4, size / 64);
                    t = Hash(x / cell, y / cell, seed) > 0.5f ? 1f : 0f; break;
                case SkinPattern.Striped:
                    int period = Mathf.Max(8, size / 16);
                    t = ((x + y) % period) < period / 4 ? 1f : 0f; break;
                default: t = 0f; break;
            }
            var c = Color.Lerp(s.Primary, s.Secondary, t);
            float grit = (Hash(x, y, seed + 7) - 0.5f) * 0.05f;
            c = new Color(Mathf.Clamp01(c.r + grit), Mathf.Clamp01(c.g + grit), Mathf.Clamp01(c.b + grit), 1f);
            float w = EdgeWear(x, y, size, s.Wear, seed);
            return Color.Lerp(c, new Color(0.48f, 0.49f, 0.50f), w * 0.85f);
        }

        public static Texture2D Get(in WeaponSkin s, int tier)
        {
            int size = SizeForTier(tier);
            string key = s.Id + "@" + size;
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;
            int seed = s.Id.GetHashCode() & 0xFFFF;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = Sample(s, x, y, size, seed);
            tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Skin_" + key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            Cache[key] = tex;
            return tex;
        }
    }
}
