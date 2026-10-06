using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>Cephe hattı öğesi: konum (x,z), yön (derece) ve ölçü.</summary>
    public struct FrontlineItem
    {
        public Vector2 Position;
        public float Yaw;
        public float Size;
        public float Size2;
    }

    /// <summary>
    /// Cephe hattı planı (saf mantık, Unity nesnesi yok): iki nokta arası zikzak siper hattı, siper boyunca kum torbası
    /// mevzileri, tek kişilik siperler, dikenli tel hatları, Çek kirpisi tank engelleri, top mermisi kraterleri (küme)
    /// ve yanmış ağaç kütükleri. Seed'e bağlı deterministik; kalite kademesi (0..3) yoğunluğu belirler.
    /// </summary>
    public sealed class FrontlinePlan
    {
        public const float TrenchWidth = 1.8f;

        public readonly List<Vector2> Trench = new List<Vector2>();
        public readonly List<FrontlineItem> Sandbags = new List<FrontlineItem>();
        public readonly List<FrontlineItem> Foxholes = new List<FrontlineItem>();
        public readonly List<List<Vector2>> WireLines = new List<List<Vector2>>();
        public readonly List<FrontlineItem> Hedgehogs = new List<FrontlineItem>();
        public readonly List<FrontlineItem> Craters = new List<FrontlineItem>();
        public readonly List<FrontlineItem> Logs = new List<FrontlineItem>();

        /// <summary>Kademe → yoğunluk çarpanı (0 = hiç).</summary>
        public static float Density(int tier)
        {
            switch (Mathf.Clamp(tier, 0, 3))
            {
                case 0: return 0.35f;
                case 1: return 0.6f;
                case 2: return 0.85f;
                default: return 1f;
            }
        }

        /// <summary>
        /// a→b arası cephe hattı. isClear: (x,z) yerleşime uygun mu (yol/dere/yapı dışı); null → hepsi uygun.
        /// </summary>
        public static FrontlinePlan Create(Vector2 a, Vector2 b, int seed, int tier, Func<float, float, bool> isClear = null)
        {
            var plan = new FrontlinePlan();
            var rng = new System.Random(seed * 7919 + 4421);
            var d = Mathf.Max(0.35f, Density(tier));
            var dir = b - a;
            var length = dir.magnitude;
            if (length < 20f)
                return plan;
            dir /= length;
            var side = new Vector2(-dir.y, dir.x);
            bool Ok(Vector2 p) => isClear == null || isClear(p.x, p.y);
            float R() => (float)rng.NextDouble();

            // Zikzak siper hattı: ana eksen etrafında dönüşümlü ±yan kayma (adım 14-22 m, kayma 3-6 m).
            var t = 0f;
            var sign = 1f;
            while (t <= length)
            {
                var off = sign * (3f + R() * 3f);
                plan.Trench.Add(a + dir * t + side * off);
                sign = -sign;
                t += 14f + R() * 8f;
            }

            if (plan.Trench.Count < 2)
                plan.Trench.Add(b);

            // Siper boyunca kum torbası mevzileri: her segmentte arka (düşman) tarafta parapet.
            for (var i = 0; i + 1 < plan.Trench.Count; i++)
            {
                var p0 = plan.Trench[i];
                var p1 = plan.Trench[i + 1];
                var seg = p1 - p0;
                var sl = seg.magnitude;
                if (sl < 1f)
                    continue;
                var sd = seg / sl;
                var sn = new Vector2(-sd.y, sd.x);
                var yaw = Mathf.Atan2(sd.x, sd.y) * Mathf.Rad2Deg;
                var step = 3.2f / d;
                for (var s = 1.5f; s < sl - 1.5f; s += step)
                {
                    var pos = p0 + sd * s + sn * (TrenchWidth * 0.5f + 0.4f);
                    if (!Ok(pos))
                        continue;
                    plan.Sandbags.Add(new FrontlineItem { Position = pos, Yaw = yaw, Size = 2.4f + R() * 0.8f, Size2 = 2 + rng.Next(0, 2) });
                }
            }

            // Tek kişilik siperler: hattın önünde (cephe yönü kuzey kabul: side*+) aralıklı.
            var foxCount = Mathf.RoundToInt(length / 30f * d);
            for (var i = 0; i < foxCount; i++)
            {
                var pos = a + dir * (length * (i + 0.5f) / Mathf.Max(1, foxCount) + (R() - 0.5f) * 6f) + side * (14f + R() * 10f);
                if (Ok(pos))
                    plan.Foxholes.Add(new FrontlineItem { Position = pos, Yaw = R() * 360f, Size = 0.9f + R() * 0.2f });
            }

            // Dikenli tel hatları: siperin önünde 2 paralel hat (kademe düşükse 1), kesik parçalar.
            var wireRows = tier >= 2 ? 2 : 1;
            for (var row = 0; row < wireRows; row++)
            {
                var line = new List<Vector2>();
                var offset = 24f + row * 6f;
                for (var s = 4f; s < length - 4f; s += 5f)
                {
                    var pos = a + dir * s + side * (offset + (R() - 0.5f) * 1.5f);
                    if (Ok(pos) && R() < 0.5f + 0.5f * d)
                    {
                        line.Add(pos);
                    }
                    else if (line.Count >= 2)
                    {
                        plan.WireLines.Add(line);
                        line = new List<Vector2>();
                    }
                    else
                        line.Clear();
                }

                if (line.Count >= 2)
                    plan.WireLines.Add(line);
            }

            // Çek kirpisi tank engelleri: tel hattı gerisinde seyrek.
            var hedgeCount = Mathf.RoundToInt(length / 28f * d);
            for (var i = 0; i < hedgeCount; i++)
            {
                var pos = a + dir * (length * (i + R() * 0.8f + 0.1f) / Mathf.Max(1, hedgeCount)) + side * (19f + R() * 2f);
                if (Ok(pos))
                    plan.Hedgehogs.Add(new FrontlineItem { Position = pos, Yaw = R() * 360f, Size = 1.4f });
            }

            // Top mermisi kraterleri: 2-4 kümeli, küme başına 3-7 krater.
            var clusters = Mathf.Max(1, Mathf.RoundToInt((2f + length / 90f) * d));
            for (var c = 0; c < clusters; c++)
            {
                var center = a + dir * (length * (0.1f + 0.8f * R())) + side * ((R() - 0.4f) * 60f);
                var n = Mathf.Max(2, Mathf.RoundToInt((3 + R() * 4) * d));
                for (var k = 0; k < n; k++)
                {
                    var ang = R() * Mathf.PI * 2f;
                    var pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (R() * 14f);
                    if (Ok(pos))
                        plan.Craters.Add(new FrontlineItem { Position = pos, Size = 1.6f + R() * 2.4f });
                }
            }

            // Yanmış ağaç kütükleri.
            var logCount = Mathf.RoundToInt(length / 22f * d);
            for (var i = 0; i < logCount; i++)
            {
                var pos = a + dir * (R() * length) + side * ((R() - 0.5f) * 70f);
                if (Ok(pos))
                    plan.Logs.Add(new FrontlineItem { Position = pos, Yaw = R() * 360f, Size = 2.5f + R() * 3f });
            }

            return plan;
        }
    }
}
