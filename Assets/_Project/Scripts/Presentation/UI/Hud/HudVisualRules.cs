using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Nişan/hasar/bomba uyarısı görsellerinin saf boyut + görünürlük kuralları (EditMode testli).
    /// Dürbünle bakarken ekranı kaplayan dev kırmızı şekil hatasının tekrarını önleyen üst sınırlar burada tutulur.
    /// </summary>
    public static class HudVisualRules
    {
        /// <summary>İsabet işaretinin en büyük toplam çapı (px).</summary>
        public const float MaxHitMarkerPx = 32f;

        /// <summary>Hasar yönü yayının en büyük kenarı (px).</summary>
        public const float MaxIndicatorPx = 120f;

        /// <summary>Bomba uyarı simgesinin en büyük kenarı (px).</summary>
        public const float MaxGrenadeIconPx = 36f;

        public const float MinCrosshairScale = 0.5f;
        public const float MaxCrosshairScale = 2f;

        /// <summary>Hasar yönü için yatay mesafe eşiği: bundan yakın kaynak yönsüz sayılır (m).</summary>
        public const float MinIndicatorSourceDistance = 0.3f;

        /// <summary>Sonlu değilse varsayılana, değilse [min,max]'a sınırlar.</summary>
        public static float SafeClamp(float value, float min, float max, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Mathf.Clamp(fallback, min, max);
            return Mathf.Clamp(value, min, max);
        }

        public static float ClampCrosshairScale(float scale) => SafeClamp(scale, MinCrosshairScale, MaxCrosshairScale, 1f);

        /// <summary>Kolun merkezden uzaklığı + uzunluğu, ölçekle çarpılıp toplam çap (2x) olarak döner.</summary>
        public static float HitMarkerExtent(float armOffset, float armLength, float scale) =>
            2f * (Mathf.Max(0f, armOffset) + Mathf.Max(0f, armLength)) * Mathf.Max(0f, scale);

        /// <summary>İsabet işareti ölçeği: toplam çap üst sınırı aşmayacak şekilde kısılır.</summary>
        public static float ClampHitMarkerScale(float armOffset, float armLength, float scale)
        {
            var baseExtent = HitMarkerExtent(armOffset, armLength, 1f);
            if (baseExtent <= 0.001f)
                return 1f;
            return SafeClamp(scale, 0.1f, MaxHitMarkerPx / baseExtent, 1f);
        }

        /// <summary>Hasar yönü göstergesi gerçek bir olay mı (hasar > 0, sonlu, kaynak yeterince uzak)?</summary>
        public static bool IndicatorShouldShow(float damage, Vector3 origin, Vector3 source)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return false;
            if (!IsFinite(origin) || !IsFinite(source))
                return false;
            var dx = source.x - origin.x;
            var dz = source.z - origin.z;
            return dx * dx + dz * dz >= MinIndicatorSourceDistance * MinIndicatorSourceDistance;
        }

        /// <summary>Hasar yayının sönme eğrisi: ilk %60 tam, sonra doğrusal söner; süre bitince 0.</summary>
        public static float IndicatorAlpha(float timeLeft, float lifetime, float strength)
        {
            if (lifetime <= 0f || timeLeft <= 0f)
                return 0f;
            var t = Mathf.Clamp01(timeLeft / lifetime);
            return Mathf.Clamp01(strength) * (t > 0.6f ? 1f : t / 0.6f) * 0.9f;
        }

        public static bool IsFinite(Vector3 v) =>
            !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
              float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }
}
