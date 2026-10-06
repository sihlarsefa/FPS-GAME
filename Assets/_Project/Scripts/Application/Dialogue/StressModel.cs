using System;

namespace Project.Application.Dialogue
{
    /// <summary>
    /// Asker başına çatışma yoğunluğu (ısı) + sağlıktan stres hâli (sakin/çatışma/panik). Histerezis ve alt geçişte
    /// bekleme süresi vardır (hâl titremez). Saf mantık.
    /// </summary>
    public sealed class StressTracker
    {
        public const float PanicEnter = 0.7f;
        public const float PanicExit = 0.5f;
        public const float CombatEnter = 0.28f;
        public const float CombatExit = 0.15f;
        public const float HeatDecayPerSecond = 0.12f;
        public const float MinDownshiftSeconds = 3f;

        private float _heat;
        private float _sinceChange = 99f;

        public DialogueStress Current { get; private set; }
        public float Heat => _heat;
        public float Score { get; private set; }

        /// <summary>Çatışma olayı ısıyı artırır: alınan hasar ~0.35, yakın isabet ~0.15, kendi atışı ~0.04, patlama ~0.5.</summary>
        public void AddHeat(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount))
                return;
            _heat = Math.Min(1.5f, _heat + amount);
        }

        public void Tick(float dt, float health01)
        {
            if (dt < 0f)
                dt = 0f;
            _heat = Math.Max(0f, _heat - HeatDecayPerSecond * dt);
            _sinceChange += dt;
            Score = ComputeScore(_heat, health01);

            var target = Classify(Score, Current);
            if (target > Current)
            {
                Current = target;
                _sinceChange = 0f;
            }
            else if (target < Current && _sinceChange >= MinDownshiftSeconds)
            {
                Current = target;
                _sinceChange = 0f;
            }
        }

        public static float ComputeScore(float heat, float health01)
        {
            var h = Math.Max(0f, Math.Min(1f, health01));
            var hurt = h < 0.5f ? (0.5f - h) : 0f;
            return Math.Max(0f, Math.Min(1.2f, Math.Min(1f, heat) * 0.75f + hurt * 1.0f + (heat > 0.3f ? hurt * 0.4f : 0f)));
        }

        public static DialogueStress Classify(float score, DialogueStress previous)
        {
            if (previous == DialogueStress.Panic)
            {
                if (score >= PanicExit)
                    return DialogueStress.Panic;
                return score >= CombatExit ? DialogueStress.Combat : DialogueStress.Calm;
            }

            if (score >= PanicEnter)
                return DialogueStress.Panic;
            if (previous == DialogueStress.Combat)
                return score >= CombatExit ? DialogueStress.Combat : DialogueStress.Calm;
            return score >= CombatEnter ? DialogueStress.Combat : DialogueStress.Calm;
        }

        public void Reset()
        {
            _heat = 0f;
            Score = 0f;
            Current = DialogueStress.Calm;
            _sinceChange = 99f;
        }
    }
}
