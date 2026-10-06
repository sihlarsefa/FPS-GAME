using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>Ufuk manzarası saf mantığı: katman tanımları, NaN korumalı yükseklik ve renk hesapları (EditMode testlenebilir).</summary>
    public static class HorizonVistaMath
    {
        public struct Layer
        {
            public float Radius, BaseY, MinH, MaxH, Lag, FogAmount, TreeH;
            public int Seed, Segments;
            public bool Forest;
        }

        /// <summary>0 = en uzak, 2 = en yakın (orman bantlı). Lag: kamera hareketinin katmana yansımayan oranı (paralaks).</summary>
        public static readonly Layer[] Layers =
        {
            new Layer { Radius = 1190f, BaseY = -60f, MinH = 30f, MaxH = 120f, Lag = 0.004f, FogAmount = 0.82f, Seed = 7, Segments = 160 },
            new Layer { Radius = 1150f, BaseY = -60f, MinH = 20f, MaxH = 80f, Lag = 0.015f, FogAmount = 0.62f, Seed = 19, Segments = 192 },
            new Layer { Radius = 1100f, BaseY = -60f, MinH = 8f, MaxH = 32f, Lag = 0.04f, FogAmount = 0.38f, Seed = 31, Segments = 384, Forest = true, TreeH = 9f },
        };

        public const float MaxParallaxOffset = 160f;
        public const int StarCount = 260;
        public const float StarMinElevation = 0.2f; // sin(yükseklik): dağ tepelerinin (~0.17) üstü

        public static float Finite(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;

        public static Color Sanitize(Color c, Color fallback)
        {
            if (float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsNaN(c.a) ||
                float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b))
                return fallback;
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }

        public static float Hash01(int i, int seed)
        {
            unchecked
            {
                uint h = (uint)(i * 374761393 + seed * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>Sırt yüksekliği (taban üstü). u 0..1 döngüsel; her zaman sonlu ve [MinH, MaxH+TreeH] içinde.</summary>
        public static float RidgeHeight(in Layer l, int segment)
        {
            var u = (segment % l.Segments) / (float)l.Segments;
            var n = Finite(Project.Core.Domain.SkyWaterRules.TileableFbm(u, 0.37f, 5, 4, l.Seed), 0.5f);
            var n2 = Finite(Project.Core.Domain.SkyWaterRules.TileableFbm(u, 0.71f, 11, 3, l.Seed + 5), 0.5f);
            var n3 = Finite(Project.Core.Domain.SkyWaterRules.TileableFbm(u, 0.93f, 23, 2, l.Seed + 9), 0.5f);
            n = Mathf.Clamp01(n);
            n2 = Mathf.Clamp01(n2);
            n3 = Mathf.Clamp01(n3);
            var shape = Mathf.Clamp01(Mathf.Pow(n, 1.3f) * 1.45f * (0.75f + 0.5f * (1f - Mathf.Abs(2f * n2 - 1f))) + (n3 - 0.5f) * 0.28f);
            var h = Mathf.Lerp(l.MinH, l.MaxH, shape);
            if (l.Forest)
            {
                var wi = segment % l.Segments;
                var tree = Hash01(wi, l.Seed);
                h += l.TreeH * ((wi & 1) == 0 ? 0.25f + 0.2f * tree : 0.55f + 0.45f * tree);
            }

            return Finite(h, l.MinH);
        }

        public static float Luma(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

        /// <summary>Rengi, parlaklığı maxLuma'yı aşmayacak şekilde ölçekler (renk tonu korunur).</summary>
        public static Color ClampLuma(Color c, float maxLuma)
        {
            var l = Luma(c);
            if (l <= maxLuma || l < 1e-5f) return c;
            var k = maxLuma / l;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        /// <summary>
        /// Gökyüzünün ufuktaki rengi tahmini (mavi-gri): sis rengi çoğu zaman neredeyse beyazdır, gökyüzü ise SkyTint'e yakındır.
        /// Dağ ve sis bandı bu renge göre boyanır; parlaklığı sis renginin parlaklığını geçmez.
        /// </summary>
        public static Color SkyHorizonColor(Color fog, Color tint, bool night)
        {
            var c = night ? Color.Lerp(tint, fog, 0.6f) : Color.Lerp(tint, fog, 0.3f);
            c = ClampLuma(c, Mathf.Max(0.02f, Luma(fog)));
            c.a = 1f;
            return Sanitize(c, fog);
        }

        public static Color LayerRidgeColor(in Layer l, int index, bool night, float fogMix, Color fog, Color tint)
        {
            var mix = Mathf.Clamp01(fogMix);
            Color c;
            var horizon = SkyHorizonColor(fog, tint, night);
            var hl = Luma(horizon);
            if (night)
            {
                var k = Mathf.Lerp(0.55f, 0.2f, index / 2f);
                c = new Color(fog.r * k, fog.g * k, fog.b * k * 1.05f, 1f);
            }
            else
            {
                // Atmosferik perspektif: uzak = ufuk gökyüzü rengi, %10-15 koyu ve mavimsi; yakın = daha koyu ve yeşilimsi.
                var depth = index / 2f; // 0 uzak, 1 yakın
                var scale = Mathf.Lerp(0.88f, 0.56f, depth);
                scale = Mathf.Lerp(scale, 0.95f, mix * 0.5f * (1f - depth * 0.5f));
                c = new Color(horizon.r * scale * Mathf.Lerp(0.94f, 0.98f, depth), horizon.g * scale * Mathf.Lerp(0.98f, 1f, depth), horizon.b * scale * Mathf.Lerp(1.04f, 1f, depth), 1f);
                if (l.Forest)
                {
                    var forest = new Color(0.14f, 0.22f, 0.12f) * Mathf.Lerp(0.6f, 1.1f, Mathf.Clamp01(hl));
                    c = Color.Lerp(c, forest, 0.55f * (1f - mix * 0.6f));
                }
                else if (index == 1)
                    c = Color.Lerp(c, new Color(c.r * 0.9f, c.g * 1.02f, c.b * 0.95f, 1f), 0.5f);

                c = ClampLuma(c, hl * (index == 0 ? 0.93f : 0.9f));
            }

            c.a = 1f;
            return Sanitize(c, fog);
        }

        /// <summary>Güneş tarafı parlaması: yatay yön benzerliği^p (0..1).</summary>
        public static float SunSide(float azimuthRad, Vector3 sunDir, float power)
        {
            var h = new Vector2(sunDir.x, sunDir.z);
            if (h.sqrMagnitude < 1e-6f) return 0f;
            h.Normalize();
            var d = Mathf.Cos(azimuthRad) * h.x + Mathf.Sin(azimuthRad) * h.y;
            return Finite(Mathf.Pow(Mathf.Clamp01(d), power), 0f);
        }

        /// <summary>Kamera yatay konumundan katman ofseti: katman ters yönde Lag kadar kayar (yakın katman daha çok), sınırlı.</summary>
        public static Vector3 ParallaxOffset(Vector3 camPos, Vector3 origin, float lag)
        {
            var d = camPos - origin;
            var o = new Vector3(-d.x * lag, 0f, -d.z * lag);
            o.x = Mathf.Clamp(Finite(o.x, 0f), -MaxParallaxOffset, MaxParallaxOffset);
            o.z = Mathf.Clamp(Finite(o.z, 0f), -MaxParallaxOffset, MaxParallaxOffset);
            return o;
        }

        public static int TriangleCount(in Layer l) => l.Segments * 3 * 2;
        public static int HazeTriangles(int segments) => segments * 2 * 2;
    }
}
