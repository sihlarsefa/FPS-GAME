using UnityEngine;

namespace Project.Infrastructure.Characters.Animation
{
    /// <summary>
    /// İrkilme (yakın mermi), sendeleme (hasar) ve çömelme geçiş yumuşatma. Saf matematik; Step(dt) ile ilerler.
    /// </summary>
    public sealed class WearyReactionState
    {
        public const float FlinchDecay = 7f;
        public const float StaggerDecay = 3.5f;
        public const float CrouchSmoothTime = 0.18f;

        private float _flinch, _flinchDir, _stagger, _staggerDir;
        private float _crouch, _crouchVel, _crouchTarget;

        public float FlinchAmount => _flinch;
        public float StaggerAmount => _stagger;
        public float Crouch => _crouch;

        /// <summary>Yakın mermi: dir -1 sol / +1 sağdan, yakınlık 0..1.</summary>
        public void Flinch(float dir, float closeness)
        {
            _flinch = Mathf.Max(_flinch, Mathf.Clamp01(closeness));
            _flinchDir = dir < 0f ? -1f : 1f;
        }

        /// <summary>Hasar: damage 0..100 -> şiddet 0..1.</summary>
        public void Stagger(float dir, float damage)
        {
            _stagger = Mathf.Max(_stagger, Mathf.Clamp01(damage / 60f));
            _staggerDir = dir < 0f ? -1f : 1f;
        }

        public void SetCrouchTarget(float t) => _crouchTarget = Mathf.Clamp01(t);

        public void Step(float dt)
        {
            if (dt <= 0f) return;
            if (dt > 0.1f) dt = 0.1f;
            _flinch = Mathf.Max(0f, _flinch - FlinchDecay * dt * _flinch - 0.05f * dt);
            _stagger = Mathf.Max(0f, _stagger - StaggerDecay * dt * _stagger - 0.05f * dt);
            _crouch = Mathf.SmoothDamp(_crouch, _crouchTarget, ref _crouchVel, CrouchSmoothTime, Mathf.Infinity, dt);
        }

        /// <summary>İrkilme + sendeleme birleşik poz ofseti.</summary>
        public WearyPose Evaluate(float time)
        {
            var p = new WearyPose();
            p.SpinePitch = 10f * _flinch + 14f * _stagger;
            p.HeadPitch = 12f * _flinch;
            p.ShoulderDropL = p.ShoulderDropR = 0.04f * _flinch;
            p.SpineRoll = _flinchDir * 6f * _flinch + _staggerDir * 12f * _stagger * Mathf.Cos(time * 18f);
            p.HipSway = _staggerDir * 0.05f * _stagger * Mathf.Sin(time * 14f);
            return p;
        }
    }
}
