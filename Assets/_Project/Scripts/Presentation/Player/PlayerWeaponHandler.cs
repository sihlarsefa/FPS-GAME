using System;
using Project.Application.Catalogs;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Combat;
using UnityEngine;

namespace Project.Presentation.Player
{
    /// <summary>
    /// Yerel oyuncunun silah ve el eylemleri: yuva seçimi (1-4 / tekerlek / X kılıf), nişan alma ve dürbün yakınlaştırma,
    /// tetik (TryTrigger → BallisticsSystem.FireWeapon, kamera sekmesi + silah görünümü), şarjör, ateş modu,
    /// silahsızken yumruk, el/sis bombası (G/T) ve iyileşme/takviye (H/J). Eşya kullanımı ateş/silah değişimi/zıplama
    /// ile iptal olur. Kare başına bellek ayırmaz.
    /// </summary>
    public sealed class PlayerWeaponHandler : IDisposable
    {
        public const float MeleeCooldownSeconds = 0.6f;
        public const float MeleeRange = 2.2f;
        public const float ThrowCooldownSeconds = 1.1f;
        public const float ThrowSpeed = 17f;
        public const float ThrowUpBoost = 3.2f;

        private const float AdsSeconds = 0.18f;
        private const float ScopeExtraSeconds = 0.08f;
        private const float ScopedBlendThreshold = 0.95f;
        private const float FallbackMuzzleDistance = 0.6f;

        private readonly PlayerController _owner;

        private WeaponRuntimeService _equipped;
        private bool _equipSynced;
        private int _lastWeaponSlot;
        private bool _reloadShown;
        private int _ammoAtReloadStart;
        private float _meleeReadyAt;
        private float _throwReadyAt;
        private float _aimBlend;
        private bool _viewModelVisible = true;
        private bool _appliedHidden;
        private bool _hiddenApplied;
        private float _appliedZoom = -1f;
        private ItemUseService _hookedItemUse;

        public PlayerWeaponHandler(PlayerController owner)
        {
            _owner = owner;
            HookItemUse(owner != null && owner.Combatant != null ? owner.Combatant.ItemUse : null);
        }

        public bool IsAiming { get; private set; }
        public bool IsScoped { get; private set; }

        /// <summary>Anlık kamera yakınlaştırması (1 = yok).</summary>
        public float CurrentZoom { get; private set; } = 1f;

        /// <summary>Anlık sapma açısı (derece) — nişangâh boyutu için.</summary>
        public float SpreadAngle { get; private set; }

        /// <summary>0..1 nişan alma geçişi.</summary>
        public float AimBlend => _aimBlend;

        /// <summary>El bombası bekleme süresi (0 = hazır).</summary>
        public float ThrowCooldownRemaining => Mathf.Max(0f, _throwReadyAt - Time.time);

        /// <summary>Nişan alırken ya da ateş ederken koşu bastırılır.</summary>
        public bool WantsSprintSuppressed(CombatInputState input)
        {
            var weapon = _owner.ActiveWeapon;
            if (weapon == null)
                return false;

            return input.Aim || input.Fire || weapon.IsBurstActive;
        }

        // ================================================================ yaya
        public void Tick(CombatInputState input, LookInputState look, float dt)
        {
            var combatant = _owner.Combatant;
            if (combatant == null)
                return;

            HookItemUse(combatant.ItemUse);
            var inventory = combatant.Inventory;
            var itemUse = combatant.ItemUse;

            if (inventory != null)
                HandleSelection(input, inventory, itemUse);

            var weapon = inventory?.ActiveWeapon;
            SyncEquipped(weapon, inventory);

            weapon?.Tick(dt);

            if (weapon != null)
            {
                if (input.ToggleFireMode)
                    HandleFireModeToggle(weapon);

                if (input.Reload)
                    HandleReloadRequest(weapon, itemUse);
            }

            UpdateAim(input, weapon, itemUse, dt);
            HandleTrigger(input, weapon, itemUse, combatant);

            if (input.ThrowGrenade)
                TryThrow(ThrowableKind.Frag, combatant, inventory, itemUse, weapon);
            else if (input.ThrowSmoke)
                TryThrow(ThrowableKind.Smoke, combatant, inventory, itemUse, weapon);

            if (input.Heal)
                TryHeal(itemUse, inventory, weapon);
            else if (input.Boost)
                TryBoost(itemUse, inventory, weapon);

            SyncReloadAnimation(weapon);
            UpdateSpread(weapon);
            UpdateViewModelMotion(look);
        }

        /// <summary>Araçta/intikalde: ateş yok, nişan kapalı, yalnızca zamanlayıcılar işler.</summary>
        public void TickPassive(float dt)
        {
            var combatant = _owner.Combatant;
            if (combatant != null)
                HookItemUse(combatant.ItemUse);

            var weapon = _owner.ActiveWeapon;
            SyncEquipped(weapon, _owner.Inventory);
            weapon?.Tick(dt);

            IsAiming = false;
            IsScoped = false;
            _aimBlend = 0f;
            ApplyZoom(1f);
            SpreadAngle = 0f;
            _owner.ViewModel?.SetAim(false);
            SyncReloadAnimation(weapon);
            ApplyViewModelVisibility();
        }

        /// <summary>Yayadan araca geçerken: şarjör değiştirme ve eşya kullanımı iptal, nişan kapalı.</summary>
        public void OnLeaveFoot()
        {
            _owner.ActiveWeapon?.CancelReload();
            CancelItemUse(_owner.ItemUse);
            IsAiming = false;
            IsScoped = false;
            _aimBlend = 0f;
            ApplyZoom(1f);
            _owner.ViewModel?.SetAim(false);
        }

        public void OnDeath()
        {
            OnLeaveFoot();
            SpreadAngle = 0f;
            var viewModel = _owner.ViewModel;
            if (viewModel != null)
            {
                viewModel.StopReload();
                viewModel.StopUse();
            }

            _reloadShown = false;
        }

        /// <summary>Yeniden doğma sonrası: silah yeniden kuşanılır, durumlar sıfırlanır.</summary>
        public void ResetState()
        {
            _equipSynced = false;
            _equipped = null;
            _reloadShown = false;
            _aimBlend = 0f;
            IsAiming = false;
            IsScoped = false;
            _meleeReadyAt = 0f;
            _throwReadyAt = 0f;
            ApplyZoom(1f);
            _owner.ActiveWeapon?.ResetState(false);
        }

        public void SetViewModelVisible(bool visible)
        {
            _viewModelVisible = visible;
            ApplyViewModelVisibility();
        }

        // ================================================================ yuva seçimi
        private void HandleSelection(CombatInputState input, InventoryService inventory, ItemUseService itemUse)
        {
            if (input.SelectSlot >= 0)
            {
                var slot = input.SelectSlot >= InventoryService.WeaponSlotCount ? -1 : input.SelectSlot;
                if (slot >= 0 && inventory.GetWeapon(slot) == null)
                {
                    _owner.Notify("Bu yuvada silah yok", 1.2f);
                    return;
                }

                SelectSlot(inventory, itemUse, slot);
                return;
            }

            if (input.CycleWeapon != 0)
            {
                if (!inventory.HasAnyWeapon)
                    return;

                var before = inventory.ActiveSlot;
                CancelItemUse(itemUse);
                inventory.CycleWeapon(input.CycleWeapon);
                if (inventory.ActiveSlot != before && inventory.ActiveSlot >= 0)
                    _lastWeaponSlot = inventory.ActiveSlot;
                return;
            }

            if (input.Holster)
            {
                if (inventory.ActiveSlot >= 0)
                {
                    _lastWeaponSlot = inventory.ActiveSlot;
                    SelectSlot(inventory, itemUse, -1);
                }
                else if (inventory.GetWeapon(_lastWeaponSlot) != null)
                {
                    SelectSlot(inventory, itemUse, _lastWeaponSlot);
                }
                else if (inventory.HasAnyWeapon)
                {
                    CancelItemUse(itemUse);
                    inventory.CycleWeapon(1);
                }
            }
        }

        private void SelectSlot(InventoryService inventory, ItemUseService itemUse, int slot)
        {
            if (slot == inventory.ActiveSlot)
                return;

            CancelItemUse(itemUse);
            if (inventory.SetActiveSlot(slot) && slot >= 0)
                _lastWeaponSlot = slot;
        }

        private void SyncEquipped(WeaponRuntimeService weapon, InventoryService inventory)
        {
            if (_equipSynced && ReferenceEquals(weapon, _equipped))
                return;

            var hadWeapon = _equipSynced;
            _equipSynced = true;
            _equipped = weapon;
            _reloadShown = false;
            _aimBlend = 0f;

            if (inventory != null && inventory.ActiveSlot >= 0)
                _lastWeaponSlot = inventory.ActiveSlot;

            var viewModel = _owner.ViewModel;
            if (viewModel != null)
            {
                viewModel.StopReload();
                viewModel.Equip(weapon?.Definition);
            }

            if (hadWeapon && weapon != null)
                PlayerController.PlaySound2D(SoundId.WeaponEquip, 0.5f);
        }

        // ================================================================ ateş modu / şarjör
        private void HandleFireModeToggle(WeaponRuntimeService weapon)
        {
            var before = weapon.CurrentFireMode;
            weapon.CycleFireMode();
            var after = weapon.CurrentFireMode;
            if (after == before)
            {
                _owner.Notify("Tek ateş modu", 1f);
                return;
            }

            PlayerController.PlaySound2D(SoundId.FireModeSwitch, 0.6f);
            _owner.Notify(FireModeName(after, weapon.Definition), 1.2f);
        }

        private void HandleReloadRequest(WeaponRuntimeService weapon, ItemUseService itemUse)
        {
            if (weapon.IsReloading)
                return;

            if (weapon.CurrentAmmo >= weapon.MagazineSize)
                return;

            if (weapon.TryBeginReload())
            {
                CancelItemUse(itemUse);
                return;
            }

            if (!weapon.CanReload)
                _owner.Notify("Yedek mermi yok", 1.5f);
        }

        private void SyncReloadAnimation(WeaponRuntimeService weapon)
        {
            var viewModel = _owner.ViewModel;
            if (weapon != null && weapon.IsReloading)
            {
                if (_reloadShown)
                    return;

                _reloadShown = true;
                _ammoAtReloadStart = weapon.CurrentAmmo;
                viewModel?.PlayReload(weapon.ReloadDuration);
                return;
            }

            if (!_reloadShown)
                return;

            _reloadShown = false;
            // Mermi artmadıysa şarjör değiştirme kesildi: animasyonu durdur.
            if (weapon == null || weapon.CurrentAmmo <= _ammoAtReloadStart)
                viewModel?.StopReload();
        }

        // ================================================================ nişan / yakınlaştırma
        private void UpdateAim(CombatInputState input, WeaponRuntimeService weapon, ItemUseService itemUse, float dt)
        {
            var motor = _owner.Motor;
            var wantsAim = input.Aim
                           && weapon != null
                           && !weapon.IsReloading
                           && (itemUse == null || !itemUse.IsUsing)
                           && (motor == null || !motor.IsSprinting);

            var definition = weapon?.Definition;
            var scoped = definition != null && definition.HasScope;
            var duration = AdsSeconds + (scoped ? ScopeExtraSeconds : 0f);
            _aimBlend = Mathf.MoveTowards(_aimBlend, wantsAim ? 1f : 0f, dt / duration);

            var targetZoom = definition != null && definition.AdsZoom > 1f ? definition.AdsZoom : 1f;
            var eased = _aimBlend * _aimBlend * (3f - 2f * _aimBlend);
            ApplyZoom(Mathf.Lerp(1f, targetZoom, eased));

            IsAiming = wantsAim;
            IsScoped = wantsAim && scoped && _aimBlend >= ScopedBlendThreshold;
            _owner.ViewModel?.SetAim(wantsAim);
            ApplyViewModelVisibility();
        }

        private void ApplyZoom(float zoom)
        {
            CurrentZoom = zoom;
            if (Mathf.Abs(zoom - _appliedZoom) < 1e-4f)
                return;

            _appliedZoom = zoom;
            _owner.CameraController?.SetZoom(zoom);
        }

        private void ApplyViewModelVisibility()
        {
            var hidden = !_viewModelVisible || IsScoped;
            if (_hiddenApplied && hidden == _appliedHidden)
                return;

            _hiddenApplied = true;
            _appliedHidden = hidden;
            _owner.ViewModel?.SetHidden(hidden);
            _owner.CameraRig?.SetViewmodelVisible(!hidden);
        }

        // ================================================================ tetik
        private void HandleTrigger(CombatInputState input, WeaponRuntimeService weapon, ItemUseService itemUse, Combatant combatant)
        {
            if (weapon == null)
            {
                if (input.FirePressed)
                {
                    CancelItemUse(itemUse);
                    TryMelee(combatant);
                }

                return;
            }

            if ((input.Fire || input.FirePressed) && itemUse != null && itemUse.IsUsing)
            {
                if (!input.FirePressed)
                    return; // kullanım sürerken basılı tutulan tetik yok sayılır

                CancelItemUse(itemUse);
            }

            // Burst devamı ve tampon atışlar için her kare çağrılır.
            if (weapon.TryTrigger(input.Fire, input.FirePressed))
            {
                FireShot(combatant, weapon);
                return;
            }

            if (input.FirePressed && weapon.CurrentAmmo <= 0 && !weapon.IsReloading)
            {
                PlayerController.PlaySound2D(SoundId.DryFire, 0.7f);
                if (weapon.CanReload)
                    weapon.TryBeginReload();
                else
                    _owner.Notify("Mermi yok", 1.5f);
            }
        }

        private void FireShot(Combatant combatant, WeaponRuntimeService weapon)
        {
            var origin = _owner.AimOrigin;
            var forward = _owner.AimForward;
            var motor = _owner.Motor;
            var stance = motor != null ? motor.CurrentStance : Stance.Standing;
            var speed = motor != null ? motor.SpeedNormalized : 0f;
            var grounded = motor == null || motor.IsGrounded;

            var spread = weapon.GetSpreadAngle(IsAiming, speed, grounded, stance);
            var viewModel = _owner.ViewModel;
            var muzzle = viewModel != null ? viewModel.MuzzleWorldPosition : Vector3.zero;
            if (muzzle.sqrMagnitude < 1e-6f)
                muzzle = origin + forward * FallbackMuzzleDistance;

            try
            {
                var ballistics = ResolveBallistics();
                if (ballistics != null)
                    ballistics.FireWeapon(combatant, weapon, origin, forward, spread, muzzle);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            weapon.GetRecoilKick(IsAiming, stance, UnityEngine.Random.value, out var pitch, out var yaw);
            _owner.CameraController?.AddRecoil(pitch, yaw);
            viewModel?.OnFire();
        }

        private static BallisticsSystem ResolveBallistics()
        {
            if (BallisticsSystem.Instance != null)
                return BallisticsSystem.Instance;

            GameContext.TryGet<CombatService>(out var combat);
            GameContext.TryGet<Core.Interfaces.IEventBus>(out var bus);
            return BallisticsSystem.Create(combat, bus);
        }

        private void TryMelee(Combatant combatant)
        {
            if (Time.time < _meleeReadyAt)
                return;

            _meleeReadyAt = Time.time + MeleeCooldownSeconds;
            _owner.ViewModel?.PlayMelee();

            if (!GameContext.HasAuthority)
                return;

            try
            {
                MeleeAttack.TryPunch(combatant, _owner.AimOrigin, _owner.AimForward, MeleeRange);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ================================================================ bombalar
        private void TryThrow(ThrowableKind kind, Combatant combatant, InventoryService inventory, ItemUseService itemUse,
            WeaponRuntimeService weapon)
        {
            if (inventory == null || Time.time < _throwReadyAt)
                return;

            var itemId = kind == ThrowableKind.Frag ? ItemIds.FragGrenade : ItemIds.SmokeGrenade;
            if (inventory.GetCount(itemId) <= 0)
            {
                _owner.Notify(kind == ThrowableKind.Frag ? "El bombası yok" : "Sis bombası yok", 1.5f);
                return;
            }

            if (!GameContext.HasAuthority)
                return;

            CancelItemUse(itemUse);
            weapon?.CancelReload();
            if (!inventory.Consume(itemId, 1))
                return;

            _throwReadyAt = Time.time + ThrowCooldownSeconds;

            var origin = _owner.AimOrigin;
            var forward = _owner.AimForward;
            var right = Vector3.Cross(Vector3.up, forward);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : _owner.transform.right;

            var spawn = origin + forward * 0.55f + right * 0.18f - Vector3.up * 0.12f;
            if (Physics.Linecast(origin, spawn, out var block, GameLayers.LineOfSightMask, QueryTriggerInteraction.Ignore))
                spawn = block.point - forward * 0.12f;

            var carry = Vector3.zero;
            var motor = _owner.Motor;
            if (motor != null)
            {
                carry = motor.Velocity;
                carry.y = Mathf.Max(0f, carry.y);
                carry *= 0.5f;
            }

            var velocity = forward * ThrowSpeed + Vector3.up * ThrowUpBoost + carry;

            try
            {
                ThrowableProjectile.Throw(kind, spawn, velocity, combatant.Id);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _owner.ViewModel?.PlayThrow();
            PlayerController.PlaySound2D(SoundId.GrenadePin, 0.7f);
        }

        // ================================================================ iyileşme / takviye
        private void TryHeal(ItemUseService itemUse, InventoryService inventory, WeaponRuntimeService weapon)
        {
            if (itemUse == null || itemUse.IsUsing)
                return;

            if (itemUse.TryBeginBestHeal())
            {
                weapon?.CancelReload();
                return;
            }

            var hasAny = inventory != null
                         && (inventory.GetCount(ItemIds.Bandage) > 0 || inventory.GetCount(ItemIds.FirstAid) > 0
                             || inventory.GetCount(ItemIds.MedKit) > 0);
            _owner.Notify(hasAny ? "Can zaten yeterli" : "İlk yardım malzemesi yok", 1.5f);
        }

        private void TryBoost(ItemUseService itemUse, InventoryService inventory, WeaponRuntimeService weapon)
        {
            if (itemUse == null || itemUse.IsUsing)
                return;

            if (itemUse.TryBeginBestBoost())
            {
                weapon?.CancelReload();
                return;
            }

            var hasAny = inventory != null
                         && (inventory.GetCount(ItemIds.EnergyDrink) > 0 || inventory.GetCount(ItemIds.Painkiller) > 0);
            _owner.Notify(hasAny ? "Takviye zaten dolu" : "Takviye eşyası yok", 1.5f);
        }

        private static void CancelItemUse(ItemUseService itemUse)
        {
            if (itemUse != null && itemUse.IsUsing)
                itemUse.Cancel();
        }

        private void HookItemUse(ItemUseService itemUse)
        {
            if (ReferenceEquals(itemUse, _hookedItemUse))
                return;

            UnhookItemUse();
            _hookedItemUse = itemUse;
            if (itemUse == null)
                return;

            itemUse.Started += OnItemUseStarted;
            itemUse.Completed += OnItemUseEnded;
            itemUse.Cancelled += OnItemUseEnded;
        }

        private void UnhookItemUse()
        {
            if (_hookedItemUse == null)
                return;

            _hookedItemUse.Started -= OnItemUseStarted;
            _hookedItemUse.Completed -= OnItemUseEnded;
            _hookedItemUse.Cancelled -= OnItemUseEnded;
            _hookedItemUse = null;
        }

        private void OnItemUseStarted(string itemId)
        {
            var duration = _hookedItemUse != null ? _hookedItemUse.Duration : 0f;
            if (duration > 0f)
                _owner.ViewModel?.PlayUse(duration);

            var definition = ItemCatalog.Get(itemId);
            var sound = definition != null && definition.Category == ItemCategory.Boost ? SoundId.Drink : SoundId.Bandage;
            PlayerController.PlaySound2D(sound, 0.7f);
        }

        private void OnItemUseEnded(string itemId)
        {
            _owner.ViewModel?.StopUse();
        }

        // ================================================================ HUD / görünüm
        private void UpdateSpread(WeaponRuntimeService weapon)
        {
            if (weapon == null)
            {
                SpreadAngle = 0f;
                return;
            }

            var motor = _owner.Motor;
            var stance = motor != null ? motor.CurrentStance : Stance.Standing;
            var speed = motor != null ? motor.SpeedNormalized : 0f;
            var grounded = motor == null || motor.IsGrounded;
            SpreadAngle = weapon.GetSpreadAngle(IsAiming, speed, grounded, stance);
        }

        private void UpdateViewModelMotion(LookInputState look)
        {
            var viewModel = _owner.ViewModel;
            if (viewModel == null)
                return;

            var motor = _owner.Motor;
            var sensitivity = _owner.CurrentSensitivity;
            viewModel.SetMotion(
                motor != null ? motor.SpeedNormalized : 0f,
                motor != null && motor.IsSprinting,
                motor == null || motor.IsGrounded,
                look.YawDelta * sensitivity,
                look.PitchDelta * sensitivity);
        }

        public static string FireModeName(FireMode mode, WeaponDefinitionData definition)
        {
            switch (mode)
            {
                case FireMode.Auto:
                    return "Otomatik atış";
                case FireMode.Burst:
                    return definition != null && definition.BurstCount == 3 ? "Seri atış (3)" : "Seri atış";
                default:
                    return "Tek atış";
            }
        }

        public void Dispose()
        {
            UnhookItemUse();
        }
    }
}
