using Project.Infrastructure.Combat;
using Project.Infrastructure.Player;
using UnityEngine;

namespace Project.Infrastructure.Audio
{
    /// <summary>
    /// Yerel oyuncunun nefesi: CharacterControllerMotor.BreathingIntensity (0..1) yükseldikçe daha sık/yüksek soluk;
    /// stamina tükenince (StaminaExhausted) bir hırıltı, toparlanınca (StaminaRecovered) rahatlama nefesi.
    /// Saf aralık/ses kuralları statik (test edilebilir). Kare başına bellek ayırmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerBreathing : MonoBehaviour
    {
        public const float MinIntensity = 0.2f;

        private CharacterControllerMotor _motor;
        private Combatant _combatant;
        private float _nextBreath;
        private bool _gasp;
        private bool _relief;

        /// <summary>Nefes aralığı (sn): yoğunluk 0.2 → ~3.2, 1 → ~0.85. Eşik altında sonsuz (çalmaz).</summary>
        public static float Interval(float intensity)
        {
            if (intensity < MinIntensity)
                return float.PositiveInfinity;
            return Mathf.Lerp(3.2f, 0.85f, Mathf.Clamp01((intensity - MinIntensity) / (1f - MinIntensity)));
        }

        public static float Volume(float intensity) => Mathf.Lerp(0.12f, 0.5f, Mathf.Clamp01(intensity));

        /// <summary>Yorgun soluk (SoldierModel.WearyBreathIn). Kendi asker tam ses; yakındaki askerler kısık 3D, uzaktakiler çalmaz.</summary>
        public static void PlayWearyBreath(Vector3 position, float effective, bool own)
        {
            if (!GameAudio.Enabled || effective < 0.05f)
                return;
            var e = Mathf.Clamp01(effective);
            if (own)
            {
                GameAudio.Play(SoundId.BreathHeavy, position, Mathf.Lerp(0.15f, 0.4f, e), Random.Range(0.92f, 1.02f), 6f);
                return;
            }

            var cam = Camera.main;
            if (cam != null && (cam.transform.position - position).sqrMagnitude > 14f * 14f)
                return;
            GameAudio.Play(SoundId.BreathHeavy, position, Mathf.Lerp(0.04f, 0.12f, e), Random.Range(0.9f, 1f), 12f);
        }

        public void Bind(CharacterControllerMotor motor, Combatant combatant)
        {
            Unsubscribe();
            _motor = motor;
            _combatant = combatant;
            if (_motor != null)
            {
                _motor.StaminaExhausted += OnExhausted;
                _motor.StaminaRecovered += OnRecovered;
            }
        }

        private void OnExhausted() => _gasp = true;
        private void OnRecovered() => _relief = true;

        private void Unsubscribe()
        {
            if (_motor == null)
                return;
            _motor.StaminaExhausted -= OnExhausted;
            _motor.StaminaRecovered -= OnRecovered;
        }

        private void OnDestroy() => Unsubscribe();

        private void Update()
        {
            if (_motor == null || !GameAudio.Enabled || (_combatant != null && !_combatant.IsAlive))
            {
                _gasp = _relief = false;
                return;
            }

            var pos = transform.position + Vector3.up * 1.6f;
            if (_gasp)
            {
                _gasp = false;
                GameAudio.Play(SoundId.BreathHeavy, pos, 0.6f, 0.85f, 6f);
                _nextBreath = Time.time + 1.1f;
                return;
            }

            if (_relief)
            {
                _relief = false;
                GameAudio.Play(SoundId.BreathHeavy, pos, 0.3f, 1.05f, 6f);
                _nextBreath = Time.time + 2f;
                return;
            }

            var intensity = _motor.BreathingIntensity;
            var interval = Interval(intensity);
            if (float.IsPositiveInfinity(interval) || Time.time < _nextBreath)
                return;

            GameAudio.Play(SoundId.BreathHeavy, pos, Volume(intensity) * Random.Range(0.85f, 1f),
                Mathf.Lerp(0.95f, 1.1f, intensity) * Random.Range(0.95f, 1.05f), 6f);
            _nextBreath = Time.time + interval;
        }
    }
}
