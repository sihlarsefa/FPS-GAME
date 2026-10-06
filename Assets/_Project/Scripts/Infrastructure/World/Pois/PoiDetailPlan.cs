using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Tek bir detay yerleşimi (XZ düzlemi + yaw + yığın katı / ölçek).</summary>
    public readonly struct PoiSlot
    {
        public readonly Vector2 Pos;
        public readonly float Yaw;
        public readonly int Level;
        public readonly float Scale;

        public PoiSlot(Vector2 pos, float yaw, int level, float scale)
        {
            Pos = pos;
            Yaw = yaw;
            Level = level;
            Scale = scale;
        }
    }

    /// <summary>
    /// POI detay geçişi için SAF mantık (Unity nesnesi üretmez): kalite kademesine göre yoğunluk ve
    /// deterministik yerleşim planları (konteyner dizilimi, kar birikintisi, mermi izi, yanık leke).
    /// </summary>
    public static class PoiDetailPlan
    {
        /// <summary>Kalite kademesi (0 Düşük .. 3 Ultra) için detay yoğunluk çarpanı.</summary>
        public static float DensityForTier(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 0.35f;
                case 1: return 0.65f;
                case 2: return 0.9f;
                default: return 1f;
            }
        }

        /// <summary>Adet * yoğunluk, en az 1 (adet &gt; 0 ise).</summary>
        public static int Scaled(int count, int tier)
            => count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * DensityForTier(tier)));

        /// <summary>
        /// Konteyner dizilimi: satır x sütun ızgara (yaw ile döner); bazı hücreler boş, bazılarına 2. kat konur.
        /// Konteyner boyu 6 m, eni 2.4 m varsayılır (aralık dahil).
        /// </summary>
        public static List<PoiSlot> ContainerGrid(Vector2 center, float yawDeg, int rows, int cols, int seed, float gapChance = 0.15f, float stackChance = 0.3f)
        {
            var list = new List<PoiSlot>();
            if (rows <= 0 || cols <= 0)
                return list;
            var rng = new System.Random(seed);
            var rad = yawDeg * Mathf.Deg2Rad;
            var fwd = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            var right = new Vector2(fwd.y, -fwd.x);
            const float stepAlong = 6.6f;
            const float stepSide = 2.9f;
            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < cols; c++)
                {
                    if (rng.NextDouble() < gapChance)
                        continue;
                    var p = center + fwd * ((r - (rows - 1) * 0.5f) * stepAlong) + right * ((c - (cols - 1) * 0.5f) * stepSide);
                    var level = rng.NextDouble() < stackChance ? 1 : 0;
                    list.Add(new PoiSlot(p, yawDeg + ((float)rng.NextDouble() - 0.5f) * 3f, level, 1f));
                }
            }

            return list;
        }

        /// <summary>Daire içinde (merkez, yarıçap) dağılmış n nokta; en az minDist aralıklı (basit reddetme).</summary>
        public static List<PoiSlot> ScatterInDisc(Vector2 center, float radius, int count, int seed, float minDist, float minScale, float maxScale)
        {
            var list = new List<PoiSlot>();
            if (count <= 0 || radius <= 0f)
                return list;
            var rng = new System.Random(seed);
            var tries = count * 12;
            while (list.Count < count && tries-- > 0)
            {
                var a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var d = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                var ok = true;
                for (var i = 0; i < list.Count && ok; i++)
                    ok = (list[i].Pos - p).sqrMagnitude >= minDist * minDist;
                if (!ok)
                    continue;
                list.Add(new PoiSlot(p, (float)rng.NextDouble() * 360f, 0, Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble())));
            }

            return list;
        }

        /// <summary>
        /// Duvar yüzeyi üzerinde mermi izi kümesi: (yatay, dikey) yerel koordinatlar; merkeze doğru yoğunlaşır.
        /// Dönen Pos.x = yatay (-width/2..width/2), Pos.y = dikey (0..height).
        /// </summary>
        public static List<PoiSlot> BulletHoleCluster(float width, float height, int count, int seed)
        {
            var list = new List<PoiSlot>();
            if (count <= 0 || width <= 0f || height <= 0f)
                return list;
            var rng = new System.Random(seed);
            var cx = ((float)rng.NextDouble() - 0.5f) * width * 0.5f;
            var cy = height * (0.35f + (float)rng.NextDouble() * 0.4f);
            for (var i = 0; i < count; i++)
            {
                var gx = (float)((rng.NextDouble() + rng.NextDouble() - 1.0) * width * 0.25);
                var gy = (float)((rng.NextDouble() + rng.NextDouble() - 1.0) * height * 0.2);
                var x = Mathf.Clamp(cx + gx, -width * 0.5f, width * 0.5f);
                var y = Mathf.Clamp(cy + gy, 0.05f, height);
                list.Add(new PoiSlot(new Vector2(x, y), (float)rng.NextDouble() * 360f, 0, 0.6f + (float)rng.NextDouble() * 0.6f));
            }

            return list;
        }
    }
}
