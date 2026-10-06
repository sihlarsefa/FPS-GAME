using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using Project.Infrastructure.Player;
using UnityEngine;

namespace Project.Infrastructure.Characters
{
    /// <summary>
    /// Maç içi savaş yıpranması sürücüsü: askerin hasar/yere düşme olaylarını dinler, en çok 10 sn'de bir
    /// SoldierModel.SetWear + SetWeary günceller; yorgun solukları PlayerBreathing'e iletir. Olay güdümlü, kare başı iş yok.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WearDriver : MonoBehaviour
    {
        public const float UpdateInterval = 10f;
        public const float WearyCap = 0.6f;
        public const float LongMatchSeconds = 720f;

        private Combatant _combatant;
        private SoldierModel _model;
        private CharacterControllerMotor _motor;
        private bool _own;
        private float _startTime;
        private float _damageTotal;
        private int _downedCount;
        private float _lastApply = -999f;
        private float _lastExhaust = -999f;
        private bool _dirty = true;

        /// <summary>Askere ekler (null-güvenli). motor yalnız yerel oyuncu için; model yoksa hiçbir şey yapmaz.</summary>
        public static WearDriver Attach(GameObject host, Combatant combatant, SoldierModel model, CharacterControllerMotor motor = null, bool own = false)
        {
            if (host == null || combatant == null || model == null)
                return null;
            var d = host.GetComponent<WearDriver>();
            if (d == null)
                d = host.AddComponent<WearDriver>();
            d.Bind(combatant, model, motor, own);
            return d;
        }

        /// <summary>Saf: yorgunluk 0..WearyCap. Düşük can (%40 altı), uzun maç, yakın zamanda stamina tükenmesi.</summary>
        public static float ComputeWeary(float healthNormalized, float matchSeconds, bool recentExhaustion)
        {
            var low = Mathf.Clamp01((0.4f - healthNormalized) / 0.4f) * 0.4f;
            var time = Mathf.Clamp01((matchSeconds - LongMatchSeconds) / 600f) * 0.3f;
            var ex = recentExhaustion ? 0.2f : 0f;
            return Mathf.Min(WearyCap, low + time + ex);
        }

        public void Bind(Combatant combatant, SoldierModel model, CharacterControllerMotor motor, bool own)
        {
            Unbind();
            _combatant = combatant;
            _model = model;
            _motor = motor;
            _own = own;
            _startTime = Time.time;
            _damageTotal = 0f;
            _downedCount = 0;
            _dirty = true;
            _combatant.Damaged += OnDamaged;
            _combatant.BecameDowned += OnDowned;
            _model.WearyBreathIn += OnBreath;
            if (_motor != null)
                _motor.StaminaExhausted += OnExhausted;
        }

        private void Unbind()
        {
            if (_combatant != null)
            {
                _combatant.Damaged -= OnDamaged;
                _combatant.BecameDowned -= OnDowned;
            }

            if (_model != null)
                _model.WearyBreathIn -= OnBreath;
            if (_motor != null)
                _motor.StaminaExhausted -= OnExhausted;
        }

        private void OnDestroy() => Unbind();

        private void OnDamaged(Combatant c, Project.Core.Domain.DamageInfo info)
        {
            _damageTotal += Mathf.Max(0f, info.Amount);
            _dirty = true;
        }

        private void OnDowned(Combatant c)
        {
            _downedCount++;
            _dirty = true;
        }

        private void OnExhausted()
        {
            _lastExhaust = Time.time;
            _dirty = true;
        }

        private void OnBreath(float effective)
        {
            if (_model != null)
                PlayerBreathing.PlayWearyBreath(_model.transform.position + Vector3.up * 1.6f, effective, _own);
        }

        private void Update()
        {
            var now = Time.time;
            if (now - _lastApply < UpdateInterval)
                return;
            // uzun maçta olay olmasa da süre katkısı için 10 sn'de bir yenile
            _lastApply = now;
            _dirty = false;
            Apply(now);
        }

        private void Apply(float now)
        {
            if (_model == null || _combatant == null || !_combatant.IsAlive)
                return;
            var matchSeconds = now - _startTime;
            var max = Mathf.Max(1f, _combatant.State.Max);
            _model.SetWear(WearRules.Compute(_damageTotal / max, matchSeconds, _downedCount));
            _model.SetWeary(ComputeWeary(_combatant.State.Normalized, matchSeconds, now - _lastExhaust < 20f));
        }
    }
}
