using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Core.Interfaces;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// ESKİ prototip silah sunucusu. Yerini <see cref="PlayerController"/> + <see cref="PlayerWeaponHandler"/> aldı.
    /// Eski kurulum kodu derlensin diye imzalar korunur; yalnızca silah durumunu tutar ve zamanlayıcısını işletir.
    /// </summary>
    [Obsolete("PlayerController.Create(PlayerSpawnArgs) kullanın.")]
    [AddComponentMenu("")]
    public sealed class WeaponPresenter : MonoBehaviour
    {
        // Eski kurulum kodu bu alanları yansıma ile doldurur; tipleri gevşek tutuldu.
        [SerializeField] private Transform fireOrigin;
        [SerializeField] private Transform fireDirection;
        [SerializeField] private Component inputReader;
        [SerializeField] private Component viewModel;
        [SerializeField] private ScriptableObject weaponConfig;

        private WeaponRuntimeService _weapon;
        private InventoryService _inventory;

        public IWeaponRuntime Weapon => _weapon;
        public IInventory Inventory => _inventory;

        public void Initialize(
            PlayerId ownerId,
            IEventBus eventBus,
            IDamageableRegistry registry,
            IHitScanner hitScanner,
            ILootProximityQuery lootQuery,
            WeaponDefinitionData weaponDefinition)
        {
            _inventory = new InventoryService(eventBus, ownerId);
            _weapon = weaponDefinition != null ? new WeaponRuntimeService(weaponDefinition, eventBus) { OwnerId = ownerId } : null;
        }

        private void Update()
        {
            _weapon?.Tick(Time.deltaTime);
        }
    }
}
