using UnityEngine;

namespace Project.Infrastructure.Rendering
{
    /// <summary>C4: Malzeme hesapları (saf; EditMode testli).</summary>
    public static class MaterialMath
    {
        /// <summary>Hedef texel/metre için karo sayısı: tiling = density * tileWorldMeters / textureSize.</summary>
        public static float TilingForTexelDensity(float texelsPerMeter, float tileWorldMeters, int textureSize)
        {
            if (texelsPerMeter <= 0f || textureSize <= 0)
                return 1f;
            // Bir karo (tileWorldMeters m) 1 UV birimi kapladığında yoğunluk = textureSize / tileWorldMeters.
            // Karo sayısı t ise yoğunluk = textureSize * t / tileWorldMeters  →  t = density * tileWorldMeters / textureSize.
            return Mathf.Max(0.01f, texelsPerMeter * Mathf.Max(0.01f, tileWorldMeters) / textureSize);
        }

        /// <summary>Makro lekelerin karo sayısı (1 / dünya boyutu).</summary>
        public static float MacroTiling(float macroScaleMeters, float tileWorldMeters)
            => Mathf.Max(0.001f, Mathf.Max(0.01f, tileWorldMeters) / Mathf.Max(0.5f, macroScaleMeters));

        /// <summary>Detay albedo çarpanı: makro gücü 0..1 → 1 civarı (_DetailAlbedoMapScale, MULx2 modunda 2 nötrdür).</summary>
        public static float MacroAlbedoScale(float macroVariation)
            => Mathf.Lerp(1f, 2f, Mathf.Clamp01(macroVariation));

        /// <summary>
        /// ORM tutarlılığı: Mask piksel düzeni R metalik, G AO, B 255 (detay maskesi), A pürüzsüzlük. Geçerli mi?
        /// </summary>
        public static bool IsMaskPixelValid(Color32 c) => c.b == 255;

        /// <summary>Bir maske dizisinin tamamı kurala uyuyor mu (üretici tutarlılık denetimi).</summary>
        public static bool IsMaskConsistent(Color32[] mask)
        {
            if (mask == null || mask.Length == 0)
                return false;
            for (var i = 0; i < mask.Length; i++)
                if (!IsMaskPixelValid(mask[i]))
                    return false;
            return true;
        }
    }
}
