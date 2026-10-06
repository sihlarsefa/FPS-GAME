using Project.Application.Movement;
using Project.Application.Services;
using Project.Core.Domain;
using UnityEngine;

namespace Project.Infrastructure.Player
{
    /// <summary>
    /// Hareket hissi köprüsü: koşudan ateşe geçiş, ADS yürüme hızı, havada isabetsizlik, yorgunluk, nefes tutma.
    /// Saf mantık Application/Movement'tadır; burası yalnız durumu besler ve sonuçları dışarı açar.
    /// </summary>
    public sealed partial class CharacterControllerMotor
    {
        private readonly SprintToFireTimer _sprintToFire = new SprintToFireTimer();
        private readonly AirAccuracyModel _airAccuracy = new AirAccuracyModel();
        private readonly FatigueModel _fatigue = new FatigueModel();
        private readonly BreathHoldModel _breathHold = new BreathHoldModel();
        private readonly SlideModel _slideModel = new SlideModel();

        private float _adsProgress;
        private float _weaponKg = 3.5f;
        private float _scopeZoom = 1f;
        private bool _holdBreathInput;
        private bool _hasPendingExit;
        private SprintExitKind _pendingExit;

        /// <summary>Silah koşudan kalkıp ateşe hazır mı (false: koşuda veya çıkış beklemesinde).</summary>
        public bool CanFireFromMovement => _sprintToFire.CanFire && !_mantling;

        /// <summary>Silah kalkma ilerlemesi 0..1 (viewmodel koşu pozundan çıkış).</summary>
        public float WeaponReadyProgress => _sprintToFire.ReadyProgress;

        /// <summary>Havada/inişte saçılma çarpanı (>=1); yayılım modeline çarpan olarak verilir.</summary>
        public float AirSpreadMultiplier => _airAccuracy.SpreadMultiplier;

        /// <summary>Havada/inişte ADS'ye girişe ek gecikme (s).</summary>
        public float AdsEntryPenalty => _airAccuracy.AdsDelayPenalty;

        /// <summary>Yorgunluk 0..1 (uzun süre düşük stamina).</summary>
        public float Fatigue => _fatigue.Value;

        /// <summary>Nefes tutma sürüyor mu.</summary>
        public bool IsHoldingBreath => _breathHold.Holding;

        /// <summary>Nişan salınımı çarpanı (nefes tutma/soluklanma).</summary>
        public float AimSwayMultiplier => _breathHold.SwayMultiplier;

        /// <summary>Nefes tutma kalan oranı 0..1 (UI).</summary>
        public float BreathRemaining => _breathHold.Remaining01;

        /// <summary>
        /// Silah sistemi her kare bildirir: ADS ilerlemesi 0..1, silah ağırlığı (kg), dürbün büyütmesi, nefes tutma tuşu.
        /// ENTEGRASYON: WeaponViewModel/ScopeController bu metodu çağırmalı; ateş öncesi CanFireFromMovement kontrol edilmeli.
        /// </summary>
        public void SetAimState(float adsProgress, float weaponKg, float scopeZoom, bool holdBreath)
        {
            _adsProgress = float.IsNaN(adsProgress) ? 0f : Mathf.Clamp01(adsProgress);
            _weaponKg = float.IsNaN(weaponKg) ? 3.5f : Mathf.Clamp(weaponKg, 0.5f, 12f);
            _scopeZoom = float.IsNaN(scopeZoom) ? 1f : Mathf.Max(1f, scopeZoom);
            _holdBreathInput = holdBreath;
        }

        private int StanceIndex => _stance == Stance.Prone ? 2 : _stance == Stance.Crouching ? 1 : 0;

        private float AdsSpeedFactor(float strafeAmount)
        {
            if (_adsProgress <= 0.001f)
                return 1f;
            return AdsMovementRules.SpeedFactor(_adsProgress, StanceIndex, _weaponKg, _scopeZoom)
                   * AdsMovementRules.StrafePenalty(_adsProgress, strafeAmount);
        }

        private void NoteWeaponExit(SprintExitKind kind)
        {
            _hasPendingExit = true;
            _pendingExit = kind;
        }

        /// <summary>Kare sonu: koşu çıkışı, havada isabet, yorgunluk, nefes tutma durumlarını ilerletir.</summary>
        private void TickFeel(float dt, float impactSpeed, bool landedThisFrame)
        {
            _sprintToFire.Tick(dt, _isSprinting, _weaponKg, _burden, _pendingExit, _hasPendingExit);
            _hasPendingExit = false;

            _airAccuracy.Tick(dt, _grounded, landedThisFrame ? impactSpeed : 0f);
            _fatigue.Tick(dt, _stamina.Normalized);

            var spent = _breathHold.Tick(dt, _holdBreathInput, _adsProgress > 0.8f, _stamina.Normalized);
            if (spent > 0f)
                SpendStamina(spent);
        }
    }
}
