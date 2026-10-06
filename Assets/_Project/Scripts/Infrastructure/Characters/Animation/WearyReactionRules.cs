using UnityEngine;

namespace Project.Infrastructure.Characters.Animation
{
    /// <summary>Saf kurallar: yakın mermi sıyrılması ve düşük canda topallama (test edilebilir).</summary>
    public static class WearyReactionRules
    {
        /// <summary>Mermi bu mesafeden (m) yakın geçerse asker ürker.</summary>
        public const float FlinchRadius = 1.5f;

        /// <summary>Can oranı bunun altındaysa yürürken topallar.</summary>
        public const float LimpHealthThreshold = 0.35f;

        /// <summary>Yakınlık 0..1 (0 = yarıçapta, 1 = tam üstünden).</summary>
        public static float Closeness(float missDistance)
        {
            return Mathf.Clamp01(1f - Mathf.Max(0f, missDistance) / FlinchRadius);
        }

        /// <summary>Topallama ağırlığı 0..1: can eşiğin altında ve yürüyorsa.</summary>
        public static float LimpWeight(float healthNormalized, float speed)
        {
            if (healthNormalized <= 0f || healthNormalized >= LimpHealthThreshold)
                return 0f;
            var depth = 1f - healthNormalized / LimpHealthThreshold;
            var walk = Mathf.Clamp01(speed / 0.5f);
            return Mathf.Clamp01(0.5f + 0.5f * depth) * walk;
        }

        /// <summary>Ürkme yönü: merminin askerin sağında (+1) mı solunda (-1) mı geçtiği.</summary>
        public static float SideOf(Vector3 origin, Vector3 dir, Vector3 soldier)
        {
            var cross = Vector3.Cross(dir, soldier - origin).y;
            return cross >= 0f ? -1f : 1f;
        }
    }
}
