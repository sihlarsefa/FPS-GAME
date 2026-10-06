using System;
using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>Faz geçişi stinger türü (ENTEGRASYON Audio: <see cref="HudStingers.Played"/> abonesi ses çalar).</summary>
    public enum HudStingerKind
    {
        ZoneAnnounced = 0,
        ZoneClosing = 1,
        ZoneFinal = 2,
        AirdropInbound = 3,
        AirdropLanded = 4
    }

    /// <summary>
    /// Faz geçişi stinger kancası. Ses ekibi <see cref="Played"/> olayına abone olur (HUD ses üretmez).
    /// ENTEGRASYON Audio: GameAudio/Music tarafı bu olayı dinleyip uygun stinger'ı çalmalı.
    /// </summary>
    public static class HudStingers
    {
        public static event Action<HudStingerKind> Played;

        public static void Raise(HudStingerKind kind)
        {
            try { Played?.Invoke(kind); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    /// <summary>Bölge/ikmal sayaç görsellerinin saf kuralları (EditMode testli).</summary>
    public static class HudTimerRules
    {
        public const float AnimMin = 0.15f;
        public const float AnimMax = 0.25f;
        public const float AnimDefault = 0.2f;
        public const float ClosingBannerSeconds = 2f;
        public const float AirdropBannerSeconds = 4f;

        public const float AmberSeconds = 30f;
        public const float AmberHoldSeconds = 10f;
        public const float RedSeconds = 3f;

        public static float ClampAnim(float seconds) =>
            float.IsNaN(seconds) ? AnimDefault : Mathf.Clamp(seconds, AnimMin, AnimMax);

        /// <summary>Ease-out cubic.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(float.IsNaN(t) ? 0f : t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Ease-in-out (smoothstep).</summary>
        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(float.IsNaN(t) ? 0f : t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Bir değeri hedefe sabit sürede (animSeconds) ulaşacak hızla yaklaştırır.</summary>
        public static float Approach(float current, float target, float dt, float animSeconds)
        {
            var step = Mathf.Max(0f, dt) / ClampAnim(animSeconds);
            return Mathf.MoveTowards(current, target, step);
        }

        /// <summary>Kalan oran (1 = yeni başladı, 0 = bitti). Süre geçersizse 0.</summary>
        public static float RemainingFraction(float remaining, float duration)
        {
            if (float.IsNaN(remaining) || float.IsNaN(duration) || duration <= 0.0001f)
                return 0f;
            return Mathf.Clamp01(remaining / duration);
        }

        /// <summary>Kalan süreye göre renk: >=30 sn beyaz, 30→10 sn amber'e, 10→3 sn kırmızıya kayar, ≤3 sn kırmızı.</summary>
        public static Color CountdownColor(float remainingSeconds, Color white, Color amber, Color red)
        {
            if (float.IsNaN(remainingSeconds) || remainingSeconds >= AmberSeconds)
                return white;
            if (remainingSeconds > AmberHoldSeconds)
                return Color.Lerp(white, amber, Mathf.InverseLerp(AmberSeconds, AmberHoldSeconds, remainingSeconds));
            if (remainingSeconds > RedSeconds)
                return Color.Lerp(amber, red, Mathf.InverseLerp(AmberHoldSeconds, RedSeconds, remainingSeconds));
            return red;
        }

        /// <summary>Bannera slide-in/out ofseti: 0 = tam yerinde, 1 = tamamen dışarıda.</summary>
        public static float BannerSlide(float age, float total, float animSeconds)
        {
            var a = ClampAnim(animSeconds);
            if (age < 0f || age >= total)
                return 1f;
            if (age < a)
                return 1f - EaseOut(age / a);
            if (age > total - a)
                return EaseInOut((age - (total - a)) / a);
            return 0f;
        }

        /// <summary>Yön oku için göreli açı (derece, -180..180) -> ok dönüşü (Z); sonsuz/NaN 0.</summary>
        public static float ArrowRotation(float relativeBearing) =>
            float.IsNaN(relativeBearing) || float.IsInfinity(relativeBearing) ? 0f : -relativeBearing;

        /// <summary>Bölge sayacı aşamaya göre bir stinger gerektirir mi?</summary>
        public static bool TryStingerForZone(Project.Core.Domain.ZoneStage stage, int phaseIndex, int phaseCount, out HudStingerKind kind)
        {
            switch (stage)
            {
                case Project.Core.Domain.ZoneStage.Waiting:
                    kind = HudStingerKind.ZoneAnnounced;
                    return true;
                case Project.Core.Domain.ZoneStage.Shrinking:
                    kind = phaseCount > 0 && phaseIndex >= phaseCount - 1 ? HudStingerKind.ZoneFinal : HudStingerKind.ZoneClosing;
                    return true;
                default:
                    kind = HudStingerKind.ZoneAnnounced;
                    return false;
            }
        }
    }
}
