using UnityEngine;

namespace Project.Infrastructure.World
{
    /// <summary>
    /// Orman kompozisyonu saf matematiği (TreeScatter kullanır): küme maskesi + açıklıklar, orman kenarı vurgusu,
    /// yol/tarla boyunca ağaç sırası, yüksekliğe göre tür karışımı, kenarda çalı oranı. Unity nesnesi gerektirmez; testlenebilir.
    /// </summary>
    public static class TreeScatterRules
    {
        public static float Smooth(float e0, float e1, float x)
        {
            if (e1 <= e0)
                return x >= e1 ? 1f : 0f;
            var t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Orman kümesi maskesi 0..1: geniş gürültü (patch) kümeleri, ince gürültü (fine) açıklıkları keser.</summary>
        public static float StandMask(float patch, float fine)
        {
            var stand = Smooth(0.46f, 0.62f, patch);
            var clearing = Smooth(0.74f, 0.84f, fine);
            return stand * (1f - clearing * 0.9f);
        }

        /// <summary>Orman kenarı vurgusu 0..1: maske ortasında (0.5) tepe yapar; iç ve dış boşlukta 0.</summary>
        public static float EdgeWeight(float mask)
        {
            var m = Mathf.Clamp01(mask);
            return 4f * m * (1f - m);
        }

        /// <summary>Yol kenarından 3..14 m şeritte ağaç sırası (siper) çarpanı: 1 .. 1+<paramref name="gain"/>; dışında 1.</summary>
        public static float RoadLineBoost(float roadEdgeDistance, float gain = 2.2f)
        {
            if (float.IsInfinity(roadEdgeDistance) || roadEdgeDistance < 3f || roadEdgeDistance > 14f)
                return 1f;
            var up = Smooth(3f, 5.5f, roadEdgeDistance);
            var down = 1f - Smooth(9f, 14f, roadEdgeDistance);
            return 1f + gain * up * down;
        }

        /// <summary>Çam payı 0..1: alçakta meşe, yükseklikte çam.</summary>
        public static float PineFraction(float height)
        {
            return Smooth(42f, 74f, height);
        }

        /// <summary>Çalı olasılığı: iç ormanda az, kenarda çok.</summary>
        public static float BushChance(float edgeWeight)
        {
            return 0.06f + 0.46f * Mathf.Clamp01(edgeWeight);
        }

        /// <summary>Verilen zar (0..1) ve konuma göre tür seçimi: çalı → kuru → çam/meşe.</summary>
        public static TreeKind PickKind(float roll, float height, float edgeWeight)
        {
            var bush = BushChance(edgeWeight);
            if (roll < bush)
                return TreeKind.Bush;
            var r = (roll - bush) / Mathf.Max(0.001f, 1f - bush);
            if (r < 0.04f)
                return TreeKind.Dead;
            var pine = PineFraction(height);
            var q = (r - 0.04f) / 0.96f;
            if (q < pine)
                return q < pine * 0.5f ? TreeKind.PineA : TreeKind.PineB;
            return TreeKind.Oak;
        }

        /// <summary>Düşmüş kütük koşulu: orman içi (maske yüksek, kenar değil) ve tıkanık değil.</summary>
        public static bool AcceptsLog(float mask, float roll)
        {
            return mask > 0.7f && roll < 0.55f;
        }
    }
}
