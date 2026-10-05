using System;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Loot;
using Project.Infrastructure.Vehicles;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// F etkileşimi ve istem metni. Öncelik: intikal aracından iniş → sürülen araçtan iniş → bakılan/yakındaki eşyayı alma
    /// → yakındaki boş araca binme. Hedef taraması 10 Hz; istem metni yalnızca hedef değişince yeniden kurulur.
    /// </summary>
    public sealed class PlayerInteraction
    {
        public const float LootLookDistance = 3f;
        public const float LootNearRadius = 1.4f;
        public const float VehicleEnterRadius = 4.5f;

        private const float ScanInterval = 0.1f;
        private const string KeyPrefix = "[F] ";
        private const string DisembarkPrompt = "[F] Araçtan in";
        private const string ExitVehiclePrompt = "[F] Araçtan in";
        private const string EnterVehiclePrompt = "[F] Kirpi'yi kullan";

        private static readonly Func<LootPickupComponent, bool> PickableFilter = IsPickable;

        private readonly PlayerController _owner;

        private float _nextScan;
        private LootPickupComponent _lootTarget;
        private DrivableVehicle _vehicleTarget;

        private LootPickupComponent _promptLoot;
        private string _promptLootText;
        private string _prompt = string.Empty;

        public PlayerInteraction(PlayerController owner)
        {
            _owner = owner;
        }

        /// <summary>Gösterilecek istem ("[F] ..."), yoksa boş.</summary>
        public string Prompt => _prompt;

        public LootPickupComponent LootTarget => _lootTarget;
        public DrivableVehicle VehicleTarget => _vehicleTarget;

        public void Tick(CombatInputState input, float dt)
        {
            if (_owner.IsInTransport)
            {
                _lootTarget = null;
                _vehicleTarget = null;
                _prompt = _owner.CanDisembark ? DisembarkPrompt : string.Empty;
                if (input.Interact && _owner.CanDisembark)
                    _owner.Disembark();
                return;
            }

            if (_owner.IsDriving)
            {
                _lootTarget = null;
                _vehicleTarget = null;
                _prompt = ExitVehiclePrompt;
                if (input.Interact)
                    _owner.ExitVehicle();
                return;
            }

            if (input.Interact || Time.time >= _nextScan)
            {
                _nextScan = Time.time + ScanInterval;
                Scan();
            }

            if (input.Interact)
                Interact();

            RefreshPrompt();
        }

        public void Clear()
        {
            _lootTarget = null;
            _vehicleTarget = null;
            _promptLoot = null;
            _promptLootText = null;
            _prompt = string.Empty;
        }

        private void Scan()
        {
            _lootTarget = null;
            _vehicleTarget = null;

            var combatant = _owner.Combatant;
            if (combatant == null || !combatant.IsAlive)
                return;

            try
            {
                var origin = _owner.AimOrigin;
                var forward = _owner.AimForward;
                var loot = LootRegistry.FindLookTarget(origin, forward, LootLookDistance);
                if (loot == null)
                    loot = LootRegistry.FindNearest(_owner.transform.position, LootNearRadius, PickableFilter);

                if (loot != null && loot.IsAvailable)
                {
                    _lootTarget = loot;
                    return;
                }

                var vehicle = VehicleRegistry.FindNearest(_owner.transform.position, VehicleEnterRadius);
                if (vehicle != null && !vehicle.HasDriver && vehicle.Health > 0f)
                    _vehicleTarget = vehicle;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static bool IsPickable(LootPickupComponent pickup) => pickup != null && pickup.IsAvailable;

        private void Interact()
        {
            var combatant = _owner.Combatant;
            if (combatant == null || !combatant.IsAlive)
                return;

            if (_lootTarget != null && _lootTarget.IsAvailable)
            {
                PickUp(_lootTarget);
                return;
            }

            if (_vehicleTarget != null)
            {
                if (!_owner.TryEnterVehicle(_vehicleTarget))
                    _owner.Notify("Araca binilemiyor", 1.5f);

                _vehicleTarget = null;
            }
        }

        private void PickUp(LootPickupComponent pickup)
        {
            if (!Infrastructure.GameContext.HasAuthority)
                return;

            PickupResult result;
            try
            {
                result = pickup.PickupBy(_owner.Combatant);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            if (result.Accepted)
            {
                PlayerController.PlaySound2D(SoundId.Pickup, 0.6f);
                if (!result.FullyTaken)
                    _owner.Notify("Envanterde yer kalmadı (bir kısmı alındı)", 1.8f);
            }
            else
            {
                _owner.Notify("Envanterde yer yok", 1.5f);
            }

            // Hedef değişmiş olabilir: hemen yeniden tara.
            _nextScan = 0f;
            Scan();
        }

        private void RefreshPrompt()
        {
            if (_lootTarget != null)
            {
                var text = _lootTarget.PromptText;
                if (!ReferenceEquals(_lootTarget, _promptLoot) || !ReferenceEquals(text, _promptLootText))
                {
                    _promptLoot = _lootTarget;
                    _promptLootText = text;
                    _prompt = string.IsNullOrEmpty(text) ? KeyPrefix + "Al" : KeyPrefix + text;
                }

                return;
            }

            _promptLoot = null;
            _promptLootText = null;
            _prompt = _vehicleTarget != null ? EnterVehiclePrompt : string.Empty;
        }
    }
}
