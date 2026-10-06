using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Hasar yönü yaylarının saf hesapları (EditMode testli): aynı yönden gelen vuruşlar tek yayda birleşir (yığılma yerine
    /// güçlenir), yay genişliği hasarla artar, en yeni N yay tutulur. Açılar bakışa göre göreli (0 = karşı, + = sağ).
    /// </summary>
    public static class DamageArcLayout
    {
        public const float MergeAngle = 18f;
        public const int MaxArcs = 6;
        public const float MinWidthDeg = 18f;
        public const float MaxWidthDeg = 54f;
        public const float DamageForMaxWidth = 60f;
        public const float Lifetime = 2.2f;

        public struct Arc
        {
            public float Angle;
            public float Strength;
            public float Width;
            public float TimeLeft;
        }

        /// <summary>Hasara göre yay genişliği (derece).</summary>
        public static float WidthForDamage(float damage)
        {
            var t = Mathf.Clamp01(damage / DamageForMaxWidth);
            return Mathf.Lerp(MinWidthDeg, MaxWidthDeg, t);
        }

        /// <summary>Güç 0..1: 10 hasar ≈ 0.4, 60+ hasar = 1 (karekök eğri).</summary>
        public static float StrengthForDamage(float damage)
        {
            if (damage <= 0f || float.IsNaN(damage))
                return 0f;
            return Mathf.Clamp01(Mathf.Sqrt(damage / DamageForMaxWidth) * 1.0f);
        }

        /// <summary>Yeni vuruşu ekler: yakın yay varsa birleştirir (güç artar, açı ağırlıklı kayar), yoksa yeni yay açar.</summary>
        public static void Add(List<Arc> arcs, float angle, float damage)
        {
            if (arcs == null || damage <= 0f || float.IsNaN(damage) || float.IsNaN(angle))
                return;
            var s = StrengthForDamage(damage);
            var w = WidthForDamage(damage);
            for (var i = 0; i < arcs.Count; i++)
            {
                var a = arcs[i];
                if (Mathf.Abs(Mathf.DeltaAngle(a.Angle, angle)) > MergeAngle)
                    continue;
                var wt = s / Mathf.Max(0.01f, s + a.Strength);
                a.Angle = a.Angle + Mathf.DeltaAngle(a.Angle, angle) * wt;
                a.Strength = Mathf.Clamp01(Mathf.Max(a.Strength, s) + 0.25f * Mathf.Min(a.Strength, s));
                a.Width = Mathf.Max(a.Width, w);
                a.TimeLeft = Lifetime;
                arcs[i] = a;
                return;
            }

            if (arcs.Count >= MaxArcs)
            {
                var oldest = 0;
                for (var i = 1; i < arcs.Count; i++)
                    if (arcs[i].TimeLeft < arcs[oldest].TimeLeft)
                        oldest = i;
                arcs.RemoveAt(oldest);
            }

            arcs.Add(new Arc { Angle = angle, Strength = s, Width = w, TimeLeft = Lifetime });
        }

        /// <summary>Süreyi ilerletir, bitenleri siler.</summary>
        public static void Tick(List<Arc> arcs, float dt)
        {
            if (arcs == null)
                return;
            for (var i = arcs.Count - 1; i >= 0; i--)
            {
                var a = arcs[i];
                a.TimeLeft -= dt;
                if (a.TimeLeft <= 0f)
                    arcs.RemoveAt(i);
                else
                    arcs[i] = a;
            }
        }

        /// <summary>Yay opaklığı (HudVisualRules.IndicatorAlpha ile aynı eğri).</summary>
        public static float Alpha(Arc a) => HudVisualRules.IndicatorAlpha(a.TimeLeft, Lifetime, a.Strength);

        /// <summary>Yay merkezinin ekran-halkası konumu (merkezden yarıçap px; saat yönü açı, 0 = yukarı).</summary>
        public static Vector2 RingPosition(float angle, float radius)
        {
            var r = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r) * radius, Mathf.Cos(r) * radius);
        }
    }
}
