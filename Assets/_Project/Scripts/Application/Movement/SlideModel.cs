using System;

namespace Project.Application.Movement
{
    /// <summary>
    /// Kayma (slide) fiziği. Apex/MW: düz zeminde ~1 sn kayar, eğim belirler: yokuş aşağı hızlanır/uzar, yokuş yukarı kısalır.
    /// Hız lineer sürtünme + eğim bileşeniyle ilerler (eski zaman bazlı eğri yerine); hız eşiğin altına inince veya
    /// azami süre dolunca biter. Kayma bitişinde zıplama momentumu korur (slide-jump). Saf, deterministik.
    /// </summary>
    public sealed class SlideModel
    {
        public const float Gravity = 9.81f;
        /// <summary>Düz zeminde sürtünme yavaşlaması (m/s²). 8,3 m/s'den 3 m/s'ye ~1,2 s.</summary>
        public const float Friction = 4.4f;
        /// <summary>Eğim ivmesine katılım (kayma kızağı sürtünmeli: g*sin*0,75).</summary>
        public const float SlopeGain = 0.75f;
        /// <summary>Altına inince kayma biter (m/s).</summary>
        public const float EndSpeed = 3.0f;
        /// <summary>Düz zeminde azami süre (s).</summary>
        public const float MaxSecondsFlat = 1.3f;
        /// <summary>Yokuş aşağıda azami süre (s).</summary>
        public const float MaxSecondsDownhill = 2.6f;
        /// <summary>Başlangıç hız kazancı (itme).</summary>
        public const float EntryBoost = 1.05f;
        /// <summary>Kayma süresince hız tavanı (koşu hızının bu katı).</summary>
        public const float SpeedCapFactor = 1.25f;

        private float _speed;
        private float _time;
        private float _maxTime = MaxSecondsFlat;
        private float _cap;
        private bool _active;

        public bool Active => _active;
        public float Speed => _speed;
        public float Time => _time;

        /// <summary>İlerleme 0..1 (kamera/viewmodel için); süre azami süreye oranı.</summary>
        public float Progress => _active && _maxTime > 0f ? Math.Min(1f, _time / _maxTime) : 0f;

        /// <summary>Kayma başlatır. Başlangıç hızı = giriş hızı * itme, sprintSpeed * <paramref name="entryCapFactor"/> ile sınırlı.</summary>
        public void Begin(float entrySpeed, float sprintSpeed, float entryCapFactor, float loadSpeedFactor)
        {
            var sp = Math.Max(0.1f, sprintSpeed);
            _cap = sp * SpeedCapFactor;
            var start = Math.Min(Math.Max(entrySpeed, 0f) * EntryBoost, sp * Math.Max(1f, entryCapFactor));
            _speed = start * (float.IsNaN(loadSpeedFactor) ? 1f : loadSpeedFactor);
            _time = 0f;
            _maxTime = MaxSecondsFlat;
            _active = true;
        }

        /// <summary>
        /// Kare adımı. <paramref name="slopeDegrees"/> zemin eğimi, <paramref name="downhillDot"/> kayma yönünün yokuş aşağıya
        /// iç çarpımı (-1..1). Dönüş: kayma sürüyor mu.
        /// </summary>
        public bool Tick(float dt, float slopeDegrees, float downhillDot)
        {
            if (!_active) return false;
            if (float.IsNaN(dt) || dt <= 0f) return true;

            var deg = float.IsNaN(slopeDegrees) ? 0f : Math.Max(0f, Math.Min(60f, slopeDegrees));
            var dot = float.IsNaN(downhillDot) ? 0f : Math.Max(-1f, Math.Min(1f, downhillDot));
            var slopeAccel = Gravity * (float)Math.Sin(deg * Math.PI / 180.0) * SlopeGain * dot;

            // Net ivme: eğim bileşeni - sürtünme. Dik yokuş aşağıda net pozitif olabilir (hızlanma).
            var net = slopeAccel - Friction;
            _speed = Math.Min(_cap, Math.Max(0f, _speed + net * dt));
            _time += dt;

            // Yokuş aşağıda süre tavanı uzar (eğim ve yönle orantılı).
            var downhill = Math.Max(0f, dot) * Math.Min(1f, deg / 20f);
            _maxTime = MaxSecondsFlat + (MaxSecondsDownhill - MaxSecondsFlat) * downhill;

            if (_speed <= EndSpeed && net < 0f || _time >= _maxTime)
                _active = false;
            return _active;
        }

        public void Cancel() => _active = false;

        /// <summary>Kayma çıkışında zıplayınca yatay hız korunma oranı (slide-jump momentumu): 1,0, hızlı kayma %5 bonus, en çok 1,05.</summary>
        public static float JumpOutKeep(float speed, float sprintSpeed)
        {
            var r = speed / Math.Max(0.1f, sprintSpeed);
            return 1f + 0.05f * Math.Min(1f, Math.Max(0f, r - 0.8f) / 0.4f);
        }

        /// <summary>Kayma yönü direksiyonu (rad/s): hız arttıkça az dönüş (momentum), 0,6..1,4.</summary>
        public static float SteerRate(float speed, float sprintSpeed)
        {
            var r = Math.Min(1.2f, Math.Max(0f, speed / Math.Max(0.1f, sprintSpeed)));
            return 1.4f - 0.8f * r;
        }
    }
}
