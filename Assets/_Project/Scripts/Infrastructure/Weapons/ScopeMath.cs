using System;

namespace Project.Infrastructure.Weapons
{
    /// <summary>Retikül türü: silah/dürbün tipine göre seçilir.</summary>
    public enum ReticleKind
    {
        None = 0,
        RedDot = 1,
        AcogChevron = 2,
        MilDot = 3
    }

    /// <summary>Kalite kademesine göre dürbün maliyet planı.</summary>
    public readonly struct ScopeTierPlan
    {
        public readonly bool PictureInPicture;
        public readonly int RenderTextureSize;
        public readonly int UpdateEveryNFrames;
        public readonly bool Glint;
        public readonly bool LensDirt;

        public ScopeTierPlan(bool pip, int size, int every, bool glint, bool dirt)
        {
            PictureInPicture = pip;
            RenderTextureSize = size;
            UpdateEveryNFrames = every;
            Glint = glint;
            LensDirt = dirt;
        }
    }

    /// <summary>Saf dürbün matematiği (Unity bağımsız, test edilebilir).</summary>
    public static class ScopeMath
    {
        public const float Gravity = 9.81f;
        public const float MinFovDegrees = 1.5f;
        public const float MaxFovDegrees = 120f;

        /// <summary>Menzil basamakları (m): PageUp/PageDown.</summary>
        public static readonly int[] ZeroingSteps = { 100, 200, 300, 400, 500 };

        public static float Sanitize(float v, float fallback = 0f)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;
        }

        /// <summary>Büyütmeye göre dikey FOV (derece): tan(fov/2) = tan(base/2) / mag.</summary>
        public static float FovFromMagnification(float baseFovDegrees, float magnification)
        {
            baseFovDegrees = Math.Max(1f, Math.Min(MaxFovDegrees, Sanitize(baseFovDegrees, 60f)));
            magnification = Math.Max(1f, Sanitize(magnification, 1f));
            var half = baseFovDegrees * 0.5f * (float)(Math.PI / 180.0);
            var fov = 2f * (float)Math.Atan(Math.Tan(half) / magnification) * (float)(180.0 / Math.PI);
            return Math.Max(MinFovDegrees, Math.Min(MaxFovDegrees, fov));
        }

        /// <summary>Hedef menzilde (aynı yükseklik, boşlukta) vuruş için gereken fırlatma açısı (derece).</summary>
        public static float ZeroingAngleDegrees(float rangeMeters, float muzzleVelocity, float gravity = Gravity)
        {
            rangeMeters = Math.Max(0f, Sanitize(rangeMeters));
            muzzleVelocity = Sanitize(muzzleVelocity, 700f);
            if (muzzleVelocity < 1f || rangeMeters <= 0f)
                return 0f;
            var s = gravity * rangeMeters / (muzzleVelocity * muzzleVelocity);
            if (s >= 1f)
                return 45f;
            return 0.5f * (float)Math.Asin(s) * (float)(180.0 / Math.PI);
        }

        /// <summary>Verilen sıfırlama basamağında mermi düşüşü (m): zeroing menzilinin dışındaki hedef için.</summary>
        public static float DropAtRange(float rangeMeters, float muzzleVelocity, float zeroingAngleDegrees, float gravity = Gravity)
        {
            if (muzzleVelocity < 1f)
                return 0f;
            var th = zeroingAngleDegrees * (float)(Math.PI / 180.0);
            var vx = muzzleVelocity * (float)Math.Cos(th);
            if (vx < 1f)
                return 0f;
            var t = rangeMeters / vx;
            return rangeMeters * (float)Math.Tan(th) - 0.5f * gravity * t * t;
        }

        public static int ClampZeroingIndex(int index)
        {
            return Math.Max(0, Math.Min(ZeroingSteps.Length - 1, index));
        }

        public static int NearestZeroingIndex(float rangeMeters)
        {
            var best = 0;
            var bestD = float.MaxValue;
            for (var i = 0; i < ZeroingSteps.Length; i++)
            {
                var d = Math.Abs(ZeroingSteps[i] - rangeMeters);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            return best;
        }

        public static int StepZeroing(int index, int direction)
        {
            return ClampZeroingIndex(index + (direction > 0 ? 1 : direction < 0 ? -1 : 0));
        }

        /// <summary>Fare tekerleği ile değişken büyütme; adım ~%12 çarpan.</summary>
        public static float StepMagnification(float current, float scrollY, float minMag, float maxMag)
        {
            if (maxMag < minMag)
                maxMag = minMag;
            current = Math.Max(minMag, Math.Min(maxMag, Sanitize(current, minMag)));
            if (Math.Abs(scrollY) < 1e-3f || maxMag - minMag < 1e-3f)
                return current;
            var factor = scrollY > 0f ? 1.12f : 1f / 1.12f;
            return Math.Max(minMag, Math.Min(maxMag, current * factor));
        }

        /// <summary>Dürbünün değişken olup olmadığı: üst sınır alt sınırdan belirgin büyük.</summary>
        public static bool IsVariable(float minMag, float maxMag) => maxMag - minMag > 0.4f;

        /// <summary>Katalog AdsZoom değerinden dürbün büyütme aralığı çıkarır (min, max).</summary>
        public static void MagnificationRange(float adsZoom, bool hasScope, bool isSniper, out float min, out float max)
        {
            adsZoom = Math.Max(1f, Sanitize(adsZoom, 1f));
            if (hasScope && isSniper && adsZoom >= 4f)
            {
                min = Math.Max(2f, adsZoom * 0.5f);
                max = adsZoom * 1.5f;
                return;
            }

            min = max = adsZoom;
        }

        /// <summary>Retikül seçimi: dürbünsüz = kırmızı nokta (veya yok), 2-5x = ACOG, daha büyük = mil-dot.</summary>
        public static ReticleKind SelectReticle(float adsZoom, bool hasScope)
        {
            if (!hasScope)
                return adsZoom >= 1.1f ? ReticleKind.RedDot : ReticleKind.None;
            return adsZoom >= 5f ? ReticleKind.MilDot : ReticleKind.AcogChevron;
        }

        /// <summary>Kalite kademesi (0..3) -> maliyet planı. 0/1 = tam ekran yakınlaştırma, 2/3 = PiP.</summary>
        public static ScopeTierPlan TierPlan(int tier)
        {
            tier = Math.Max(0, Math.Min(3, tier));
            switch (tier)
            {
                case 0: return new ScopeTierPlan(false, 0, 1, false, false);
                case 1: return new ScopeTierPlan(false, 0, 1, true, false);
                case 2: return new ScopeTierPlan(true, 512, 2, true, true);
                default: return new ScopeTierPlan(true, 1024, 1, true, true);
            }
        }

        /// <summary>
        /// Göz kutusu gölgesi: sway/yanlış hizalama büyüdükçe karanlık halka artar.
        /// Dönüş: x,y = halkanın kayması (-1..1), z = karanlık 0..1.
        /// </summary>
        public static void EyeBox(float swayX, float swayY, float misalign, out float shiftX, out float shiftY, out float darkness)
        {
            swayX = Sanitize(swayX);
            swayY = Sanitize(swayY);
            misalign = Math.Max(0f, Math.Min(1f, Sanitize(misalign)));
            shiftX = Math.Max(-1f, Math.Min(1f, swayX));
            shiftY = Math.Max(-1f, Math.Min(1f, swayY));
            var mag = (float)Math.Sqrt(shiftX * shiftX + shiftY * shiftY);
            darkness = Math.Max(0f, Math.Min(1f, 0.25f + mag * 0.5f + misalign * 0.6f));
        }

        /// <summary>Nefes durumuna göre dürbün titreme genliği (derece): tutarken ~0, bitkin = büyük.</summary>
        public static float ShakeAmplitude(bool holding, bool exhausted, float breathRemaining, float breathMax)
        {
            if (holding)
                return 0.01f;
            if (exhausted || breathRemaining <= 0f)
                return 0.45f;
            var missing = breathMax > 0f ? 1f - Math.Max(0f, Math.Min(1f, breathRemaining / breathMax)) : 0f;
            return 0.12f + 0.15f * missing;
        }

        /// <summary>
        /// Parıltı şiddeti 0..1: dürbün lensi güneşe yakın bir yönü gözlemciye yansıtırken parlar.
        /// scopeForward = namlu yönü (birim), toObserver = dürbünden gözlemciye (birim), toSun = güneş yönü (birim).
        /// </summary>
        public static float GlintIntensity(float fx, float fy, float fz, float ox, float oy, float oz, float sx, float sy, float sz, float distance)
        {
            // Lens normali namlu yönünün tersidir; gözlemci lensin önünde olmalı.
            var facing = fx * ox + fy * oy + fz * oz;
            if (facing < 0.5f)
                return 0f;
            // Yansıma: güneş ışığı gözlemciye yakınsa parlar.
            var sunAlign = ox * sx + oy * sy + oz * sz;
            if (sunAlign < 0.2f)
                return 0f;
            var g = (facing - 0.5f) * 2f * Math.Max(0f, (sunAlign - 0.2f) / 0.8f);
            var dist = Math.Max(0f, Sanitize(distance));
            var distFade = dist < 15f ? dist / 15f : dist > 600f ? 0f : 1f;
            return Math.Max(0f, Math.Min(1f, g * distFade));
        }

        /// <summary>Retikülün zeroing basamağıyla kaydığı dikey kayma (piksel/ekran birimi 0..1): düşüş ofseti.</summary>
        public static float MilDotHoldover(int zeroingIndex, float rangeMeters, float muzzleVelocity)
        {
            var zero = ZeroingSteps[ClampZeroingIndex(zeroingIndex)];
            var ang = ZeroingAngleDegrees(zero, muzzleVelocity);
            var drop = DropAtRange(rangeMeters, muzzleVelocity, ang);
            return drop;
        }
    }
}
