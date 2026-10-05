using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// ESKİ prototip can bileşeni. Yerini <c>Project.Infrastructure.Combat.Combatant</c> aldı; yalnızca eski
    /// sahneler/kurulumlar derlenmeye devam etsin diye tutulur. Çalışır durumdadır (HealthService sarmalayıcısı).
    /// </summary>
    [Obsolete("Project.Infrastructure.Combat.Combatant kullanın.")]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class PlayerHealthComponent : MonoBehaviour, IDamageable, IHealable, IHealthReadModel
    {
        [SerializeField] private float maxHealth = 100f;

        private HealthService _healthService;
        private IDamageableRegistry _registry;
        private bool _registered;

        public PlayerId OwnerId { get; private set; }
        public HealthState State => _healthService?.State ?? new HealthState(0f, Mathf.Max(1f, maxHealth));
        public bool IsAlive => _healthService?.IsAlive ?? false;

        public void Initialize(PlayerId ownerId, IEventBus eventBus, IDamageableRegistry registry = null)
        {
            Unregister();

            OwnerId = ownerId;
            _healthService = new HealthService(ownerId, Mathf.Max(1f, maxHealth), eventBus);
            _registry = registry;
            if (_registry != null)
            {
                _registry.Register(this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            Unregister();
        }

        public void ApplyDamage(DamageInfo damage) => _healthService?.ApplyDamage(damage);
        public void Heal(float amount) => _healthService?.Heal(amount);

        private void Unregister()
        {
            if (_registered && _registry != null)
                _registry.Unregister(this);

            _registered = false;
        }
    }
}
