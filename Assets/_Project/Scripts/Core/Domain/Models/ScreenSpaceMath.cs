using System;

namespace Project.Core.Domain
{
    /// <summary>Kontakt gölgesi kalite kademesi (saf veri). Düşük kapalı; Orta en ucuz; Ultra en pahalı.</summary>
    public readonly struct ContactShadowTier
    {
        public readonly bool Enabled;
        /// <summary>Çözünürlük bölen (2 = yarı çözünürlük).</summary>
        public readonly int ResolutionDivisor;
        public readonly int Steps;
        /// <summary>Güneş yönünde ışın uzunluğu (m).</summary>
        public readonly float RayLength;
        /// <summary>Örtücü kalınlığı (m); daha kalın şeyler "arkadan geçilir" sayılır.</summary>
        public readonly float Thickness;
        /// <summary>Kameradan bu mesafeden sonra sönümlenir (m).</summary>
        public readonly float MaxDistance;
        /// <summary>Gölgeli bölgede en fazla ne kadar koyulaştırır (0..1).</summary>
        public readonly float Strength;

        public ContactShadowTier(bool enabled, int div, int steps, float rayLength, float thickness, float maxDistance, float strength)
        {
            Enabled = enabled; ResolutionDivisor = div; Steps = steps; RayLength = rayLength; Thickness = thickness; MaxDistance = maxDistance; Strength = strength;
        }
    }

    /// <summary>SSR-lite kalite kademesi (saf veri). Yalnız Yüksek/Ultra açık.</summary>
    public readonly struct SsrTier
    {
        public readonly bool Enabled;
        public readonly int ResolutionDivisor;
        public readonly int MaxSteps;
        /// <summary>Adım uzunluğu (piksel); küçük = hassas ama pahalı.</summary>
        public readonly float StridePx;
        public readonly float MaxDistance;
        public readonly float ThicknessAbs;
        public readonly float ThicknessRel;
        public readonly int RefineSteps;
        /// <summary>Bu pürüzsüzlüğün altında yansıma yok (roughness cutoff).</summary>
        public readonly float SmoothnessCutoff;
        public readonly float SmoothnessFadeStart;
        public readonly float EdgeFade;
        public readonly float Strength;

        public SsrTier(bool enabled, int div, int maxSteps, float stridePx, float maxDistance, float thicknessAbs, float thicknessRel,
            int refine, float smoothCutoff, float smoothFade, float edgeFade, float strength)
        {
            Enabled = enabled; ResolutionDivisor = div; MaxSteps = maxSteps; StridePx = stridePx; MaxDistance = maxDistance;
            ThicknessAbs = thicknessAbs; ThicknessRel = thicknessRel; RefineSteps = refine; SmoothnessCutoff = smoothCutoff;
            SmoothnessFadeStart = smoothFade; EdgeFade = edgeFade; Strength = strength;
        }
    }

    /// <summary>Ekran-uzayı efektlerinin (kontakt gölge, SSR-lite) saf matematiği: kademe tabloları, sönümler ve gölgelendiricinin
    /// CPU referans ışın yürüyüşü (testler için). Gölgelendirici bu işlevleri birebir yansıtır.</summary>
    public static class ScreenSpaceMath
    {
        public const int TierCount = 4;

        private static int ClampTier(int tier) => tier < 0 ? 0 : tier >= TierCount ? TierCount - 1 : tier;

        // ---------------------------------------------------------------- kademe tabloları

        /// <summary>0 Düşük (kapalı), 1 Orta (8 adım), 2 Yüksek (12), 3 Ultra (16). Hepsi yarı çözünürlük.</summary>
        public static ContactShadowTier ContactShadowsForTier(int tier)
        {
            switch (ClampTier(tier))
            {
                case 0: return new ContactShadowTier(false, 2, 0, 0f, 0f, 0f, 0f);
                case 1: return new ContactShadowTier(true, 2, 8, 0.45f, 0.35f, 25f, 0.55f);
                case 2: return new ContactShadowTier(true, 2, 12, 0.6f, 0.4f, 40f, 0.7f);
                default: return new ContactShadowTier(true, 2, 16, 0.8f, 0.45f, 60f, 0.8f);
            }
        }

        /// <summary>0-1 kapalı; 2 Yüksek (yarı çöz., 24 adım); 3 Ultra (yarı çöz., 40 adım, daha ince adım).</summary>
        public static SsrTier SsrForTier(int tier)
        {
            switch (ClampTier(tier))
            {
                case 2: return new SsrTier(true, 2, 24, 4f, 45f, 0.35f, 0.02f, 3, 0.45f, 0.75f, 0.12f, 0.8f);
                case 3: return new SsrTier(true, 2, 40, 3f, 80f, 0.3f, 0.015f, 5, 0.4f, 0.7f, 0.12f, 1f);
                default: return new SsrTier(false, 2, 0, 4f, 0f, 0.3f, 0.02f, 0, 1f, 1f, 0.1f, 0f);
            }
        }

        // ---------------------------------------------------------------- kontakt gölge

        /// <summary>Mesafe sönümü: maxDist*0.6'ya kadar 1, maxDist'te 0 (smoothstep).</summary>
        public static float ContactDistanceFade(float eyeDepth, float maxDistance)
        {
            if (maxDistance <= 0f)
                return 0f;
            return 1f - SmoothStep(maxDistance * 0.6f, maxDistance, eyeDepth);
        }

        /// <summary>Kendi yüzeyiyle çakışmayı önleyen derinlik ofseti (m); mesafeyle büyür.</summary>
        public static float ContactBias(float eyeDepth) => 0.012f + eyeDepth * 0.0016f;

        /// <summary>Kontakt gölge isabeti: ışın noktası yüzeyin ARKASINDA ve kalınlık içinde.</summary>
        public static bool ContactHit(float rayEyeDepth, float sceneEyeDepth, float eyeDepth, float thickness)
        {
            var diff = rayEyeDepth - sceneEyeDepth;
            return diff > ContactBias(eyeDepth) && diff < thickness + eyeDepth * 0.004f;
        }

        /// <summary>Isabet zayıflaması: ışının sonuna doğru gölge incelir (0..1 kapalılık).</summary>
        public static float ContactOcclusion(float t) => 1f - Clamp01(t) * 0.5f;

        /// <summary>Uygulama çarpanı (renge çarpılır): gölge haritasında zaten gölgedeki yerde (shadowMask 0) etkisiz.</summary>
        public static float ContactApplyFactor(float visibility, float strength, float shadowMask)
            => Lerp(1f, visibility, Clamp01(strength) * Clamp01(shadowMask));

        // ---------------------------------------------------------------- SSR

        /// <summary>Pürüzsüzlük ağırlığı: cutoff altı 0, fadeStart üstü 1 (roughness cutoff).</summary>
        public static float SmoothnessWeight(float smoothness, float cutoff, float fadeStart)
        {
            if (fadeStart <= cutoff)
                return smoothness >= cutoff ? 1f : 0f;
            return SmoothStep(cutoff, fadeStart, smoothness);
        }

        /// <summary>Alfa (malzeme pürüzsüzlüğü) yoksa ıslaklık sezgiseli: yukarı bakan yüzey * ıslaklık.</summary>
        public static float WetSmoothness(float normalY, float wetness)
            => Clamp01(wetness) * SmoothStep(0.6f, 0.95f, normalY);

        /// <summary>Ekran kenarı sönümü (0 kenarda, 1 içeride).</summary>
        public static float ScreenEdgeFade(float u, float v, float fade)
        {
            if (fade <= 0f)
                return (u < 0f || u > 1f || v < 0f || v > 1f) ? 0f : 1f;
            var m = Math.Min(Math.Min(u, 1f - u), Math.Min(v, 1f - v));
            return SmoothStep(0f, fade, m);
        }

        /// <summary>Kat edilen mesafeye göre sönüm: 1 - (d/max)^2.</summary>
        public static float SsrDistanceFade(float travelled, float maxDistance)
        {
            if (maxDistance <= 0f)
                return 0f;
            var x = Clamp01(travelled / maxDistance);
            return 1f - x * x;
        }

        /// <summary>Schlick Fresnel.</summary>
        public static float Fresnel(float nDotV, float f0)
        {
            var x = 1f - Clamp01(nDotV);
            var x2 = x * x;
            return f0 + (1f - f0) * x2 * x2 * x;
        }

        /// <summary>Yansıma yönü kameraya doğru ise (görünür veri yok) sönümlenir. viewZ: görünüm uzayı Z (ileri = eksi).</summary>
        public static float TowardCameraFade(float reflectedViewZ) => 1f - SmoothStep(-0.1f, 0.25f, reflectedViewZ);

        /// <summary>Arka yüze isabet reddi: yansıma ışını ile isabet normali aynı yöne bakıyorsa reddedilir.</summary>
        public static bool RejectBackfaceHit(float hitNormalDotRay) => hitNormalDotRay > 0.15f;

        /// <summary>Parlak piksellerden kaynaklı ateş böceklerini yumuşakça bastırır (HDR sıkıştırma).</summary>
        public static float SoftClampHdr(float c) => c / (1f + Math.Max(0f, c) * 0.0625f);

        // ---------------------------------------------------------------- CPU referans ışın yürüyüşü

        public struct MarchResult
        {
            public bool Hit;
            public float U, V, T, EyeDepth;
        }

        /// <summary>
        /// Ekran-uzayı doğrusal ışın yürüyüşü (hi-Z yok): (u,v) ve 1/w ekranda doğrusal enterpole edilir, görünüm derinliği = 1/k.
        /// Gölgelendirici ile aynı: adım sayısı = clamp(ceil(pikselUzunluğu/stride), 1, maxSteps), isabette ikili arama ile inceltme.
        /// sceneEye(u,v) = o noktadaki görünür yüzeyin göz derinliği (gökyüzü = +sonsuz).
        /// </summary>
        public static MarchResult MarchScreenRay(float u0, float v0, float w0, float u1, float v1, float w1, int maxSteps, float stridePx,
            float screenW, float screenH, float jitter01, float thicknessAbs, float thicknessRel, float biasAbs, float biasRel, int refineSteps,
            Func<float, float, float> sceneEye)
        {
            var result = new MarchResult();
            if (sceneEye == null || w0 <= 0f || w1 <= 0f || maxSteps <= 0)
                return result;
            var k0 = 1f / w0;
            var k1 = 1f / w1;
            var dx = (u1 - u0) * screenW;
            var dy = (v1 - v0) * screenH;
            var distPx = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distPx < 1f)
                return result;
            var steps = (int)Math.Ceiling(distPx / Math.Max(0.5f, stridePx));
            steps = steps < 1 ? 1 : steps > maxSteps ? maxSteps : steps;
            var jit = Clamp01(jitter01);
            var tPrev = 0f;
            for (var i = 0; i < steps; i++)
            {
                var t = (i + 0.25f + 0.75f * jit) / steps;
                var u = Lerp(u0, u1, t);
                var v = Lerp(v0, v1, t);
                if (u < 0f || u > 1f || v < 0f || v > 1f)
                    break;
                var w = 1f / Lerp(k0, k1, t);
                var se = sceneEye(u, v);
                var diff = w - se;
                var thick = thicknessAbs + thicknessRel * w;
                if (diff > biasAbs + biasRel * w && diff < thick)
                {
                    var lo = tPrev;
                    var hi = t;
                    for (var j = 0; j < refineSteps; j++)
                    {
                        var mid = 0.5f * (lo + hi);
                        var mw = 1f / Lerp(k0, k1, mid);
                        var ms = sceneEye(Lerp(u0, u1, mid), Lerp(v0, v1, mid));
                        if (mw - ms > biasAbs + biasRel * mw)
                            hi = mid;
                        else
                            lo = mid;
                    }
                    result.Hit = true;
                    result.T = hi;
                    result.U = Lerp(u0, u1, hi);
                    result.V = Lerp(v0, v1, hi);
                    result.EyeDepth = 1f / Lerp(k0, k1, hi);
                    return result;
                }
                tPrev = t;
            }
            return result;
        }

        // ---------------------------------------------------------------- yardımcılar

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float SmoothStep(float e0, float e1, float x)
        {
            if (e1 == e0)
                return x >= e1 ? 1f : 0f;
            var t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }
    }
}
