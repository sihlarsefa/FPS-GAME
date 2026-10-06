using System;
using Project.Application.Catalogs;
using Project.Application.Combat.Feel;
using Project.Application.Services;
using Project.Core.Domain;
using Project.Infrastructure;
using Project.Infrastructure.Audio;
using Project.Infrastructure.Audio.Foley;
using Project.Infrastructure.Audio.HdrMix;
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
        private readonly ScopeInput _scope = new ScopeInput();
        private string _lastZeroingToast;

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

            var toggle = _owner.Settings != null && _owner.Settings.ToggleAds;
            var aim = toggle ? (_toggles.AdsLatched || _toggles.PendingPress || ((input.Aim || input.AimPressed) && !_toggles.AimHeldPrev)) : input.Aim;
            return aim || input.Fire || weapon.IsBurstActive;
        }

        // ================================================================ yaya
        public void Tick(CombatInputState input, LookInputState look, float dt)
        {
            var combatant = _owner.Combatant;
            if (combatant == null)
                return;

            HookItemUse(combatant.ItemUse);
            EnsureFoley(combatant);
            var inventory = combatant.Inventory;
            var itemUse = combatant.ItemUse;

            if (inventory != null)
                HandleSelection(input, inventory, itemUse);

            var weapon = inventory?.ActiveWeapon;
            SyncEquipped(weapon, inventory);
            SyncAttachmentVisuals(weapon);

            weapon?.Tick(dt);

            if (weapon != null)
            {
                if (input.ToggleFireMode)
                    HandleFireModeToggle(weapon);

                if (input.Reload)
                    HandleReloadRequest(weapon, itemUse);
            }

            UpdateAim(input, weapon, itemUse, dt);
            TickScope(weapon, dt);
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
        private readonly AimAssist _aimAssist = new AimAssist();
        private readonly ToggleInputAdapter _toggles = new ToggleInputAdapter();

        /// <summary>Gamepad nişan yardımı (yavaşlama + ADS mıknatısı) uygulanmış bakış. Fare girdisinde değişmez.</summary>
        public LookInputState ModifyLook(LookInputState look, float dt)
        {
            var settings = _owner.Settings;
            if (settings == null || settings.AimAssistStrength <= 0)
                return look;

            var cam = _owner.CameraRig != null ? _owner.CameraRig.transform : (Camera.main != null ? Camera.main.transform : null);
            return _aimAssist.Modify(look, cam, _owner.Combatant, IsAiming, _owner.CurrentSensitivity, settings.InvertY,
                settings.AimAssistStrength, AimAssist.GamepadLookActive(), dt);
        }

        /// <summary>Aç/kapa eğilme ayarını girdiye uygular.</summary>
        public MovementInputState AdaptMovement(MovementInputState movement)
        {
            return _toggles.AdaptMovement(movement, _owner.Settings != null && _owner.Settings.ToggleCrouch);
        }

        public void TickPassive(float dt)
        {
            var combatant = _owner.Combatant;
            if (combatant != null)
                HookItemUse(combatant.ItemUse);

            var weapon = _owner.ActiveWeapon;
            SyncEquipped(weapon, _owner.Inventory);
            SyncAttachmentVisuals(weapon);
            weapon?.Tick(dt);
            TickFireFeel(dt);

            if (IsAiming)
                AudioMix.SetAdsFocus(false);
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
            if (IsAiming)
                AudioMix.SetAdsFocus(false);
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

            if (input.CycleWeapon != 0 && !_scope.ConsumesWheel)
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

            // Çek/kılıfla sesi WeaponFoleyDriver'da (oyuncu + bot tek yol); eski tek-ses çağrısı çift çalardı.
        }

        private readonly System.Collections.Generic.List<string> _attIds = new System.Collections.Generic.List<string>(5);
        private string _attSig = "";
        private Project.Infrastructure.Weapons.WeaponModel _attModel;

        /// <summary>Takılı aksesuar mesh'lerini viewmodel üzerinde günceller (imza/model değişince; her karede kurmaz).</summary>
        private void SyncAttachmentVisuals(WeaponRuntimeService weapon)
        {
            var vm = _owner.ViewModel;
            var model = vm != null ? vm.Model : null;
            if (model == null)
                return;

            _attIds.Clear();
            weapon?.GetAttachments(_attIds);
            var sig = string.Join("|", _attIds);
            if (ReferenceEquals(model, _attModel) && sig == _attSig)
                return;

            _attModel = model;
            _attSig = sig;
            try
            {
                Project.Infrastructure.Weapons.WeaponAttachmentVisuals.Apply(model, _attIds, vm.Layer, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private bool _foleyAttached;

        /// <summary>Oyuncu nesnesine teçhizat + silah foley bileşenlerini bir kez ekler.</summary>
        private void EnsureFoley(Combatant combatant)
        {
            if (_foleyAttached || combatant == null)
                return;

            _foleyAttached = true;
            try
            {
                GearFoleyEmitter.Attach(combatant.gameObject, true,
                    () => combatant.Inventory != null && combatant.Inventory.ActiveWeapon != null ? combatant.Inventory.ActiveWeapon.WeaponId : null);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        // ================================================================ ateş modu / şarjör
        private void TickScope(WeaponRuntimeService weapon, float dt)
        {
            try
            {
                var rig = _owner.CameraRig;
                _scope.Tick(weapon != null ? weapon.WeaponId : null, IsAiming, IsScoped, _aimBlend, IsHoldingBreath,
                    BreathRemaining, rig != null ? rig.WorldCamera : null, null, null, Vector2.zero, dt);
                var toast = _scope.ZeroingToast;
                if (toast != _lastZeroingToast)
                {
                    _lastZeroingToast = toast;
                    if (toast != null)
                        _owner.Notify(toast, 1.2f);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void HandleFireModeToggle(WeaponRuntimeService weapon)
        {
            var before = weapon.CurrentFireMode;
            weapon.CycleFireMode();
            var after = weapon.CurrentFireMode;
            if (after == before)
            {
                // Silahın tek ateş modu var (ör. JNG-90 tek atış, PMT-76 otomatik).
                _owner.Notify(FireModeName(after, weapon.Definition) + " — tek mod", 1f);
                return;
            }

            // Seçici tıkı WeaponFoleyDriver'da (mod değişimini izler).
            _owner.ViewModel?.PlayFireModeSwitch();
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
                viewModel?.PlayReload(weapon.CurrentAmmo == 0, weapon.ReloadDuration);
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
            var canAim = weapon != null
                         && !weapon.IsReloading
                         && (itemUse == null || !itemUse.IsUsing)
                         && (motor == null || !motor.IsSprinting);
            var wantsAim = _toggles.ResolveAim(input.Aim, input.AimPressed, _owner.Settings != null && _owner.Settings.ToggleAds, canAim, dt) && canAim;

            var definition = weapon?.Definition;
            var scoped = definition != null && definition.HasScope;
            var adsTime = definition != null && definition.AdsTime > 0f ? definition.AdsTime : AdsSeconds;
            if (_owner.Combatant != null)
                adsTime *= _owner.Combatant.LimbAdsTimeMultiplier; // RC1: yaralı kol ADS yavaş
            var duration = adsTime + (scoped ? ScopeExtraSeconds : 0f);
            _aimBlend = AdsBlend.Step(_aimBlend, wantsAim, Mathf.Min(dt, 0.05f), duration); // ilk kare/hitch'te sıçrama yok

            var targetZoom = definition != null && definition.AdsZoom > 1f ? definition.AdsZoom : 1f;
            var eased = AdsBlend.Smooth(_aimBlend);
            ApplyZoom(Mathf.Lerp(1f, targetZoom, eased));
            if (!Mathf.Approximately(eased, _lastAimBlur))
            {
                _lastAimBlur = eased;
                Project.Infrastructure.Rendering.PostProcessing.SetAimBlur(eased);
            }

            if (wantsAim != IsAiming)
            {
                AudioMix.SetAdsFocus(wantsAim);
                if (wantsAim && weapon != null)
                    WeaponFoley.PlayAds(weapon.WeaponId, _owner.AimOrigin, true);
            }

            IsAiming = wantsAim;
            IsScoped = wantsAim && scoped && _aimBlend >= ScopedBlendThreshold;
            UpdateHoldBreath(dt);
            _owner.ViewModel?.SetAim(wantsAim);
            ApplyViewModelVisibility();
        }

        private float _lastAimBlur;
        private readonly HoldBreathState _breath = new HoldBreathState();

        /// <summary>Nefes tutma kalan süre (sn); dürbünlü silahta Shift basılıyken azalır.</summary>
        public float BreathRemaining => _breath.Remaining;

        public bool IsHoldingBreath => _breath.IsHolding;

        private void UpdateHoldBreath(float dt)
        {
            var wantsHold = false;
            try
            {
                wantsHold = IsScoped && Project.Infrastructure.Input.InputBindings.Held(BindAction.Sprint);
            }
            catch (Exception)
            {
                // Girdi sistemi hazır değilse nefes tutma devre dışı.
            }

            _breath.Update(dt, wantsHold, IsScoped);
            // Koşu yorgunluğu (salt okunur motor bilgisi): koşunca dolar, ~5 sn'de söner.
            var motor = _owner.Motor;
            _sprintFatigue = WeaponSwayRules.StepFatigue(_sprintFatigue, motor != null && motor.IsSprinting, dt);
        }

        private float _sprintFatigue;

        /// <summary>Dürbünde: yorgunluk x nefes (WeaponSwayRules.Combined); değilse 1.</summary>
        private float ScopedSway => IsScoped ? WeaponSwayRules.Combined(1f - _sprintFatigue, _breath.SwayMultiplier, _breath.IsHolding) : 1f;

        private float SwayMultiplier => ScopedSway * Suppression.SwayMultiplier * (_owner.Combatant != null ? _owner.Combatant.LimbSwayMultiplier : 1f); // RC1: yaralı kol

        private void ApplyZoom(float zoom)
        {
            CurrentZoom = zoom;
            if (Mathf.Abs(zoom - _appliedZoom) < 1e-4f)
                return;

            _appliedZoom = zoom;
            _owner.CameraController?.SetZoom(_scope.MainCameraZoom(zoom));
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
                // Boş tetik sesi: WeaponFoleyDriver (WeaponRuntimeService.DryFired).
                _owner.ViewModel?.PlayDryFire();
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

            var spread = weapon.GetBlendedSpread(_aimBlend, speed, grounded, stance, SwayMultiplier);
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
            ApplyFireFeel(weapon, pitch, yaw, muzzle, forward);
            viewModel?.OnFire();
        }

        private float _feelShake;
        private float _pumpReboundAt = -1f;
        private float _pumpReboundPitch;

        /// <summary>Kalibre bazlı his katmanı: yalnızca kamera vuruşu + flaş/duman; recoil değerleri değişmez.</summary>
        private void ApplyFireFeel(WeaponRuntimeService weapon, float pitch, float yaw, Vector3 muzzle, Vector3 forward)
        {
            var def = weapon.Definition;
            var suppressed = weapon.IsSuppressed;
            var shots = weapon.SprayShots;
            var feel = FireFeelRules.For(def.Category, def.AmmoType);

            var kickP = pitch * FireFeelRules.KickPitchMultiplier(def.Category, def.AmmoType, shots, suppressed);
            var kickY = yaw * FireFeelRules.KickYawMultiplier(def.Category, def.AmmoType, shots, suppressed);

            _feelShake = FireFeelRules.ShakeAccumulate(_feelShake, def.Category, def.AmmoType);
            if (_feelShake > 0f)
            {
                kickP += (UnityEngine.Random.value * 2f - 1f) * _feelShake;
                kickY += (UnityEngine.Random.value * 2f - 1f) * _feelShake;
            }

            _owner.AddCameraKick(kickP, kickY);

            if (feel.PumpDelay > 0f)
            {
                _pumpReboundAt = Time.time + feel.PumpDelay;
                _pumpReboundPitch = FireFeelRules.PumpReboundPitch(kickP);
            }

            try
            {
                Project.Infrastructure.Vfx.GameVfx.FireFeelLayer(muzzle, forward,
                    FireFeelRules.FlashScale(def.Category, def.AmmoType, shots, suppressed),
                    FireFeelRules.SmokeRate(def.Category, def.AmmoType, shots, weapon.FireIntervalSeconds, suppressed));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void TickFireFeel(float dt)
        {
            _feelShake = FireFeelRules.ShakeDecay(_feelShake, dt);
            if (_pumpReboundAt > 0f && Time.time >= _pumpReboundAt)
            {
                _pumpReboundAt = -1f;
                _owner.AddCameraKick(_pumpReboundPitch, 0f);
            }
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
            {
                // İstemci: vuruşu sunucuya iste (hasar yalnızca otoritede).
                if (GameContext.Network is Project.Core.Interfaces.IPlayerActionSink sink)
                {
                    try
                    {
                        sink.SubmitMelee(new MeleeRequest(combatant.Id, ToF3(_owner.AimOrigin), ToF3(_owner.AimForward),
                            MeleeRange, (uint)Time.frameCount));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                return;
            }

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

            var itemId = ThrowableRules.ItemIdFor(kind);
            if (inventory.GetCount(itemId) <= 0)
            {
                _owner.Notify(kind == ThrowableKind.Frag ? "El bombası yok" : kind == ThrowableKind.Smoke ? "Sis bombası yok"
                    : kind == ThrowableKind.Flash ? "Flaş bombası yok" : kind == ThrowableKind.Molotov ? "Molotof yok" : "Yem bombası yok", 1.5f);
                return;
            }

            var remote = !GameContext.HasAuthority;
            if (remote && !(GameContext.Network is Project.Core.Interfaces.IPlayerActionSink))
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
                if (remote)
                {
                    // İstemci: bombayı sunucu üretir; yerel yalnızca animasyon/ses.
                    ((Project.Core.Interfaces.IPlayerActionSink)GameContext.Network).SubmitThrow(new ThrowRequest(
                        combatant.Id, ThrowCodeFor(kind),
                        ToF3(spawn), ToF3(velocity), (uint)Time.frameCount));
                }
                else
                {
                    ThrowableProjectile.Throw(kind, spawn, velocity, combatant.Id);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            _owner.ViewModel?.PlayThrow(kind == ThrowableKind.Smoke);
            PlayerController.PlaySound2D(SoundId.GrenadePin, 0.7f);
        }

        private static ThrowKindCode ThrowCodeFor(ThrowableKind kind)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke: return ThrowKindCode.Smoke;
                case ThrowableKind.Flash: return ThrowKindCode.Flash;
                case ThrowableKind.Molotov: return ThrowKindCode.Molotov;
                case ThrowableKind.Decoy: return ThrowKindCode.Decoy;
                default: return ThrowKindCode.Frag;
            }
        }

        private static Float3 ToF3(Vector3 v) => new Float3(v.x, v.y, v.z);

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
                _owner.ViewModel?.PlayUse(duration, itemId);

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
            SpreadAngle = weapon.GetBlendedSpread(_aimBlend, speed, grounded, stance, SwayMultiplier);
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
                look.YawDelta * sensitivity * Suppression.SwayMultiplier,
                look.PitchDelta * sensitivity * Suppression.SwayMultiplier);

            var stance = ViewmodelStance.Stand;
            var strafe = 0f;
            if (motor != null)
            {
                if (motor.IsSliding) stance = ViewmodelStance.Slide;
                else if (motor.CurrentStance == Stance.Crouching) stance = ViewmodelStance.Crouch;
                else if (motor.CurrentStance == Stance.Prone) stance = ViewmodelStance.Prone;
                strafe = Vector3.Dot(motor.Velocity, motor.transform.right) / 5f;
            }

            viewModel.SetStance(stance);
            viewModel.SetStrafe(strafe);
            viewModel.SetHoldingBreath(IsHoldingBreath);
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
            _scope.Dispose();
        }
    }
}
