using System;

namespace Project.Application.Combat.Feel
{
    /// <summary>
    /// Saf silah sallanma kurallari: kosudan sonra yorgunluk (stamina) sallanmasi ve nefes tutma ile birlesik carpan.
    /// ViewmodelMotionMath.AdsBreath'in suppressionMul girdisine veya yayilim carpanina beslenir.
    /// </summary>
    public static class WeaponSwayRules
    {
        /// <summary>Tam yorgunken (stamina 0) ek sallanma: 1 + 1.2 = 2.2x.</summary>
        public const float MaxFatigueExtra = 1.2f;

        /// <summary>Kosu bitince yorgunlugun dinlenme suresi (sn) tam toparlanma icin.</summary>
        public const float FatigueRecoverSeconds = 5f;

        /// <summary>Kosarken yorgunluk dolma hizi (1/sn): ~3 sn kosu = tam yorgun.</summary>
        public const float FatigueGainPerSecond = 0.33f;

        public const float HoldBreathMinMultiplier = 0.2f;

        /// <summary>Yorgunluk 0..1 adimi: kosarken artar, dinlenirken azalir.</summary>
        public static float StepFatigue(float fatigue, bool sprinting, float dt)
        {
            if (float.IsNaN(dt) || dt <= 0f) return Clamp01(fatigue);
            var f = Clamp01(fatigue);
            f += sprinting ? FatigueGainPerSecond * dt : -dt / FatigueRecoverSeconds;
            return Clamp01(f);
        }

        /// <summary>Stamina 0..1 (1 = dinc) -> sallanma carpani 1..2.2.</summary>
        public static float StaminaSwayMultiplier(float stamina01)
        {
            return 1f + MaxFatigueExtra * (1f - Clamp01(stamina01));
        }

        /// <summary>
        /// Birlesik carpan: yorgunluk sallanmasi x nefes carpani (HoldBreathState.SwayMultiplier).
        /// Nefes tutulurken yorgunluk etkisi %50 zayiflar (sakinlesme) ama sifirlanmaz.
        /// </summary>
        public static float Combined(float stamina01, float breathSwayMultiplier, bool holdingBreath)
        {
            var fatigue = StaminaSwayMultiplier(stamina01);
            if (holdingBreath) fatigue = 1f + (fatigue - 1f) * 0.5f;
            var b = float.IsNaN(breathSwayMultiplier) ? 1f : Math.Max(HoldBreathMinMultiplier, breathSwayMultiplier);
            return fatigue * b;
        }

        private static float Clamp01(float v) { return float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v; }
    }
}
