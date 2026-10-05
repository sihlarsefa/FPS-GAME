using System;
using Project.Application.Catalogs;
using Project.Core.Domain;
using Project.Core.Events;
using Project.Core.Interfaces;

namespace Project.Application.Services
{
    /// <summary>
    /// Süreli eşya kullanımı (bandaj 4 sn, ilk yardım 6 sn, medkit 8 sn, enerji içeceği 4 sn, ağrı kesici 6 sn).
    /// Bitince eşyayı tüketir, canı (HealCap'e kadar) yeniler ya da boost ekler. ItemUsedEvent yayınlar.
    /// Kullanım sırasında hareket hızı yarıya iner. Cancel() ile (ateş, silah değişimi, zıplama) iptal edilir.
    /// </summary>
    public sealed class ItemUseService
    {
        /// <summary>Kullanım sırasında hareket hızı çarpanı.</summary>
        public const float UsingSpeedMultiplier = 0.5f;

        private const float HealthEpsilon = 0.01f;

        private readonly PlayerId _ownerId;
        private readonly InventoryService _inventory;
        private readonly HealthService _health;
        private readonly BoostService _boost;
        private readonly IEventBus _eventBus;

        private ItemDefinition _current;
        private float _elapsed;
        private float _duration;

        public ItemUseService(PlayerId ownerId, InventoryService inventory, HealthService health, BoostService boost, IEventBus eventBus)
        {
            _ownerId = ownerId;
            _inventory = inventory;
            _health = health;
            _boost = boost;
            _eventBus = eventBus;
        }

        public PlayerId OwnerId => _ownerId;
        public bool IsUsing => _current != null;
        public string CurrentItemId => _current?.Id;

        /// <summary>Kullanılan eşyanın Türkçe adı (kullanım yoksa boş).</summary>
        public string CurrentItemName => _current != null ? _current.DisplayName : string.Empty;

        /// <summary>Kullanımın toplam süresi (sn).</summary>
        public float Duration => _current != null ? _duration : 0f;

        public float Progress
        {
            get
            {
                if (_current == null)
                    return 0f;

                if (_duration <= 0f)
                    return 1f;

                var progress = _elapsed / _duration;
                return progress < 0f ? 0f : progress > 1f ? 1f : progress;
            }
        }

        public float RemainingSeconds
        {
            get
            {
                if (_current == null)
                    return 0f;

                var remaining = _duration - _elapsed;
                return remaining > 0f ? remaining : 0f;
            }
        }

        public float MovementSpeedMultiplier => _current != null ? UsingSpeedMultiplier : 1f;

        /// <summary>Kullanım başladığında (eşya id).</summary>
        public event Action<string> Started;

        /// <summary>Kullanım tamamlandığında (eşya id).</summary>
        public event Action<string> Completed;

        /// <summary>Kullanım iptal edildiğinde (eşya id).</summary>
        public event Action<string> Cancelled;

        public bool CanUse(string itemId)
        {
            if (!ItemCatalog.TryGet(itemId, out var definition))
                return false;

            return CanUse(definition);
        }

        public bool TryBegin(string itemId)
        {
            if (!ItemCatalog.TryGet(itemId, out var definition))
                return false;

            if (_current != null && string.Equals(_current.Id, definition.Id, StringComparison.Ordinal))
                return false;

            if (!CanUse(definition))
                return false;

            if (_current != null)
                Cancel();

            _current = definition;
            _elapsed = 0f;
            _duration = definition.UseSeconds > 0f ? definition.UseSeconds : 0f;

            _eventBus?.Publish(new ItemUsedEvent(_ownerId, definition.Id, true, false));
            Started?.Invoke(definition.Id);

            if (_duration <= 0f)
                Complete();

            return true;
        }

        public bool TryBeginBestHeal()
        {
            if (_inventory == null || _health == null || !_health.IsAlive)
                return false;

            var itemId = _inventory.BestHealItem(_health.Current);
            return itemId != null && TryBegin(itemId);
        }

        public bool TryBeginBestBoost()
        {
            if (_inventory == null || _boost == null)
                return false;

            if (_health != null && !_health.IsAlive)
                return false;

            var itemId = _inventory.BestBoostItem(_boost.Value);
            return itemId != null && TryBegin(itemId);
        }

        public void Cancel()
        {
            if (_current == null)
                return;

            var itemId = _current.Id;
            _current = null;
            _elapsed = 0f;
            _duration = 0f;

            _eventBus?.Publish(new ItemUsedEvent(_ownerId, itemId, false, false));
            Cancelled?.Invoke(itemId);
        }

        public void Tick(float deltaTime)
        {
            if (_current == null)
                return;

            // Ölüm, eşyanın elden çıkması ya da eşyanın artık işe yaramaması (ör. boost iyileştirmesiyle can sınıra
            // ulaştı) kullanımı iptal eder; eşya boşa harcanmaz.
            if (!CanUse(_current))
            {
                Cancel();
                return;
            }

            if (deltaTime > 0f)
                _elapsed += deltaTime;

            if (_elapsed >= _duration)
                Complete();
        }

        private bool CanUse(ItemDefinition definition)
        {
            if (definition == null || _inventory == null)
                return false;

            if (_inventory.GetCount(definition.Id) <= 0)
                return false;

            if (_health != null && !_health.IsAlive)
                return false;

            switch (definition.Category)
            {
                case ItemCategory.Medical:
                    return _health != null && _health.Current < Math.Min(definition.HealCap, _health.Max) - HealthEpsilon;
                case ItemCategory.Boost:
                    return _boost != null && _boost.Value < BoostService.Max - 0.5f;
                default:
                    return false;
            }
        }

        private void Complete()
        {
            var definition = _current;
            _current = null;
            _elapsed = 0f;
            _duration = 0f;

            if (definition == null)
                return;

            if (_inventory == null || !_inventory.Consume(definition.Id, 1))
            {
                _eventBus?.Publish(new ItemUsedEvent(_ownerId, definition.Id, false, false));
                Cancelled?.Invoke(definition.Id);
                return;
            }

            if (definition.Category == ItemCategory.Medical)
            {
                if (_health != null && definition.HealAmount > 0f)
                    _health.HealCapped(definition.HealAmount, definition.HealCap);
            }
            else if (definition.Category == ItemCategory.Boost)
            {
                if (_boost != null && definition.BoostAmount > 0f)
                    _boost.Add(definition.BoostAmount);
            }

            _eventBus?.Publish(new ItemUsedEvent(_ownerId, definition.Id, false, true));
            Completed?.Invoke(definition.Id);
        }
    }
}
