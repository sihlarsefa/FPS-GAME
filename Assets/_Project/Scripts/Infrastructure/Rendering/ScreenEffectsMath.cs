using System;
using Project.Core.Domain;

namespace Project.Infrastructure.Rendering
{
    /// <summary>
    /// Bastırma (suppression) ve düşük can efektlerinin saf matematiği (Unity bağımsız, test edilebilir).
    /// </summary>
    public static class ScreenEffectsMath
    {
        /// <summary>Bir yakın ıska ölçeği: yakın = güçlü. radius dışı 0.</summary>
        public static float NearMissImpulse(float distance, float radius)
        {
            if (radius <= 0f || float.IsNaN(distance) || distance < 0f || distance > radius)
                return 0f;
            var closeness = 1f - distance / radius;
            return 0.12f + 0.28f * closeness;
        }

        /// <summary>Bastırma ölçeri (0..1) üstüne darbe ekler.</summary>
        public static float AddSuppression(float meter, float impulse)
        {
            return Clamp01(meter + Math.Max(0f, impulse));
        }

        /// <summary>Ölçer zamanla söner (saniyede decayPerSecond).</summary>
        public static float DecaySuppression(float meter, float deltaTime, float decayPerSecond = 0.28f)
        {
            return Clamp01(meter - Math.Max(0f, deltaTime) * Math.Max(0f, decayPerSecond));
        }

        /// <summary>Vinyet opaklığı (0..1).</summary>
        public static float VignetteAlpha(float suppression) => 0.75f * Smooth(suppression);

        /// <summary>Renk solması (0..1), vinyetten daha hafif.</summary>
        public static float DesaturationAlpha(float suppression) => 0.35f * Smooth(suppression);

        /// <summary>Nişan sallantısı gücü (0..1): eşik altında yok.</summary>
        public static float SwayAmount(float suppression)
        {
            return Clamp01((suppression - 0.15f) / 0.85f);
        }

        /// <summary>
        /// Düşük canda ses boğuklaştırma kesme frekansı (Hz). Can eşiğinin üstünde 22000 (kapalı sayılır);
        /// eşiğin altında 22000'den 1200'e iner.
        /// </summary>
        public static float MuffleCutoff(float healthFraction, float threshold = 0.25f)
        {
            if (threshold <= 0f || healthFraction >= threshold)
                return 22000f;
            var t = Clamp01(1f - healthFraction / threshold);
            return 22000f + (1200f - 22000f) * t;
        }

        /// <summary>
        /// Hasar yönüne göre kamera yumruğu. relativeAngleDeg: oyuncunun baktığı yöne göre kaynağın açısı
        /// (0 = ön, +90 = sağ). Ön vuruş kamerayı yukarı, yandan vuruş yana + roll verir.
        /// </summary>
        public static void PunchFor(float damage, float relativeAngleDeg, out float pitch, out float yaw, out float roll)
        {
            var strength = Clamp01(damage / 60f);
            var magnitude = 0.8f + 2.6f * strength;
            var rad = relativeAngleDeg * (float)(Math.PI / 180.0);
            var side = (float)Math.Sin(rad);
            var front = (float)Math.Cos(rad);
            pitch = magnitude * (0.5f + 0.5f * front);
            yaw = -side * magnitude * 0.9f;
            roll = -side * magnitude * 0.7f;
        }

        // ---------------------------------------------------------------- Post 2.0: derecelendirme ön ayarı

        /// <summary>Derecelendirme vektörü düzeni (düz float[]: kolay harmanlanır).</summary>
        public const int GradeTemperature = 0, GradeTint = 1, GradeSaturation = 2, GradeContrast = 3, GradeExposure = 4,
            GradeFilter = 5,       // r,g,b (5..7)
            GradeShadows = 8,      // r,g,b,w (8..11)
            GradeMidtones = 12,    // 12..15
            GradeHighlights = 16,  // 16..19
            GradeLength = 20;

        /// <summary>Preset geçişi süresi (sn).</summary>
        public const float GradeBlendSeconds = 0.5f;

        /// <summary>Saate göre taban pozlama (AtmospherePreset.PostExposure ile uyumlu).</summary>
        public static float TimeExposure(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Safak: return 0.0f;
                case TimeOfDay.Aksam: return 0.05f;
                case TimeOfDay.Gece: return 0.20f;
                default: return 0.15f;
            }
        }

        /// <summary>Harita + saat için "Anadolu askerî" derecelendirme vektörü (SMH + beyaz dengesi + renk ayarı).</summary>
        public static float[] BuildGrade(MapGrade g, TimeOfDay time)
        {
            float dTemp = 0f, dTint = 0f, dSat = 0f, dCon = 0f;
            float sr = 1f, sg = 1f, sb = 1f, hr = 1f, hg = 1f, hb = 1f;
            switch (time)
            {
                case TimeOfDay.Gunduz: dSat = 8f; break;
                case TimeOfDay.Safak: dTemp = 6f; dTint = 4f; dSat = -4f; dCon = 3f; hr = 1.04f; hb = 0.97f; break;
                case TimeOfDay.Aksam: dTemp = 10f; dTint = 2f; dSat = 0f; dCon = 8f; hr = 1.06f; hb = 0.9f; break;
                case TimeOfDay.Gece: dTemp = -20f; dTint = -2f; dSat = -10f; dCon = 6f; sr = 0.9f; sg = 0.96f; sb = 1.12f; hr = 0.94f; hb = 1.05f; break;
            }

            var v = new float[GradeLength];
            v[GradeTemperature] = Clamp(g.Temperature + dTemp, -100f, 100f);
            v[GradeTint] = Clamp(g.Tint + dTint, -100f, 100f);
            v[GradeSaturation] = Clamp(g.Saturation + dSat, -100f, 100f);
            v[GradeContrast] = Clamp(g.Contrast + dCon, -100f, 100f);
            v[GradeExposure] = TimeExposure(time) + g.ExposureOffset;
            v[GradeFilter] = g.ColorFilter[0]; v[GradeFilter + 1] = g.ColorFilter[1]; v[GradeFilter + 2] = g.ColorFilter[2];
            for (var i = 0; i < 4; i++)
            {
                var m = i == 0 ? sr : i == 1 ? sg : i == 2 ? sb : 1f;
                var h = i == 0 ? hr : i == 1 ? hg : i == 2 ? hb : 1f;
                v[GradeShadows + i] = g.Shadows[i] * m;
                v[GradeMidtones + i] = g.Midtones[i];
                v[GradeHighlights + i] = g.Highlights[i] * h;
            }

            return v;
        }

        /// <summary>Preset geçişini 0,5 sn'de yumuşak (smoothstep) harmanlayan saf durum makinesi.</summary>
        public sealed class GradeBlender
        {
            private float[] _from, _to, _cur;
            private float _elapsed;
            private bool _hasTarget;

            public float Duration = GradeBlendSeconds;
            public bool HasTarget => _hasTarget;
            public float[] Current => _cur;
            public bool Blending => _hasTarget && _elapsed < Duration;

            /// <summary>Yeni hedef. snap veya ilk hedefse anında; değilse mevcut değerden harmanlar.</summary>
            public void SetTarget(float[] target, bool snap = false)
            {
                if (target == null)
                    return;
                if (!_hasTarget || snap)
                {
                    _from = (float[])target.Clone(); _to = (float[])target.Clone(); _cur = (float[])target.Clone();
                    _elapsed = Duration; _hasTarget = true;
                    return;
                }

                var same = target.Length == _to.Length;
                for (var i = 0; same && i < target.Length; i++)
                    if (Math.Abs(target[i] - _to[i]) > 1e-5f) same = false;
                if (same)
                    return;

                _from = (float[])_cur.Clone();
                _to = (float[])target.Clone();
                _elapsed = 0f;
            }

            /// <summary>Zamanı ilerletir; değer değiştiyse true.</summary>
            public bool Advance(float deltaTime)
            {
                if (!Blending)
                    return false;
                _elapsed = Math.Min(Duration, _elapsed + Math.Max(0f, deltaTime));
                var t = Duration <= 0f ? 1f : _elapsed / Duration;
                t = t * t * (3f - 2f * t);
                for (var i = 0; i < _cur.Length; i++)
                    _cur[i] = _from[i] + (_to[i] - _from[i]) * t;
                return true;
            }
        }

        // ---------------------------------------------------------------- SSAO kademeleri

        public readonly struct SsaoTier
        {
            public readonly bool Enabled;
            public readonly float Intensity, Radius, Falloff;
            public readonly bool Downsample;
            public readonly int Samples; // 0 düşük, 1 orta, 2 yüksek

            public SsaoTier(bool enabled, float intensity, float radius, float falloff, bool downsample, int samples)
            {
                Enabled = enabled; Intensity = intensity; Radius = radius; Falloff = falloff; Downsample = downsample; Samples = samples;
            }
        }

        /// <summary>Kalite kademesine (0..3) göre SSAO: Düşük kapalı; Orta yarı çözünürlük; Yüksek/Ultra tam çözünürlük.</summary>
        public static SsaoTier SsaoForTier(int tier)
        {
            tier = tier < 0 ? 0 : tier > 3 ? 3 : tier;
            switch (tier)
            {
                case 0: return new SsaoTier(false, 0f, 0.2f, 100f, true, 0);
                case 1: return new SsaoTier(true, 0.7f, 0.35f, 100f, true, 0);
                case 2: return new SsaoTier(true, 1.0f, 0.5f, 100f, true, 1);
                default: return new SsaoTier(true, 1.2f, 0.6f, 100f, false, 2);
            }
        }

        /// <summary>
        /// Harita-saat bazlı SSAO ince ayarı. interior01: 0 açık alan .. 1 iç mekân/gölgelik altı; hour: 0..24 oyun saati.
        /// Gerekçe: AO yalnız ortam ışığını karartır (AfterOpaque=false). İç mekân/gölgelik altında ortam ışığı baskın olduğundan
        /// yoğunluk +%50, yarıçap +%25 (köşe/mobilya dibi okunur); açık alanda doğrudan güneş baskın, AO'yu %15 kısıp halo/kirli görüntüyü önleriz.
        /// Gün ortası (10-15) güneş dik, ortam payı düşük: x0,9; alacakaranlık/gece ortam payı yüksek: x1,1. Düşük kademe kapalı kalır.
        /// </summary>
        public static SsaoTier SsaoFor(int tier, float interior01, float hour)
        {
            var b = SsaoForTier(tier);
            if (!b.Enabled)
                return b;
            var k = Clamp01(interior01);
            var intensity = b.Intensity * (0.85f + 0.65f * k);          // 0,85 (açık) .. 1,50 (iç mekân)
            var radius = b.Radius * (0.9f + 0.35f * k);                  // 0,90 .. 1,25
            intensity *= SsaoHourFactor(hour);
            return new SsaoTier(true, Clamp(intensity, 0f, 2.0f), Clamp(radius, 0.1f, 0.9f), b.Falloff, b.Downsample, b.Samples);
        }

        /// <summary>Saat çarpanı: 10-15 arası 0,9; 18-05 arası 1,1; aralar yumuşak geçiş.</summary>
        public static float SsaoHourFactor(float hour)
        {
            var h = hour % 24f; if (h < 0f) h += 24f;
            var noon = Smooth((h - 8f) / 2f) * (1f - Smooth((h - 15f) / 3f));   // 8-10 yükselir, 15-18 iner
            return 1.1f - 0.2f * noon;
        }

        // ---------------------------------------------------------------- Kontakt gölge gündüz/gece

        /// <summary>
        /// Kontakt gölge yoğunluğu (hedef): gündüz 0,35, gece 0,5. daylight01: 1 gündüz .. 0 gece.
        /// Gerekçe: gündüz güneş gölge haritası zaten güçlü, kontakt gölge yalnız temas çizgisini oturtur (ince, 0,35); gece tek yönlü ay/ışık
        /// zayıf olduğundan temas gölgesi şekli ayırt ettirir (0,5). Kademe tablosundaki Strength (0,55-0,8) 0,8'e göre ölçek olarak biner.
        /// </summary>
        public static float ContactStrengthTarget(float daylight01) => 0.5f + (0.35f - 0.5f) * Smooth(daylight01);

        /// <summary>Kontakt ışın uzunluğu çarpanı: gündüz 0,9 (kısa, sızıntısız), gece 1,15 (uzun ay gölgesi).</summary>
        public static float ContactLengthScale(float daylight01) => 1.15f + (0.9f - 1.15f) * Smooth(daylight01);

        /// <summary>Kademe temel gücünü (tablo) gündüz/gece hedefine çevirir; Ultra (0,8) = hedefin kendisi.</summary>
        public static float ContactStrength(float tierStrength, float daylight01) =>
            Clamp01(ContactStrengthTarget(daylight01) * (tierStrength / 0.8f));

        // ---------------------------------------------------------------- DoF eğrileri

        private static float SmoothStep01(float t) => Smooth(t);

        /// <summary>ADS DoF başlangıcı (m): hafif yakın bulanıklık, nişan arttıkça biraz uzaklaşır.</summary>
        public static float AdsDofStart(float ads01) => 0.1f + 0.15f * SmoothStep01(ads01);

        /// <summary>ADS DoF bitişi (m): 1000 (etkisiz) → 0,6; smoothstep eğrisi (ani sıçrama yok).</summary>
        public static float AdsDofEnd(float ads01)
        {
            var s = SmoothStep01(ads01);
            return s <= 0f ? 1000f : Math.Max(0.6f, 1.5f - 0.9f * s) + (1f - s) * (1f - s) * 40f;
        }

        /// <summary>Ölüm kamerası DoF bitişi (m): ilk anda geniş, 1,5 sn'de 1 m'ye iner.</summary>
        public static float DeathDofEnd(float secondsSinceDeath)
        {
            var s = SmoothStep01(secondsSinceDeath / 1.5f);
            return 12f + (1f - 12f) * s;
        }

        /// <summary>Ölüm kamerası gaussian yarıçapı (0.5..1.5) zamanla büyür.</summary>
        public static float DeathDofRadius(float secondsSinceDeath) => 0.6f + 0.9f * SmoothStep01(secondsSinceDeath / 1.5f);

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
