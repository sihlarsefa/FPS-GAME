using UnityEngine;

namespace Project.Presentation.UI
{
    /// <summary>
    /// Can/zırh çubuğu için saf model (EditMode testli): ana değer anında düşer; "hayalet" şerit (ghost) bekleme süresinden
    /// sonra sabit hızla ana değere iner — hasarın ne kadar olduğunu gösterir (Warzone/Apex tarzı). İyileşmede hayalet anında yetişir.
    /// Ayrıca düşük can nabzı ve segment doluluğu.
    /// </summary>
    public struct VitalBarModel
    {
        public const float GhostHoldSeconds = 0.45f;
        public const float GhostDrainPerSecond = 0.6f;
        public const float LowThreshold = 0.25f;

        public float Value;
        public float Ghost;
        private float _hold;
        private bool _init;

        public void Tick(float target01, float dt)
        {
            target01 = float.IsNaN(target01) ? 0f : Mathf.Clamp01(target01);
            if (!_init)
            {
                Value = Ghost = target01;
                _init = true;
                return;
            }

            if (target01 < Value - 0.0001f)
                _hold = GhostHoldSeconds;
            Value = target01;
            if (Ghost <= Value)
            {
                Ghost = Value;
                return;
            }

            if (_hold > 0f)
            {
                _hold -= dt;
                return;
            }

            Ghost = Mathf.Max(Value, Ghost - GhostDrainPerSecond * dt);
        }

        public bool IsLow => Value <= LowThreshold && Value > 0f;

        /// <summary>Düşük canda 0..1 nabız (2 Hz); aksi halde 0.</summary>
        public float LowPulse(float time) => IsLow ? 0.5f + 0.5f * Mathf.Sin(time * 4f * Mathf.PI) : 0f;

        /// <summary>Çubuğu <paramref name="segments"/> parçaya böler; dolu segment sayısı (kısmi segment yukarı yuvarlanır).</summary>
        public static int FilledSegments(float value01, int segments)
        {
            if (segments <= 0)
                return 0;
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(value01) * segments - 0.0001f), 0, segments);
        }

        /// <summary>Can rengi: yüksek beyaz, orta kehribar, düşük imza kırmızısı.</summary>
        public static Color HealthColor(float value01)
        {
            if (value01 > 0.5f)
                return Color.white;
            if (value01 > LowThreshold)
                return UiTheme.Amber;
            return HudRules.SignatureRed;
        }
    }
}
