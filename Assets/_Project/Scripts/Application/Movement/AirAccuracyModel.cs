using System;

namespace Project.Application.Movement
{
    /// <summary>
    /// Zıplama / havadayken isabetsizlik. CoD/Battlefield: havadayken saçılma birkaç kat artar; inişten sonra kısa
    /// toparlanma (silah oturur). İniş hızı sertse toparlanma uzar. <see cref="SpreadMultiplier"/> değeri
    /// Application/Combat/Feel'deki yayılım modeline çarpan olarak verilir (1 = etkisiz).
    /// </summary>
    public sealed class AirAccuracyModel
    {
        /// <summary>Havada saçılma çarpanı (tırmanılan tepe noktasında en yüksek).</summary>
        public const float AirborneSpread = 5.5f;
        /// <summary>Havaya çıkıştaki ilk yükseliş süresi (s): çarpan tam değere bu sürede varır.</summary>
        public const float RampSeconds = 0.08f;
        /// <summary>Yumuşak iniş sonrası toparlanma süresi (s).</summary>
        public const float BaseLandingRecover = 0.28f;
        /// <summary>Sert iniş ek toparlanma süresi (s, 12 m/s'de).</summary>
        public const float HardLandingExtra = 0.45f;

        private bool _airborne;
        private float _airTime;
        private float _recover;
        private float _recoverTotal;
        private float _landingPeak = 1f;

        /// <summary>Şu anki saçılma çarpanı (>=1).</summary>
        public float SpreadMultiplier
        {
            get
            {
                if (_airborne)
                {
                    var t = RampSeconds <= 0f ? 1f : Math.Min(1f, _airTime / RampSeconds);
                    return 1f + (AirborneSpread - 1f) * t;
                }

                if (_recover > 0f && _recoverTotal > 0f)
                {
                    var k = _recover / _recoverTotal;
                    // Kare eğri: ilk anda hızlı, sonda yavaş toparlanır.
                    return 1f + (_landingPeak - 1f) * k * k;
                }

                return 1f;
            }
        }

        public bool Airborne => _airborne;

        /// <summary>Kısa süre ADS'ye girişte ek gecikme (s): havada/inişte nişan almak yavaş.</summary>
        public float AdsDelayPenalty => _airborne ? 0.12f : (_recover > 0f ? 0.08f * (_recover / Math.Max(0.01f, _recoverTotal)) : 0f);

        /// <summary>İniş sonrası toparlanma süresi (s).</summary>
        public static float RecoverSeconds(float impactSpeed)
        {
            var v = float.IsNaN(impactSpeed) || impactSpeed < 0f ? 0f : impactSpeed;
            var hard = Math.Min(1f, Math.Max(0f, (v - 4f) / 8f));
            return BaseLandingRecover + HardLandingExtra * hard;
        }

        /// <summary>İniş anındaki tepe saçılma çarpanı: 6 m/s altı yumuşak (2.0), 12+ sert (4.2).</summary>
        public static float LandingPeak(float impactSpeed)
        {
            var v = float.IsNaN(impactSpeed) || impactSpeed < 0f ? 0f : impactSpeed;
            var t = Math.Min(1f, Math.Max(0f, (v - 3f) / 9f));
            return 2f + 2.2f * t;
        }

        /// <summary>Kare adımı. <paramref name="grounded"/> yerde mi; <paramref name="impactSpeed"/> yalnız yere değdiği karede anlamlı.</summary>
        public void Tick(float dt, bool grounded, float impactSpeed)
        {
            if (float.IsNaN(dt) || dt < 0f) dt = 0f;

            if (!grounded)
            {
                if (!_airborne)
                {
                    _airborne = true;
                    _airTime = 0f;
                    _recover = 0f;
                }

                _airTime += dt;
                return;
            }

            if (_airborne)
            {
                _airborne = false;
                // Çok kısa havalanma (basamak/kenar) ceza doğurmaz.
                if (_airTime >= 0.12f)
                {
                    _recoverTotal = RecoverSeconds(impactSpeed);
                    _recover = _recoverTotal;
                    _landingPeak = Math.Min(AirborneSpread, LandingPeak(impactSpeed));
                }

                _airTime = 0f;
            }

            if (_recover > 0f)
                _recover = Math.Max(0f, _recover - dt);
        }

        public void Reset()
        {
            _airborne = false;
            _airTime = 0f;
            _recover = 0f;
            _recoverTotal = 0f;
        }
    }
}
