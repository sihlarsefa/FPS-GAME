using System;

namespace Project.Application.Movement
{
    /// <summary>
    /// Tarkov tarzı yorgunluk: stamina uzun süre düşük kalırsa harcama artar (en çok x1,35), yeterince dinlenince söner.
    /// Ayrıca nefes tutma (keskin nişancı): tutma süresi stamina ile ölçeklenir, bırakınca kısa süre "soluklanma" salınımı artar.
    /// </summary>
    public sealed class FatigueModel
    {
        public const float LowThreshold = 0.30f;
        public const float RestThreshold = 0.65f;
        public const float BuildSeconds = 6f;
        public const float DecaySeconds = 8f;
        public const float MaxDrainBonus = 0.35f;

        private float _fatigue;

        /// <summary>0..1 yorgunluk.</summary>
        public float Value => _fatigue;

        /// <summary>Stamina harcama çarpanı (1..1,35).</summary>
        public float DrainMultiplier => 1f + MaxDrainBonus * _fatigue;

        public void Tick(float dt, float staminaNormalized)
        {
            if (float.IsNaN(dt) || dt <= 0f) return;
            var s = float.IsNaN(staminaNormalized) ? 1f : staminaNormalized;
            if (s < LowThreshold)
                _fatigue = Math.Min(1f, _fatigue + dt / BuildSeconds);
            else if (s > RestThreshold)
                _fatigue = Math.Max(0f, _fatigue - dt / DecaySeconds);
        }

        public void Reset() => _fatigue = 0f;
    }

    /// <summary>
    /// Nefes tutma: ADS'de tuşla nişan salınımını azaltır. Süre: 4,5 s * stamina (alt sınır %35). Bitince 2,2 s boyunca
    /// salınım 1,6x (soluk alma). Tutarken stamina ufak harcanır (HoldDrainPerSecond).
    /// </summary>
    public sealed class BreathHoldModel
    {
        public const float MaxHoldSeconds = 4.5f;
        public const float WindedSeconds = 2.2f;
        public const float HoldDrainPerSecond = 4f;
        public const float SwayWhileHolding = 0.15f;
        public const float SwayWhileWinded = 1.6f;

        private float _held;
        private float _limit;
        private float _windedLeft;
        private bool _holding;

        public bool Holding => _holding;
        public bool Winded => _windedLeft > 0f;

        /// <summary>Nişan salınımı çarpanı.</summary>
        public float SwayMultiplier => _holding ? SwayWhileHolding : (_windedLeft > 0f ? 1f + (SwayWhileWinded - 1f) * Math.Min(1f, _windedLeft / WindedSeconds) : 1f);

        /// <summary>Kalan tutma oranı 0..1 (UI).</summary>
        public float Remaining01 => _holding && _limit > 0f ? Math.Max(0f, 1f - _held / _limit) : 1f;

        public static float HoldLimit(float staminaNormalized)
        {
            var s = float.IsNaN(staminaNormalized) ? 1f : Math.Max(0.35f, Math.Min(1f, staminaNormalized));
            return MaxHoldSeconds * s;
        }

        /// <summary>Kare adımı. Dönüş: bu karede harcanan stamina.</summary>
        public float Tick(float dt, bool wantsHold, bool aiming, float staminaNormalized)
        {
            if (float.IsNaN(dt) || dt <= 0f) return 0f;
            var spent = 0f;

            if (wantsHold && aiming && _windedLeft <= 0f)
            {
                if (!_holding)
                {
                    _holding = true;
                    _held = 0f;
                    _limit = HoldLimit(staminaNormalized);
                }

                _held += dt;
                spent = HoldDrainPerSecond * dt;
                if (_held >= _limit)
                {
                    _holding = false;
                    _windedLeft = WindedSeconds;
                }
            }
            else
            {
                if (_holding)
                {
                    _holding = false;
                    // Erken bırakma kısa soluklanma: tutulan orana göre.
                    _windedLeft = WindedSeconds * (_limit > 0f ? Math.Min(1f, _held / _limit) : 0f) * 0.6f;
                }

                _windedLeft = Math.Max(0f, _windedLeft - dt);
            }

            return spent;
        }
    }
}
