using System;

namespace Project.Application.Viewmodel
{
    /// <summary>
    /// ADS giriş/çıkış eğrisi. Giriş: kalkışta hafif yavaş, sonda yumuşak oturan smootherstep; çıkış: daha hızlı (x0.7 süre)
    /// ve ease-out. Bitişte küçük bir "oturma" darbesi yayı tetiklenir (silah gözlere oturunca hafifçe tık).
    /// </summary>
    public sealed class AdsCurveBlend
    {
        public const float ExitTimeScale = 0.7f;
        public const float SwayAtAds = 0.35f;

        private float _t;       // lineer ilerleme 0..1
        private bool _wasFull;
        private SpringAxis3 _settle;

        /// <summary>Eğrilenmiş ağırlık 0..1.</summary>
        public float Weight { get; private set; }
        public float Linear => _t;
        public bool IsFull => _t >= 1f;
        /// <summary>Oturma darbesi (m, z ekseni).</summary>
        public float Settle => _settle.Position;

        public static float EnterCurve(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        public static float ExitCurve(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            var u = 1f - t;
            return 1f - u * u * u; // ease-out kübik (t: 1'den 0'a giderken çağıran tersler)
        }

        /// <summary>Sallanma çarpanı: nişanda 1 -> 0.35.</summary>
        public float SwayScale => 1f - (1f - SwayAtAds) * Weight;

        /// <summary>Viewmodel FOV çarpanı: nişanda 8% daralır (ViewmodelDynamics.AdsViewmodelFov ile uyumlu).</summary>
        public float FovScale => 1f - 0.08f * Weight;

        public void Step(bool aiming, float adsSeconds, float dt)
        {
            if (dt <= 0f)
                return;
            if (adsSeconds < 0.05f) adsSeconds = 0.05f;
            if (aiming)
            {
                _t += dt / adsSeconds;
                if (_t > 1f) _t = 1f;
                Weight = EnterCurve(_t);
            }
            else
            {
                _t -= dt / (adsSeconds * ExitTimeScale);
                if (_t < 0f) _t = 0f;
                // Çıkışta eğri, girişin ayna görüntüsü gibi hızlı başlar: ağırlık = 1 - ExitCurve(1 - t).
                Weight = 1f - ExitCurve(1f - _t);
            }

            var full = _t >= 1f;
            if (full && !_wasFull)
                _settle.Velocity -= 0.06f;
            _wasFull = full;
            _settle.Step(0f, 1200f, 0.55f, dt);
        }

        public void Reset()
        {
            _t = 0f; Weight = 0f; _wasFull = false; _settle = default;
        }
    }
}
