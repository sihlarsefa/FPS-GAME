using System;

namespace Project.Infrastructure.Characters.WeaponHold
{
    /// <summary>
    /// Üçüncü şahıs hazır-duruş durumu (saf mantık): alçak hazır (hareketsizken silah iner, indirme yavaş / kaldırma hızlı)
    /// ve duvar geri çekmesi (yakın engelde silah gövdeye çekilip namlu kalkar). Tarkov/Sandstorm'da görülen davranış.
    /// </summary>
    public sealed class WeaponReadyState
    {
        public const float LowerSeconds = 0.70f;
        public const float RaiseSeconds = 0.18f;
        public const float WallInRate = 9f;
        public const float WallOutRate = 4.5f;
        public const float MoveSpeedThreshold = 0.3f;

        private float _idleTime;
        private float _lowReady;
        private float _wallPull;

        /// <summary>0 = silah nişan hazır, 1 = tam alçak hazır.</summary>
        public float LowReady => _lowReady;

        /// <summary>0..1 duvar geri çekme ağırlığı.</summary>
        public float WallPull => _wallPull;

        public float IdleTime => _idleTime;

        /// <summary>Atış, yeniden doldurma ya da nişan alma: sayacı sıfırlar ve silahı hemen kaldırır.</summary>
        public void NotifyActivity()
        {
            _idleTime = 0f;
        }

        public void Reset()
        {
            _idleTime = 0f;
            _lowReady = 0f;
            _wallPull = 0f;
        }

        public void Update(float dt, float speed, float idleToLowReady, float obstacleDistance, float weaponLength, bool allowLowReady)
        {
            if (dt <= 0f)
                return;

            dt = Math.Min(dt, 0.1f);
            if (speed > MoveSpeedThreshold || !allowLowReady)
                _idleTime = 0f;
            else
                _idleTime += dt;

            var wantLow = allowLowReady && _idleTime >= idleToLowReady ? 1f : 0f;
            var seconds = wantLow > _lowReady ? LowerSeconds : RaiseSeconds;
            _lowReady = MoveToward(_lowReady, wantLow, dt / seconds);

            var wallTarget = WeaponHoldMath.WallPullTarget(obstacleDistance, weaponLength);
            var rate = wallTarget > _wallPull ? WallInRate : WallOutRate;
            _wallPull += (wallTarget - _wallPull) * (1f - (float)Math.Exp(-rate * dt));
            if (Math.Abs(wallTarget - _wallPull) < 0.001f)
                _wallPull = wallTarget;
        }

        private static float MoveToward(float v, float target, float maxDelta)
        {
            if (Math.Abs(target - v) <= maxDelta)
                return target;
            return v + Math.Sign(target - v) * maxDelta;
        }
    }
}
