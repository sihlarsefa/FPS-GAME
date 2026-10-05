using System;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Can hesabı. ApplyDamage zırh SONRASI hasarı alır (zırhı CombatService uygular).
    /// PlayerDamagedEvent (tam bilgiyle) ve ölümde PlayerDiedEvent(victim, attacker, weaponId, headshot) yayınlar.
    /// </summary>
    public sealed class HealthService : IDamageable, IHealable, IHealthReadModel
    {
        /// <summary>Bu değerin altındaki kalan can ölüm sayılır (kayan nokta artıkları "0 canla yaşayan" bırakmasın).</summary>
        private const float DeathEpsilon = 0.001f;

        private readonly IEventBus _eventBus;
        private bool _dead;

        public PlayerId OwnerId { get; }
        public HealthState State { get; private set; }
        public bool IsAlive => !_dead && State.IsAlive;
        public float Current => State.Current;
        public float Max => State.Max;

        /// <summary>Son ölümü getiren hasar (ölü değilse anlamsız).</summary>
        public DamageInfo LastLethalDamage { get; private set; }

        /// <summary>Hasar uygulandıktan sonra (event'lerden sonra) tetiklenir.</summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>Can sıfıra indiğinde bir kez tetiklenir.</summary>
        public event Action<DamageInfo> Died;

        /// <summary>Gerçekten can eklendiğinde (eklenen miktar) tetiklenir.</summary>
        public event Action<float> Healed;

        public HealthService(PlayerId ownerId, float maxHealth, IEventBus eventBus)
        {
            OwnerId = ownerId;
            _eventBus = eventBus;
            if (float.IsNaN(maxHealth) || maxHealth <= 0f)
                maxHealth = 100f;
            State = new HealthState(maxHealth, maxHealth);
        }

        public void ApplyDamage(DamageInfo damage)
        {
            if (!IsAlive)
                return;

            var amount = damage.Amount;
            if (float.IsNaN(amount) || amount <= 0f)
                return;

            var before = State.Current;
            var after = before - amount;
            if (after < DeathEpsilon)
                after = 0f;

            var applied = before - after;
            State = State.WithCurrent(after);

            var died = after <= 0f;
            if (died)
                _dead = true;

            var appliedInfo = damage.WithAmount(applied);
            if (died)
                LastLethalDamage = appliedInfo;

            _eventBus?.Publish(new PlayerDamagedEvent(OwnerId, damage.AttackerId, applied, after, damage.BodyPart,
                damage.SourceWeaponId, damage.SourcePosition, damage.HasSourcePosition));

            if (died)
                _eventBus?.Publish(new PlayerDiedEvent(OwnerId, damage.AttackerId, damage.SourceWeaponId, damage.IsHeadshot));

            Damaged?.Invoke(appliedInfo);

            if (died)
                Died?.Invoke(appliedInfo);
        }

        public void Heal(float amount)
        {
            HealCapped(amount, State.Max);
        }

        /// <summary>Canı en fazla 'cap' değerine kadar iyileştirir (ör. bandaj 75'e kadar).</summary>
        public void HealCapped(float amount, float cap)
        {
            if (!IsAlive || float.IsNaN(amount) || amount <= 0f || float.IsNaN(cap))
                return;

            var max = State.Max;
            if (cap > max)
                cap = max;

            var current = State.Current;
            if (current >= cap)
                return;

            var target = current + amount;
            if (target > cap)
                target = cap;

            State = State.WithCurrent(target);
            Healed?.Invoke(target - current);
        }

        /// <summary>Canı tam doldurur ve ölü durumdan çıkarır (antrenman hedefleri için).</summary>
        public void ResetToFull()
        {
            _dead = false;
            State = new HealthState(State.Max, State.Max);
        }
    }
}
