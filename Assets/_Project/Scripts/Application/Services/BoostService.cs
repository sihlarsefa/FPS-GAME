using System;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Boost barı (0-100): zamanla azalır, seviyeye göre saniyede can yeniler, %60 üstünde küçük hız bonusu.
    /// İyileştirme küçük parçalar halinde biriktirilir ve en az 1 can olunca uygulanır (kare başına olay/ağ trafiği yok).
    /// </summary>
    public sealed class BoostService
    {
        public const float Max = 100f;

        /// <summary>Saniyede sönen boost miktarı.</summary>
        public const float DecayPerSecond = 0.6f;

        /// <summary>Bu değerin üstünde hız bonusu verilir.</summary>
        public const float SpeedBonusThreshold = 60f;
        public const float SpeedBonusMultiplier = 1.04f;

        // İyileştirme kademeleri (can/sn): >0 → 0.5, >40 → 1.0, >80 → 1.5
        public const float HealTier1Rate = 0.5f;
        public const float HealTier2Threshold = 40f;
        public const float HealTier2Rate = 1f;
        public const float HealTier3Threshold = 80f;
        public const float HealTier3Rate = 1.5f;

        private const float HealChunk = 1f;

        private float _value;
        private float _pendingHeal;

        public float Value => _value;
        public float Normalized => _value / Max;
        public float SpeedMultiplier => _value > SpeedBonusThreshold ? SpeedBonusMultiplier : 1f;
        public bool IsActive => _value > 0f;

        /// <summary>Anlık iyileştirme hızı (can/sn).</summary>
        public float HealPerSecond => HealRateFor(_value);

        /// <summary>Boost değeri değiştiğinde (ekleme/sıfırlama) tetiklenir; söndürme her karede tetiklenmez.</summary>
        public event Action Changed;

        public static float HealRateFor(float value)
        {
            if (value > HealTier3Threshold)
                return HealTier3Rate;

            if (value > HealTier2Threshold)
                return HealTier2Rate;

            return value > 0f ? HealTier1Rate : 0f;
        }

        public void Add(float amount)
        {
            if (!(amount > 0f) || float.IsInfinity(amount))
                return;

            var next = _value + amount;
            _value = next > Max ? Max : next;
            Changed?.Invoke();
        }

        public void Reset()
        {
            if (_value <= 0f && _pendingHeal <= 0f)
                return;

            _value = 0f;
            _pendingHeal = 0f;
            Changed?.Invoke();
        }

        /// <summary>Boost'u söndürür ve hedef canlıysa iyileştirir.</summary>
        public void Tick(float deltaTime, IHealable target, bool targetAlive)
        {
            if (_value <= 0f || !(deltaTime > 0f))
                return;

            // Kademe sınırlarını atlayan büyük adımlarda da doğru miktar için alt adımlara böl.
            var remaining = deltaTime;
            while (remaining > 0f && _value > 0f)
            {
                var step = remaining > 0.25f ? 0.25f : remaining;
                remaining -= step;

                if (targetAlive && target != null)
                    _pendingHeal += HealRateFor(_value) * step;

                _value -= DecayPerSecond * step;
                if (_value < 0f)
                    _value = 0f;
            }

            if (!targetAlive || target == null)
            {
                _pendingHeal = 0f;
                return;
            }

            if (_pendingHeal >= HealChunk)
            {
                var amount = (float)Math.Floor(_pendingHeal);
                _pendingHeal -= amount;
                target.Heal(amount);
            }

            if (_value <= 0f && _pendingHeal > 0f)
            {
                // Bar bittiğinde kalan küsuratı da ver.
                var rest = _pendingHeal;
                _pendingHeal = 0f;
                target.Heal(rest);
            }
        }
    }
}
